
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



    public static string FormatVector3(Vector3 vec, string format = "{0:0.000}")
    {
        return $"({string.Format(format, vec.X)}, {string.Format(format, vec.Y)}, {string.Format(format, vec.Z)})";
    }



    public class ObsRowFiller
    {
        private readonly int _row;
        private readonly float[,] _obs;
        private int _index = 0;
        public int Count => _index;



        public ObsRowFiller(int row, float[,] obs)
        {
            _row = row;
            _obs = obs;
        }



        public void Float(float x)
        {
            _obs[_row, _index] = x;
            _index++;
        }



        public void Array(float[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                Float(arr[i]);
            }
        }



        public void Vec3(Vector3 vec)
        {
            Float(vec.X);
            Float(vec.Y);
            Float(vec.Z);
        }
    }
}
