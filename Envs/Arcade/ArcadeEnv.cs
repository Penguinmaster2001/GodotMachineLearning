
using System;
using Godot;
using PPO.Envs.Chase;
using PPO.Envs.Common;
using PPO.Ppo;
using TorchSharp;



namespace PPO.Envs.Arcade;



public class ArcadeEnv : IEnv
{
    public string[] InputLabels { get; } = [
        "gndA",                                 // Altitude above ground
        "aSpd", "vSpd",                         // Airspeed, vertical speed
        "loVx", "loVy", "loVz",                 // Local velocities
        "loAx", "loAy", "loAz",                 // Local accelerations
        "pich", "sYaw", "cYaw",                 // Attitude, pitch (sin, cos) for yaw
        "sRol", "cRol", "sHed", "cHed",         // Attitude, (sin, cos) for roll and heading
        "gupX", "gupY", "gupZ",                 // Up vector
        "fwdX", "fwdY", "fwdZ",                 // Forward vector
        "Dpch", "Dyaw", "Drol", "Dhed",         // Angular velocity, turn rate
        "Apch", "Ayaw", "Arol",                 // Angular acceleration
        "alfa", "beta", "gama",                 // Angle of attack, sideslip angle, and flight path angle
        "dens", "mach",                         // Air density and mach number
        "Cpch", "Cyaw", "Crol",                 // Current attitude controls
        "Spch", "Syaw", "Srol",                 // Current control surface states
        "thtl", "thst",                         // Throttle and thrust state
        "spdE", "vSdE", "DHdE",                 // Target v speed, speed, and turn rate errors
        "aggr"                                  // Aggressiveness
    ];

    #region Normalization
    private const float _speedScale = 60.0f;           // airspeed / local velocity components (aSpd, loVx/y/z)
    private const float _verticalSpeedScale = 20.0f;   // vertical speed and speed-tracking error (vSpd, spdE)
    private const float _accelerationScale = 20.0f;    // local accelerations (loAx/y/z)
    private const float _pitchRange = Mathf.Pi / 2.0f; // pitch attitude
    private const float _angularRateScale = 3.0f;      // angular velocities (Dpch, Dyaw, Drol)
    private const float _angleOfAttackScale = 0.35f;   // alpha, sized to normal pre-stall AoA band
    private const float _sideslipScale = 0.20f;        // beta
    private const float _flightPathAngleScale = 0.35f; // gamma
    private const float _groundAltitudeScale = 500.0f; // gndA

    public Func<float, float>[] Normalizations { get; } = new Utils.NormalizationBuilder()
        .Add(NormalizationFunctions.DivideTanh(_groundAltitudeScale))    // gndA
        .Add(NormalizationFunctions.DivideTanh(_speedScale))             // aSpd
        .Add(NormalizationFunctions.DivideTanh(_verticalSpeedScale))     // vSpd
        .Add(NormalizationFunctions.DivideTanh(_speedScale), 3)          // loV
        .Add(NormalizationFunctions.DivideTanh(_accelerationScale), 3)   // loA
        .Add(NormalizationFunctions.Divide(_pitchRange))                 // pich
        .Add(NormalizationFunctions.Identity, 6)                         // sYaw, cYaw, sHed, cHed, sRol, cRol
        .Add(NormalizationFunctions.Identity, 3)                         // gup
        .Add(NormalizationFunctions.Identity, 3)                         // fwd
        .Add(NormalizationFunctions.DivideTanh(_angularRateScale), 4)    // Datt
        .Add(NormalizationFunctions.DivideTanh(_angularRateScale), 3)    // Aatt
        .Add(NormalizationFunctions.DivideTanh(_angleOfAttackScale))     // alfa
        .Add(NormalizationFunctions.DivideTanh(_sideslipScale))          // beta
        .Add(NormalizationFunctions.DivideTanh(_flightPathAngleScale))   // gama
        .Add(NormalizationFunctions.DivideTanh(2.0f))                    // pres
        .Add(NormalizationFunctions.DivideTanh(2.0f))                    // mach
        .Add(NormalizationFunctions.Identity, 3)                         // ctrl (C)
        .Add(NormalizationFunctions.Identity, 3)                         // ctrl (S)
        .Add(NormalizationFunctions.Identity, 2)                         // thtl, thst
        .Add(NormalizationFunctions.DivideTanh(_speedScale))             // spdE
        .Add(NormalizationFunctions.DivideTanh(_verticalSpeedScale))     // vSdE
        .Add(NormalizationFunctions.DivideTanh(_angularRateScale))       // DHdE
        .Add(NormalizationFunctions.Identity)                            // aggr
        .Build();
    #endregion
    public long InputSize => InputLabels.Length;

