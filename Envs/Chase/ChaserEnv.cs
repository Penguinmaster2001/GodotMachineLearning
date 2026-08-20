
using System;
using Godot;
using PPO.Envs.Ball;
using PPO.Ppo;
using TorchSharp;



namespace PPO.Envs.Chase;



public class ChaserEnv : IEnv
{
    /// <summary>
    /// 12
    /// 3: Vector3 local vel
    /// 3: Vector3 local acc
    /// 3: Vector3 local angular vel
    /// 3: Vector3 to target in local frame
    /// 1: current throttle
    /// 1: current thrust
    /// 3: current attitude
    /// </summary>
    public string[] InputLabels { get; } = ["velX", "velY", "velZ", "accX", "accY", "accZ", "Dpch", "Drol", "Dyaw", "tgtX", "tgtY", "tgtZ", "thtl", "thst", "pich", "roll", "yaww"];
    public long InputSize => 3 + 3 + 3 + 3 + 1 + 1 + 3;

    /// <summary>
    /// 4
    /// 1: throttle
    /// 3: pitch, roll, yaw
    /// </summary>
    public string[] OutputLabels { get; } = ["thtl", "pich", "roll", "yaww"];
    public long OutputSize => 1 + 3;

    public int NumEnvs => _chasers.Length;

    private readonly ChaserNode[] _chasers;
    private readonly TargetNode[] _targets;
    private readonly int[] _stepCounts;
    private readonly Action<ChaserNode, TargetNode> _reset;


    private int _maxSteps = 3072;
    private float _targetTolerance = 30.0f;



    public ChaserEnv(ChaserNode[] chasers, TargetNode[] targets, Action<ChaserNode, TargetNode> reset)
    {
        _chasers = chasers;
        _targets = targets;
        _reset = reset;

        _stepCounts = new int[chasers.Length];
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
            var throttle = action[i, 0].item<float>();
            var turning = new Vector3(action[i, 1].item<float>(), action[i, 2].item<float>(), action[i, 3].item<float>());

            _chasers[i].UpdateControls(throttle, turning);
        }
    }



    public (torch.Tensor reward, torch.Tensor terminated, torch.Tensor truncated) Evaluate()
    {
        var reward = new float[NumEnvs];
        var terminated = new bool[NumEnvs];
        var truncated = new bool[NumEnvs];

        for (int i = 0; i < NumEnvs; i++)
        {
            _stepCounts[i]++;

            float dist = _targets[i].GlobalPosition.DistanceTo(_chasers[i].GlobalPosition);

            const float stepPenalty = 0.5f;
            var inputPenalty = (_chasers[i].Turning.LengthSquared() + (_chasers[i].Throttle * _chasers[i].Throttle)) / 2.0f;
            var spinPenalty = _chasers[i].AngularVelocity.LengthSquared() * 3.0f;
            var distancePenalty = dist / 200.0f;
            var speedIncentive = _chasers[i].LinearVelocity.Project(_targets[i].GlobalPosition - _chasers[i].GlobalPosition).Length() / 20.0f;
            speedIncentive *= speedIncentive < 0.0f ? 1.0f : 2.0f;
            speedIncentive -= 4.0f;
            reward[i] = speedIncentive - (inputPenalty + distancePenalty + stepPenalty + spinPenalty);

            bool reachedTarget = dist < _targetTolerance;
            bool timedOut = _stepCounts[i] >= _maxSteps;

            terminated[i] = reachedTarget;
            truncated[i] = timedOut && !reachedTarget;

            if (reachedTarget)
            {
                reward[i] += 10.0f * stepPenalty * _maxSteps;
            }

            _chasers[i].Reward = reward[i];
        }

        return (torch.tensor(reward), torch.tensor(terminated), torch.tensor(truncated));
    }



    public void ResetEnv(int i)
    {
        _reset(_chasers[i], _targets[i]);
        _stepCounts[i] = 0;
    }



    public void Close()
    {
    }



    private void FillObsRow(float[,] obs, int i)
    {
        var chaser = _chasers[i];
        var inverseBasis = chaser.GlobalBasis.Inverse();

        var localVel = inverseBasis * chaser.LinearVelocity;
        var localAcc = inverseBasis * chaser.Acceleration;
        var localRot = inverseBasis * chaser.AngularVelocity;
        var localToTarget = inverseBasis * (_targets[i].GlobalPosition - chaser.GlobalPosition);

        FillVec3(obs, i, 0, 0, localVel);
        FillVec3(obs, i, 1, 0, localAcc);
        FillVec3(obs, i, 2, 0, localRot);
        FillVec3(obs, i, 3, 0, localToTarget);
        obs[i, 12] = chaser.Throttle;
        obs[i, 13] = chaser.CurrentThrust;
        FillVec3(obs, i, 4, 2, chaser.Turning);
    }



    private void FillVec3(float[,] obs, int i, int j, int offset, Vector3 vec)
    {
        obs[i, (j * 3) + 0 + offset] = vec.X;
        obs[i, (j * 3) + 1 + offset] = vec.Y;
        obs[i, (j * 3) + 2 + offset] = vec.Z;
    }
}
