
using System.Collections.Generic;
using Godot;
using PPO.Aero.Arcade;
using PPO.Ui;



namespace PPO.Envs.Arcade;



public partial class ArcadeTestEnv : Node3D
{
    [Export]
    public ArcadeAircraft Aircraft;

    [Export]
    public ArcadeController ArcadeController;

    [Export]
    public Hud Hud;

    [Export]
    public Tagger Tagger;



    public override void _Ready()
    {
        ArcadeController.SetChannels(new Dictionary<string, int>
        {
            {"pitch", 0},
            {"yaw", 1},
            {"roll", 2},
            {"throttle", 3},
        });

        Aircraft.WorldVars = new();
        Hud.InputNames = Aircraft.InputNames;
        Hud.InputData = Aircraft.GetObservation;
    }



    public override void _PhysicsProcess(double delta)
    {
        ArcadeController.Update((float)delta);

        Aircraft.SetControls(
            ArcadeController.GetChannel("pitch"),
            ArcadeController.GetChannel("yaw"),
            ArcadeController.GetChannel("roll"),
            ArcadeController.GetChannel("throttle")
        );

        if (Input.IsActionPressed("action_primary"))
        {
            Hud.HitCount += Tagger.Tag();
        }
    }
}
