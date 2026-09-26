
namespace PPO.Aero.Components;



public interface IComponentConfig<out T>
{
    T Create();
}
