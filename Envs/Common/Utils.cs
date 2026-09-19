
using System;
using System.Collections.Generic;
using Godot;



namespace PPO.Envs.Common;



public static class Utils
{
    public static Color GenerateColor(int i, int num)
    {
        var angle = i % Mathf.Tau;
        var radius = Mathf.Sqrt(0.1f + (i * 0.8f / num));
        var height = 0.1f + 0.8f * ((100.0f * i / num) % 1.0f);

        var hue = angle / Mathf.Tau;
        var lightness = radius;
        var saturation = height;
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



    public static string FormatVector3(Vector3 vec, string format = "{0,11:000000.000}")
    {
        return $"({string.Format(format, vec.X)}, {string.Format(format, vec.Y)}, {string.Format(format, vec.Z)})";
    }



    public class NormalizationBuilder
    {
        private readonly List<Func<float, float>> _normalizations = [];



        public NormalizationBuilder Add(Func<float, float> func, int repeat = 1)
        {
            for (int i = 0; i < repeat; i++)
            {
                _normalizations.Add(func);
            }

            return this;
        }



        public Func<float, float>[] Build() => [.. _normalizations];
    }



    public class ObsRowFiller
    {
        private readonly int _row;
        private readonly float[,] _obs;
        private readonly Func<float, float>[] _normalizations;
        private int _index = 0;
        public int Count => _index;



        public ObsRowFiller(int row, float[,] obs, Func<float, float>[] normalizations)
        {
            _row = row;
            _obs = obs;
            _normalizations = normalizations;
        }



        public void Add(float x)
        {
            _obs[_row, _index] = _normalizations[_index](x);
            _index++;
        }



        public void Add((float a, float b) pair)
        {
            Add(pair.a);
            Add(pair.b);
        }



        public void Add((float a, float b, float c) tuple)
        {
            Add(tuple.a);
            Add(tuple.b);
            Add(tuple.c);
        }



        public void Add(Vector2 vec)
        {
            Add(vec.X);
            Add(vec.Y);
        }



        public void Add(Vector3 vec)
        {
            Add(vec.X);
            Add(vec.Y);
            Add(vec.Z);
        }



        public void Add(params float[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                Add(arr[i]);
            }
        }



        public void Add(Vector2[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                Add(arr[i]);
            }
        }



        public void Add(Vector3[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                Add(arr[i]);
            }
        }
    }
}
