
using PPO.Aero.Controls;
using PPO.Aero.Sensors;



namespace PPO.Aero;



public class BuildContext
{
    public ChannelMapper<IControlSink> ControlChannels { get; set; }
    public ChannelMapper<ISensorSource> SensorChannels { get; set; }
}