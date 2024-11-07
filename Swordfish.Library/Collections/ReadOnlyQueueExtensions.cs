using System;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Collections;

public static class ReadOnlyQueueExtensions
{
    public static bool AssertTakeIgnoreCase(this ReadOnlyQueue<string> queue, string expected)
    {
        return queue.Take().Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryTake(this ReadOnlyQueue<string> queue, string expected)
    {
        if (!queue.Peek().Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        queue.Take();
        return true;

    }

    public static bool AssertTake(this ReadOnlyQueue<string> queue, string expected)
    {
        return queue.Take().Equals(expected, StringComparison.CurrentCulture);
    }

    public static bool AssertTake(
        this ReadOnlyQueue<string> queue,
        string expected,
        StringComparison comparison)
    {
        return queue.Take().Equals(expected, comparison);
    }
}