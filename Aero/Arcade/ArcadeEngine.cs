
using System;
using Godot;



namespace PPO.Aero.Arcade;



public class ArcadeEngine
{
    public ArcadeEngineParameters Parameters;

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
            EngineSpool += delta * Parameters.ThrottleResponse;
            EngineSpool = MathF.Min(EngineSpool, throttle);
        }
        else if (EngineSpool > throttle)
        {
            EngineSpool -= delta * Parameters.ThrottleResponse;
            EngineSpool = MathF.Max(EngineSpool, throttle);
        }

        EngineSpool = Math.Clamp(EngineSpool, 0.0f, 1.0f);

        Density = MathF.Pow(worldVars.AirDensity(globalPosition) / worldVars.AirDensitySeaLevel, Parameters.DensityExp);
        AirspeedTerm = 1.0f - (airspeed / Parameters.EngineMaxVel);
        RamAirTerm = airspeed * airspeed / (Parameters.RamRecoveryVel * Parameters.RamRecoveryVel);

        Thrust = EngineSpool * Parameters.MaxThrust * Density * (AirspeedTerm + RamAirTerm);
    }
}
