
using System;
using Godot;



namespace PPO.Aero.Arcade;



public partial class ArcadeAircraft : RigidBody3D
{
    #region Config
    [Export]
    public bool TuningMode = false;

    [Export]
    public ArcadeParametersResource ParametersResource;
    public WorldVars WorldVars { get; set; }

    [Export]
    public MeshInstance3D Mesh;
    #endregion


    #region Control inputs
    public float Roll { get; set; }     // + = roll right
    public float Yaw { get; set; }      // + = yaw right
    public float Pitch { get; set; }    // + = nose up
    public float Throttle { get; set; } // [0, 1]
    #endregion


    #region State
    public float AirSpeed { get; private set; }
    public Vector3 LocalVelocity { get; private set; }
    public Vector3 PrevVelocity { get; private set; }
    public Vector3 PrevAngularVel { get; private set; }
    public Vector3 Acceleration { get; set; }
    public Vector3 AngularAcceleration { get; set; }
    public float AoA { get; private set; }
    public float SideslipAngle { get; private set; }
    public float FlightPathAngle { get; private set; }
    public ArcadeParameters Parameters { get; private set; }
    public ArcadeEngine Engine { get; private set; } = new();
    public Vector3 ControlSurfaceState { get; private set; }
    public float Heading { get; private set; }
    public float PrevHeading { get; private set; }
    public float TurnRate { get; private set; }
    public float DeltaTime { get; set; }
    #endregion



    public override void _Ready()
    {
        Parameters = ParametersResource.Create();
        Engine.Parameters = Parameters.EngineParameters;
    }



    public void SetColor(Color color)
    {
        Mesh.MaterialOverride = new StandardMaterial3D()
        {
            AlbedoColor = color,
        };
    }



    public void SetControls(float pitch, float yaw, float roll, float throttle)
    {
        Pitch = Mathf.Clamp(pitch, -1.0f, 1.0f);
        Yaw = Mathf.Clamp(yaw, -1.0f, 1.0f);
        Roll = Mathf.Clamp(roll, -1.0f, 1.0f);
        Throttle = Mathf.Clamp(throttle, 0.0f, 1.0f);
    }



