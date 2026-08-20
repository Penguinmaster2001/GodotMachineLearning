
using System.Collections.Generic;
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
    private PackedScene _resultIndicator;

    [Export]
    private int _num;

    [Export]
    private Node3D _start;

    [Export]
    private Node3D _end;

    private List<ChaserNode> _chasers;
    private List<TargetNode> _targets;

    [Export]
    private AgentUi _ui;

    [Export]
    private Camera3D _followCam;

    [Export]
    private Camera3D _observeCam;

    private enum CameraMode
    {
        Observe,
        Follow,
    }

    private CameraMode _currentCameraMode = CameraMode.Observe;

    [Export]
    private long _updateFramePeriod = 5;
    private long _updateCount = 0;


    [Export]
    private string _loadCheckpointName;

    [Export]
    public string CheckpointName
    {
        get => _checkpointName; set
        {
            _checkpointName = value;
            _trainer?.CheckpointPath = ProjectSettings.GlobalizePath(Path.Combine("res://", "Checkpoints", _checkpointName));
        }
    }
    private string _checkpointName;

    [Export]
    private bool _loadFromCheckpoint;

    [Export]
    private bool _saveCheckpoints;



    private PpoTrainer _trainer;



    public override void _Ready()
    {
        var rng = new RandomNumberGenerator();

        _chasers = new(_num);
        _targets = new(_num);
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

            var chaser = _chaser.Instantiate<ChaserNode>();
            chaser.SetColor(color);
            _chasers.Add(chaser);
            AddChild(chaser);

            var target = _target.Instantiate<TargetNode>();
            target.SetColor(color);
            _targets.Add(target);
            AddChild(target);
        }
        RemoveChild(_followCam);
        _chasers[0].AddChild(_followCam);
        _followCam.Position = new(0.0f, 0.0f, 3.0f);

        var env = new ChaserEnv([.. _chasers], [.. _targets], (c, t) =>
        {
            var indicator = _resultIndicator.Instantiate<ResultIndicator>();
            indicator.Position = c.Position;
            indicator.SetColor(c.Reward > 0.0f ? Color.FromOkHsl(0.33f, 1.0f, 0.5f) : Color.FromOkHsl(0.0f, 1.0f, 0.5f));
            AddChild(indicator);

            c.Position = new Vector3(
                    rng.RandfRange(_start.Position.X, _end.Position.X),
                    rng.RandfRange(_start.Position.Y, _end.Position.Y),
                    rng.RandfRange(_start.Position.Z, _end.Position.Z)
                );

            var dist = rng.RandfRange(-100.0f, 100.0f);
            t.Position = c.Position + new Vector3(rng.RandfRange(-1.0f, 1.0f) * dist, rng.RandfRange(-1.0f, 1.0f) * dist, dist);

            c.LinearVelocity = Vector3.Zero;
            c.Acceleration = Vector3.Zero;
            c.AngularVelocity = Vector3.Zero;
            c.Rotation = Vector3.Zero;
            c.Age = 0.0f;
        });

        env.Reset();

        var options = new PpoOptions
        {
            UseCuda = true,
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

        if (_loadFromCheckpoint)
        {
            _trainer = PpoTrainer.Load(env, options, ProjectSettings.GlobalizePath(Path.Combine("res://", "Checkpoints", _loadCheckpointName)));
            _trainer.ResetCount();
        }
        else
        {
            _trainer = PpoTrainer.CreateNew(env, options);
        }
        _trainer.CheckpointPath = ProjectSettings.GlobalizePath(Path.Combine("res://", "Checkpoints", _checkpointName));

        _ui.Env = env;
        _ui.Agent = _trainer.Agent;
        _ui.Stats = _trainer.Stats;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_trainer.IsDone)
        {
            return;
        }

        _trainer.SaveNextUpdate |= _saveCheckpoints || Input.IsKeyPressed(Key.Space);

        _updateCount++;
        if (_updateCount % _updateFramePeriod == 0)
        {
            _trainer.Tick();
        }
    }



    public override void _Input(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Keycode: Key.Space })
        {
            switch (_currentCameraMode)
            {
                case CameraMode.Follow:
                    _observeCam.MakeCurrent();
                    _currentCameraMode = CameraMode.Observe;
                    break;
                case CameraMode.Observe:
                    _followCam.MakeCurrent();
                    _currentCameraMode = CameraMode.Follow;
                    break;
            }
        }
    }
}