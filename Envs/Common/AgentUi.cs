
using System.Text;
using Godot;
using PPO.Benchmarking;
using PPO.Envs.Chase;
using PPO.Ppo;



namespace PPO.Envs.Common;



public partial class AgentUi : Control
{
    public IAgent EnvAgent;
    public TargetNode Target;

    public IEnv Env;
    public Agent Agent;
    public IReadOnlyStats Stats;
    public int AgentId = 0;

    [Export]
    private Label _inputLabel;

    [Export]
    private Label _outputLabel;

    [Export]
    private Label _rewardLabel;

    [Export]
    private Label _statsLabel;



    public override void _Process(double delta)
    {
        var inputs = Env.PrevObservation;
        var action = Env.PrevActuation;

        if (inputs is null) return;
        if (action is null) return;

        var inputShape = inputs.shape;
        var inputData = inputs.data<float>();
        var inputVals = new StringBuilder();
        for (int i = 0; i < inputShape[1]; i++)
        {
            inputVals.AppendLine($"{Env.InputLabels[i]}: {inputData[AgentId, i],10:00.0000}");
        }

        var actionShape = action.shape;
        var actionData = action.data<float>();
        var actionVals = new StringBuilder();
        for (int i = 0; i < actionShape[1]; i++)
        {
            actionVals.AppendLine($"{Env.OutputLabels[i]}: {actionData[AgentId, i],10:00.0000}");
        }

        var stats = new StringBuilder();
        foreach (var stat in Stats.StatNames)
        {
            stats.AppendLine($"{stat.Key}: {stat.Value.val,10:00.0000} {stat.Value.ave,10:00.0000}");
        }
        foreach (var rewardStat in Env.RewardStats)
        {
            stats.AppendLine($"{rewardStat.Item1}: {rewardStat.Item2,10:00.0000}");
        }

        _inputLabel.Text = $"{inputVals}";
        _outputLabel.Text = $"{actionVals}";
        _rewardLabel.Text = $"rewd: {EnvAgent.Reward,10:00.0000}\nagee: {EnvAgent.Age,10:00.0000}\nstrk: {EnvAgent.HitStreak,10:00.0000}";

        _statsLabel.Text = $"{stats}";
    }
}
