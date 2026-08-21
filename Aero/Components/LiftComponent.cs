
using Godot;



namespace PPO.Aero.Components;



public class LiftComponent : IAeroComponent
{
    public float DragMultiplier;
    public float DragCoefficient;
    public float LiftingArea;
    public float LiftMultiplier;
    public Curve ColAoaCurve;



    public virtual Moment Calculate(AeroBodyState state, AeroContext context)
    {
        Vector2 liftVel = new(state.LocalLinearVelocity.Z, state.LocalLinearVelocity.Y);
        float liftVelSquared = liftVel.LengthSquared();
        float liftCoefficient = ColAoaCurve.SampleBaked(Mathf.RadToDeg(state.AngleOfAttack));
        float lift = LiftMultiplier
            * LiftingArea
            * context.WorldVars.AirDensity(state.GlobalCenterOfLift)
            * liftVelSquared
            * liftCoefficient;

        Vector3 liftDirection = new Vector3(0.0f, -liftVel.X, liftVel.Y).Normalized();

        float drag = DragMultiplier
            * liftCoefficient * liftCoefficient
            * state.LocalLinearVelocity.LengthSquared();

        Vector3 dragDirection = -state.LocalLinearVelocity.Normalized();

        var moment = new Moment((lift * liftDirection) + (drag * dragDirection), state.CenterOfLift);

        // GD.Print($"state: {state}, liftVel: {liftVel} liftCoefficient: {liftCoefficient} lift: {lift} liftDirection: {liftDirection} drag: {drag} dragDirection: {dragDirection} moment: {moment}");

        return moment;
    }
}