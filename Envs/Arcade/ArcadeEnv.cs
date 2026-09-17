
using System;
using Godot;
using PPO.Aero.Arcade;
using PPO.Envs.Chase;
using PPO.Envs.Common;
using PPO.Ppo;
using TorchSharp;



namespace PPO.Envs.Arcade;



public class ArcadeEnv : IEnv
{
    public string[] InputLabels { get; } = [
        "velX", "velY", "velZ",
        "accX", "accY", "accZ",
        "Dpch", "Dyaw", "Drol",
        "gupX", "gupY", "gupZ",
        "fwdX", "fwdY", "fwdZ",
        "alfa",
        "pich", "roll", "yaww",
        "thtl", "thst",
        "gndA",
        "altE", "rolE", "pitE", "hedE", "tdst",
    ];
    public Func<float, float>[] Normalizations { get; } = new Utils.NormalizationBuilder()
        .Add(NormalizationFunctions.DivideTanh(60.0f), 3) // vel
        .Add(NormalizationFunctions.DivideTanh(20.0f), 3) // acc
        .Add(NormalizationFunctions.DivideTanh(6.0f), 3)  // Datt
        .Add(NormalizationFunctions.Identity, 3)          // gup
        .Add(NormalizationFunctions.Identity, 3)          // fwd
        .Add(NormalizationFunctions.DivideTanh(0.35f))    // alpha
        .Add(NormalizationFunctions.Identity, 3)          // ctrl
        .Add(NormalizationFunctions.Identity, 2)          // thtl
        .Add(NormalizationFunctions.DivideTanh(500.0f))   // gndA
        .Add(NormalizationFunctions.DivideTanh(500.0f))   // altE
        .Add(NormalizationFunctions.Divide(Mathf.Pi))     // rolE
        .Add(NormalizationFunctions.Divide(Mathf.Pi))     // pitE
        .Add(NormalizationFunctions.Divide(Mathf.Pi))     // hedE
        // .Add(NormalizationFunctions.DivideTanh(60.0f))    // spdE
        .Add(NormalizationFunctions.DivideTanh(1000.0f))  // tdst
        .Build();
    public long InputSize => InputLabels.Length;

    public string[] OutputLabels { get; } = ["thtl", "pich", "roll", "yaww"];
    public long OutputSize => OutputLabels.Length;

    public int NumEnvs => _aircraft.Length;

    private readonly ArcadeAircraft[] _aircraft;
    private readonly TargetNode[] _targets;
    private readonly int[] _stepCounts;
    private readonly Action<ArcadeAircraft, TargetNode> _reset;
    private readonly Action<ArcadeAircraft, TargetNode> _resetTarget;


    private int _maxSteps = 128;

    public float TargetAltTolerance = 25.0f;



    public ArcadeEnv(
        ArcadeAircraft[] aircraft,
        TargetNode[] targets,
        Action<ArcadeAircraft, TargetNode> reset,
        Action<ArcadeAircraft, TargetNode> resetTarget)
    {
        _aircraft = aircraft;
        _targets = targets;
        _reset = reset;
        _resetTarget = resetTarget;

        _stepCounts = new int[aircraft.Length];
    }



    public torch.Tensor Reset()
    {
        var obs = new float[NumEnvs, InputSize];
        for (int i = 0; i < NumEnvs; i++)
        {
            ResetEnv(i);
            FillObsRow(obs, i);
        }
        return torch.tensor(obs);
    }



    public torch.Tensor Observe()
    {
        var obs = new float[NumEnvs, InputSize];
        for (int i = 0; i < NumEnvs; i++)
        {
            FillObsRow(obs, i);
        }
        return torch.tensor(obs);
    }



    public void Actuate(torch.Tensor action)
    {
        for (int i = 0; i < NumEnvs; i++)
        {
            _aircraft[i].SetControls(
                action[i, 1].item<float>(),
                action[i, 2].item<float>(),
                action[i, 3].item<float>(),
                _aircraft[i].Throttle + (0.5f * action[i, 0].item<float>())
            );
        }
    }



