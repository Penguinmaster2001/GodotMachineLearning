
using System;
using System.Collections.Generic;



namespace PPO.Benchmarking;



public class Stats : IReadOnlyStats
{
    public const int HistoryLength = 10;
    public IReadOnlyDictionary<string, (float val, float ave)> StatNames => _statNames;
    private readonly Dictionary<string, (float val, float ave)> _statNames = [];
    private readonly Dictionary<string, (int offset, float[] hist)> _statHistories = [];



    public void Add(string name, float val)
    {
        if (!_statHistories.TryGetValue(name, out var hist))
        {
            hist.hist = new float[HistoryLength];
            Array.Fill(hist.hist, val);
            hist.offset = 0;
        }

        var oldest = hist.hist[hist.offset];
        hist.hist[hist.offset] = val;
        hist.offset = (hist.offset + 1) % HistoryLength;

        _statHistories[name] = hist;

        if (!_statNames.TryGetValue(name, out var stat))
        {
            stat.ave = val;
            stat.val = val;
        }

        stat.val = val;
        stat.ave -= oldest / HistoryLength;
        stat.ave += val / HistoryLength;

        _statNames[name] = stat;
    }
}
