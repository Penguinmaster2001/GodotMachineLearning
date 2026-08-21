
namespace PPO.Aero.Controls;



public interface IControlSink
{
    string ControlChannel { get; }

    void SetCommand(float value);
}
