using System;

namespace Swordfish.Library.Collections;

public static class ReadOnlyQueueExtensions
{
    public static bool AssertTakeIgnoreCase(this ReadOnlyQueue<string> queue, string expected)
    {
        return queue.Take().Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryTake(this ReadOnlyQueue<string> queue, string expected)
    {
        if (queue.Peek().Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            queue.Take();
            return true;
        }

        return false;
    }

    public static bool AssertTake(this ReadOnlyQueue<string> queue, string expected)
    {
        return queue.Take().Equals(expected, StringComparison.CurrentCulture);
    }

    public static bool AssertTake(
        this ReadOnlyQueue<string> queue,
        string expected,
        StringComparison comparison
    )
    {
        return queue.Take().Equals(expected, comparison);
    }
}