
using Godot;



namespace PPO.Running;



public partial class SimClock : Node
{
    [Export]
    public float MaxFps = 30.0f;

    [Export]
    public bool Enabled = false;

    private ulong _lastRenderTimeUsec;
    private int _step;



    public override async void _Ready()
    {
        if (!Enabled) return;

        RenderingServer.RenderLoopEnabled = false;
        Engine.PhysicsTicksPerSecond = 10_000_000;
        Engine.TimeScale = Engine.PhysicsTicksPerSecond / 60.0;

        _lastRenderTimeUsec = Time.GetTicksUsec();
        GetTree().Paused = true;

        while (true)
        {
            GetTree().Paused = false;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }


    long f = 0;
    public override async void _PhysicsProcess(double dt)
    {
        if (!Enabled) return;

        _step++;
        f++;

        var now = Time.GetTicksUsec();
        if ((now - _lastRenderTimeUsec) / 1_000_000.0 > 1.0 / MaxFps)
        {
            RenderingServer.ForceDraw();
            RenderingServer.ForceSync();
            _lastRenderTimeUsec = now;
            // GD.Print(f);
            f = 0;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        GetTree().Paused = true;
    }
}
