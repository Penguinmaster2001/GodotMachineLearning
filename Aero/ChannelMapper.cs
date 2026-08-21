
using System;
using System.Collections.Generic;
using Godot;



namespace PPO.Aero;



public abstract class ChannelMapper<T>
{
    public bool StrictRegistration = false;
    public int NumChannels => _channelToId.Count;
    public IReadOnlyDictionary<string, int> ChannelToId => _channelToId;
    private readonly Dictionary<string, int> _channelToId = [];

    public int NumSinks { get; private set; }
    protected readonly List<List<T>> _idToSinks = [];

    private readonly Func<T, string> _getChannel;



    protected ChannelMapper(Func<T, string> getChannel, List<string> channels)
    {
        _getChannel = getChannel;

        for (int channel = 0; channel < channels.Count; channel++)
        {
            _channelToId[channels[channel]] = channel;
            _idToSinks.Add([]);

            GD.Print($"Channel {channels[channel]}: {channel}");
        }
    }



    public void Register(T sink)
    {
        var channel = _getChannel(sink);
        if (!_channelToId.TryGetValue(channel, out var id))
        {
            if (StrictRegistration) throw new Exception($"Channel {channel} is not registered.");
            else return;
        }

        _idToSinks[id].Add(sink);
        NumSinks++;
    }
}



public class ChannelRouter<T, TCommand> : ChannelMapper<T>
{
    private readonly Action<T, TCommand> _apply;



    public ChannelRouter(Func<T, string> getChannel, List<string> channels, Action<T, TCommand> apply)
        : base(getChannel, channels)
    {
        _apply = apply;
    }



    public void Route(List<TCommand> commands)
    {
        for (int channel = 0; channel < int.Min(commands.Count, NumChannels); channel++)
        {
            for (int sink = 0; sink < _idToSinks[channel].Count; sink++)
            {
                _apply(_idToSinks[channel][sink], commands[channel]);
            }
        }
    }
}



public class ChannelReader<T, TOutput> : ChannelMapper<T>
{
    private readonly Func<T, TOutput> _read;



    public ChannelReader(Func<T, string> getChannel, List<string> channels, Func<T, TOutput> read)
        : base(getChannel, channels)
    {
        _read = read;
    }




    public U[] Read<U>(Func<TOutput[], U> selector)
    {
        var channels = new U[NumChannels];
        for (int channel = 0; channel < NumChannels; channel++)
        {
            var sinks = new TOutput[_idToSinks[channel].Count];
            for (int sink = 0; sink < _idToSinks[channel].Count; sink++)
            {
                sinks[sink] = _read(_idToSinks[channel][sink]);
            }

            channels[channel] = selector(sinks);
        }

        return channels;
    }
}
