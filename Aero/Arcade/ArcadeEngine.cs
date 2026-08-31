
using Godot;



namespace PPO.Aero.Arcade;



public class ArcadeEngine
{
    #region Parameters
    public float MaxThrust;
    public float DensityExp;
    public float EngineMaxVel;
    public float RamRecoveryVel;
    public float ThrottleResponse;
    #endregion

    #region State
    public float EngineSpool { get; set; }
    public float Thrust { get; set; }
    public float Density { get; private set; }
    public float AirspeedTerm { get; private set; }
    public float RamAirTerm { get; private set; }
    #endregion




    public void Update(float throttle, WorldVars worldVars, float airspeed, Vector3 globalPosition, float delta)
    {
        if (EngineSpool < throttle)
        {
            EngineSpool += delta * ThrottleResponse;
            EngineSpool = Mathf.Min(EngineSpool, throttle);
        }
        else if (EngineSpool > throttle)
        {
            EngineSpool -= delta * ThrottleResponse;
            EngineSpool = Mathf.Max(EngineSpool, throttle);
        }

        EngineSpool = Mathf.Clamp(EngineSpool, 0.0f, 1.0f);

        Density = Mathf.Pow(worldVars.AirDensity(globalPosition) / worldVars.AirDensitySeaLevel, DensityExp);
        AirspeedTerm = 1.0f - (airspeed / EngineMaxVel);
        RamAirTerm = airspeed * airspeed / (RamRecoveryVel * RamRecoveryVel);

        Thrust = EngineSpool * MaxThrust * Density * (AirspeedTerm + RamAirTerm);
    }
}
