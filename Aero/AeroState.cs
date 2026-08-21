
using Godot;



namespace PPO.Aero;



public record AeroState
{
    public float DeltaTime;

    public Vector3 LocalLinearVelocity;
    public Vector3 LocalAngularVelocity;
    public Vector3 LocalLinearAcceleration;



    public Vector3 VelocityAtPoint(Vector3 point)
    {
        return LocalLinearVelocity + LocalAngularVelocity.Cross(point);
    }



    public static AeroState operator *(Basis basis, AeroState aeroState) => new()
    {
        DeltaTime = aeroState.DeltaTime,
        LocalLinearVelocity = basis * aeroState.LocalLinearVelocity,
        LocalAngularVelocity = basis * aeroState.LocalAngularVelocity,
        LocalLinearAcceleration = basis * aeroState.LocalLinearAcceleration
    };



    public static AeroState operator *(AeroState aeroState, Basis basis) => basis * aeroState;
}
