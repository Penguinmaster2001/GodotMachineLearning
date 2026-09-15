
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
    public float EngineMaxVel;

    [Export]
    public float RamRecoveryVel;

    [Export]
    public float ThrottleResponse;



    public ArcadeEngineParameters Create() => new()
    {
        MaxThrust = MaxThrust,
        DensityExp = DensityExp,
        EngineMaxVel = EngineMaxVel,
        RamRecoveryVel = RamRecoveryVel,
        ThrottleResponse = ThrottleResponse,
    };
}
