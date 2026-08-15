using TorchSharp;
using TorchSharp.Modules;
using nn = TorchSharp.torch.nn;



namespace PPO.Ppo;



public class Agent : nn.Module
{
    public Sequential Critic;
    public Sequential Actor;      // outputs the mean of the action distribution
    public Parameter LogStd;      // state-independent, learned log standard deviation



    public Agent(IEnv env) : base("agent")
    {
        Critic = nn.Sequential(
            CreateLayer(nn.Linear(env.InputSize, 8)),
            nn.Tanh(),
            CreateLayer(nn.Linear(8, 8)),
            nn.Tanh(),
            CreateLayer(nn.Linear(8, 1), std: 1.0f)
        );

        Actor = nn.Sequential(
            CreateLayer(nn.Linear(env.InputSize, 8)),
            nn.Tanh(),
            CreateLayer(nn.Linear(8, 8)),
            nn.Tanh(),
            // Outputs the mean of each action dimension. std = 0.01 keeps the
            // initial mean-output close to zero rather than confidently biased.
            CreateLayer(nn.Linear(8, env.OutputSize), std: 0.01f)
        );

        // log(std) starts at 0 => std = 1 initially. This is a free parameter,
        // NOT a function of the input state — the network never sees it and
        // never computes it per-step; it's just another set of trainable
        // weights that gradient descent adjusts over the course of training,
        // typically shrinking as the policy becomes more confident.
        LogStd = torch.nn.Parameter(torch.zeros(env.OutputSize));

        RegisterComponents();
    }



    public torch.Tensor GetValue(torch.Tensor x)
    {
        return Critic.call(x);
    }



#nullable enable
    public (torch.Tensor action, torch.Tensor logProb, torch.Tensor entropy, torch.Tensor value) GetActionAndValue(torch.Tensor x, torch.Tensor? action = null)
    {
        var mean = Actor.call(x);                       // shape [batch, OutputSize]
        var std = LogStd.exp().expand_as(mean);          // broadcast to match batch

        var probs = new TorchSharp.Modules.Normal(mean, std);

        action ??= probs.sample();

        // Sum across the action-dimension axis: joint log-prob / entropy of
        // the full action vector, not per-component. For OutputSize == 1
        // (your ball-on-track test) this sum is a no-op, but keep it — you'll
        // need it the moment you add a second control input.
        var logProb = probs.log_prob(action).sum(dim: -1);
        var entropy = probs.entropy().sum(dim: -1);

        return (action, logProb, entropy, GetValue(x));
    }
#nullable disable



    private nn.Module<torch.Tensor, torch.Tensor> CreateLayer(Linear layer, float std = 1.414f, float bias = 0.0f)
    {
        nn.init.orthogonal_(layer.weight, std);
        nn.init.constant_(layer.bias, bias);
        return layer;
    }
}
