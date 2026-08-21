
using Godot;



namespace PPO.Envs.Common;



public partial class ResultIndicator : MeshInstance3D
{
    [Export]
    public double Lifetime = 1.0;
    private double _clock = 0.0;

    private Vector3 _initialPos;



    public void SetColor(Color color)
    {
        MaterialOverride = new StandardMaterial3D()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
        };
    }



    public override void _Ready()
    {
        _initialPos = Position;
    }



    public override void _Process(double delta)
    {
        if (Lifetime <= _clock)
        {
            QueueFree();
        }

        Position = _initialPos + (0.5f * (float)Mathf.Pow(_clock / Lifetime, 2.0) * Vector3.Up);

        _clock += delta;
    }
}
