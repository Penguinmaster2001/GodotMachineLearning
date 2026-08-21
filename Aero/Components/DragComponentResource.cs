
using Godot;



namespace PPO.Aero.Components;



[GlobalClass]
public partial class DragComponentResource : Resource, IComponentConfig<DragComponent>
{
    [Export]
    public float DragMultiplier;

    [Export]
    public float DragCoefficient;



    public DragComponent Create() => new()
    {
        DragMultiplier = DragMultiplier,
        DragCoefficient = DragCoefficient,
    };
}