    public string[] OutputLabels { get; } = ["pich", "yaww", "roll", "thtl"];
    public long OutputSize => OutputLabels.Length;

    public int NumEnvs => _agents.Length;

    public (string, float)[] RewardStats { get; private set; } = [];
    private readonly Utils.RewardBuilder _rewardBuilder = new(["spdE", "vSpE", "DhdE", "beta", "bank", "ctEf", "acSm", "angA", "linA"]);
    public torch.Tensor PrevObservation { get; private set; }
    public torch.Tensor PrevActuation { get; private set; }

    private readonly ArcadeAircraftAgent[] _agents;
    private readonly TargetNode[] _targets;
    private readonly int[] _stepCounts;
    private readonly Action<ArcadeAircraftAgent, TargetNode> _reset;
    private readonly Action<ArcadeAircraftAgent, TargetNode> _resetTarget;


    // private const int _maxSteps = 128;
    private const int _maxSteps = 1024;
    // private const int _maxSteps = 8192;



    public ArcadeEnv(
        ArcadeAircraftAgent[] aircraft,
        TargetNode[] targets,
        Action<ArcadeAircraftAgent, TargetNode> reset,
        Action<ArcadeAircraftAgent, TargetNode> resetTarget)
    {
        _agents = aircraft;
        _targets = targets;
        _reset = reset;
        _resetTarget = resetTarget;

        _stepCounts = new int[aircraft.Length];
    }



    public torch.Tensor Reset()
    {
        var obs = new float[NumEnvs, InputSize];
        for (int i = 0; i < NumEnvs; i++)
        {
            ResetEnv(i);
            FillObsRow(obs, i);
        }
        return torch.tensor(obs);
    }



    public torch.Tensor Observe()
    {
        var obs = new float[NumEnvs, InputSize];
        for (int i = 0; i < NumEnvs; i++)
        {
            FillObsRow(obs, i);
        }
        var obsTensor = torch.tensor(obs);
        PrevObservation = obsTensor.cpu();
        return obsTensor;
    }



    public void Actuate(torch.Tensor action)
    {
        PrevActuation = action.cpu();
        for (int i = 0; i < NumEnvs; i++)
        {
            var agent = _agents[i];
            agent.SetControls(
                Mathf.Tanh(action[i, 0].item<float>()),
                Mathf.Tanh(action[i, 1].item<float>()),
                Mathf.Tanh(action[i, 2].item<float>()),
                agent.Aircraft.Throttle + Mathf.Tanh(action[i, 3].item<float>())
            );
        }
    }



