
using System.Collections.Generic;
using System.IO;
using Godot;
using PPO.Aero.Arcade;
using PPO.Envs.Arcade;
using PPO.Envs.Chase;
using PPO.Envs.Common;
using PPO.Ui;



namespace PPO.Ppo;



public partial class TrainerBootstrap : Node
{
    [Export]
    private PackedScene _aircraft;

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

    private List<ArcadeAircraft> _aircrafts = [];
    private List<TargetNode> _targets = [];

    [Export]
    private AgentUi _ui;

    [Export]
    public Godot.Collections.Array<Node3D> Cameras = [];
    public int CurrentCamera = 0;

    [Export]
    private CameraFollowsRigidbody _followCam;

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
        var (options, env) = ArcadeEnvFactory.CreateEnv(
            _aircrafts,
            _targets,
            _num,
            _start.Position,
            _end.Position,
            () => _aircraft.Instantiate<ArcadeAircraft>(),
            () => _target.Instantiate<TargetNode>(),
            () => _resultIndicator.Instantiate<ResultIndicator>(),
            n => AddChild(n));

        _followCam.ToFollow = _aircrafts[0];

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
        _ui.EnvAgent = _aircrafts[0];
        _ui.Target = _targets[0];
    }



    public override void _PhysicsProcess(double delta)
    {
        if (_trainer.IsDone)
        {
            return;
        }

        _trainer.SaveNextUpdate = _saveCheckpoints;

        _updateCount++;
        if (_updateCount % _updateFramePeriod == 0)
        {
            _trainer.Tick();
        }
    }



    public override void _Input(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Keycode: Key.Space } key)
        {
            CurrentCamera = (CurrentCamera + Cameras.Count + (key.ShiftPressed ? -1 : 1)) % Cameras.Count;

            switch (Cameras[CurrentCamera])
            {
                case Camera3D camera:
                    camera.MakeCurrent();
                    break;
                case CameraFollowsRigidbody camera:
                    camera.MakeCurrent();
                    break;
            }
        }
    }
}