
using System.Text;
using Godot;
using PPO.Envs.Ball;
using PPO.Ppo;



namespace PPO.Envs.Chase;



public partial class AgentUi : Control
{
    public ChaserNode Chaser;
    public TargetNode Target;

    public IEnv Env;
    public Agent Agent;

    [Export]
    private Label _inputs;

    [Export]
    private Label _outputs;

    [Export]
    private Label _reward;



    public override void _Process(double delta)
    {
        var inputs = Env.Observe().cuda();
        var (action, _, _, _) = Agent.GetActionAndValue(inputs);
        var (reward, _, _) = Env.Evaluate();

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

        _inputs.Text = $"{inputVals}";
        _outputs.Text = $"{actionVals}";
        _reward.Text = $"rewd: {reward[0].item<float>(),10:00.0000}";
    }
}
