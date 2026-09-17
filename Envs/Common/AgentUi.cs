
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
        // var inputs = Env.Observe().cuda();
        var inputs = Env.Observe();
        var (action, _, _, _) = Agent.GetActionAndValue(inputs);
        var (reward, _, _) = Env.Evaluate(false);

        reward = reward.cpu();
        inputs = inputs.cpu();
        action = action.cpu();

        var inputShape = inputs.shape;
        var inputData = inputs.data<float>();
        var inputVals = new StringBuilder();
        for (int i = 0; i < inputShape[1]; i++)
        {
            inputVals.AppendLine($"{Env.InputLabels[i]}: {inputData[0, i],10:00.0000}");
        }

        var actionShape = action.shape;
        var actionData = action.data<float>();
        var actionVals = new StringBuilder();
        for (int i = 0; i < actionShape[1]; i++)
        {
            actionVals.AppendLine($"{Env.OutputLabels[i]}: {actionData[0, i],10:00.0000}");
        }

        var stats = new StringBuilder();
        foreach (var stat in Stats.StatNames)
        {
            stats.AppendLine($"{stat.Key}: {stat.Value.val,10:00.0000} {stat.Value.ave,10:00.0000}");
        }

        _inputLabel.Text = $"{inputVals}";
        _outputLabel.Text = $"{actionVals}";
        _rewardLabel.Text = $"rewd: {reward[0].item<float>(),10:00.0000}";
        _rewardLabel.Text = $"rewd: {reward[0].item<float>(),10:00.0000}\nagee: {EnvAgent.Age,10:00.0000}\nstrk: {EnvAgent.HitStreak,10:00.0000}";
        _statsLabel.Text = $"{stats}";
    }
}
