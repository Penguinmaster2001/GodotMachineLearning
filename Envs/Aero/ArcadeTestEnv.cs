
using System.Collections.Generic;
using Godot;
using PPO.Aero.Arcade;



namespace PPO.Envs.Aero;



public partial class ArcadeTestEnv : Node3D
{
    [Export]
    public ArcadeAircraft Aircraft;

    [Export]
    public ArcadeController ArcadeController;



    public override void _Ready()
    {
        ArcadeController.SetChannels(new Dictionary<string, int>
        {
            {"pitch", 0},
            {"roll", 1},
            {"yaw", 2},
            {"throttle", 3},
        });
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
