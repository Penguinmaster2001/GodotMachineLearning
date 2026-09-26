
using Godot;
using PPO.Aero.Components;



namespace PPO.Aero.Arcade;



[GlobalClass]
public partial class ArcadeEngineResource : Resource, IComponentConfig<ArcadeEngineParameters>
{
    [Export]
    public float MaxThrust;

    [Export]
    public float DensityExp;

    [Export]
    public Curve MachThrust;

    [Export]
    public float ThrottleResponse;



    public ArcadeEngineParameters Create() => new()
    {
        MaxThrust = MaxThrust,
        DensityExp = DensityExp,
        MachThrust = MachThrust.SampleBaked,
        ThrottleResponse = ThrottleResponse,
    };
}
