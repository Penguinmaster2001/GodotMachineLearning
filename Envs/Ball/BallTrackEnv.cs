
using System;
using Godot;
using TorchSharp;



namespace PPO.Ppo;



public class BallTrackEnv : IEnv
{
    public long InputSize => 2;
    public long OutputSize => 1;
    public int NumEnvs => _balls.Length;

    private readonly RigidBody3D[] _balls;
    private readonly Node3D[] _targets;
    private readonly Vector3[] _startPositions;
    private readonly int[] _stepCounts;

    private const int MaxSteps = 200;
    private const float TargetTolerance = 0.1f;
    private const float StopSpeedTolerance = 0.05f;
    private const float MaxCommandSpeed = 5.0f;



    public BallTrackEnv(RigidBody3D[] balls, Node3D[] targets)
    {
        _balls = balls;
        _targets = targets;

        _startPositions = new Vector3[balls.Length];
        for (int i = 0; i < balls.Length; i++)
        {
            _startPositions[i] = balls[i].GlobalPosition;
        }

        _stepCounts = new int[balls.Length];
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



    // Pure read — no side effects, no physics advancement. Safe to call as
    // many times per frame as convenient (PpoTrainer calls it more than once).
    public torch.Tensor Observe()
    {
        var obs = new float[NumEnvs, InputSize];
        for (int i = 0; i < NumEnvs; i++)
        {
            FillObsRow(obs, i);
        }
        return torch.tensor(obs);
    }



    // Pure write — sets target velocity. The actual motion happens when
    // Godot's engine advances physics between this call and the next frame's
    // Observe(), not here.
    public void Actuate(torch.Tensor action)
    {
        for (int i = 0; i < NumEnvs; i++)
        {
            float commandedSpeed = action[i, 0].item<float>();
            commandedSpeed = Math.Clamp(commandedSpeed, -MaxCommandSpeed, MaxCommandSpeed);

            var velocity = _balls[i].LinearVelocity;
            velocity.Y = commandedSpeed;
            _balls[i].LinearVelocity = velocity;
        }
    }



    // Computed from currently-observable state only. Increments each env's
    // step counter as a side effect (needed for the timeout/truncation check).
    public (torch.Tensor reward, torch.Tensor terminated, torch.Tensor truncated) Evaluate()
    {
        var reward = new float[NumEnvs];
        var terminated = new bool[NumEnvs];
        var truncated = new bool[NumEnvs];

        for (int i = 0; i < NumEnvs; i++)
        {
            _stepCounts[i]++;

            float dist = SignedDistance(i);
            float speed = _balls[i].LinearVelocity.Y;

            reward[i] = -MathF.Abs(dist / 6.0f) - 0.01f;

            bool reachedTarget = MathF.Abs(dist) < TargetTolerance
                                  && MathF.Abs(speed) < StopSpeedTolerance;
            bool timedOut = _stepCounts[i] >= MaxSteps;

            terminated[i] = reachedTarget;
            truncated[i] = timedOut && !reachedTarget;

            if (reachedTarget)
            {
                reward[i] += 10.0f;
            }
        }

        return (torch.tensor(reward), torch.tensor(terminated), torch.tensor(truncated));
    }



    public void ResetEnv(int i)
    {
        _balls[i].GlobalPosition = _startPositions[i];
        _balls[i].LinearVelocity = Vector3.Zero;
        _stepCounts[i] = 0;
    }



    public void Close()
    {
    }



    private float SignedDistance(int i)
    {
        return _targets[i].GlobalPosition.Y - _balls[i].GlobalPosition.Y;
    }



    private void FillObsRow(float[,] obs, int i)
    {
        obs[i, 0] = SignedDistance(i);
        obs[i, 1] = _balls[i].LinearVelocity.Y;
    }
}
