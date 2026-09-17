
using System;
using Godot;



namespace PPO.Aero.Arcade;



public class ArcadeParameters
{
    public float DragCoefficient = 0.05f;
    public float InducedDragCoefficient = 0.02f;
    public float LiftMultiplier = 1.5f;
    public Func<float, float> LiftCurve;
    public float StallAoA;
    public Vector3 TurnRates;
    public float RateResponsiveness;
    public Vector2 YAccelerationLimits;
    public float AoALimit;
    public float AoALimitStrength;
    public float TurnTorqueMultiplier;
    public float ReferenceSpeed;
    public float MinRateScale = 0.1f;
    public float MaxRateScale = 2.0f;
    public float YawCorrectionStrength = 0.3f;
    public float PitchCorrectionStrength = 0.3f;
    public float AdverseYawStrength = 0.3f;
    public float DihedralStrength = 0.15f;
    public float StabilizerPitchStrength;
    public float SideslipRollStrength;
    public ArcadeEngineParameters EngineParameters;
}
