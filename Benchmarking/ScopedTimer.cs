
using System;



namespace PPO.Benchmarking;



public static class ScopedTimer
{
    public static IDisposable Start(string name = "")
    {
        return new Timer(name);
    }



    private class Timer : IDisposable
    {
        private readonly DateTime _start;
        private readonly string _name;



        public Timer(string name = "")
        {
            _start = DateTime.Now;
            _name = name;
        }



        public void Dispose()
        {
            Console.WriteLine($"Timer {_name} ended: {DateTime.Now - _start}");
        }
    }
}
