using Swordfish.Library.Util;

namespace WaywardBeyond.AssetTools.Search;

/// <summary>
///     Bounded back and forward navigation stack for jumping between assets, files, and spans.
/// </summary>
public sealed class NavigationHistory
{
    private readonly int _maxCapacity;
    private readonly List<NavigationTarget> _backStack = [];
    private readonly List<NavigationTarget> _forwardStack = [];

    /// <summary>
    ///     Creates a navigation history stack holding at most <paramref name="maxCapacity"/> back entries.
    /// </summary>
    public NavigationHistory(int maxCapacity = 128)
    {
        if (maxCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCapacity), "Capacity must be positive.");
        }

        _maxCapacity = maxCapacity;
    }

    /// <summary>
    ///     Gets the current active navigation target, or null when history is empty.
    /// </summary>
    public NavigationTarget? Current { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether navigating backward is possible.
    /// </summary>
    public bool CanGoBack => _backStack.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether navigating forward is possible.
    /// </summary>
    public bool CanGoForward => _forwardStack.Count > 0;

    /// <summary>
    ///     Gets the number of entries in the back stack.
    /// </summary>
    public int BackCount => _backStack.Count;

    /// <summary>
    ///     Gets the number of entries in the forward stack.
    /// </summary>
    public int ForwardCount => _forwardStack.Count;

    /// <summary>
    ///     Navigates to <paramref name="target"/>, pushing the previous target onto the back stack and clearing forward history.
    /// </summary>
    public void Navigate(in NavigationTarget target)
    {
        if (Current.HasValue && Current.Value.Equals(target))
        {
            return;
        }

        if (Current.HasValue)
        {
            _backStack.Add(Current.Value);
            if (_backStack.Count > _maxCapacity)
            {
                _backStack.RemoveAt(0);
            }
        }

        _forwardStack.Clear();
        Current = target;
    }

    /// <summary>
    ///     Moves one step backward in history and returns the newly active target.
    /// </summary>
    public Result<NavigationTarget> GoBack()
    {
        if (_backStack.Count == 0)
        {
            return Result<NavigationTarget>.FromFailure("Navigation back history is empty.");
        }

        if (Current.HasValue)
        {
            _forwardStack.Add(Current.Value);
        }

        int lastIndex = _backStack.Count - 1;
        NavigationTarget previous = _backStack[lastIndex];
        _backStack.RemoveAt(lastIndex);
        Current = previous;
        return Result<NavigationTarget>.FromSuccess(previous);
    }

    /// <summary>
    ///     Moves one step forward in history and returns the newly active target.
    /// </summary>
    public Result<NavigationTarget> GoForward()
    {
        if (_forwardStack.Count == 0)
        {
            return Result<NavigationTarget>.FromFailure("Navigation forward history is empty.");
        }

        if (Current.HasValue)
        {
            _backStack.Add(Current.Value);
            if (_backStack.Count > _maxCapacity)
            {
                _backStack.RemoveAt(0);
            }
        }

        int lastIndex = _forwardStack.Count - 1;
        NavigationTarget next = _forwardStack[lastIndex];
        _forwardStack.RemoveAt(lastIndex);
        Current = next;
        return Result<NavigationTarget>.FromSuccess(next);
    }

    /// <summary>
    ///     Clears the current target and both back and forward history stacks.
    /// </summary>
    public void Clear()
    {
        Current = null;
        _backStack.Clear();
        _forwardStack.Clear();
    }
}
