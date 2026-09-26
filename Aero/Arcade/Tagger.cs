
using Godot;



namespace PPO.Aero.Arcade;



public partial class Tagger : Node3D
{
    [Export]
    public Node3D Source;


    [Export]
    private RayCast3D _raycast;

    [Export]
    private float _spread;

    [Export]
    private int _numPerFrame;

    private RandomNumberGenerator _rng = new();



    public int Tag()
    {
        var count = 0;
        GlobalTransform = Source.GlobalTransform;
        RotationOrder = EulerOrder.Zyx;
        for (int _ = 0; _ < _numPerFrame; _++)
        {
            _raycast.Rotation = Vector3.Zero;
            _raycast.RotateZ(_rng.RandfRange(-Mathf.Pi, Mathf.Pi));
            _raycast.RotateY(_rng.RandfRange(-Mathf.DegToRad(_spread), Mathf.DegToRad(_spread)));
            
            _raycast.ForceRaycastUpdate();
            if (_raycast.GetCollider() is CollisionObject3D collision)
            {
                count++;
            }
        }

        return count;
    }
}
