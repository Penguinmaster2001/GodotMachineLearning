
namespace PPO.Ppo;



public class PpoOptions
{
    public bool UseCuda;
    public int Seed;
    public int NumSteps;
    public double LearningRate;
    public int NumEnvs;
    public int TotalTimesteps;
    public int BatchSize;
    public int MinibatchSize;
    public int UpdateEpochs;
    public bool AnnealLR;

    public int[] HiddenLayerSizes = [];

    public bool Gae = true;
    public double Gamma = 0.99;
    public double GaeLambda = 0.95;

    public double ClipCoef = 0.2;
    public bool ClipVLoss = true;
    public bool NormAdv = true;

    public double EntCoef = 0.0;
    public double VfCoef = 0.5;
    public double MaxGradNorm = 0.5;

    // null = disabled
    public double? TargetKl = null;

    // CAPS
    public double CapsTemporalCoef = 0.005;
    public double CapsSpatialCoef = 0.005;
}