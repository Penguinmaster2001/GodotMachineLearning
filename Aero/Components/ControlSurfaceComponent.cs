
using Godot;
using PPO.Aero.Controls;
using PPO.Aero.Sensors;



namespace PPO.Aero.Components;



public class ControlSurfaceComponent : LiftComponent, IAeroComponent, IControlSink, ISensorSource
{
    #region config
    public float MaxAngle;
    public float ZeroAngle;
    public float MinAngle;
    public float ActuationSpeed;
    public float ControlMultiplier;

    public string ControlChannel { get; set; }
    public string SensorChannel { get; set; }
    #endregion

    #region state
    public float CurrentAngle;
    public float TargetAngle;
    #endregion



    public override Moment Calculate(AeroBodyState state, AeroContext context)
    {
        if (CurrentAngle < TargetAngle)
        {
            CurrentAngle += state.DeltaTime * ActuationSpeed;
            CurrentAngle = Mathf.Min(CurrentAngle, TargetAngle);
        }
        else if (CurrentAngle > TargetAngle)
        {
            CurrentAngle -= state.DeltaTime * ActuationSpeed;
            CurrentAngle = Mathf.Max(CurrentAngle, TargetAngle);
        }
        
        state.AngleOfAttack += Mathf.DegToRad(CurrentAngle);

        return base.Calculate(state, context);
    }



    public float[] Read()
    {
        float normalized;
        if (CurrentAngle <= ZeroAngle)
        {
            normalized = -Mathf.InverseLerp(ZeroAngle, MinAngle, CurrentAngle);
        }
        else
        {
            normalized = Mathf.InverseLerp(ZeroAngle, MaxAngle, CurrentAngle);
        }

        return [normalized / ControlMultiplier];
    }



    public void SetCommand(float value)
    {
        value *= ControlMultiplier;
        if (value <= 0.0f)
        {
            TargetAngle = Mathf.Lerp(ZeroAngle, MinAngle, -value);
        }
        else
        {
            TargetAngle = Mathf.Lerp(ZeroAngle, MaxAngle, value);
        }
    }
}