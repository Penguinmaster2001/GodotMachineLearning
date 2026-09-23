
using System;
using System.Collections.Generic;
using Godot;
using PPO.Envs.Chase;
using PPO.Envs.Common;
using PPO.Ppo;



namespace PPO.Envs.Arcade;



public static class ArcadeEnvFactory
{
    public static (PpoOptions, IEnv) CreateEnv(
        List<ArcadeAircraftAgent> agents,
        List<TargetNode> targets,
        int num,
        Vector3 start,
        Vector3 end,
        Func<ArcadeAircraftAgent> chaserFactory,
        Func<TargetNode> targetFactory,
        Func<ResultIndicator> indicatorFactory,
        Action<Node> useNode)
    {
        var rng = new RandomNumberGenerator();
        var worldVars = new WorldVars();
        var initialStates = InitialStateManager.InitialStateContainer.Load(
            ProjectSettings.GlobalizePath("res://Data/initial_states.json"));

        for (int i = 0; i < num; i++)
        {
            var color = Utils.GenerateColor(i, num);

            var chaser = chaserFactory();
            chaser.Aircraft.SetColor(color);
            chaser.Aircraft.WorldVars = worldVars;
            agents.Add(chaser);
            useNode(chaser.Aircraft);

            var target = targetFactory();
            target.SetColor(color);
            targets.Add(target);
            useNode(target);
        }

        // var scale = 1000.0f;
        var env = new ArcadeEnv([.. agents], [.. targets], (c, t) =>
        {
            var success = c.Aircraft.GlobalPosition.DistanceTo(t.GlobalPosition) < 50.0f;
            var indicator = indicatorFactory();
            indicator.Position = c.Aircraft.Position;
            indicator.SetColor(success ? Color.FromOkHsl(0.33f, 1.0f, 0.5f) : Color.FromOkHsl(0.0f, 1.0f, 0.5f));
            useNode(indicator);

            c.HitStreak = 0;

            // if (success)
            // {
            //     c.HitStreak++;
            // }
            // else
            // {
            //     c.HitStreak = 0;
            // }
            var state = initialStates.Sample();
            var orientation = Basis.FromEuler(new(state.RotationXZ.X, rng.RandfRange(-Mathf.Pi, Mathf.Pi), state.RotationXZ.Y));
            c.ResetTo(Utils.RandVector3(rng, start, end), orientation, new(state.LocalVelocity.X, state.LocalVelocity.Y, -rng.RandfRange(50.0f, 120.0f)), state.LocalAngularVelocity);
            c.Aircraft.Throttle = 1.0f;
            t.GlobalPosition = Utils.RandVector3(rng, start, end);
            t.GlobalRotation = rng.RandfRange(-Mathf.Pi, Mathf.Pi) * Vector3.Up;
            // t.GlobalPosition = (c.GlobalPosition + (8.0f * scale * Vector3.Forward) + Utils.RandVector3(rng, -scale, scale)).Clamp(start, end);

            // c.LinearVelocity = 30.0f * Utils.RandVector3(rng);
            // c.AngularVelocity = 5.0f * Utils.RandVector3(rng);
            // c.Rotation = Mathf.DegToRad(rng.RandfRange(-20.0f, 20.0f)) * Vector3.Forward;
            c.Age = 0.0f;
            c.TargetSpeed = rng.RandfRange(65.0f, 75.0f);
            c.TargetVSpeed = 0.0f;
            c.TargetTurnRate = Mathf.Sign(rng.RandfRange(-1.0f, 1.0f)) * Mathf.DegToRad(rng.RandfRange(2.0f, 5.0f));
            c.Aggressiveness = rng.RandfRange(0.0f, 1.0f);
            c.MaxRates = new(
                rng.RandfRange(-20.0f, -5.0f),                  // V speed lower
                rng.RandfRange(2.0f, 8.0f),                     // V speed upper
                Mathf.DegToRad(rng.RandfRange(2.0f, 5.0f)),     // Turn rate
                0.0f);
        },
        (c, t) =>
        {
            t.GlobalPosition = Utils.RandVector3(rng, start, end);
            t.GlobalRotation = rng.RandfRange(-Mathf.Pi, Mathf.Pi) * Vector3.Up;
            c.TargetSpeed = rng.RandfRange(65.0f, 75.0f);
            c.TargetVSpeed = 0.0f;
            c.TargetTurnRate = Mathf.Sign(rng.RandfRange(-1.0f, 1.0f)) * Mathf.DegToRad(rng.RandfRange(2.0f, 5.0f));
            c.Aggressiveness = rng.RandfRange(0.0f, 1.0f);
            c.MaxRates = new(
                rng.RandfRange(-20.0f, -5.0f),
                rng.RandfRange(2.0f, 15.0f),
                Mathf.DegToRad(rng.RandfRange(2.0f, 5.0f)),
                0.0f);
        });

        env.Reset();

        var options = new PpoOptions
        {
            UseCuda = true,
            NumSteps = 1024,
            NumEnvs = env.NumEnvs,
            LearningRate = 3e-4,
            TotalTimesteps = 32_000_000,
            BatchSize = 1024 * env.NumEnvs,
            MinibatchSize = 8196,
            UpdateEpochs = 4,
            AnnealLR = false,
            EntCoef = 0.02,
            Gamma = 0.997,
            HiddenLayerSizes = [64, 64]
        };

        return (options, env);
    }
}
