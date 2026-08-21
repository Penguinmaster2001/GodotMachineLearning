
namespace PPO.Aero.Sensors;



public interface ISensorSource
{
    string SensorChannel { get; }

    float[] Read();
}
