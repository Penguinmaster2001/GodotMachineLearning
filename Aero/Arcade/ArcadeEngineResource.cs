
using Godot;
using PPO.Aero.Components;



namespace PPO.Aero.Arcade;



[GlobalClass]
public partial class ArcadeEngineResource : Resource, IComponentConfig<ArcadeEngine>
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



    public ArcadeEngine Create() => new()
    {
        MaxThrust = MaxThrust,
        DensityExp = DensityExp,
        EngineMaxVel = EngineMaxVel,
        RamRecoveryVel = RamRecoveryVel,
        ThrottleResponse = ThrottleResponse,
    };



    public void UpdateParams(ArcadeEngine engine)
    {
        engine.MaxThrust = MaxThrust;
        engine.DensityExp = DensityExp;
        engine.EngineMaxVel = EngineMaxVel;
        engine.RamRecoveryVel = RamRecoveryVel;
        engine.ThrottleResponse = ThrottleResponse;
    }
}
