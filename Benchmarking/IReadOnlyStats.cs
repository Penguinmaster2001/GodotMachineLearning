
using System.Collections.Generic;



namespace PPO.Benchmarking;



public interface IReadOnlyStats
{
    IReadOnlyDictionary<string, (float val, float ave)> StatNames { get; }
}
