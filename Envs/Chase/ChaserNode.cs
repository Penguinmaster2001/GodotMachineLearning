
using Godot;



namespace PPO.Envs.Chase;



public partial class ChaserNode : RigidBody3D
{
    private Vector3 _prevVel;
    public Vector3 Acceleration { get; set; }
    public float Age;
    public float Reward;

    [Export]
    public float Thrust;

    [Export]
    public Vector2 ThrottleLimits;
    public float CurrentThrust;
    public float Throttle;

    [Export]
    public Vector3 TurnAuthority;

    [Export]
    public Vector3 TurnLimits;
    public Vector3 Turning;

    [Export]
    public MeshInstance3D Mesh;



    public override void _PhysicsProcess(double delta)
    {
        Acceleration = (LinearVelocity - _prevVel) / (float)delta;
        _prevVel = LinearVelocity;
        Age += (float)delta;
        
        ApplyTorque(GlobalBasis * (TurnAuthority * Turning));
        ApplyForce(-Basis.Column2 * CurrentThrust);
    }



    public void SetColor(Color color)
    {
        Mesh.MaterialOverride = new StandardMaterial3D()
        {
            AlbedoColor = color,
        };
    }



    public void UpdateControls(float throttle, Vector3 turning)
    {
        Turning = (turning / 4.0f).Clamp(-TurnLimits, TurnLimits);

        Throttle = Mathf.Clamp(throttle / 4.0f, ThrottleLimits.X, ThrottleLimits.Y);
        CurrentThrust = Thrust * Throttle;
    }
}
