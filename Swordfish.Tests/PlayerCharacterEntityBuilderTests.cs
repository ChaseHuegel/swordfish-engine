#nullable enable
using System.Numerics;
using Swordfish.ECS;
using Xunit;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;

namespace Swordfish.Tests;

/// <summary>Pins the local player entity composition at join.</summary>
public class PlayerCharacterEntityBuilderTests
{
    [Fact]
    public void DecorateSeatsTheAuthoritativeSpawnAndResetsPredictionState()
    {
        var store = new DataStore();
        var builder = new PlayerCharacterEntityBuilder(new FakeRenderContext(store));
        int entity = store.Alloc();

        var character = new Character(
            default,
            1UL,
            _Playtime: new Playtime(),
            "Test",
            0, 0, 0, 0, 0, 0,
            _Body: string.Empty,
            _ActiveInventorySlot: 0,
            _GameMode: GameMode.Creative,
            _Statistics: null,
            _Inventory: null
        );

        var position = new Vector3(1f, 2f, 3f);
        var orientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f);

        builder.Decorate(new Entity(entity, store), character, position, orientation);

        Assert.True(store.TryGet(entity, out TransformComponent transform));
        Assert.Equal(position, transform.Position);
        Assert.Equal(orientation, transform.Orientation);
        Assert.Equal(CharacterDefaults.PlayerScale, transform.Scale);

        Assert.True(store.TryGet(entity, out PendingInputComponent pending));
        Assert.NotNull(pending.Ring);

        Assert.True(store.TryGet(entity, out InputComponent input));
        Assert.Equal(0f, input.LookPitch);
        Assert.Equal(0f, input.LookYaw);
        Assert.Equal(0f, input.LookRoll);
    }
}
