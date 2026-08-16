
using Godot;


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

        var balls = new RigidBody3D[_num];
        var targets = new Node3D[_num];
        for (int i = 0; i < _num; i++)
        {
            var pos = _start.Position.Lerp(_end.Position, (float)i / (_num - 1));
            var ball = _ball.Instantiate<RigidBody3D>();
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

        var env = new BallTrackEnv(balls, targets);
        var options = new PpoOptions
        {
            UseCuda = false,
            NumSteps = 512,
            NumEnvs = env.NumEnvs,
            LearningRate = 3e-4,
            TotalTimesteps = 3_000_000,
            BatchSize = 512 * env.NumEnvs,
            MinibatchSize = 512,
            UpdateEpochs = 10,
            AnnealLR = true,
            EntCoef = 0.001
        };

        _trainer = new PpoTrainer(env, options);
    }



    public override void _PhysicsProcess(double delta)
    {
        if (_trainer.IsDone)
        {
            return; // or trigger evaluation / save weights / stop the scene
        }

        _trainer.Tick();
    }
}