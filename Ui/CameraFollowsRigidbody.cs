
using Godot;



namespace PPO.Ui;



public partial class CameraFollowsRigidbody : Node3D
{
    [Export]
    private RigidBody3D _toFollow;


    [Export]
    private Vector3 _followOffset;


    [Export]
    private float _mouseSensitivity;

    private Node3D _rotationOffset;
    private Vector3 _rotation;

    private Camera3D _camera;

    private double _timer;

    private bool _moving;



    public override void _Ready()
    {
        _rotationOffset = GetChild<Node3D>(0);
        _camera = _rotationOffset.GetChild<Camera3D>(0);
    }



    public override void _Process(double delta)
    {
        Position = _toFollow.GlobalPosition;

        Vector3 forward = _toFollow.LinearVelocity;

        // var up = 10.0f * (_toFollow.GlobalPosition - WorldVars.PlanetCenter);
        var up = Vector3.Up;
        _moving = forward.Length() > 25.0f;
        if (_moving && (_toFollow.GlobalPosition + forward).AngleTo(up) > 0.01f)
        {
            Transform = Transform.InterpolateWith(Transform.LookingAt(_toFollow.GlobalPosition + forward, Vector3.Up), (float)(0.99 * delta));

            _timer -= delta;
        }



        if (Input.IsMouseButtonPressed(MouseButton.Middle))
        {
            _rotation += -new Vector3(Input.GetLastMouseVelocity().Y, Input.GetLastMouseVelocity().X, 0.0f) / _mouseSensitivity;
            _timer = 5.0;
        }
        else if (_timer <= 0.0)
        {
            _rotation = _rotation.Slerp(Vector3.Zero, (float)(0.99 * delta));
        }

        if (Input.IsMouseButtonPressed(MouseButton.WheelUp))
        {
            _followOffset += Vector3.Forward;
        }

        if (Input.IsMouseButtonPressed(MouseButton.WheelDown))
        {
            _followOffset += Vector3.Back;
        }

        _rotationOffset.Rotation = _rotation;
        _camera.Position = _followOffset;
    }
}
