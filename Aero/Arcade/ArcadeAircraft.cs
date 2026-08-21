
using Godot;



namespace PPO.Aero.Arcade;



public partial class ArcadeAircraft : RigidBody3D
{
    #region Tunables

    [Export]
    public float ThrustForce = 20.0f;       // N, at full throttle
    [Export]
    public float DragCoefficient = 0.05f;    // opposes velocity, scales with speed^2
    [Export]
    public float LiftCoefficient = 1.5f;     // vertical force per (speed * gravity), scales with forward speed
    [Export]
    public float MinLiftSpeed = 3.0f;        // below this speed, lift falls off (stall-ish behavior)

    [Export]
    public float MaxPitchRate = 90.0f;       // deg/s at full input
    [Export]
    public float MaxRollRate = 180.0f;       // deg/s at full input
    [Export]
    public float MaxYawRate = 45.0f;         // deg/s at full input
    [Export]
    public float RateResponsiveness = 8.0f;  // higher = snappier tracking of target angular velocity

    #endregion

    #region Control inputs

    public float Pitch;    // + = nose up
    public float Roll;     // + = roll right
    public float Yaw;      // + = yaw right
    public float Throttle; // [0, 1]

    #endregion

    public Vector3 StartPosition;
    public Basis StartBasis;



    public override void _Ready()
    {
        StartPosition = GlobalPosition;
        StartBasis = GlobalBasis;
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

        Vector3 localVel = GlobalBasis.Transposed() * LinearVelocity;
        float forwardSpeed = -localVel.Z; // forward = -Z
        float speed = LinearVelocity.Length();

        // --- Thrust: along local forward ---
        Vector3 thrust = GlobalBasis * new Vector3(0, 0, -1) * (Throttle * ThrustForce);

        // --- Drag: opposes velocity, grows with speed^2, this is what lets speed settle ---
        Vector3 drag = speed > 0.01f
            ? -LinearVelocity.Normalized() * DragCoefficient * speed * speed
            : Vector3.Zero;

        // --- Lift: counters gravity, scales with forward speed, falls off below MinLiftSpeed (stall) ---
        float liftFactor = Mathf.Clamp(forwardSpeed / MinLiftSpeed, 0.0f, 1.0f);
        float liftMagnitude = LiftCoefficient * Mathf.Max(forwardSpeed, 0.0f) * liftFactor;
        Vector3 lift = GlobalBasis * new Vector3(0, 1, 0) * liftMagnitude;

        ApplyCentralForce(thrust + drag + lift);
        // Gravity is applied automatically by Godot's own physics (RigidBody3D.GravityScale),
        // no need to compute it manually here.

        // --- Rotation: directly command angular velocity toward a target, damped ---
        Vector3 targetLocalAngularVelocity = new(
            Mathf.DegToRad(Pitch * MaxPitchRate),
            Mathf.DegToRad(Yaw * MaxYawRate),
            Mathf.DegToRad(-Roll * MaxRollRate) // roll right = negative local Z rotation (right-hand rule about forward axis)
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
    }



    /// <summary>Basic observation vector, extend as needed for your policy.</summary>
    public float[] GetObservation()
    {
        Vector3 localVel = GlobalBasis.Transposed() * LinearVelocity;
        Vector3 localAngVel = GlobalBasis.Transposed() * AngularVelocity;
        Vector3 up = GlobalBasis.Y;
        Vector3 fwd = -GlobalBasis.Z;

        return new float[]
        {
            localVel.X, localVel.Y, localVel.Z,
            localAngVel.X, localAngVel.Y, localAngVel.Z,
            up.X, up.Y, up.Z,
            fwd.X, fwd.Y, fwd.Z,
        };
    }
}
