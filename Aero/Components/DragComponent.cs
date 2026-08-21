
namespace PPO.Aero.Components;



public class DragComponent : IAeroComponent
{
    public float DragMultiplier;
    public float DragCoefficient;



    public Moment Calculate(AeroBodyState state, AeroContext context)
    {
        float drag = DragMultiplier
            * DragCoefficient
            * state.LocalLinearVelocity.LengthSquared();

        return new(drag * -state.LocalLinearVelocity.Normalized(), state.CenterOfLift);
    }
}
