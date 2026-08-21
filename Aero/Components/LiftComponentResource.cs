
using Godot;



namespace PPO.Aero.Components;



[GlobalClass]
public partial class LiftComponentResource : Resource, IComponentConfig<LiftComponent>
{
    [Export]
    public float DragMultiplier;

    [Export]
    public float DragCoefficient;

    [Export]
    public float LiftingArea;

    [Export]
    public float LiftMultiplier;

    [Export]
    public Curve ColAoaCurve;



    public LiftComponent Create() => new()
    {
        DragMultiplier = DragMultiplier,
        DragCoefficient = DragCoefficient,
        LiftingArea = LiftingArea,
        LiftMultiplier = LiftMultiplier,
        ColAoaCurve = ColAoaCurve,
    };
}
