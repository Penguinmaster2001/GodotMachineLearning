
using System;



namespace PPO.Aero.Arcade;



public class ArcadeEngineParameters
{
    public float MaxThrust;
    public float DensityExp;
    public Func<float, float> MachThrust;
    public float ThrottleResponse;
}
