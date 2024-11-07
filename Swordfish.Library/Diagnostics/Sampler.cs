using System;
using System.Linq;

namespace Swordfish.Library.Diagnostics;

public class Sampler
{
    public int Length => _sampleCount;

    public double Average {
        get {
            lock (_lock)
            {
                return _total / _sampleCount;
            }
        }
    }

    private readonly object _lock = new();
    private readonly double[] _samples;

    private int _sampleCount;
    private int _currentIndex;
    private double _total;

    public Sampler(int length = 60)
    {
        _samples = new double[length];
    }

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

            if (_sampleCount < _samples.Length)
            {
                _sampleCount++;
            }
        }
    }

    public Sample GetSnapshot() {
        lock (_lock) {
            double[] sortedSamples = _samples[.._sampleCount].OrderBy(d => d).ToArray();
            double median = sortedSamples[Math.Clamp((_sampleCount - 1) / 2, 0, _sampleCount)];

            return new Sample(Average, median, sortedSamples[_sampleCount - 1], sortedSamples[0]);
        }
    }
}