
using Godot;



namespace PPO.Envs.Common;



public static class Utils
{
    public static Color GenerateColor(int i, int num)
    {
        var hue = (float)i / num;
        var (up, dn) = (0.85f, 0.5f);
        var (lightness, saturation) = (i % 4) switch
        {
            0 => (up, up),
            1 => (up, dn),
            2 => (dn, up),
            3 => (dn, dn),
            _ => (0.0f, 0.0f)
        };
        return Color.FromOkHsl(hue, saturation, lightness);
    }



    public static Vector3 RandVector3(RandomNumberGenerator rng, float min = -1.0f, float max = 1.0f)
    {
        return new(rng.RandfRange(min, max), rng.RandfRange(min, max), rng.RandfRange(min, max));
    }



    public static Vector3 RandVector3(RandomNumberGenerator rng, Vector3 min, Vector3 max)
    {
        return new(rng.RandfRange(min.X, max.X), rng.RandfRange(min.Y, max.Y), rng.RandfRange(min.Z, max.Z));
    }
}
