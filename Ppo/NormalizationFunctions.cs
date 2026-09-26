
using System;



namespace PPO.Ppo;



public static class NormalizationFunctions
{
    public static Func<float, float> Identity => (x) => x;



    public static Func<float, float> Divide(float s) => (x) => x / s;



    public static Func<float, float> DivideTanh(float s) => (x) => MathF.Tanh(0.549306144334f * x / s);
}
