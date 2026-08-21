
namespace PPO.Aero.Components;



public interface IComponentConfig<out T>
    where T : IAeroComponent
{
    T Create();
}
