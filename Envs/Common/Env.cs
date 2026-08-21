
using System;
using Godot;
using PPO.Ppo;
using TorchSharp;



namespace PPO.Envs.Common;



public class Env<T> : IEnv
{
    public string[] InputLabels { get; }
    public long InputSize { get; }

    public string[] OutputLabels { get; }
    public long OutputSize { get; }

    public int NumEnvs => _envs.Length;

    private readonly T[] _envs;
    private readonly int[] _stepCounts;
    private readonly Action<T> _reset;
    private readonly Func<T, (float, bool, bool)> _score;


    // private int _maxSteps = 3072;
    // private float _targetTolerance = 30.0f;



    public Env(
        T[] envs,
        Action<T> reset,
        Func<T, (float, bool, bool)> score,
        string[] inputLabels,
        long inputSize,
        string[] outputLabels,
        long outputSize)
    {
        _envs = envs;
        _reset = reset;
        _score = score;

        InputLabels = inputLabels;
        InputSize = inputSize;

        OutputLabels = outputLabels;
        OutputSize = outputSize;

        _stepCounts = new int[envs.Length];
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
            // var throttle = action[i, 0].item<float>();
            // var turning = new Vector3(action[i, 1].item<float>(), action[i, 2].item<float>(), action[i, 3].item<float>());

            // _envs[i].UpdateControls(throttle, turning);
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
            (reward[i], terminated[i], truncated[i]) = _score(_envs[i]);
        }

        return (torch.tensor(reward), torch.tensor(terminated), torch.tensor(truncated));
    }



    public void ResetEnv(int i)
    {
        _reset(_envs[i]);
        _stepCounts[i] = 0;
    }



    public void Close()
    {
    }



    private void FillObsRow(float[,] obs, int i)
    {
        // var chaser = _envs[i];
        // var inverseBasis = chaser.GlobalBasis.Inverse();

        // var localVel = inverseBasis * chaser.LinearVelocity;
        // var localAcc = inverseBasis * chaser.Acceleration;
        // var localRot = inverseBasis * chaser.AngularVelocity;
        // var localToTarget = inverseBasis * (_targets[i].GlobalPosition - chaser.GlobalPosition);

        // FillVec3(obs, i, 0, 0, localVel);
        // FillVec3(obs, i, 1, 0, localAcc);
        // FillVec3(obs, i, 2, 0, localRot);
        // FillVec3(obs, i, 3, 0, localToTarget);
        // obs[i, 12] = chaser.Throttle;
        // obs[i, 13] = chaser.CurrentThrust;
        // FillVec3(obs, i, 4, 2, chaser.Turning);
    }



    private void FillVec3(float[,] obs, int i, int j, int offset, Vector3 vec)
    {
        obs[i, (j * 3) + 0 + offset] = vec.X;
        obs[i, (j * 3) + 1 + offset] = vec.Y;
        obs[i, (j * 3) + 2 + offset] = vec.Z;
    }
}
