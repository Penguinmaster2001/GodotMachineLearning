
using Godot;



namespace PPO.Envs.Ball;



public partial class BallNode : RigidBody3D
{
    private Vector3 _prevVel;
    public Vector3 Acceleration { get; set; }
    public float age;

    [Export]
    public MeshInstance3D Mesh;



    public override void _PhysicsProcess(double delta)
    {
        Acceleration = (LinearVelocity - _prevVel) / (float)delta;
        age += (float)delta;
    }



    public void SetColor(Color color)
    {
        if (Mesh.MaterialOverride is StandardMaterial3D mat)
        {
            mat.AlbedoColor = color;
        }
    }
}
