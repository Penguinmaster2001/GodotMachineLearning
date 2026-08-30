
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



    public override void _Ready()
    {
        ArcadeController.SetChannels(new Dictionary<string, int>
        {
            {"pitch", 0},
            {"roll", 1},
            {"yaw", 2},
            {"throttle", 3},
        });

        Hud.InputNames = Aircraft.InputNames;
        Hud.InputData = Aircraft.GetObservation;
    }



    public override void _PhysicsProcess(double delta)
    {
        ArcadeController.Update((float)delta);

        Aircraft.SetControls(
            ArcadeController.GetChannel("pitch"),
            ArcadeController.GetChannel("roll"),
            ArcadeController.GetChannel("yaw"),
            ArcadeController.GetChannel("throttle")
        );
    }
}