    public override void _PhysicsProcess(double delta)
    {
        if (TuningMode)
        {
            Parameters = ParametersResource.Create();
            Engine.Parameters = Parameters.EngineParameters;
        }

        float dt = (float)delta;
        DeltaTime += dt;

        Acceleration = (LinearVelocity - PrevVelocity) / dt;
        PrevVelocity = LinearVelocity;
        AngularAcceleration = (AngularVelocity - PrevAngularVel) / dt;
        PrevAngularVel = AngularVelocity;
        Heading = Mathf.Atan2(-GlobalBasis.Z.X, GlobalBasis.Z.Z);
        TurnRate = Mathf.AngleDifference(Heading, PrevHeading) / dt;
        PrevHeading = Heading;

        var inverseBasis = GlobalBasis.Transposed();
        LocalVelocity = inverseBasis * LinearVelocity;
        var localAngVel = inverseBasis * AngularVelocity;
        AirSpeed = LinearVelocity.Length();


        // --- Thrust ---
        Engine.Update(Throttle, WorldVars, -LocalVelocity.Z, GlobalPosition, dt);
        Vector3 thrust = -GlobalBasis.Z * Engine.Thrust;


        // --- Lift ---
        float u = -LocalVelocity.Z; // forward relative wind component
        float w = -LocalVelocity.Y; // downward relative wind component
        float v = -LocalVelocity.X;
        AoA = (Mathf.Abs(u) > 0.01f || Mathf.Abs(w) > 0.01f) ? Mathf.Atan2(w, u) : 0.0f;
        SideslipAngle = (Mathf.Abs(u) > 0.01f || Mathf.Abs(v) > 0.01f) ? Mathf.Atan2(v, u) : 0.0f;
        FlightPathAngle = AirSpeed > 0.01f ? Mathf.Asin(LinearVelocity.Y / AirSpeed) : 0.0f;
        float liftPlaneSpeedSq = LocalVelocity.Y * LocalVelocity.Y + LocalVelocity.Z * LocalVelocity.Z; // excludes lateral/spanwise X

        float cl = Parameters.LiftCurve is not null ? Parameters.LiftCurve(Mathf.RadToDeg(AoA)) : 0.0f;
        float liftMagnitude = Parameters.LiftMultiplier * cl * liftPlaneSpeedSq;

        Vector3 liftDirLocal = new(0.0f, -LocalVelocity.Z, LocalVelocity.Y);
        Vector3 liftDir = liftDirLocal.LengthSquared() > 0.001f ? liftDirLocal.Normalized() : Vector3.Zero;
        Vector3 lift = GlobalBasis * liftDir * liftMagnitude;


        // --- Drag---
        Vector3 drag = AirSpeed > 0.01f
            ? -LinearVelocity.Normalized() * Parameters.DragCoefficient * AirSpeed * AirSpeed
            : Vector3.Zero;

        float inducedDrag = Parameters.InducedDragCoefficient * cl * cl * liftPlaneSpeedSq;
        drag += AirSpeed > 0.01f ? -LinearVelocity.Normalized() * inducedDrag : Vector3.Zero;

        ApplyCentralForce(thrust + drag + lift);

        // --- Rotation ---
        float aoaDeg = Mathf.Abs(Mathf.RadToDeg(AoA));
        float controlEffectiveness = Mathf.Clamp(1.0f - (aoaDeg - Parameters.StallAoA) / 10.0f, 0.2f, 1.0f);
        float rateScale = Mathf.Clamp(controlEffectiveness * AirSpeed * AirSpeed / (Parameters.ReferenceSpeed * Parameters.ReferenceSpeed), Parameters.MinRateScale, Parameters.MaxRateScale);
        float yawRateScale = Mathf.Clamp(Mathf.Clamp(1.0f - (Math.Abs(Mathf.RadToDeg(SideslipAngle)) - Parameters.StallAoA) / 10.0f, 0.2f, 1.0f) * AirSpeed * AirSpeed / (Parameters.ReferenceSpeed * Parameters.ReferenceSpeed), Parameters.MinRateScale, Parameters.MaxRateScale);

        float sideVel = LocalVelocity.X;
        float upVel = LocalVelocity.Y;
        float stabilizerPitch = liftMagnitude * Parameters.StabilizerPitchStrength;
        float yawCorrectionRate = Mathf.Abs(sideVel) * -sideVel * Parameters.YawCorrectionStrength;
        float pitchCorrection = Mathf.Abs(upVel) * upVel * Parameters.PitchCorrectionStrength;
        float adverseYawRate = Parameters.AdverseYawStrength * Roll * Mathf.Abs(Roll) * AirSpeed / Parameters.ReferenceSpeed;
        float sideslipRollRate = Parameters.SideslipRollStrength * sideVel * AirSpeed / Parameters.ReferenceSpeed;
        float dihedralCorrection = liftMagnitude * Parameters.DihedralStrength * Mathf.Clamp(-GlobalRotation.Z / Mathf.Pi, -0.2f, 0.2f);

        // G-limiting
        float limitedPitch = Pitch;
        if (Mathf.Abs(LocalVelocity.Z) > 0.01f)
        {
            limitedPitch = Mathf.Clamp(Pitch * Mathf.DegToRad(Parameters.TurnRates.X) * rateScale, Parameters.YAccelerationLimits.X / Mathf.Abs(LocalVelocity.Z), Parameters.YAccelerationLimits.Y / Mathf.Abs(LocalVelocity.Z)) / (Mathf.DegToRad(Parameters.TurnRates.X) * rateScale);
        }

        // AoA Limiting
        float aoaMult = 1.0f;
        if (Mathf.Abs(AoA) > Mathf.DegToRad(Parameters.AoALimit))
        {
            aoaMult = Parameters.AoALimitStrength * Mathf.DegToRad(Parameters.AoALimit) / AoA;
        }
        limitedPitch *= aoaMult;

        Vector3 targetControls = new(limitedPitch, Yaw, -Roll);
        Vector3 targetLocalAngVel = targetControls * Parameters.TurnRates * Mathf.Pi / 180.0f;

        // Proportional controller
        Vector3 AngVelErr = Parameters.ControllerGains * (targetLocalAngVel - localAngVel);
        ControlSurfaceState = ControlSurfaceState.MoveToward(AngVelErr.Clamp(-1.0f, 1.0f), Parameters.RateResponsiveness * dt);
        // ControlSurfaceState = ControlSurfaceState.MoveToward(targetControls.Clamp(-1.0f, 1.0f), Parameters.RateResponsiveness * dt);
        // GD.Print($"{Utils.FormatVector3(AngVelErr)}\t{Utils.FormatVector3(ControlSurfaceState)}\t{Utils.FormatVector3(Inertia * ControlSurfaceState * new Vector3(rateScale, yawRateScale, rateScale))}");

        // ControlSurfaceState = ControlSurfaceState.MoveToward(targetControls, Parameters.RateResponsiveness * dt);

        // Vector3 targetLocalAngularVelocity =
        //     (Parameters.TurnRates * ControlSurfaceState * new Vector3(rateScale, yawRateScale, rateScale) * Mathf.Pi / 180.0f)
        //     + new Vector3(0.0f, adverseYawRate, sideslipRollRate);

        Vector3 localAeroEffects = new(stabilizerPitch + pitchCorrection, yawCorrectionRate, dihedralCorrection);
        Vector3 worldTorque = GlobalBasis * ((Inertia * Parameters.TurnTorqueMultiplier * (ControlSurfaceState + new Vector3(0.0f, adverseYawRate, sideslipRollRate)) * new Vector3(rateScale, yawRateScale, rateScale)) + localAeroEffects);

        ApplyTorque(worldTorque);
    }



