using System;
using System.Linq;

namespace Swordfish.Library.Diagnostics
{
    public class Sampler
    {
        public int Length => SampleCount;

        public double Average {
            get {
                lock (Lock)
                {
                    return Total / SampleCount;
                }
            }
        }

        private readonly object Lock = new object();
        private readonly double[] Samples;

        private int SampleCount;
        private int CurrentIndex;
        private double Total;

        public Sampler(int length = 60)
        {
            Samples = new double[length];
        }

        public void Record(double value)
        {
            lock (Lock)
            {
                Total -= Samples[CurrentIndex];
                Total += value;

                Samples[CurrentIndex] = value;

                CurrentIndex++;
                if (CurrentIndex >= Samples.Length)
                    CurrentIndex = 0;

                if (SampleCount < Samples.Length)
                    SampleCount++;
            }
        }

        public Sample GetSnapshot() {
            lock (Lock) {
                double[] sortedSamples = Samples[..SampleCount].OrderBy(d => d).ToArray();
                double median = sortedSamples[Math.Clamp((SampleCount - 1) / 2, 0, SampleCount)];

                return new Sample(Average, median, sortedSamples[SampleCount - 1], sortedSamples[0]);
            }
        }
    }
}