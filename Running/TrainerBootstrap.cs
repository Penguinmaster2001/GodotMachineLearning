
using System;
using System.IO;
using Godot;
using PPO.Envs.Ball;
using PPO.Envs.Chase;



namespace PPO.Ppo;



public partial class TrainerBootstrap : Node
{
    [Export]
    private PackedScene _chaser;

    [Export]
    private PackedScene _target;

    [Export]
    private int _num;

    [Export]
    private Node3D _start;

    [Export]
    private Node3D _end;

    [Export]
    private string _checkpointName;

    [Export]
    private bool _loadFromCheckpoint;

    [Export]
    private bool _saveCheckpoints;



    private PpoTrainer _trainer;



    public override void _Ready()
    {
        var rng = new RandomNumberGenerator();

        var chasers = new ChaserNode[_num];
        var targets = new TargetNode[_num];
        for (int i = 0; i < _num; i++)
        {
            var hue = 4.0f * (i / 4) / _num;
            var (up, dn) = (0.85f, 0.5f);
            var (lightness, saturation) = (i % 4) switch
            {
                0 => (up, up),
                1 => (up, dn),
                2 => (dn, up),
                3 => (dn, dn),
                _ => (0.0f, 0.0f)
            };
            var color = Color.FromOkHsl(hue, saturation, lightness);

            GD.Print($"{color}");

            var chaser = _chaser.Instantiate<ChaserNode>();
            chaser.SetColor(color);
            chasers[i] = chaser;
            AddChild(chaser);

            var target = _target.Instantiate<TargetNode>();
            target.SetColor(color);
            targets[i] = target;
            AddChild(target);
        }

        var env = new ChaserEnv(chasers, targets, (c, t) =>
        {
            c.Position = new Vector3(
                    rng.RandfRange(_start.Position.X, _end.Position.X),
                    rng.RandfRange(_start.Position.Y, _end.Position.Y),
                    rng.RandfRange(_start.Position.Z, _end.Position.Z)
                );

            t.Position = c.Position + new Vector3(rng.RandfRange(-1.5f, 1.5f), 0.0f, -rng.RandfRange(5.0f, 8.0f));

            c.LinearVelocity = Vector3.Zero;
            c.Acceleration = Vector3.Zero;
            c.Rotation = Vector3.Zero;
            c.age = 0.0f;
        });

        env.Reset();

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
            HiddenLayerSizes = [32, 32]
        };

        var path = ProjectSettings.GlobalizePath(Path.Combine("res://", "Checkpoints", _checkpointName));
        GD.Print(path);
        if (_loadFromCheckpoint)
        {
            _trainer = PpoTrainer.Load(env, options, path);
            _trainer.ResetCount();
        }
        else
        {
            _trainer = PpoTrainer.CreateNew(env, options);
        }
        _trainer.CheckpointPath = ProjectSettings.GlobalizePath(path);
    }



    public override void _PhysicsProcess(double delta)
    {
        if (_trainer.IsDone)
        {
            return;
        }

        _trainer.SaveNextUpdate |= _saveCheckpoints || Input.IsKeyPressed(Key.Space);

        _trainer.Tick();
    }
}