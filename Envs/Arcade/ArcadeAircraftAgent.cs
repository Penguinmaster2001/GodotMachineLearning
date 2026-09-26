
using Godot;
using PPO.Aero.Arcade;
using PPO.Ppo;



namespace PPO.Envs.Arcade;



public class ArcadeAircraftAgent : IAgent
{
    public ArcadeAircraft Aircraft { get; set; }

    public float Age { get; set; }
    public float Reward { get; set; }
    public int HitStreak { get; set; }

    #region Commands
    public float Aggressiveness { get; set; }
    public float TargetSpeed { get; set; }
    public float TargetVSpeed { get; set; }
    public float TargetTurnRate { get; set; }
    public Vector4 MaxRates { get; set; }
    public Vector3 Action { get; set; }
    #endregion

    #region State
    public Vector3 PrevControls { get; set; }
    #endregion



    public ArcadeAircraftAgent(ArcadeAircraft aircraft)
    {
        Aircraft = aircraft;
    }



    public void ResetTo(Vector3 position, Basis basis, Vector3 linearVelocity = default, Vector3 angularVelocity = default)
    {
        Aircraft.ResetTo(position, basis, linearVelocity, angularVelocity);
        
        Action = Vector3.Zero;
    }



    public void SetControls(float pitch, float yaw, float roll, float throttle)
    {
        PrevControls = new(Aircraft.Pitch, Aircraft.Yaw, Aircraft.Roll);
        Action = new(pitch, yaw, roll);
        Aircraft.SetControls(pitch, yaw, roll, throttle);
    }
}
