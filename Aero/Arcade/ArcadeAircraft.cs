
using Godot;
using PPO.Ppo;



namespace PPO.Aero.Arcade;



public partial class ArcadeAircraft : RigidBody3D, IAgent
{
    #region Tunables
    [Export]
    public float DragCoefficient = 0.05f;    // opposes velocity, scales with speed^2

    [Export]
    public float InducedDragCoefficient = 0.02f;

    [Export]
    public float LiftMultiplier = 1.5f;      // overall lift scale (area * air density, lumped)

    [Export]
    public Curve LiftCurve;                  // Cl(AoA in degrees), gives stall angle + zero-angle lift shape

    [Export]
    public float StallAoA;

    [Export]
    public float MaxPitchRate = 90.0f;       // deg/s at full input, before speed scaling

    [Export]
    public float MaxRollRate = 180.0f;       // deg/s at full input, before speed scaling

    [Export]
    public float MaxYawRate = 45.0f;         // deg/s at full input, before speed scaling

    [Export]
    public float RateResponsiveness = 2.0f;  // higher = snappier tracking of target angular velocity

    [Export]
    public float ReferenceSpeed = 10.0f;     // speed at which pilot rate authority is nominal (scale = 1)

    [Export]
    public float MinRateScale = 0.1f;        // floor on control authority at low speed (0 = none at rest)

    [Export]
    public float MaxRateScale = 2.0f;        // cap on control authority at high speed

    [Export]
    public float WeathervaneStrength = 0.3f; // yaw rate per unit sideslip*speed, turns nose into the curve

    [Export]
    public float AdverseYawStrength = 0.3f; // yaw rate per unit sideslip*speed, turns nose into the curve

    [Export]
    public float DihedralStrength = 0.15f;   // roll rate per unit sideslip*speed, restoring roll from sideslip

    [Export]
    public ArcadeEngineResource EngineParams;

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
    public ArcadeEngine Engine { get; private set; }
    #endregion

    [Export]
    public MeshInstance3D Mesh;


    public Vector3 StartPosition;
    public Basis StartBasis;



    public override void _Ready()
    {
        StartPosition = GlobalPosition;
        StartBasis = GlobalBasis;

        Engine = EngineParams.Create();
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
        float dt = (float)delta;
        Age += dt;

        Acceleration = (LinearVelocity - _prevVel) / dt;
        _prevVel = LinearVelocity;

        var inverseBasis = GlobalBasis.Transposed();
        LocalVel = inverseBasis * LinearVelocity;
        var localAngVel = inverseBasis * AngularVelocity;
        float speed = LinearVelocity.Length();


        // --- Thrust: along local forward ---
        EngineParams.UpdateParams(Engine);
        Engine.Update(Throttle, WorldVars, LocalVel.Z, GlobalPosition, dt);
        Vector3 thrust = -Basis.Column2 * Engine.Thrust;


        // --- Lift: Cl(AoA) curve gives stall angle + zero-angle lift shape ---
        float u = -LocalVel.Z; // forward relative wind component
        float w = -LocalVel.Y; // downward relative wind component
        AoA = (Mathf.Abs(u) > 0.01f || Mathf.Abs(w) > 0.01f) ? Mathf.Atan2(w, u) : 0.0f;
        float liftPlaneSpeedSq = LocalVel.Y * LocalVel.Y + LocalVel.Z * LocalVel.Z; // excludes lateral/spanwise X

        float cl = LiftCurve is not null ? LiftCurve.SampleBaked(Mathf.RadToDeg(AoA)) : 0.0f;
        float liftMagnitude = LiftMultiplier * cl * liftPlaneSpeedSq;

        Vector3 liftDirLocal = new(0.0f, -LocalVel.Z, LocalVel.Y);
        Vector3 liftDir = liftDirLocal.LengthSquared() > 0.0001f ? liftDirLocal.Normalized() : Vector3.Zero;
        Vector3 lift = GlobalBasis * liftDir * liftMagnitude;


        // --- Drag: opposes velocity, grows with speed^2, this is what lets speed settle ---
        Vector3 drag = speed > 0.01f
            ? -LinearVelocity.Normalized() * DragCoefficient * speed * speed
            : Vector3.Zero;


        float inducedDrag = InducedDragCoefficient * cl * cl * liftPlaneSpeedSq;
        drag += speed > 0.01f ? -LinearVelocity.Normalized() * inducedDrag : Vector3.Zero;

        ApplyCentralForce(thrust + drag + lift);

        // --- Rotation: directly command angular velocity toward a target, damped ---
        float aoaDeg = Mathf.Abs(Mathf.RadToDeg(AoA));
        float controlEffectiveness = Mathf.Clamp(1.0f - (aoaDeg - StallAoA) / 10.0f, 0.2f, 1.0f);
        float rateScale = Mathf.Clamp(controlEffectiveness * speed * speed / (ReferenceSpeed * ReferenceSpeed), MinRateScale, MaxRateScale);

        // Weathervane + dihedral: aerodynamic effects from sideslip, scale naturally with speed, not clamped
        float sideVel = LocalVel.X;
        float weathervaneYawRate = -WeathervaneStrength * sideVel * speed / ReferenceSpeed;
        float adverseYawRate = AdverseYawStrength * Roll * Mathf.Abs(Roll) * speed / ReferenceSpeed;
        float dihedralRollRate = DihedralStrength * sideVel * speed / ReferenceSpeed;

        Vector3 targetLocalAngularVelocity = new(
            Mathf.DegToRad(Pitch * MaxPitchRate * rateScale),
            Mathf.DegToRad(Yaw * MaxYawRate * rateScale) + weathervaneYawRate + adverseYawRate,
            Mathf.DegToRad(-Roll * MaxRollRate * rateScale) + dihedralRollRate
        );
        Vector3 targetWorldAngularVelocity = GlobalBasis * targetLocalAngularVelocity;


        AngularVelocity = AngularVelocity.Lerp(targetWorldAngularVelocity, 1.0f - Mathf.Exp(-RateResponsiveness * dt));
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
        "aoa",
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

        return [
            LocalVel.X, LocalVel.Y, LocalVel.Z,
            localAcc.X, localAcc.Y, localAcc.Z,
            localAngVel.X, localAngVel.Y, localAngVel.Z,
            up.X, up.Y, up.Z,
            fwd.X, fwd.Y, fwd.Z,
            AoA,
            Pitch, Roll, Yaw,
            Throttle, Engine.Thrust / Engine.MaxThrust,
        ];
    }
}
