using WaywardBeyond.Bricks;

namespace Swordfish.Tests;

/// <summary>
/// A cached <see cref="IBrickIdMap"/> loaded from the shipped brick tomls, so tests assert and pass the
/// same data-driven ids the game derives, without hardcoding a brick catalog in test code.
/// </summary>
public static class TestBricks
{
    public static IBrickIdMap Map { get; } = BrickDatabaseTests.CreateSharedBrickDatabase();
}