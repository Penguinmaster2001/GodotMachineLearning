
using Godot;



namespace PPO.Aero.Components;



[GlobalClass]
public partial class ControlSurfaceComponentResource : Resource, IComponentConfig<ControlSurfaceComponent>
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

    [Export]
    public float MaxAngle;

    [Export]
    public float ZeroAngle;

    [Export]
    public float MinAngle;

    [Export]
    public float ActuationSpeed;

    [Export]
    public float ControlMultiplier;

    [Export]
    public string ControlChannel { get; set; }

    [Export]
    public string SensorChannel { get; set; }



    public ControlSurfaceComponent Create() => new()
    {
        DragMultiplier = DragMultiplier,
        DragCoefficient = DragCoefficient,
        LiftingArea = LiftingArea,
        LiftMultiplier = LiftMultiplier,
        ColAoaCurve = ColAoaCurve,
        MaxAngle = MaxAngle,
        ZeroAngle = ZeroAngle,
        MinAngle = MinAngle,
        ActuationSpeed = ActuationSpeed,
        ControlMultiplier = ControlMultiplier,
        ControlChannel = ControlChannel,
        SensorChannel = SensorChannel,
    };
}