    public (torch.Tensor reward, torch.Tensor terminated, torch.Tensor truncated) Evaluate(bool step = true)
    {
        var reward = new float[NumEnvs];
        var terminated = new bool[NumEnvs];
        var truncated = new bool[NumEnvs];

        for (int i = 0; i < NumEnvs; i++)
        {
            if (step)
            {
                _stepCounts[i]++;
            }

            var agent = _agents[i];
            var aircraft = agent.Aircraft;
            var target = _targets[i];

            agent.Age += aircraft.DeltaTime;
            aircraft.DeltaTime = 0.0f;

            // if (_stepCounts[i] % 1024 == 0)
            // {
            //     _resetTarget(_aircraft[i], _targets[i]);
            // }

            // target = _aircraft[(i + 1) % _aircraft.Length];

            const float _speedTolerance = 15.0f;
            const float _vSpeedTolerance = 5.0f;
            float _turnRateTolerance = 0.4f * agent.MaxRates.Z;
            const float _groundSafetyAltitude = 50.0f;
            const float _groundProximityPenalty = 2.0f;
            const float _crashPenalty = 500.0f;
            const float _sideslipTolerance = 0.03f;
            float aggressionMult = 1.0f - agent.Aggressiveness;
            var aggressionTolerance = 1.0f + (0.8f * agent.Aggressiveness);

            var actionDelta = agent.Action - agent.PrevControls;
            var actionSmoothnessPenalty = aggressionMult * actionDelta.Length();

            var angularAccelPenalty = aggressionMult * aircraft.AngularAcceleration.LengthSquared() * 0.1f / Mathf.Pi;
            var linearAccelPenalty = aggressionMult * aircraft.Acceleration.LengthSquared() * 0.01f;
            var controlEffortPenalty = aggressionMult * agent.Action.LengthSquared();
            var bankAngleIncentive = aggressionMult * TrackingReward(Mathf.Abs(Mathf.Atan2(aircraft.GlobalBasis.Y.X, aircraft.GlobalBasis.Y.Y)), aggressionTolerance * 0.25f);
            // var throttlePenalty = aggressionMult * TrackingReward(0.4f - aircraft.Throttle, 0.6f * aggressionTolerance, 4.0f);

            agent.TargetVSpeed = Mathf.Clamp((target.GlobalPosition.Y - aircraft.GlobalPosition.Y) / 25.0f, agent.MaxRates.X, agent.MaxRates.Y);

            var toTarget = target.GlobalPosition - aircraft.GlobalPosition;
            var headingToTarget = Mathf.Atan2(toTarget.X, -toTarget.Z);
            // var headingToTarget = target.GlobalRotation.Y;
            var headingError = Mathf.AngleDifference(aircraft.Heading, headingToTarget);
            var targetTurnRate = Mathf.Clamp(headingError * 0.15f, -agent.MaxRates.Z, agent.MaxRates.Z);
            // var targetTurnRate = 0.0f;
            // aircraft.TargetTurnRate = Mathf.MoveToward(aircraft.TargetTurnRate, targetTurnRate, Mathf.DegToRad(30.0f) / 10.0f);
            // aircraft.TargetTurnRate = targetTurnRate;

            static float TrackingReward(float error, float tolerance, float power = 1.5f)
            {
                // return 1.0f - Mathf.Clamp(Mathf.Abs(error) / tolerance, 0.0f, 1.0f);
                return 1.0f / (1.0f + Mathf.Pow(Mathf.Abs(error) / tolerance, power));
            }

            var crashed = aircraft.GlobalPosition.Y <= 0.0f;
            terminated[i] = crashed;

            bool timedOut = _stepCounts[i] >= _maxSteps;
            truncated[i] = timedOut && !crashed;

            var speed = -aircraft.LocalVelocity.Z;
            var speedErr = speed - agent.TargetSpeed;
            var vSpeedErr = aircraft.LinearVelocity.Y - agent.TargetVSpeed;
            var turnRateErr = aircraft.TurnRate - agent.TargetTurnRate;

            reward[i] = _rewardBuilder.SumRewards(
                  +1.00f * TrackingReward(speedErr, aggressionTolerance * _speedTolerance),
                  +1.50f * TrackingReward(vSpeedErr, aggressionTolerance * _vSpeedTolerance),
                  +3.00f * TrackingReward(turnRateErr, aggressionTolerance * _turnRateTolerance),
                  +0.08f * 1.0f * TrackingReward(aircraft.SideslipAngle, aggressionTolerance * _sideslipTolerance),
                  +0.10f * 0.0f * bankAngleIncentive,
                  -0.10f * 1.0f * controlEffortPenalty,
                  -0.10f * 1.0f * actionSmoothnessPenalty,
                  -0.15f * 1.0f * angularAccelPenalty,
                  -0.10f * 1.0f * linearAccelPenalty
            );

            if (aircraft.GlobalPosition.Y < _groundSafetyAltitude)
            {
                reward[i] -= _groundProximityPenalty;
            }

            if (crashed)
            {
                reward[i] -= _crashPenalty;
            }

            agent.Reward = reward[i];

            if (aircraft.GlobalPosition.DistanceTo(target.GlobalPosition) < 200.0f)
            {
                agent.HitStreak += 1;
                _resetTarget(agent, target);
            }
        }

        RewardStats = _rewardBuilder.GetAverage(true);

        return (torch.tensor(reward), torch.tensor(terminated), torch.tensor(truncated));
    }



