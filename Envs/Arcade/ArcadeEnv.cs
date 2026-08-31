
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

        "altE", "ptcE", "rolE", "hedE", "spdE",
    ];
    public long InputSize => InputLabels.Length;

    public string[] OutputLabels { get; } = ["thtl", "pich", "roll", "yaww"];
    public long OutputSize => OutputLabels.Length;

    public int NumEnvs => _aircraft.Length;

    private readonly ArcadeAircraft[] _aircraft;
    private readonly TargetNode[] _targets;
    private readonly int[] _stepCounts;
    private readonly Action<ArcadeAircraft, TargetNode> _reset;


    private int _maxSteps = 256;

    public float TargetAltTolerance = 50.0f;



    public ArcadeEnv(ArcadeAircraft[] aircraft, TargetNode[] targets, Action<ArcadeAircraft, TargetNode> reset)
    {
        _aircraft = aircraft;
        _targets = targets;
        _reset = reset;

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
                _aircraft[i].Throttle + (0.5f *  action[i, 0].item<float>())
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


            var speedIncentive = Mathf.Clamp(-_aircraft[i].LocalVel.Z / 100.0f, 0.0f, 3.0f);
            var altIncentive = Mathf.Clamp(_aircraft[i].GlobalPosition.Y / _targets[i].GlobalPosition.Y, 0.0f, 1.0f);
            reward[i] = altIncentive + speedIncentive;

            if (_aircraft[i].LocalVel.Z < -50.0f)
            {
                reward[i] += 10.0f;
            }

            if (Mathf.Abs(_aircraft[i].GlobalPosition.Y - _targets[i].GlobalPosition.Y) < TargetAltTolerance)
            {
                reward[i] += 10.0f;
            }

            if (crashed)
            {
                reward[i] -= 20.0f;
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
        var filler = new Utils.ObsRowFiller(aircraft, obs);
        filler.Array(_aircraft[aircraft].GetObservation());

        filler.Float(_aircraft[aircraft].GlobalPosition.Y);
        filler.Float(_targets[aircraft].GlobalPosition.Y - _aircraft[aircraft].GlobalPosition.Y);
        filler.Float(-_aircraft[aircraft].GlobalRotation.X);
        filler.Float(-_aircraft[aircraft].GlobalRotation.Z);
        filler.Float(-_aircraft[aircraft].GlobalRotation.Y);
        filler.Float(-100.0f - (_aircraft[aircraft].GlobalBasis.Transposed() * _aircraft[aircraft].LinearVelocity).Z);

        if (filler.Count != InputSize)
        {
            throw new Exception();
        }
    }
}
