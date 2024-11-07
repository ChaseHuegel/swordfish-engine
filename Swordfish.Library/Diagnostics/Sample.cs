// ReSharper disable UnusedMember.Global
namespace Swordfish.Library.Diagnostics;

public readonly struct Sample(in double average, in double median, in double highest, in double lowest)
{
    public readonly double Average = average;
    public readonly double Median = median;
    public readonly double Highest = highest;
    public readonly double Lowest = lowest;
}