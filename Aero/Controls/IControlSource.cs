
using System.Collections.Generic;



namespace PPO.Aero.Controls;



public interface IControlSource
{
    void SetChannels(IReadOnlyDictionary<string, int> channelToId);

    List<float> Read();


    void Update(AeroState state);
}
