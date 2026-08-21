
using System;
using System.Collections.Generic;
using Godot;
using PPO.Envs.Common;
using PPO.Ppo;



namespace PPO.Envs.Chase;



public static class ChaserEnvFactory
{
    public static (PpoOptions, IEnv) CreateEnv(
        List<ChaserNode> chasers,
        List<TargetNode> targets,
        int num,
        Vector3 start,
        Vector3 end,
        Func<ChaserNode> chaserFactory,
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
            chasers.Add(chaser);
            useNode(chaser);

            var target = targetFactory();
            target.SetColor(color);
            targets.Add(target);
            useNode(target);
        }

        var env = new ChaserEnv([.. chasers], [.. targets], (c, t) =>
        {
            var success = c.GlobalPosition.DistanceTo(t.GlobalPosition) < 30.0f;
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

            c.Position = Utils.RandVector3(rng, start, end);

            t.Position = c.Position + Utils.RandVector3(rng, -500.0f, 500.0f);

            c.LinearVelocity = 30.0f * Utils.RandVector3(rng);
            c.Acceleration = Vector3.Zero;
            c.AngularVelocity = 5.0f * Utils.RandVector3(rng);
            c.Rotation = Mathf.Tau * Utils.RandVector3(rng);
            c.Age = 0.0f;
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
