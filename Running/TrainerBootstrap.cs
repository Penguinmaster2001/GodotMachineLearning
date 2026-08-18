
using Godot;
using PPO.Envs.Ball;



namespace PPO.Ppo;



public partial class TrainerBootstrap : Node
{
    [Export]
    private PackedScene _ball;

    [Export]
    private PackedScene _target;

    [Export]
    private int _num;

    [Export]
    private Node3D _start;

    [Export]
    private Node3D _end;



    private PpoTrainer _trainer;



    public override void _Ready()
    {
        var rng = new RandomNumberGenerator();

        var balls = new BallNode[_num];
        var targets = new Node3D[_num];
        for (int i = 0; i < _num; i++)
        {
            var pos = _start.Position.Lerp(_end.Position, (float)i / (_num - 1));
            var ball = _ball.Instantiate<BallNode>();
            pos.Y = rng.RandfRange(_start.Position.Y, _end.Position.Y);
            ball.Position = pos;
            AddChild(ball);
            balls[i] = ball;

            var target = _target.Instantiate<Node3D>();
            pos.Y = (_start.Position.Y + _end.Position.Y) / 2.0f;
            target.Position = pos;
            AddChild(target);
            targets[i] = target;
        }

        var env = new BallTrackEnv(balls, targets, b =>
        {
            var pos = b.Position;
            pos.Y = rng.RandfRange(_start.Position.Y, _end.Position.Y);
            b.Position = pos;
            b.LinearVelocity = rng.RandfRange(-2.0f, 2.0f) * Vector3.Up;
            b.Acceleration = Vector3.Zero;
            b.age = 0.0f;
        });

        var options = new PpoOptions
        {
            UseCuda = false,
            NumSteps = 512,
            NumEnvs = env.NumEnvs,
            LearningRate = 3e-4,
            TotalTimesteps = 32_000_000,
            BatchSize = 512 * env.NumEnvs,
            MinibatchSize = 8196,
            UpdateEpochs = 4,
            AnnealLR = true,
            EntCoef = 0.001,
            HiddenLayerSizes = [8, 8]
        };

        _trainer = PpoTrainer.Load(env, options, ProjectSettings.GlobalizePath("res://Checkpoints/1d_acc_ball"));
        _trainer.ResetCount();
        // _trainer = PpoTrainer.CreateNew(env, options);
        _trainer.CheckpointPath = ProjectSettings.GlobalizePath("res://Checkpoints/1d_acc_ball");
    }



    public override void _PhysicsProcess(double delta)
    {
        if (_trainer.IsDone)
        {
            return;
        }

        _trainer.SaveNextUpdate |= Input.IsKeyPressed(Key.Space);

        _trainer.Tick();
    }
}