
using System.Collections.Generic;
using System.IO;
using Godot;
using PPO.Envs.Chase;
using PPO.Envs.Common;



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

    private List<ChaserNode> _chasers = [];
    private List<TargetNode> _targets = [];

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
        var (options, env) = ChaserEnvFactory.CreateEnv(
            _chasers,
            _targets,
            _num,
            _start.Position,
            _end.Position,
            () => _chaser.Instantiate<ChaserNode>(),
            () => _target.Instantiate<TargetNode>(),
            () => _resultIndicator.Instantiate<ResultIndicator>(),
            n => AddChild(n));

        RemoveChild(_followCam);
        _chasers[0].AddChild(_followCam);
        _followCam.Position = new(0.0f, 0.0f, 3.0f);

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
        // _ui.Chaser = _chasers[0];
        // _ui.Target = _targets[0];
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