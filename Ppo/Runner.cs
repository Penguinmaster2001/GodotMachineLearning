
using System;
using System.Collections.Generic;
using System.Linq;
using TorchSharp;
using torch = TorchSharp.torch;



namespace PPO.Ppo;



public class Runner
{
    public DeviceType Device;



    public Runner()
    {
        Device = torch.cuda_is_available() ? DeviceType.CUDA : DeviceType.CPU;
    }



    public void Run(IEnv env, PpoOptions args)
    {
        var agent = new Agent(env).to(Device);
        var optimizer = torch.optim.Adam(agent.parameters(), lr: args.LearningRate, eps: 1e-5);

        var obs = torch.zeros([args.NumSteps, args.NumEnvs, env.InputSize]).to(Device);
        var actions = torch.zeros([args.NumSteps, args.NumEnvs, env.OutputSize]).to(Device);
        var logProbs = torch.zeros([args.NumSteps, args.NumEnvs]).to(Device);
        var rewards = torch.zeros([args.NumSteps, args.NumEnvs]).to(Device);
        var dones = torch.zeros([args.NumSteps, args.NumEnvs]).to(Device);
        var values = torch.zeros([args.NumSteps, args.NumEnvs]).to(Device);

        var globalStep = 0;
        var startTime = DateTime.Now;
        var nextObs = env.Reset().to(Device);
        var nextDone = torch.zeros(args.NumEnvs).to(Device);
        var numUpdates = args.TotalTimesteps / args.BatchSize;
        var rng = new Random(args.Seed);

        for (int update = 1; update < numUpdates + 1; update++)
        {
            if (args.AnnealLR)
            {
                var frac = 1.0 - (update - 1.0) / numUpdates;
                var lrNow = frac * args.LearningRate;
                foreach (var group in optimizer.ParamGroups)
                {
                    group.LearningRate = lrNow;
                }
            }

            // ---- Rollout collection ----
            for (int step = 0; step < args.NumSteps; step++)
            {
                globalStep += args.NumEnvs;
                obs[step] = nextObs;
                dones[step] = nextDone;

                torch.Tensor action, logProb, value;
                using (torch.no_grad())
                {
                    (action, logProb, _, value) = agent.GetActionAndValue(nextObs);
                    values[step] = value.flatten();
                }

                actions[step] = action;
                logProbs[step] = logProb;

                var (rawNextObs, reward, terminated, truncated) = env.Step(action.cpu());
                rewards[step] = reward.to(Device).view(-1);

                // Treat truncated the same as terminated for bootstrapping purposes here;
                // if you want to bootstrap value on truncation (recommended for
                // time-limit cutoffs rather than true terminal states), handle
                // terminated/truncated separately in your GAE loop below.
                var done = terminated.logical_or(truncated).to(torch.float32);

                nextObs = rawNextObs.to(Device);
                nextDone = done.to(Device);

                // TODO: episode-return logging (equivalent to the Python
                // "episode" info dict) — depends on how you want to surface
                // metrics; e.g. have IEnv raise an event on episode completion
                // and accumulate reward/length there instead of polling info dicts.
            }

            // ---- Bootstrap value + advantage estimation ----
            torch.Tensor advantages, returns;
            using (torch.no_grad())
            {
                var nextValue = agent.GetValue(nextObs).reshape(1, -1);

                if (args.Gae)
                {
                    advantages = torch.zeros_like(rewards).to(Device);
                    var lastGaeLam = torch.zeros_like(nextDone);

                    for (int t = args.NumSteps - 1; t >= 0; t--)
                    {
                        torch.Tensor nextNonTerminal, nextValues;
                        if (t == args.NumSteps - 1)
                        {
                            nextNonTerminal = 1.0 - nextDone;
                            nextValues = nextValue;
                        }
                        else
                        {
                            nextNonTerminal = 1.0 - dones[t + 1];
                            nextValues = values[t + 1];
                        }

                        var delta = rewards[t] + args.Gamma * nextValues * nextNonTerminal - values[t];
                        lastGaeLam = delta + args.Gamma * args.GaeLambda * nextNonTerminal * lastGaeLam;
                        advantages[t] = lastGaeLam;
                    }

                    returns = advantages + values;
                }
                else
                {
                    returns = torch.zeros_like(rewards).to(Device);

                    for (int t = args.NumSteps - 1; t >= 0; t--)
                    {
                        torch.Tensor nextNonTerminal, nextReturn;
                        if (t == args.NumSteps - 1)
                        {
                            nextNonTerminal = 1.0 - nextDone;
                            nextReturn = nextValue;
                        }
                        else
                        {
                            nextNonTerminal = 1.0 - dones[t + 1];
                            nextReturn = returns[t + 1];
                        }

                        returns[t] = rewards[t] + args.Gamma * nextNonTerminal * nextReturn;
                    }

                    advantages = returns - values;
                }
            }

            // ---- Flatten the batch ----
            var bObs = obs.reshape(-1, env.InputSize);
            var bLogProbs = logProbs.reshape(-1);
            var bActions = actions.reshape(-1, env.OutputSize);
            var bAdvantages = advantages.reshape(-1);
            var bReturns = returns.reshape(-1);
            var bValues = values.reshape(-1);

            // ---- Optimize policy and value network ----
            var bInds = Enumerable.Range(0, args.BatchSize).ToArray();
            var clipFracs = new List<float>();
            torch.Tensor approxKl = null;
            torch.Tensor pgLoss = null, vLoss = null, entropyLoss = null, oldApproxKl = null;

            for (int epoch = 0; epoch < args.UpdateEpochs; epoch++)
            {
                Shuffle(bInds, rng);

                for (int start = 0; start < args.BatchSize; start += args.MinibatchSize)
                {
                    var end = Math.Min(start + args.MinibatchSize, args.BatchSize);
                    var mbIndsArr = bInds[start..end];
                    var mbInds = torch.tensor(mbIndsArr.Select(i => (long)i).ToArray()).to(Device);

                    var mbObs = bObs.index_select(0, mbInds);
                    var mbActions = bActions.index_select(0, mbInds);

                    var (_, newLogProb, entropy, newValueRaw) = agent.GetActionAndValue(mbObs, mbActions);

                    var logRatio = newLogProb - bLogProbs.index_select(0, mbInds);
                    var ratio = logRatio.exp();

                    using (torch.no_grad())
                    {
                        // http://joschu.net/blog/kl-approx.html
                        oldApproxKl = (-logRatio).mean();
                        approxKl = ((ratio - 1) - logRatio).mean();
                        clipFracs.Add(((ratio - 1.0).abs() > args.ClipCoef).to(torch.float32).mean().item<float>());
                    }

                    var mbAdvantages = bAdvantages.index_select(0, mbInds);
                    if (args.NormAdv)
                    {
                        mbAdvantages = (mbAdvantages - mbAdvantages.mean()) / (mbAdvantages.std() + 1e-8);
                    }

                    // Policy loss
                    var pgLoss1 = -mbAdvantages * ratio;
                    var pgLoss2 = -mbAdvantages * torch.clamp(ratio, 1 - args.ClipCoef, 1 + args.ClipCoef);
                    pgLoss = torch.max(pgLoss1, pgLoss2).mean();

                    // Value loss
                    var newValue = newValueRaw.view(-1);
                    var mbReturns = bReturns.index_select(0, mbInds);
                    var mbValues = bValues.index_select(0, mbInds);

                    if (args.ClipVLoss)
                    {
                        var vLossUnclipped = (newValue - mbReturns).pow(2);
                        var vClipped = mbValues + torch.clamp(newValue - mbValues, -args.ClipCoef, args.ClipCoef);
                        var vLossClipped = (vClipped - mbReturns).pow(2);
                        var vLossMax = torch.max(vLossUnclipped, vLossClipped);
                        vLoss = 0.5 * vLossMax.mean();
                    }
                    else
                    {
                        vLoss = 0.5 * (newValue - mbReturns).pow(2).mean();
                    }

                    entropyLoss = entropy.mean();
                    var loss = pgLoss - args.EntCoef * entropyLoss + vLoss * args.VfCoef;

                    optimizer.zero_grad();
                    loss.backward();
                    torch.nn.utils.clip_grad_norm_(agent.parameters(), args.MaxGradNorm);
                    optimizer.step();
                }

                if (args.TargetKl.HasValue && approxKl.item<float>() > args.TargetKl.Value)
                {
                    break;
                }
            }

            var yPred = bValues.cpu();
            var yTrue = bReturns.cpu();
            var varY = yTrue.var();
            var explainedVar = varY.item<float>() == 0
                ? float.NaN
                : 1 - (yTrue - yPred).var().item<float>() / varY.item<float>();

            var sps = (int)(globalStep / (DateTime.Now - startTime).TotalSeconds);
            Console.WriteLine($"update={update} global_step={globalStep} SPS={sps} " +
                              $"pg_loss={pgLoss?.item<float>():F4} v_loss={vLoss?.item<float>():F4} " +
                              $"entropy={entropyLoss?.item<float>():F4} approx_kl={approxKl?.item<float>():F4} " +
                              $"explained_var={explainedVar:F4}");
        }

        env.Close();
    }



    private static void Shuffle(int[] array, Random rng)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }



    public (torch.Tensor std, torch.Tensor mean) EvaluateAgent(IEnv env, int numEpisodes, Agent agent)
    {
        var episodeRewards = new List<float>();

        for (int episode = 0; episode < numEpisodes; episode++)
        {
            var state = env.Reset().to(Device);
            var done = false;
            float episodeRewardTotal = 0.0f;

            while (!done)
            {
                torch.Tensor action;
                using (torch.no_grad())
                {
                    (action, _, _, _) = agent.GetActionAndValue(state);
                }

                var (newState, reward, terminated, truncated) = env.Step(action.cpu());
                episodeRewardTotal += reward.mean().item<float>();
                done = terminated.logical_or(truncated).any().item<bool>();
                state = newState.to(Device);
            }

            episodeRewards.Add(episodeRewardTotal);
        }

        return torch.std_mean(torch.tensor(episodeRewards.ToArray()));
    }
}