    /// <summary>Convenience reset for RL episode boundaries.</summary>
    public void ResetTo(Vector3 position, Basis basis, Vector3 linearVelocity = default, Vector3 angularVelocity = default)
    {
        GlobalPosition = position;
        GlobalBasis = basis;
        LinearVelocity = basis * linearVelocity;
        AngularVelocity = basis * angularVelocity;
        Pitch = Roll = Yaw = 0.0f;
        Throttle = 0.0f;
        PrevVelocity = linearVelocity;
        Acceleration = Vector3.Zero;
        PrevAngularVel = angularVelocity;
        ControlSurfaceState = Vector3.Zero;

        Engine.Reset();
    }



    public string[] InputNames = [
        "velX", "velY", "velZ",
        "accX", "accY", "accZ",
        "Dpch", "Dyaw", "Drol",
        "gupX", "gupY", "gupZ",
        "fwdX", "fwdY", "fwdZ",
        "alfa",
        "bank",
        "head",
        "thed",
        "hedE",
        "dhed",
        "vSpd",
        "pich", "roll", "yaww",
        "thtl", "thst",
    ];



    public float[] GetObservation()
    {
        var inverseBasis = GlobalBasis.Transposed();
        Vector3 localAcc = inverseBasis * Acceleration;
        Vector3 localAngVel = inverseBasis * AngularVelocity;
        Vector3 up = GlobalBasis.Y;
        Vector3 fwd = -GlobalBasis.Z;
        var target = new Vector3(0.0f, 500.0f, -5000.0f);
        var horizontalToTarget = (target - GlobalPosition) * new Vector3(1.0f, 0.0f, 1.0f);
        var headingToTarget = Mathf.Atan2(horizontalToTarget.X, -horizontalToTarget.Z);
        float headingError = Mathf.AngleDifference(Heading, headingToTarget);
        float vSpeed = LinearVelocity.Y;
        var bank = Mathf.Abs(Mathf.Atan2(GlobalBasis.Y.X, GlobalBasis.Y.Y));

        return [
            LocalVelocity.X, LocalVelocity.Y, LocalVelocity.Z,
            localAcc.X, localAcc.Y, localAcc.Z,
            localAngVel.X, localAngVel.Y, localAngVel.Z,
            up.X, up.Y, up.Z,
            fwd.X, fwd.Y, fwd.Z,
            AoA,
            Mathf.RadToDeg(bank),
            Mathf.PosMod(Mathf.RadToDeg(Heading), 360.0f),
            Mathf.PosMod(Mathf.RadToDeg(headingToTarget), 360.0f),
            Mathf.RadToDeg(headingError),
            Mathf.RadToDeg(TurnRate),
            vSpeed,
            ControlSurfaceState.X, ControlSurfaceState.Z, ControlSurfaceState.Y,
            Throttle, Engine.Thrust / Engine.Parameters.MaxThrust,
        ];
    }
}
