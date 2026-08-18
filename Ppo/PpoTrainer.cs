using System;
using System.IO;
using System.Linq;
using PPO.Benchmarking;
using TorchSharp;
using TorchSharp.Modules;
using torch = TorchSharp.torch;



namespace PPO.Ppo;



// Replaces Runner. Instead of owning a blocking loop, this is a state
// machine: call Tick() once per Godot physics frame. Internally it tracks
// whether it's currently collecting a rollout (one env-interaction per Tick)
// or ready to run a PPO update (pure tensor math, no env interaction).
public class PpoTrainer
{
    private DeviceType _device;
    private readonly IEnv _env;
    private readonly PpoOptions _args;
    private readonly Agent _agent;
    private readonly OptimizerHelper _optimizer;
    private readonly Random _rng;

    private readonly torch.Tensor _obs, _actions, _logProbs, _rewards, _dones, _values;

    private torch.Tensor _currentObs;
    private torch.Tensor _currentDone;

    private int _stepIndex;
    private int _updateIndex;
    private readonly int _numUpdates;
    private DateTime _startTime;
    private int _globalStep;

    public bool IsDone => _updateIndex >= _numUpdates;

    public string CheckpointPath = null;
    public bool SaveNextUpdate = false;



    public static PpoTrainer CreateNew(IEnv env, PpoOptions args)
    {
        var agent = new Agent(env, args.HiddenLayerSizes);
        return new(env, args, agent);
    }



    public static PpoTrainer Load(IEnv env, PpoOptions args, string path)
    {
        Console.WriteLine($"Loading from {path}");

        var agent = new Agent(env, args.HiddenLayerSizes);
        agent.load($"{path}.agent.dat");

        using var reader = new BinaryReader(File.OpenRead($"{path}.meta.dat"));
        return new(env, args, agent)
        {
            _updateIndex = reader.ReadInt32(),
            _globalStep = reader.ReadInt32(),
            _stepIndex = 0,
        };
    }



    private PpoTrainer(IEnv env, PpoOptions args, Agent agent)
    {
        _env = env;
        _args = args;
        _device = (args.UseCuda && torch.cuda_is_available()) ? DeviceType.CUDA : DeviceType.CPU;

        Console.WriteLine(_device);

        _agent = agent.to(_device);
        _optimizer = torch.optim.Adam(agent.parameters(), lr: _args.LearningRate, eps: 1e-5);

        _obs = torch.zeros([args.NumSteps, args.NumEnvs, env.InputSize]).to(_device);
        _actions = torch.zeros([args.NumSteps, args.NumEnvs, env.OutputSize]).to(_device);
        _logProbs = torch.zeros([args.NumSteps, args.NumEnvs]).to(_device);
        _rewards = torch.zeros([args.NumSteps, args.NumEnvs]).to(_device);
        _dones = torch.zeros([args.NumSteps, args.NumEnvs]).to(_device);
        _values = torch.zeros([args.NumSteps, args.NumEnvs]).to(_device);

        _currentObs = env.Reset().to(_device);
        _currentDone = torch.zeros(args.NumEnvs).to(_device);

        _numUpdates = args.TotalTimesteps / args.BatchSize;
        _rng = new Random(args.Seed);
        _startTime = DateTime.Now;
    }



    // Call this once per _PhysicsProcess. Each call advances exactly one
    // rollout step, OR (when a rollout is full) runs one full PPO update.
    // The update itself is pure tensor math — no env interaction — so if a
    // full update is too slow to run inside one physics frame, this is the
    // one piece you could safely move to a background thread (see notes below).
    public void Tick()
    {
        if (IsDone) return;

        if (_stepIndex >= _args.NumSteps)
        {
            RunUpdate();
            _stepIndex = 0;

            if (SaveNextUpdate)
            {
                SaveCheckpoint(CheckpointPath);
                SaveNextUpdate = false;
            }

            _updateIndex++;

            if (_args.AnnealLR && !IsDone)
            {
                var frac = 1.0 - (double)_updateIndex / _numUpdates;
                var lrNow = frac * _args.LearningRate;
                foreach (var group in _optimizer.ParamGroups)
                {
                    group.LearningRate = lrNow;
                }
            }

            return;
        }

        CollectStep();
    }



    private void CollectStep()
    {
        _globalStep += _args.NumEnvs;

        _obs[_stepIndex] = _currentObs;
        _dones[_stepIndex] = _currentDone;

        torch.Tensor action, logProb, value;
        using (torch.no_grad())
        {
            (action, logProb, _, value) = _agent.GetActionAndValue(_currentObs);
            _values[_stepIndex] = value.flatten();
        }

        _actions[_stepIndex] = action;
        _logProbs[_stepIndex] = logProb;

        // Apply the action now. Its physical effect will be visible on the
        // NEXT Tick() call's Observe(), after Godot advances physics for
        // this frame in between calls to PpoTrainer.
        _env.Actuate(action.cpu());

        // Evaluate the transition resulting from the PREVIOUS action, using
        // state that reflects Godot's physics step from just before this
        // Tick() was called. Observe()/Evaluate() read "now", which is the
        // result of last frame's Actuate().
        // var nextObs = _env.Observe();
        var (reward, terminated, truncated) = _env.Evaluate();

        _rewards[_stepIndex] = reward.to(_device).view(-1);
        var done = terminated.logical_or(truncated).to(torch.float32);

        for (int i = 0; i < _args.NumEnvs; i++)
        {
            if (done[i].item<float>() > 0.5f)
            {
                _env.ResetEnv(i);
            }
        }

        _currentObs = _env.Observe().to(_device); // re-read in case ResetEnv changed anything
        _currentDone = done.to(_device);

        _stepIndex++;
    }



