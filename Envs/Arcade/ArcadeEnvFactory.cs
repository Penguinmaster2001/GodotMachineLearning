
using System;
using System.Collections.Generic;
using Godot;
using PPO.Aero.Arcade;
using PPO.Envs.Chase;
using PPO.Envs.Common;
using PPO.Ppo;



namespace PPO.Envs.Arcade;



public static class ArcadeEnvFactory
{
    public static (PpoOptions, IEnv) CreateEnv(
        List<ArcadeAircraft> aircraft,
        List<TargetNode> targets,
        int num,
        Vector3 start,
        Vector3 end,
        Func<ArcadeAircraft> chaserFactory,
        Func<TargetNode> targetFactory,
        Func<ResultIndicator> indicatorFactory,
        Action<Node> useNode)
    {
        var rng = new RandomNumberGenerator();

        for (int i = 0; i < num; i++)
        {
            var color = Utils.GenerateColor(i, num);

            var chaser = chaserFactory();
            chaser.SetColor(color);
            aircraft.Add(chaser);
            useNode(chaser);

            var target = targetFactory();
            target.SetColor(color);
            targets.Add(target);
            useNode(target);
        }

        var scale = 100.0f;
        var env = new ArcadeEnv([.. aircraft], [.. targets], (c, t) =>
        {
            var success = c.GlobalPosition.DistanceTo(t.GlobalPosition) < 50.0f;
            var indicator = indicatorFactory();
            indicator.Position = c.Position;
            indicator.SetColor(success ? Color.FromOkHsl(0.33f, 1.0f, 0.5f) : Color.FromOkHsl(0.0f, 1.0f, 0.5f));
            useNode(indicator);

            if (success)
            {
                c.HitStreak++;
            }
            else
            {
                c.HitStreak = 0;
            }

            c.ResetTo(Utils.RandVector3(rng, start, end), Basis.Identity, 100.0f * Vector3.Forward);
            t.GlobalPosition = c.GlobalPosition + (rng.RandfRange(-scale, scale) * Vector3.Up);
            // t.GlobalPosition = (c.GlobalPosition + (8.0f * scale * Vector3.Forward) + Utils.RandVector3(rng, -scale, scale)).Clamp(start, end);

            // c.LinearVelocity = 30.0f * Utils.RandVector3(rng);
            // c.AngularVelocity = 5.0f * Utils.RandVector3(rng);
            // c.Rotation = Mathf.Tau * Utils.RandVector3(rng);
            c.Age = 0.0f;
            c.TargetSpeed = 55.0f;
            c.TargetVSpeed = 0.0f;
            c.TargetTurnRate = 0.0f;
        },
        (c, t) =>
        {
            t.GlobalPosition = (t.GlobalPosition + (8.0f * scale * c.GlobalBasis.Column2) + Utils.RandVector3(rng, -scale, scale)).Clamp(start, end);
        });

        env.Reset();

        var options = new PpoOptions
        {
            UseCuda = true,
            NumSteps = 512,
            NumEnvs = env.NumEnvs,
            LearningRate = 3e-4,
            TotalTimesteps = 32_000_000,
            BatchSize = 512 * env.NumEnvs,
            MinibatchSize = 8196,
            UpdateEpochs = 4,
            AnnealLR = true,
            EntCoef = 0.001,
            HiddenLayerSizes = [32, 32]
        };

        return (options, env);
    }
}
