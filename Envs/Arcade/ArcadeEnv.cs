
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
        "altE", "sHdE", "cHdE", "spdE"          // Target altitude, heading (sin, cos), and speed errors
    ];

    #region Normalization
    private const float _speedScale = 60.0f;           // airspeed / local velocity components (aSpd, loVx/y/z)
    private const float _verticalSpeedScale = 20.0f;   // vertical speed and speed-tracking error (vSpd, spdE)
    private const float _accelerationScale = 20.0f;    // local accelerations (loAx/y/z)
    private const float _pitchRange = Mathf.Pi / 2.0f; // pitch attitude
    private const float _angularRateScale = 6.0f;      // angular velocities (Dpch, Dyaw, Drol)
    private const float _angleOfAttackScale = 0.35f;   // alpha, sized to normal pre-stall AoA band
    private const float _sideslipScale = 0.20f;        // beta
    private const float _flightPathAngleScale = 0.35f; // gamma
    private const float _groundAltitudeScale = 500.0f; // gndA
    private const float _altitudeErrorScale = 100.0f;  // altE

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
        .Add(NormalizationFunctions.DivideTanh(_altitudeErrorScale))     // altE
        .Add(NormalizationFunctions.Identity, 2)                         // sHdE, cHdE
        .Add(NormalizationFunctions.DivideTanh(_verticalSpeedScale))     // spdE
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


    private int _maxSteps = 128;

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

            var crashed = _aircraft[i].GlobalPosition.Y <= 0.0f;
            terminated[i] = crashed;

            bool timedOut = _stepCounts[i] >= _maxSteps;
            truncated[i] = timedOut && !crashed;


            var speedIncentive = Mathf.Clamp((-_aircraft[i].LocalVel.Z - 40.0f) / 60.0f, -1.0f, 3.0f);
            var altIncentive = Mathf.Clamp(_aircraft[i].GlobalPosition.Y / _targets[i].GlobalPosition.Y, 0.0f, 1.0f);
            reward[i] = altIncentive + speedIncentive;
            var angleToTarget = Mathf.Atan2(_aircraft[i].GlobalPosition.X - _targets[i].GlobalPosition.X,
                _aircraft[i].GlobalPosition.Z - _targets[i].GlobalPosition.Z);
            var angleErr = Mathf.AngleDifference(_aircraft[i].GlobalRotation.Y, angleToTarget);

            // reward[i] += (0.1f - Mathf.Abs(angleErr)) / 3.0f;
            // reward[i] -= (_aircraft[i].GlobalPosition.DistanceTo(_targets[i].GlobalPosition) - 50.0f) / 300.0f;

            if (_aircraft[i].GlobalPosition.DistanceTo(_targets[i].GlobalPosition) < 200.0f)
            {
                reward[i] += 200.0f;
                _resetTarget(_aircraft[i], _targets[i]);
            }

            if (_aircraft[i].LocalVel.Z > -40.0f)
            {
                reward[i] -= 3.0f;
            }

            if (Mathf.Abs(_aircraft[i].GlobalPosition.Y - _targets[i].GlobalPosition.Y) < TargetAltTolerance)
            {
                reward[i] += 5.0f;
            }

            if (_aircraft[i].GlobalPosition.Y < 50.0f)
            {
                reward[i] -= 2.0f;
            }

            if (crashed)
            {
                reward[i] -= 500.0f;
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

        // "altE", "sHdE", "cHdE", "spdE"          // Target altitude, heading (sin, cos), and speed errors
        filler.Add(aircraft.GlobalPosition.Y - target.GlobalPosition.Y);
        var angleToTarget = Mathf.Atan2(aircraft.GlobalPosition.X - target.GlobalPosition.X,
            aircraft.GlobalPosition.Z - target.GlobalPosition.Z);
        filler.Add(Mathf.SinCos(Mathf.AngleDifference(aircraft.GlobalRotation.Y, angleToTarget)));
        filler.Add(aircraft.TargetSpeed - speed);

        if (filler.Count != InputSize)
        {
            throw new Exception();
        }
    }
}
