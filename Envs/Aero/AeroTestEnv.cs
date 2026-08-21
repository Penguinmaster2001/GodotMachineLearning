
using Godot;
using PPO.Aero;
using PPO.Aero.Controls;
using PPO.Aero.Sensors;



namespace PPO.Envs.Aero;



public partial class AeroTestEnv : Node3D
{
    [Export]
    public Aircraft Aircraft;

    [Export]
    public PlayerController PlayerController;

    public WorldVars WorldVars = new();



    public override void _Ready()
    {
        Aircraft.Controller = PlayerController;
        Aircraft.Sensors = new PrintingSensorSink();

        Aircraft.Build(new(WorldVars), [.. PlayerController.Channels], []);
    }
}
