using System.Numerics;
using System.Threading;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Renderers;
using Swordfish.Library.Types;
using Swordfish.Settings;
using Xunit;

namespace Swordfish.Tests;

public class SceneBufferTests
{
    [Fact]
    public void ShrinkingEntityCountDoesNotPublishStaleEntityModels()
    {
        using var context = new GLRenderContext(
            gl: null!,
            glContext: null!,
            new FakeWindowContext(),
            new SynchronizationContext(),
            new RenderSettings());

        DataStore store = new();
        int entityA = CreateRenderableEntity(store, new Vector3(1f, 0f, 0f));
        int entityB = CreateRenderableEntity(store, new Vector3(2f, 0f, 0f));
        int entityC = CreateRenderableEntity(store, new Vector3(3f, 0f, 0f));
        Uuid uuidA = store.GetUuid(entityA);

        // Tick 1 & 2 with 3 entities so the first scene buffer is returned to _freeSceneBuffers on the second GetSceneContext().
        context.Tick(0f, store);
        RenderScene scene1 = context.GetSceneContext();
        Assert.Equal(3, scene1.EntityModels.Length);

        context.Tick(0f, store);
        RenderScene scene2 = context.GetSceneContext();
        Assert.Equal(3, scene2.EntityModels.Length);

        // Shrink to 1 entity; Tick 3 reuses the pooled length-3 buffer.
        store.Free(entityB);
        store.Free(entityC);

        context.Tick(0f, store);
        RenderScene scene3 = context.GetSceneContext();
        Assert.Equal(1, scene3.EntityModels.Length);
        Assert.Equal(uuidA, scene3.EntityModels.Span[0].Entity);

        // Shrink to 0 entities; Tick 4 must publish an empty slice.
        store.Free(entityA);

        context.Tick(0f, store);
        RenderScene scene4 = context.GetSceneContext();
        Assert.Equal(0, scene4.EntityModels.Length);
    }

    private static int CreateRenderableEntity(DataStore store, Vector3 position)
    {
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TransformComponent(position, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(entity, new MeshRendererComponent(new MeshRenderer(new Mesh([], [], [], [], []))));
        return entity;
    }
}
