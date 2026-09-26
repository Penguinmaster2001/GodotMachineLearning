
using Godot;



public class WorldVars
{
    public float AirDensitySeaLevel { get; } = 1.2250f;
    public float AirDensityHalfAltitude { get; } = 30_000.0f;
    private float AirDensityExpMultiplier { get; }
    public float PlanetRadius { get; } = 600_000.0f;
    public float PlanetGravityAtRadius { get; } = 9.81f;
    private double PlanetGravityMultiplier { get; }
    public Vector3 PlanetCenter { get; }



    public WorldVars()
    {
        PlanetCenter = (50.0f + PlanetRadius) * new Vector3(0.0f, -1.0f, 0.0f).Normalized();
        PlanetGravityMultiplier = -(double)PlanetGravityAtRadius * PlanetRadius * PlanetRadius;
        AirDensityExpMultiplier = Mathf.Log(2.0f) / AirDensityHalfAltitude;
    }



    public float AirDensity(Vector3 position)
    {
        // position -= PlanetCenter;
        // return AirDensitySeaLevel * Mathf.Exp(AirDensityExpMultiplier * (PlanetRadius - position.Length()));
        position.Y -= PlanetCenter.Y;
        return AirDensitySeaLevel * Mathf.Exp(AirDensityExpMultiplier * (PlanetRadius - position.Y));
    }



    public float SpeedOfSound(Vector3 position)
    {
        return 330.0f + (position.Y * (280.0f - 330.0f) / 10000.0f);
    }



    public Vector3 Gravity(Vector3 position)
    {
        // position -= PlanetCenter;
        // return (float)(PlanetGravityMultiplier / (double)position.LengthSquared()) * position.Normalized();
        position.Y -= PlanetCenter.Y;
        return (float)(PlanetGravityMultiplier / (double)(position.Y * position.Y)) * Vector3.Up;

        // return 9.81f * Vector3.Up;
    }
}
