
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;



namespace PPO.Aero.Arcade;



public partial class ArcadeController : Node
{
    [Export]
    public float ThrottleResponse;

    [Export]
    private Vector3 _trimSpeeds;

    [Export]
    public Godot.Collections.Array<string> Channels = [];

    private readonly List<float> _state = [];
    private IReadOnlyDictionary<string, int> _channelToId;



    public void Update(float deltaTime)
    {
        // state.SpoilerInput = 0.0f;
        // if (Input.IsActionPressed("spoilers"))
        // {
        //     state.SpoilerInput = 1.0f;
        // }

        // if (Input.IsActionPressed("trim pitch up"))
        // {
        //     state.PitchTrimInput -= _trimSpeeds.X * state.DeltaTime;
        // }

        // if (Input.IsActionPressed("trim pitch down"))
        // {
        //     state.PitchTrimInput += _trimSpeeds.X * state.DeltaTime;
        // }
        // state.PitchTrimInput = Mathf.Clamp(state.PitchTrimInput, -1.0f, 1.0f);

        // state.TrimLevels = new(state.PitchTrimInput, 0.0f, 0.0f);


        // if (Input.IsActionJustPressed("flaps"))
        // {
        //     state.FlapInput = state.FlapInput > 0.0f ? 0.0f : 1.0f;

        //     GD.Print($"Flaps: {state.FlapInput}");
        // }

        // if (Input.IsActionJustPressed("gear toggle"))
        // {
        //     state.GearDown = !state.GearDown;
        // }

        // state.BrakeInput = 0.0f;
        // if (Input.IsActionPressed("brakes"))
        // {
        //     state.BrakeInput = 1.0f;
        // }

        SetChannel("pitch", Input.GetAxis("pitch_down", "pitch_up"));
        SetChannel("roll", Input.GetAxis("roll_left", "roll_right"));
        SetChannel("yaw", Input.GetAxis("yaw_right", "yaw_left"));

        ThrustInput(deltaTime);
    }



    private void ThrustInput(float deltaTime)
    {
        if (Input.IsActionPressed("throttle_up"))
        {
            ModChannel("throttle", t => t + ThrottleResponse * deltaTime);
        }

        if (Input.IsActionPressed("throttle_down"))
        {
            ModChannel("throttle", t => t - ThrottleResponse * deltaTime);
        }

        ModChannel("throttle", t => t + Input.GetActionStrength("throttle_axis") * ThrottleResponse * deltaTime);

        if (Input.IsPhysicalKeyPressed(Key.X))
        {
            SetChannel("throttle", 0.0f);
        }

        if (Input.IsPhysicalKeyPressed(Key.Z))
        {
            SetChannel("throttle", 1.0f);
        }

        ModChannel("throttle", t => Mathf.Clamp(t, -0.2f, 1.0f));
    }



    private void SetChannel(string channel, float input)
    {
        if (!_channelToId.TryGetValue(channel, out var id)) return;

        _state[id] = input;
    }



    private void ModChannel(string channel, Func<float, float> func)
    {
        if (!_channelToId.TryGetValue(channel, out var id)) return;

        _state[id] = func(_state[id]);
    }



    public List<float> Read() => _state;



    public void SetChannels(IReadOnlyDictionary<string, int> channelToId)
    {
        _channelToId = channelToId;
        var len = _channelToId.Values.Max() + 1;

        _state.Clear();
        for (int i = 0; i < len; i++)
        {
            _state.Add(0.0f);
        }
    }



    public float GetChannel(string channel)
    {
        return _state[_channelToId[channel]];
    }
}
