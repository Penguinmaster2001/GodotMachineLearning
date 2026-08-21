
using System.Collections.Generic;



namespace PPO.Aero.Sensors;



public class PrintingSensorSink : ISensorSink
{
    private IReadOnlyDictionary<string, int> _channelToId;



    public void SetChannels(IReadOnlyDictionary<string, int> channelToId) => _channelToId = channelToId;



    public void Update(float[][] data)
    {
        
    }
}