    private void RunUpdate()
    {
        using var updateTimer = ScopedTimer.Start("Run update");
        var args = _args;
        torch.Tensor advantages, returns;

        using (torch.no_grad())
        {
            var nextValue = _agent.GetValue(_currentObs).view(-1);
            advantages = torch.zeros_like(_rewards).to(_device);
            var lastGaeLam = torch.zeros_like(_currentDone);

            for (int t = args.NumSteps - 1; t >= 0; t--)
            {
                torch.Tensor nextNonTerminal, nextValues;
                if (t == args.NumSteps - 1)
                {
                    nextNonTerminal = 1.0 - _currentDone;
                    nextValues = nextValue;
                }
                else
                {
                    nextNonTerminal = 1.0 - _dones[t + 1];
                    nextValues = _values[t + 1];
                }

                var delta = _rewards[t] + args.Gamma * nextValues * nextNonTerminal - _values[t];
                lastGaeLam = delta + args.Gamma * args.GaeLambda * nextNonTerminal * lastGaeLam;
                advantages[t] = lastGaeLam;
            }

            returns = advantages + _values;
        }

        var bObs = _obs.reshape(-1, _env.InputSize);
        var bLogProbs = _logProbs.reshape(-1);
        var bActions = _actions.reshape(-1, _env.OutputSize);
        var bAdvantages = advantages.reshape(-1);
        var bReturns = returns.reshape(-1);
        var bValues = _values.reshape(-1);

        var bInds = Enumerable.Range(0, args.BatchSize).ToArray();
        torch.Tensor approxKl = null, pgLoss = null, vLoss = null, entropyLoss = null;

        for (int epoch = 0; epoch < args.UpdateEpochs; epoch++)
        {
            using var epochTimer = ScopedTimer.Start($"epoch {epoch}");
            Shuffle(bInds, _rng);

            for (int start = 0; start < args.BatchSize; start += args.MinibatchSize)
            {
                var end = Math.Min(start + args.MinibatchSize, args.BatchSize);
                var mbInds = torch.tensor(bInds[start..end].Select(i => (long)i).ToArray()).to(_device);

                var mbObs = bObs.index_select(0, mbInds);
                var mbActions = bActions.index_select(0, mbInds);

                var (_, newLogProb, entropy, newValueRaw) = _agent.GetActionAndValue(mbObs, mbActions);
                var logRatio = newLogProb - bLogProbs.index_select(0, mbInds);
                var ratio = logRatio.exp();

                using (torch.no_grad())
                {
                    approxKl = ((ratio - 1) - logRatio).mean();
                }

                var mbAdvantages = bAdvantages.index_select(0, mbInds);
                if (args.NormAdv)
                {
                    mbAdvantages = (mbAdvantages - mbAdvantages.mean()) / (mbAdvantages.std() + 1e-8);
                }

                var pgLoss1 = -mbAdvantages * ratio;
                var pgLoss2 = -mbAdvantages * torch.clamp(ratio, 1 - args.ClipCoef, 1 + args.ClipCoef);
                pgLoss = torch.max(pgLoss1, pgLoss2).mean();

                var newValue = newValueRaw.view(-1);
                var mbReturns = bReturns.index_select(0, mbInds);
                var mbValues = bValues.index_select(0, mbInds);

                if (args.ClipVLoss)
                {
                    var vLossUnclipped = (newValue - mbReturns).pow(2);
                    var vClipped = mbValues + torch.clamp(newValue - mbValues, -args.ClipCoef, args.ClipCoef);
                    var vLossClipped = (vClipped - mbReturns).pow(2);
                    vLoss = 0.5 * torch.max(vLossUnclipped, vLossClipped).mean();
                }
                else
                {
                    vLoss = 0.5 * (newValue - mbReturns).pow(2).mean();
                }

                entropyLoss = entropy.mean();
                var loss = pgLoss - args.EntCoef * entropyLoss + vLoss * args.VfCoef;

                _optimizer.zero_grad();
                loss.backward();
                torch.nn.utils.clip_grad_norm_(_agent.parameters(), args.MaxGradNorm);
                _optimizer.step();
            }

            if (args.TargetKl.HasValue && approxKl.item<float>() > args.TargetKl.Value)
            {
                break;
            }
        }

        var sps = (int)(_globalStep / (DateTime.Now - _startTime).TotalSeconds);
        var yPred = bValues.cpu();
        var yTrue = bReturns.cpu();
        var varY = yTrue.var();
        var explainedVar = varY.item<float>() == 0
            ? float.NaN
            : 1 - (yTrue - yPred).var().item<float>() / varY.item<float>();
        Console.WriteLine($"\nupdate={_updateIndex + 1} global_step={_globalStep} SPS={sps} " +
                          $"pg_loss={pgLoss?.item<float>():F4} v_loss={vLoss?.item<float>():F4} " +
                          $"approx_kl={approxKl?.item<float>():F4} " +
                          $"explained_variance={explainedVar:F4}");
    }



    private static void Shuffle(int[] array, Random rng)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }



    public void SaveCheckpoint(string path = "")
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (string.IsNullOrWhiteSpace(CheckpointPath)) return;

            path = CheckpointPath;
        }

        Console.WriteLine($"Saving to {path}");

        _agent.save($"{path}.agent.dat");

        using var writer = new BinaryWriter(File.OpenWrite($"{path}.meta.dat"));
        writer.Write(_updateIndex);
        writer.Write(_globalStep);
    }



    public void ResetCount()
    {
        _updateIndex = 0;
        _agent.LogStd = torch.nn.Parameter(torch.zeros(_env.OutputSize));
    }
}
