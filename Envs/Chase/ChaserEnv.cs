
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
    /// </summary>
    public long InputSize => 3 + 3 + 3 + 3;

    /// <summary>
    /// 4
    /// 1: throttle
    /// 3: pitch, roll, yaw
    /// </summary>
    public long OutputSize => 1 + 3;

    public int NumEnvs => _chasers.Length;

    private readonly ChaserNode[] _chasers;
    private readonly TargetNode[] _targets;
    private readonly int[] _stepCounts;
    private readonly Action<ChaserNode, TargetNode> _reset;

    
    private int _maxSteps = 1024;
    private float _targetTolerance = 0.5f;



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
            var throttle = Mathf.Clamp(action[i, 0].item<float>(), 0.0f, 1.0f);
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
            var angle = _chasers[i].LinearVelocity.AngleTo(_targets[i].GlobalPosition - _chasers[i].GlobalPosition);
            var inFront = Mathf.RadToDeg(angle) < 45.0f;

            reward[i] = (angle / 30.0f) - MathF.Abs(dist / 5.0f) - 0.01f;

            bool reachedTarget = MathF.Abs(dist) < _targetTolerance;;
            bool timedOut = _stepCounts[i] >= _maxSteps;

            terminated[i] = reachedTarget || !inFront;
            truncated[i] = timedOut && !reachedTarget && inFront;

            if (reachedTarget)
            {
                reward[i] += 20.0f;
            }

            if (!inFront)
            {
                reward[i] -= 10.0f;
            }
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

        FillVec3(obs, i, 0, localVel);
        FillVec3(obs, i, 1, localAcc);
        FillVec3(obs, i, 2, localRot);
        FillVec3(obs, i, 3, localToTarget);
    }



    private void FillVec3(float[,] obs, int i, int j, Vector3 vec)
    {
        obs[i, (j * 3) + 0] = vec.X;
        obs[i, (j * 3) + 1] = vec.Y;
        obs[i, (j * 3) + 2] = vec.Z;
    }
}
