namespace Swordfish.Library.Diagnostics
{
    public readonly struct Sample
    {
        public readonly double Average;
        public readonly double Median;
        public readonly double Highest;
        public readonly double Lowest;

        public Sample(double average, double median, double highest, double lowest)
        {
            Average = average;
            Median = median;
            Highest = highest;
            Lowest = lowest;
        }
    }
}