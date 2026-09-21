
using System;
using Godot;
using PPO.Ppo;
using TorchSharp;



namespace PPO.Envs.Ball;



public class BallTrackEnv : IEnv
{
    public string[] InputLabels => [];
    public long InputSize => 3;
    public string[] OutputLabels => [];
    public long OutputSize => 1;
    public int NumEnvs => _balls.Length;

    private readonly BallNode[] _balls;
    private readonly Node3D[] _targets;
    private readonly Vector3[] _startPositions;
    private readonly int[] _stepCounts;
    private readonly Action<BallNode> _reset;
    public (string, float)[] RewardStats { get; private set; } = [];

    
    [Export]
    private int MaxSteps = 512;
    
    [Export]
    private float TargetTolerance = 0.05f;
    
    [Export]
    private float StopSpeedTolerance = 0.05f;
    
    [Export]
    private float MaxCommandSpeed = 25.0f;



    public BallTrackEnv(BallNode[] balls, Node3D[] targets, Action<BallNode> reset)
    {
        _balls = balls;
        _targets = targets;
        _reset = reset;

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
        var ave = 0.0f;
        for (int i = 0; i < NumEnvs; i++)
        {
            float commandedSpeed = 3.0f * action[i, 0].item<float>();
            commandedSpeed = Math.Clamp(commandedSpeed, -MaxCommandSpeed, MaxCommandSpeed);
            ave += MathF.Abs(commandedSpeed);

            _balls[i].ApplyForce(commandedSpeed * Vector3.Up);
        }

        // GD.Print($"ave: {ave / _balls.Length}");
    }



    // Computed from currently-observable state only. Increments each env's
    // step counter as a side effect (needed for the timeout/truncation check).
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

            float dist = SignedDistance(i);
            float speed = _balls[i].LinearVelocity.Y;

            reward[i] = -MathF.Abs(dist / 2.0f) - 0.01f;

            bool reachedTarget = MathF.Abs(dist) < TargetTolerance
                                  && MathF.Abs(speed) < StopSpeedTolerance;
            bool timedOut = _stepCounts[i] >= MaxSteps;
            bool hitEdge = MathF.Abs(dist) >= 12.0f;

            terminated[i] = reachedTarget || hitEdge;
            truncated[i] = timedOut && !reachedTarget && !hitEdge;

            if (reachedTarget)
            {
                reward[i] += 60.0f / _balls[i].age;
            }

            if (hitEdge)
            {
                reward[i] -= 10.0f;
            }
        }

        return (torch.tensor(reward), torch.tensor(terminated), torch.tensor(truncated));
    }



    public void ResetEnv(int i)
    {
        _reset(_balls[i]);
        // _balls[i].GlobalPosition = _startPositions[i];
        // _balls[i].LinearVelocity = Vector3.Zero;
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
        obs[i, 2] = _balls[i].Acceleration.Y;
    }
}
