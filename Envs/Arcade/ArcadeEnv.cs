
using System;
using Godot;
using PPO.Aero.Arcade;
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
        "pich", "sHed", "cHed", "sRol", "cRol", // Attitude, (sin, cos) for heading and roll
        "gupX", "gupY", "gupZ",                 // Up vector
        "fwdX", "fwdY", "fwdZ",                 // Forward vector
        "Dpch", "Dyaw", "Drol",                 // Angular velocity
        "alfa", "beta", "gama",                 // Angle of attack, sideslip angle, and flight path angle
        "Cpch", "Cyaw", "Crol",                 // Current attitude controls
        "Spch", "Syaw", "Srol",                 // Current control surface states
        "thtl", "thst",                         // Throttle and thrust state
        "spdE", "vSdE", "DHdE"                  // Target v speed, speed, and turn rate errors
        // TODO: Add aggression (how smooth / rapidly to correct errors), mach number, air density
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
        .Add(NormalizationFunctions.Identity, 4)                         // sHed, cHed, sRol, cRol
        .Add(NormalizationFunctions.Identity, 3)                         // gup
        .Add(NormalizationFunctions.Identity, 3)                         // fwd
        .Add(NormalizationFunctions.DivideTanh(_angularRateScale), 3)    // Datt
        .Add(NormalizationFunctions.DivideTanh(_angleOfAttackScale))     // alfa
        .Add(NormalizationFunctions.DivideTanh(_sideslipScale))          // beta
        .Add(NormalizationFunctions.DivideTanh(_flightPathAngleScale))   // gama
        .Add(NormalizationFunctions.Identity, 3)                         // ctrl (C)
        .Add(NormalizationFunctions.Identity, 3)                         // ctrl (S)
        .Add(NormalizationFunctions.Identity, 2)                         // thtl, thst
        .Add(NormalizationFunctions.DivideTanh(_speedScale))             // spdE
        .Add(NormalizationFunctions.DivideTanh(_verticalSpeedScale))     // vSdE
        .Add(NormalizationFunctions.DivideTanh(_angularRateScale))       // DHdE
        .Build();
    #endregion
    public long InputSize => InputLabels.Length;

    public string[] OutputLabels { get; } = ["thtl", "pich", "roll", "yaww"];
    public long OutputSize => OutputLabels.Length;

    public int NumEnvs => _aircraft.Length;

    private readonly ArcadeAircraft[] _aircraft;
    private readonly TargetNode[] _targets;
    private readonly int[] _stepCounts;
    private readonly Action<ArcadeAircraft, TargetNode> _reset;
    private readonly Action<ArcadeAircraft, TargetNode> _resetTarget;


    private int _maxSteps = 4096;

    public float TargetAltTolerance = 25.0f;



    public ArcadeEnv(
        ArcadeAircraft[] aircraft,
        TargetNode[] targets,
        Action<ArcadeAircraft, TargetNode> reset,
        Action<ArcadeAircraft, TargetNode> resetTarget)
    {
        _aircraft = aircraft;
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
        return torch.tensor(obs);
    }



    public void Actuate(torch.Tensor action)
    {
        for (int i = 0; i < NumEnvs; i++)
        {
            _aircraft[i].SetControls(
                action[i, 1].item<float>(),
                action[i, 2].item<float>(),
                action[i, 3].item<float>(),
                _aircraft[i].Throttle + (0.5f * action[i, 0].item<float>())
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

            // if (_stepCounts[i] % 128 == 0)
            // {
            //     _resetTarget(_aircraft[i], _targets[i]);
            // }

            var target = _targets[i];
            // var target = _aircraft[(i + 1) % _aircraft.Length];

            const float _speedTolerance = 15.0f;     // m/s
            const float _vSpeedTolerance = 1.0f;      // m/s
            const float _turnRateTolerance = 0.017f;    // rad/s
            const float _groundSafetyAltitude = 50.0f;
            const float _groundProximityPenalty = 2.0f;
            const float _crashPenalty = 500.0f;
            const float _actionSmoothnessWeight = 0.01f;
            const float _angularAccelSmoothnessWeight = 0.05f;
            const float _sideslipTolerance = 0.03f;
            const float _controlEffortWeight = 0.05f; 

            var action = new Vector3(_aircraft[i].Pitch, _aircraft[i].Yaw, _aircraft[i].Roll);
            var actionDelta = action - _aircraft[i].PrevControls;
            var actionSmoothnessPenalty = actionDelta.LengthSquared();
            _aircraft[i].PrevControls = action;

            var angularAccel = _aircraft[i].AngularVelocity - _aircraft[i].PrevAngularVel;
            var angularAccelPenalty = angularAccel.LengthSquared();
            var controlEffortPenalty = action.LengthSquared();

            _aircraft[i].TargetVSpeed = Mathf.Clamp(0.5f * (_aircraft[i].GlobalPosition.Y - target.GlobalPosition.Y), -15.0f, 7.0f);
            // _aircraft[i].TargetTurnRate = Mathf.DegToRad(Mathf.Clamp(0.5f * Mathf.RadToDeg(Mathf.AngleDifference(_aircraft[i].GlobalRotation.Y, _targets[i].GlobalRotation.Y)), -6.0f, 6.0f));
            var headingToTarget = Mathf.Atan2(_aircraft[i].GlobalPosition.X - target.GlobalPosition.X,
                _aircraft[i].GlobalPosition.Z - target.GlobalPosition.Z);
            var targetTurnRate = Mathf.DegToRad(Mathf.Clamp(0.5f * Mathf.RadToDeg(Mathf.AngleDifference(_aircraft[i].GlobalRotation.Y, headingToTarget)), -8.0f, 8.0f));
            // _aircraft[i].TargetTurnRate = Mathf.MoveToward(_aircraft[i].TargetTurnRate, targetTurnRate, 0.1f / 60.0f);
            _aircraft[i].TargetTurnRate = targetTurnRate;

            static float TrackingReward(float error, float tolerance, float power = 1.5f)
            {
                // return 1.0f - Mathf.Clamp(Mathf.Abs(error) / tolerance, 0.0f, 1.0f);
                return 1.0f / (1.0f + Mathf.Pow(Mathf.Abs(error) / tolerance, power));
            }

            var crashed = _aircraft[i].GlobalPosition.Y <= 0.0f;
            terminated[i] = crashed;

            bool timedOut = _stepCounts[i] >= _maxSteps;
            truncated[i] = timedOut && !crashed;

            var speed = -_aircraft[i].LocalVel.Z;
            var speedErr = speed - _aircraft[i].TargetSpeed;
            var vSpeedErr = _aircraft[i].LinearVelocity.Y - _aircraft[i].TargetVSpeed;
            var turnRateErr = _aircraft[i].TurnRate - _aircraft[i].TargetTurnRate;

            reward[i] =
                    0.80f * TrackingReward(speedErr, _speedTolerance)
                  + 1.20f * TrackingReward(vSpeedErr, _vSpeedTolerance)
                  + 2.00f * TrackingReward(turnRateErr, _turnRateTolerance)
                  + 0.05f * TrackingReward(_aircraft[i].SideslipAngle, _sideslipTolerance)
                  - 0.10f * controlEffortPenalty
                  - 0.02f * actionSmoothnessPenalty
                  - 0.02f * angularAccelPenalty;

            if (_aircraft[i].GlobalPosition.Y < _groundSafetyAltitude)
            {
                reward[i] -= _groundProximityPenalty;
            }

            if (crashed)
            {
                reward[i] -= _crashPenalty;
            }

            _aircraft[i].Reward = reward[i];
        }

        return (torch.tensor(reward), torch.tensor(terminated), torch.tensor(truncated));
    }



    public void ResetEnv(int i)
    {
        _reset(_aircraft[i], _targets[i]);
        _stepCounts[i] = 0;
    }



    public void Close()
    {
    }



    private void FillObsRow(float[,] obs, int aircraftId)
    {
        var filler = new Utils.ObsRowFiller(aircraftId, obs, Normalizations);
        var aircraft = _aircraft[aircraftId];
        var inverseBasis = aircraft.GlobalBasis.Transposed();
        var target = _targets[aircraftId];
        var speed = aircraft.LinearVelocity.Length();

        // "gndA",                                 // Altitude above ground
        filler.Add(aircraft.GlobalPosition.Y);

        // "aSpd", "vSpd",                         // Airspeed, vertical speed
        filler.Add(speed, aircraft.LinearVelocity.Y);

        // "loVx", "loVy", "loVz",                 // Local velocities
        filler.Add(aircraft.LocalVel);

        // "loAx", "loAy", "loAz",                 // Local accelerations
        filler.Add(inverseBasis * aircraft.Acceleration);

        // "pich", "sHed", "cHed", "sRol", "cRol", // Attitude, (sin, cos) for heading and roll
        filler.Add(aircraft.GlobalRotation.X);
        filler.Add(Mathf.SinCos(aircraft.GlobalRotation.Y));
        filler.Add(Mathf.SinCos(aircraft.GlobalRotation.Z));

        // "gupX", "gupY", "gupZ",                 // Up vector
        filler.Add(aircraft.GlobalBasis.X);

        // "fwdX", "fwdY", "fwdZ",                 // Forward vector
        filler.Add(aircraft.GlobalBasis.Z);

        // "Dpch", "Dyaw", "Drol",                 // Angular velocity
        filler.Add(inverseBasis * aircraft.AngularVelocity);

        // "alfa", "beta", "gama",                 // Angle of attack, sideslip angle, and flight path angle
        filler.Add(aircraft.AoA, aircraft.SideslipAngle, aircraft.FlightPathAngle);

        // "Cpch", "Cyaw", "Crol",                 // Current attitude controls
        filler.Add(aircraft.Pitch, aircraft.Yaw, aircraft.Roll);

        // "Spch", "Syaw", "Srol",                 // Current control surface states
        filler.Add(aircraft.ControlSurfaceState);

        // "thtl", "thst",                         // Throttle and thrust state
        filler.Add(aircraft.Throttle, aircraft.Engine.Thrust / aircraft.Engine.Parameters.MaxThrust);

        // "spdE", "vSdE", "DHdE"                  // Target speed, v speed, and turn rate errors
        filler.Add(aircraft.TargetSpeed - speed,
            aircraft.TargetVSpeed - aircraft.LinearVelocity.Y,
            aircraft.TargetTurnRate - aircraft.TurnRate);

        if (filler.Count != InputSize)
        {
            throw new Exception();
        }
    }
}