    public (torch.Tensor reward, torch.Tensor terminated, torch.Tensor truncated) Evaluate(bool step = true)
    {
        var reward = new float[NumEnvs];
        var terminated = new bool[NumEnvs];
        var truncated = new bool[NumEnvs];

        for (int i = 0; i < NumEnvs; i++)
        {
            if (step)
            {
                _stepCounts[i]++;
            }

            var crashed = _aircraft[i].GlobalPosition.Y <= 0.0f;
            terminated[i] = crashed;

            bool timedOut = _stepCounts[i] >= _maxSteps;
            truncated[i] = timedOut && !crashed;


            var speedIncentive = Mathf.Clamp((-_aircraft[i].LocalVel.Z - 40.0f) / 60.0f, -1.0f, 3.0f);
            var altIncentive = Mathf.Clamp(_aircraft[i].GlobalPosition.Y / _targets[i].GlobalPosition.Y, 0.0f, 1.0f);
            reward[i] = altIncentive + speedIncentive;
            var angleToTarget = Mathf.Atan2(_aircraft[i].GlobalPosition.X - _targets[i].GlobalPosition.X,
                _aircraft[i].GlobalPosition.Z - _targets[i].GlobalPosition.Z);
            var angleErr = Mathf.AngleDifference(_aircraft[i].GlobalRotation.Y, angleToTarget);

            reward[i] += (0.1f - Mathf.Abs(angleErr)) / 3.0f;
            reward[i] -= (_aircraft[i].GlobalPosition.DistanceTo(_targets[i].GlobalPosition) - 50.0f) / 300.0f;

            if (_aircraft[i].GlobalPosition.DistanceTo(_targets[i].GlobalPosition) < 200.0f)
            {
                reward[i] += 200.0f;
                _resetTarget(_aircraft[i], _targets[i]);
            }

            if (_aircraft[i].LocalVel.Z > -40.0f)
            {
                reward[i] -= 3.0f;
            }

            if (Mathf.Abs(_aircraft[i].GlobalPosition.Y - _targets[i].GlobalPosition.Y) < TargetAltTolerance)
            {
                reward[i] += 5.0f;
            }

            if (_aircraft[i].GlobalPosition.Y < 50.0f)
            {
                reward[i] -= 2.0f;
            }

            if (crashed)
            {
                reward[i] -= 500.0f;
            }

            _aircraft[i].Reward = reward[i];
        }

        return (torch.tensor(reward), torch.tensor(terminated), torch.tensor(truncated));
    }



    public void ResetEnv(int i)
    {
        _reset(_aircraft[i], _targets[i]);
        _stepCounts[i] = 0;
    }



    public void Close()
    {
    }



    private void FillObsRow(float[,] obs, int aircraft)
    {
        var filler = new Utils.ObsRowFiller(aircraft, obs, Normalizations);
        filler.Add(_aircraft[aircraft].GetObservation());

        filler.Add(_aircraft[aircraft].GlobalPosition.Y);
        filler.Add(_targets[aircraft].GlobalPosition.Y - _aircraft[aircraft].GlobalPosition.Y);
        filler.Add(_aircraft[aircraft].GlobalRotation.Z);
        filler.Add(_aircraft[aircraft].GlobalRotation.X);
        var angleToTarget = Mathf.Atan2(_aircraft[aircraft].GlobalPosition.X - _targets[aircraft].GlobalPosition.X,
            _aircraft[aircraft].GlobalPosition.Z - _targets[aircraft].GlobalPosition.Z);
        filler.Add(Mathf.AngleDifference(_aircraft[aircraft].GlobalRotation.Y, angleToTarget));
        // filler.Add(-60.0f - (_aircraft[aircraft].GlobalBasis.Transposed() * _aircraft[aircraft].LinearVelocity).Z);
        filler.Add(_aircraft[aircraft].GlobalPosition.DistanceTo(_targets[aircraft].GlobalPosition));

        if (filler.Count != InputSize)
        {
            throw new Exception();
        }
    }
}
