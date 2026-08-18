
using Godot;



namespace PPO.Envs.Ball;



public partial class BallNode : RigidBody3D
{
    private Vector3 _prevVel;
    public Vector3 Acceleration { get; set; }
    public float age;



    public override void _PhysicsProcess(double delta)
    {
        Acceleration = (LinearVelocity - _prevVel) / (float)delta;
        age += (float)delta;
    }
}
