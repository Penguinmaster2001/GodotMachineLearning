
using System;
using Godot;
using PPO.Envs.Common;
using PPO.Ppo;



namespace PPO.Aero.Arcade;



public partial class ArcadeAircraft : RigidBody3D, IAgent
{
    [Export]
    public bool TuningMode = false;

    #region Tunables
    [Export]
    public ArcadeParametersResource ParametersResource;
    public WorldVars WorldVars = new();
    #endregion


    #region Control inputs
    public float Pitch;    // + = nose up
    public float Roll;     // + = roll right
    public float Yaw;      // + = yaw right
    public float Throttle; // [0, 1]
    #endregion


    #region RL data
    public Vector3 LocalVel;
    private Vector3 _prevVel;
    public Vector3 Acceleration { get; set; }
    public float AoA { get; private set; }
    public float Age { get; set; }
    public float Reward { get; set; }
    public int HitStreak { get; set; } = 0;
    public ArcadeParameters Parameters { get; private set; }
    public ArcadeEngine Engine { get; private set; } = new();
    #endregion

    [Export]
    public MeshInstance3D Mesh;


    public Vector3 StartPosition;
    public Basis StartBasis;



    public override void _Ready()
    {
        StartPosition = GlobalPosition;
        StartBasis = GlobalBasis;

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



    public void SetControls(float pitch, float roll, float yaw, float throttle)
    {
        Pitch = Mathf.Clamp(pitch, -1.0f, 1.0f);
        Roll = Mathf.Clamp(roll, -1.0f, 1.0f);
        Yaw = Mathf.Clamp(yaw, -1.0f, 1.0f);
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
        Age += dt;

        Acceleration = (LinearVelocity - _prevVel) / dt;
        _prevVel = LinearVelocity;

        var inverseBasis = GlobalBasis.Transposed();
        LocalVel = inverseBasis * LinearVelocity;
        var localAngVel = inverseBasis * AngularVelocity;
        float speed = LinearVelocity.Length();


        // --- Thrust ---
        Engine.Update(Throttle, WorldVars, -LocalVel.Z, GlobalPosition, dt);
        Vector3 thrust = -Basis.Column2 * Engine.Thrust;


        // --- Lift ---
        float u = -LocalVel.Z; // forward relative wind component
        float w = -LocalVel.Y; // downward relative wind component
        float v = -LocalVel.X;
        AoA = (Mathf.Abs(u) > 0.01f || Mathf.Abs(w) > 0.01f) ? Mathf.Atan2(w, u) : 0.0f;
        float hAoa = (Mathf.Abs(u) > 0.01f || Mathf.Abs(v) > 0.01f) ? Mathf.Atan2(v, u) : 0.0f;
        float liftPlaneSpeedSq = LocalVel.Y * LocalVel.Y + LocalVel.Z * LocalVel.Z; // excludes lateral/spanwise X

        float cl = Parameters.LiftCurve is not null ? Parameters.LiftCurve(Mathf.RadToDeg(AoA)) : 0.0f;
        float liftMagnitude = Parameters.LiftMultiplier * cl * liftPlaneSpeedSq;

        Vector3 liftDirLocal = new(0.0f, -LocalVel.Z, LocalVel.Y);
        Vector3 liftDir = liftDirLocal.LengthSquared() > 0.001f ? liftDirLocal.Normalized() : Vector3.Zero;
        Vector3 lift = GlobalBasis * liftDir * liftMagnitude;


        // --- Drag---
        Vector3 drag = speed > 0.01f
            ? -LinearVelocity.Normalized() * Parameters.DragCoefficient * speed * speed
            : Vector3.Zero;


        float inducedDrag = Parameters.InducedDragCoefficient * cl * cl * liftPlaneSpeedSq;
        drag += speed > 0.01f ? -LinearVelocity.Normalized() * inducedDrag : Vector3.Zero;

        ApplyCentralForce(thrust + drag + lift);

        // --- Rotation ---
        float aoaDeg = Mathf.Abs(Mathf.RadToDeg(AoA));
        float controlEffectiveness = Mathf.Clamp(1.0f - (aoaDeg - Parameters.StallAoA) / 10.0f, 0.2f, 1.0f);
        float rateScale = Mathf.Clamp(controlEffectiveness * speed * speed / (Parameters.ReferenceSpeed * Parameters.ReferenceSpeed), Parameters.MinRateScale, Parameters.MaxRateScale);
        float yawRateScale = Mathf.Clamp(Mathf.Clamp(1.0f - (Math.Abs(Mathf.RadToDeg(hAoa)) - Parameters.StallAoA) / 10.0f, 0.2f, 1.0f) * speed * speed / (Parameters.ReferenceSpeed * Parameters.ReferenceSpeed), Parameters.MinRateScale, Parameters.MaxRateScale);

        float sideVel = LocalVel.X;
        float upVel = LocalVel.Y;
        float stabilizerPitch = liftMagnitude * Parameters.StabilizerPitchStrength;
        float yawCorrectionRate = Mathf.Abs(sideVel) * -sideVel * Parameters.YawCorrectionStrength;
        float pitchCorrection = Mathf.Abs(upVel) * upVel * Parameters.PitchCorrectionStrength;
        float adverseYawRate = Parameters.AdverseYawStrength * Roll * Mathf.Abs(Roll) * speed / Parameters.ReferenceSpeed;
        float sideslipRollRate = Parameters.SideslipRollStrength * sideVel * speed / Parameters.ReferenceSpeed;
        float dihedralCorrection = liftMagnitude * Parameters.DihedralStrength * Mathf.Clamp(-GlobalRotation.Z / Mathf.Pi, -0.2f, 0.2f);

        // G-limiting
        float pitchRate = Mathf.Clamp(Mathf.DegToRad(Pitch * Parameters.TurnRates.X * rateScale), Parameters.YAccelerationLimits.X / Mathf.Abs(LocalVel.Z), Parameters.YAccelerationLimits.Y / Mathf.Abs(LocalVel.Z));

        // AoA Limiting
        float aoaMult = 1.0f;
        if (Mathf.Abs(AoA) > Mathf.DegToRad(Parameters.AoALimit))
        {
            aoaMult = Parameters.AoALimitStrength * Mathf.DegToRad(Parameters.AoALimit) / AoA;
        }
        GD.Print($"{aoaMult}\t{Mathf.DegToRad(Parameters.AoALimit)}\t{Mathf.Abs(AoA)}");

        Vector3 targetLocalAngularVelocity = new(
            aoaMult * pitchRate,
            Mathf.DegToRad(Yaw * Parameters.TurnRates.Y * yawRateScale) + adverseYawRate,
            Mathf.DegToRad(-Roll * Parameters.TurnRates.Z * rateScale) + sideslipRollRate
        );

        // Proportional controller
        Vector3 AngVelErr = Parameters.TurnTorqueMultiplier * (targetLocalAngularVelocity - localAngVel);
        Vector3 localAeroEffects = new(stabilizerPitch + pitchCorrection, yawCorrectionRate, dihedralCorrection);
        Vector3 worldTorque = GlobalBasis * ((Inertia * AngVelErr) + localAeroEffects);

        ApplyTorque(worldTorque);
    }



    /// <summary>Convenience reset for RL episode boundaries.</summary>
    public void ResetTo(Vector3 position, Basis basis, Vector3 linearVelocity = default, Vector3 angularVelocity = default)
    {
        GlobalPosition = position;
        GlobalBasis = basis;
        LinearVelocity = linearVelocity;
        AngularVelocity = angularVelocity;
        Pitch = Roll = Yaw = 0.0f;
        Throttle = 0.0f;
        Acceleration = Vector3.Zero;
        _prevVel = Vector3.Zero;
    }



    public string[] InputNames = [
        "velX", "velY", "velZ",
        "accX", "accY", "accZ",
        "Dpch", "Dyaw", "Drol",
        "gupX", "gupY", "gupZ",
        "fwdX", "fwdY", "fwdZ",
        "aofa",
        "head",
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
        float heading = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(fwd.X, -fwd.Z)), 360.0f);
        float vSpeed = LinearVelocity.Y;

        return [
            LocalVel.X, LocalVel.Y, LocalVel.Z,
            localAcc.X, localAcc.Y, localAcc.Z,
            localAngVel.X, localAngVel.Y, localAngVel.Z,
            up.X, up.Y, up.Z,
            fwd.X, fwd.Y, fwd.Z,
            AoA,
            heading,
            vSpeed,
            Pitch, Roll, Yaw,
            Throttle, Engine.Thrust / Engine.Parameters.MaxThrust,
        ];
    }
}