    public void ResetEnv(int i)
    {
        _reset(_agents[i], _targets[i]);
        _stepCounts[i] = 0;
    }



    public void Close()
    {
    }



    private void FillObsRow(float[,] obs, int aircraftId)
    {
        var filler = new Utils.ObsRowFiller(aircraftId, obs, Normalizations);
        var agent = _agents[aircraftId];
        var aircraft = agent.Aircraft;
        var inverseBasis = aircraft.GlobalBasis.Transposed();

        // "gndA",                                 // Altitude above ground
        filler.Add(aircraft.GlobalPosition.Y);

        // "aSpd", "vSpd",                         // Airspeed, vertical speed
        filler.Add(aircraft.AirSpeed, aircraft.LinearVelocity.Y);

        // "loVx", "loVy", "loVz",                 // Local velocities
        filler.Add(aircraft.LocalVelocity);

        // "loAx", "loAy", "loAz",                 // Local accelerations
        filler.Add(inverseBasis * aircraft.Acceleration);

        // "pich", "sYaw", "cYaw",                 // Attitude, pitch (sin, cos) for yaw
        filler.Add(aircraft.GlobalRotation.X);
        filler.Add(Mathf.SinCos(aircraft.GlobalRotation.Y));
        // "sRol", "cRol", "sHed", "cHed",         // Attitude, (sin, cos) for roll and heading
        filler.Add(Mathf.SinCos(aircraft.GlobalRotation.Z));
        filler.Add(Mathf.SinCos(aircraft.Heading));

        // "gupX", "gupY", "gupZ",                 // Up vector
        filler.Add(aircraft.GlobalBasis.Y);

        // "fwdX", "fwdY", "fwdZ",                 // Forward vector
        filler.Add(-aircraft.GlobalBasis.Z);

        // "Dpch", "Dyaw", "Drol", "Dhed",         // Angular velocity, turn rate
        filler.Add(inverseBasis * aircraft.AngularVelocity);
        filler.Add(aircraft.TurnRate);

        // "Apch", "Ayaw", "Arol",                 // Angular acceleration
        filler.Add(inverseBasis * aircraft.AngularAcceleration);

        // "alfa", "beta", "gama",                 // Angle of attack, sideslip angle, and flight path angle
        filler.Add(aircraft.AoA, aircraft.SideslipAngle, aircraft.FlightPathAngle);

        // "dens", "mach",                         // Air density and mach number
        filler.Add(aircraft.WorldVars.AirDensity(aircraft.GlobalPosition),
            aircraft.AirSpeed / aircraft.WorldVars.SpeedOfSound(aircraft.GlobalPosition));

        // "Cpch", "Cyaw", "Crol",                 // Current attitude controls
        filler.Add(aircraft.Pitch, aircraft.Yaw, aircraft.Roll);

        // "Spch", "Syaw", "Srol",                 // Current control surface states
        filler.Add(aircraft.ControlSurfaceState);

        // "thtl", "thst",                         // Throttle and thrust state
        filler.Add(aircraft.Throttle, aircraft.Engine.Thrust / aircraft.Engine.Parameters.MaxThrust);

        // "spdE", "vSdE", "DHdE"                  // Target speed, v speed, and turn rate errors
        filler.Add(agent.TargetSpeed - aircraft.AirSpeed,
            agent.TargetVSpeed - aircraft.LinearVelocity.Y,
            Mathf.AngleDifference(agent.TargetTurnRate, aircraft.TurnRate));

        // "aggr"                                  // Aggressiveness
        filler.Add(agent.Aggressiveness);

        if (filler.Count != InputSize)
        {
            throw new Exception();
        }
    }
}
