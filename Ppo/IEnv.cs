
using TorchSharp;



namespace PPO.Ppo;



public interface IEnv
{
    long InputSize { get; }
    long OutputSize { get; }
    int NumEnvs { get; }



    // Returns shape [NumEnvs, InputSize]
    torch.Tensor Reset();



    // action: shape [NumEnvs, OutputSize]
    // returns: (nextObs [NumEnvs, InputSize], reward [NumEnvs], terminated [NumEnvs], truncated [NumEnvs])
    // Batched shapes are required even for NumEnvs == 1, since the rollout buffers
    // in Runner.Run always index by env dimension.
    (torch.Tensor nextObs, torch.Tensor reward, torch.Tensor terminated, torch.Tensor truncated) Step(torch.Tensor action);



    void Close();
}