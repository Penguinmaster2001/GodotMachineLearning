
using System.Collections.Generic;



namespace PPO.Aero.Sensors;



public interface ISensorSink
{
    void SetChannels(IReadOnlyDictionary<string, int> channelToId);

    void Update(float[][] data);
}
