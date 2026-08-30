
namespace PPO.Ppo;



public interface IAgent
{
    float Age { get; }
    float Reward { get; }
    int HitStreak { get; }
}
