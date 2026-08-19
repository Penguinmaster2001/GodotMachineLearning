
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
    public float CurrentThrust;
    public float Throttle;

    [Export]
    public Vector3 TurnAuthority;
    public Vector3 Turning;

    [Export]
    public MeshInstance3D Mesh;



    public override void _PhysicsProcess(double delta)
    {
        Acceleration = (LinearVelocity - _prevVel) / (float)delta;
        _prevVel = LinearVelocity;
        Age += (float)delta;
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
        Turning = turning;
        ApplyTorque(GlobalBasis * (TurnAuthority * Turning));

        Throttle = throttle;
        CurrentThrust = Thrust * Throttle;
        ApplyForce(-Basis.Column2 * CurrentThrust);
    }
}
