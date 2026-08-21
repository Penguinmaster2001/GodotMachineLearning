
using Godot;



namespace PPO.Aero;



public record AeroBodyState
{
    public float DeltaTime;
    public Vector3 CenterOfLift;
    public Vector3 GlobalCenterOfLift;
    public Vector3 CenterOfMass;
    public Vector3 GlobalCenterOfMass;
    public float AngleOfAttack;
    public Vector3 LocalLinearVelocity;
    public Vector3 LocalAngularVelocity;
}
