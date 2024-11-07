using System;
using System.Linq;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Diagnostics;

public class Sampler(in int length = 60)
{
    public int Length { get; private set; }

    public double Average {
        get {
            lock (_lock)
            {
                return _total / Length;
            }
        }
    }

    private readonly object _lock = new();
    private readonly double[] _samples = new double[length];

    private int _currentIndex;
    private double _total;

    public void Record(double value)
    {
        lock (_lock)
        {
            _total -= _samples[_currentIndex];
            _total += value;

            _samples[_currentIndex] = value;

            _currentIndex++;
            if (_currentIndex >= _samples.Length)
            {
                _currentIndex = 0;
            }

            if (Length < _samples.Length)
            {
                Length++;
            }
        }
    }

    public Sample GetSnapshot()
    {
        lock (_lock) 
        {
            double[] sortedSamples = _samples[..Length].OrderBy(d => d).ToArray();
            double median = sortedSamples[Math.Clamp((Length - 1) / 2, 0, Length)];

            return new Sample(Average, median, sortedSamples[Length - 1], sortedSamples[0]);
        }
    }
}