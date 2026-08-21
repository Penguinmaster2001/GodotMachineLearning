
using System.Collections.Generic;
using System.Linq;
using Godot;
using PPO.Aero.Controls;
using PPO.Aero.Sensors;



namespace PPO.Aero;



public partial class Aircraft : RigidBody3D
{
    [Export]
    public AeroBody Root;

    [Export]
    public PackedScene DebugBlock;
    private List<Node3D> _debugBlocks = [];

    public AeroContext Context;

    public IControlSource Controller;
    public ChannelRouter<IControlSink, float> ControlChannels { get; set; }

    public ISensorSink Sensors;
    public ChannelReader<ISensorSource, float[]> SensorChannels { get; set; }

    public Vector3 PrevVel = Vector3.Zero;



    public void Build(AeroContext aeroContext, List<string> controlChannels, List<string> sensorChannels)
    {
        Context = aeroContext;
        ControlChannels = new(s => s.ControlChannel, controlChannels, (s, c) => s.SetCommand(c));
        SensorChannels = new(s => s.SensorChannel, sensorChannels, s => s.Read());

        var buildContext = new BuildContext()
        {
            ControlChannels = ControlChannels,
            SensorChannels = SensorChannels,
        };

        Mass = Root.Build(buildContext, Context);

        Controller.SetChannels(ControlChannels.ChannelToId);
        Sensors.SetChannels(SensorChannels.ChannelToId);
    }



    public override void _PhysicsProcess(double delta)
    {
        var inverseBasis = Basis.Transposed();
        var state = new AeroState()
        {
            DeltaTime = (float)delta,
            LocalLinearVelocity = inverseBasis * LinearVelocity,
            LocalAngularVelocity = inverseBasis * AngularVelocity,
            LocalLinearAcceleration = inverseBasis * (LinearVelocity - PrevVel) / (float)delta,
        };
        PrevVel = LinearVelocity;

        Controller.Update(state);
        ControlChannels.Route(Controller.Read());

        foreach (var block in _debugBlocks)
        {
            block.Visible = false;
            block.QueueFree();
        }
        _debugBlocks.Clear();
        var moments = Root.CalculateForces(state).ToArray();
        var (force, torque) = Moment.SumForceTorque(moments);
        foreach (var moment in moments.Append(new(force, Vector3.Zero)))
        {
            var block = DebugBlock.Instantiate<Node3D>();
            AddSibling(block);
            _debugBlocks.Add(block);
            block.GlobalPosition = GlobalTransform * moment.Offset;
            var f = GlobalBasis * moment.Force;
            var len = f.Length();
            if (!Mathf.IsZeroApprox(len))
            {
                var up = Vector3.Right;
                if (up.Cross(f).IsZeroApprox())
                {
                    up = Vector3.Back;
                }
                block.GlobalTransform = block.GlobalTransform.LookingAt(f, up);
                var scale = block.Scale;
                scale.Z = 0.2f * len;
                block.Scale = scale;
            }
        }

        ApplyCentralForce(GlobalBasis * force);
        ApplyTorque(GlobalBasis * torque);

        Sensors.Update(SensorChannels.Read<float[]>(s => [.. s.Select(v => v.Average())]));
    }
}
