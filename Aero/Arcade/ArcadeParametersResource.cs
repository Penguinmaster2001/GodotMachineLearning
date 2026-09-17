
using Godot;
using PPO.Aero.Components;



namespace PPO.Aero.Arcade;



[GlobalClass]
public partial class ArcadeParametersResource : Resource, IComponentConfig<ArcadeParameters>
{
    [Export]
    public float DragCoefficient = 0.05f;

    [Export]
    public float InducedDragCoefficient = 0.02f;

    [Export]
    public float LiftMultiplier = 1.5f;

    [Export]
    public Curve LiftCurve;

    [Export]
    public float StallAoA;

    [Export]
    public Vector3 TurnRates;

    [Export]
    public float RateResponsiveness;

    [Export]
    public Vector2 YAccelerationLimits;

    [Export]
    public float AoALimit;

    [Export]
    public float AoALimitStrength;

    [Export]
    public float TurnTorqueMultiplier;

    [Export]
    public float ReferenceSpeed;

    [Export]
    public float MinRateScale = 0.1f;

    [Export]
    public float MaxRateScale = 2.0f;

    [Export]
    public float YawCorrectionStrength = 0.3f;

    [Export]
    public float PitchCorrectionStrength;

    [Export]
    public float AdverseYawStrength = 0.3f;

    [Export]
    public float DihedralStrength = 0.15f;

    [Export]
    public float StabilizerPitchStrength;

    [Export]
    public float SideslipRollStrength;

    [Export]
    public ArcadeEngineResource EngineParameters;



    public ArcadeParameters Create() => new()
    {
        DragCoefficient = DragCoefficient,
        InducedDragCoefficient = InducedDragCoefficient,
        LiftMultiplier = LiftMultiplier,
        LiftCurve = LiftCurve.SampleBaked,
        StallAoA = StallAoA,
        TurnRates = TurnRates,
        RateResponsiveness = RateResponsiveness,
        YAccelerationLimits = YAccelerationLimits,
        AoALimit = AoALimit,
        AoALimitStrength = AoALimitStrength,
        TurnTorqueMultiplier = TurnTorqueMultiplier,
        ReferenceSpeed = ReferenceSpeed,
        MinRateScale = MinRateScale,
        MaxRateScale = MaxRateScale,
        YawCorrectionStrength = YawCorrectionStrength,
        PitchCorrectionStrength = PitchCorrectionStrength,
        AdverseYawStrength = AdverseYawStrength,
        DihedralStrength = DihedralStrength,
        StabilizerPitchStrength = StabilizerPitchStrength,
        SideslipRollStrength = SideslipRollStrength,
        EngineParameters = EngineParameters.Create(),
    };
}
