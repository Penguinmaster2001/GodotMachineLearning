
namespace PPO.Aero.Components;



public interface IAeroComponent
{
    Moment Calculate(AeroBodyState state, AeroContext context);
}
