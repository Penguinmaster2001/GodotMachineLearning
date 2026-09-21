
using Godot;



namespace PPO.Envs.Arcade;



public static class InitialStateManager
{
    private record State(
        Vector3 ControlSurfaceState, Vector4 InputState,
        Vector3 LocalVelocity, Vector3 LocalAcceleration,
        Vector2 RotationXZ, Vector3 LocalAngularVelocity, Vector3 LocalAngularAcceleration);


    
}
