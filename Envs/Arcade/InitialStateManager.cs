
using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using PPO.Aero.Arcade;



namespace PPO.Envs.Arcade;



public static class InitialStateManager
{
    public record State(
        Vector3 ControlSurfaceState, Vector4 InputState,
        Vector3 LocalVelocity, Vector3 LocalAcceleration,
        Vector2 RotationXZ, Vector3 LocalAngularVelocity, Vector3 LocalAngularAcceleration)
    {
        public static State CopyFromAircraft(ArcadeAircraft aircraft)
        {
            var inverseBasis = aircraft.GlobalBasis.Transposed();
            return new(
                aircraft.ControlSurfaceState,
                new(aircraft.Pitch, aircraft.Yaw, aircraft.Roll, aircraft.Throttle),
                aircraft.LocalVelocity,
                inverseBasis * aircraft.Acceleration,
                new(aircraft.GlobalRotation.X, aircraft.GlobalRotation.Z),
                inverseBasis * aircraft.AngularVelocity,
                inverseBasis * aircraft.AngularAcceleration);
        }



        public State Mirrored()
        {
            return new(
                ControlSurfaceState * new Vector3(-1, -1, 1),
                InputState * new Vector4(1, -1, -1, 1),
                LocalVelocity * new Vector3(-1, 1, 1),
                LocalAcceleration * new Vector3(-1, 1, 1),
                RotationXZ * new Vector2(1, -1),
                LocalAngularVelocity * new Vector3(1, -1, -1),
                LocalAngularAcceleration * new Vector3(1, -1, -1));
        }
    }



    public static readonly JsonSerializerOptions InitialStateJsonOptions = new()
    {
        IncludeFields = true,
    };



    public class InitialStateContainer
    {
        private readonly State[] _states;
        private readonly RandomNumberGenerator _rng = new();


        private InitialStateContainer(State[] states)
        {
            _states = states;
        }



        public State Sample() => _states[_rng.Randi() % _states.Length];



        public static InitialStateContainer Load(string path)
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            var states = JsonSerializer.Deserialize<State[]>(file.GetAsText(), InitialStateJsonOptions) ?? throw new NullReferenceException();

            return new(states);
        }
    }




    public class InitialStateRecorder
    {
        private readonly List<State> _states = [];



        public void Record(ArcadeAircraft aircraft)
        {
            var state = State.CopyFromAircraft(aircraft);
            _states.Add(state);
            _states.Add(state.Mirrored());
        }



        public void SaveRecordedStates(string path)
        {
            var json = JsonSerializer.Serialize(_states, InitialStateJsonOptions);
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file.StoreString(json);
        }
    }
}
