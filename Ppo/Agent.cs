
using System.Collections.Generic;
using TorchSharp;
using TorchSharp.Modules;
using nn = TorchSharp.torch.nn;



namespace PPO.Ppo;



public class Agent : nn.Module
{
    public Sequential Critic;
    public Sequential Actor;      // outputs the mean of the action distribution
    public Parameter LogStd;      // state-independent, learned log standard deviation



    public Agent(IEnv env, int[] hiddenSizes) : base("agent")
    {
        Critic = BuildMlp(env.InputSize, hiddenSizes, outputSize: 1, outputStd: 1.0f);
        Actor = BuildMlp(env.InputSize, hiddenSizes, outputSize: env.OutputSize, outputStd: 0.01f);

        LogStd = nn.Parameter(torch.zeros(env.OutputSize));

        RegisterComponents();
    }



    public Agent ToDevice(DeviceType device)
    {
        this.to(device);
        // LogStd = nn.Parameter(LogStd.to(device));
        // RegisterComponents();

        return this;
    }



    private Sequential BuildMlp(long inputSize, int[] hiddenSizes, long outputSize, float outputStd)
    {
        var modules = new List<nn.Module<torch.Tensor, torch.Tensor>>();
        long prevSize = inputSize;

        foreach (var hidden in hiddenSizes)
        {
            modules.Add(CreateLayer(nn.Linear(prevSize, hidden)));
            modules.Add(nn.Tanh());
            prevSize = hidden;
        }

        modules.Add(CreateLayer(nn.Linear(prevSize, outputSize), std: outputStd));

        return nn.Sequential(modules);
    }



    public torch.Tensor GetValue(torch.Tensor x) =>  Critic.call(x);



    public torch.Tensor GetMeanAction(torch.Tensor x) => Actor.call(x);



#nullable enable
    public (torch.Tensor action, torch.Tensor logProb, torch.Tensor entropy, torch.Tensor value) GetActionAndValue(torch.Tensor x, torch.Tensor? action = null)
    {
        var mean = Actor.call(x);                       // shape [batch, OutputSize]
        // var std = LogStd.exp().expand_as(mean);         // broadcast to match batch
        var std = LogStd.cuda().exp().expand_as(mean);         // broadcast to match batch

        var probs = new Normal(mean, std);

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
