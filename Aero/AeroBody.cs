
using System.Collections.Generic;
using System.Linq;
using Godot;
using PPO.Aero.Components;
using PPO.Aero.Controls;
using PPO.Aero.Sensors;



namespace PPO.Aero;



public partial class AeroBody : Node3D
{
    [Export]
    public bool Enabled = true;

    [Export]
    protected float _mass;

    [Export]
    public virtual Vector3 CenterOfLift { get; set; }

    [Export]
    public virtual Vector3 CenterOfMass { get; set; }
    public virtual AeroContext Context { get; set; }

    [Export]
    public Godot.Collections.Array<Resource> Components;

    protected AeroBodyState _state;
    protected readonly List<AeroBody> _subBodies = [];
    protected readonly List<IAeroComponent> _components = [];



    public override void _Ready()
    {
        _state = new();
    }



    public virtual float Build(BuildContext buildContext, AeroContext aeroContext)
    {
        Context = aeroContext;
        var totalMass = _mass;
        Basis = Basis.Orthonormalized();
        _subBodies.Clear();
        _subBodies.AddRange(GetChildren().OfType<AeroBody>());

        foreach (var subBody in _subBodies)
        {
            totalMass += subBody.Build(buildContext, aeroContext);

            if (subBody is IControlSink controlSink)
            {
                buildContext.ControlChannels.Register(controlSink);
            }

            if (subBody is ISensorSource sensorSource)
            {
                buildContext.SensorChannels.Register(sensorSource);
            }
        }

        _components.Clear();
        if (Components is not null && Components.Count > 0)
        {
            _components.AddRange(Components.OfType<IComponentConfig<IAeroComponent>>().Select(c => c.Create()));

            foreach (var component in _components)
            {
                if (component is IControlSink controlSink)
                {
                    buildContext.ControlChannels.Register(controlSink);
                }

                if (component is ISensorSource sensorSource)
                {
                    buildContext.SensorChannels.Register(sensorSource);
                }
            }
        }

        return totalMass;
    }



    public virtual IEnumerable<Moment> CalculateForces(AeroState aeroState)
    {
        if (!Enabled) return [];

        // Should only be rotation, so transpose = inverse
        aeroState = Basis.Transposed() * aeroState;
        UpdateState(aeroState);

        return CalculateComponents(aeroState)
            .Concat(CalculateSubBodies(aeroState))
            .Append(CalculateWorld(aeroState));
    }



    protected virtual void UpdateState(AeroState aeroState)
    {
        _state.DeltaTime = aeroState.DeltaTime;
        
        _state.CenterOfLift = CenterOfLift;
        _state.GlobalCenterOfLift = GlobalPosition + (GlobalBasis * CenterOfLift);

        _state.CenterOfMass = CenterOfMass;
        _state.GlobalCenterOfMass = GlobalPosition + (GlobalBasis * CenterOfMass);

        _state.LocalLinearVelocity = aeroState.VelocityAtPoint(CenterOfLift);
        _state.LocalAngularVelocity = aeroState.LocalAngularVelocity;

        if (_state.LocalLinearVelocity.IsZeroApprox())
        {
            _state.AngleOfAttack = 0.0f;
        }
        else
        {
            _state.AngleOfAttack = -Mathf.Atan2(_state.LocalLinearVelocity.Y, -_state.LocalLinearVelocity.Z);
        }
    }



    protected virtual Moment CalculateWorld(AeroState aeroState)
        => new(_mass * (GlobalBasis.Transposed() * Context.WorldVars.Gravity(_state.GlobalCenterOfMass)), _state.CenterOfMass);



    protected virtual IEnumerable<Moment> CalculateComponents(AeroState aeroState)
        => _components.Select(c => c.Calculate(_state, Context));



    protected virtual IEnumerable<Moment> CalculateSubBodies(AeroState aeroState)
        => _subBodies.SelectMany(b => b.CalculateForces(aeroState).Select(m => b.Transform * m));
}
