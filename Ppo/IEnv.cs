
using TorchSharp;



namespace PPO.Ppo;



public interface IEnv
{
    string[] InputLabels { get; }
    long InputSize { get; }
    string[] OutputLabels { get; }
    long OutputSize { get; }
    int NumEnvs { get; }



    /// <summary>
    /// Read current state for every env. Called once per physics frame,
    /// reflecting whatever the engine did since the last Actuate() call.
    /// </summary>
    /// <returns></returns>
    torch.Tensor Observe();



    /// <summary>
    /// Apply an action for every env. The effect only becomes visible on the
    /// NEXT Observe() call, after Godot has advanced physics in between.
    /// </summary>
    /// <param name="action"></param> 
    void Actuate(torch.Tensor action);



    /// <summary>
    /// Compute reward/terminated/truncated for the transition that just
    /// occurred, based on currently-observable state. Called right after
    /// Observe(), same frame.
    /// </summary>
    /// <returns></returns>
    (torch.Tensor reward, torch.Tensor terminated, torch.Tensor truncated) Evaluate(bool step = true);



    /// <summary>
    /// Reset a single env's underlying nodes back to a fresh episode start.
    /// </summary>
    /// <param name="index"></param>
    void ResetEnv(int index);



    /// <summary>
    /// Reset every env; returns the initial observation batch.
    /// </summary>
    /// <returns></returns>
    torch.Tensor Reset();



    void Close();
}