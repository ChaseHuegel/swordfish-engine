using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.Player;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class PlayerViewModelSystem(
    in PlayerData playerData,
    in IAssetDatabase<Material> materialDatabase,
    in IAssetDatabase<Mesh> meshDatabase
) : IEntitySystem
{
    private readonly PlayerData _playerData = playerData;
    private readonly IAssetDatabase<Material> _materialDatabase = materialDatabase;
    private readonly IAssetDatabase<Mesh> _meshDatabase = meshDatabase;

    private Entity? _viewModelEntity;
    private ModelDefinition _currentViewModel;

    private struct ForEachAction : IForEach<PlayerComponent, InventoryComponent>
    {
        public PlayerViewModelSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in InventoryComponent inventory)
        {
            //  Create the entity if it doesn't exist yet
            if (!Owner._viewModelEntity.HasValue)
            {
                Owner._viewModelEntity = new Entity(store.Alloc(), store);
                Owner._viewModelEntity.Value.AddOrUpdate(new IdentifierComponent("PlayerViewModel"));
                Owner._viewModelEntity.Value.AddOrUpdate(new TransformComponent());
            }

            Result<ItemSlot> mainHandResult = Owner._playerData.GetMainHand(store, entity, inventory);
            ModelDefinition viewModel;
            if (mainHandResult.Success && WaywardBeyond.IsPlaying())
            {
                viewModel = mainHandResult.Value.Item.ViewModel ?? default;
            }
            else
            {
                viewModel = default;
            }

            if (viewModel.Equals(Owner._currentViewModel))
            {
                //  Do nothing if the view model isn't changing
                return;
            }

            Owner._currentViewModel = viewModel;

            //  Cleanup any pre-existing mesh renderer
            MeshRendererComponent? meshRendererComponent = Owner._viewModelEntity.Value.Get<MeshRendererComponent>();
            if (meshRendererComponent != null)
            {
                if (meshRendererComponent.Value.MeshRenderer != null)
                {
                    meshRendererComponent.Value.MeshRenderer.Dispose();
                    meshRendererComponent.Value.MeshRenderer.Mesh.Dispose();
                }

                Owner._viewModelEntity.Value.Remove<MeshRendererComponent>();
            }

            //  Attempt to resolve resources for the view model
            if (viewModel.Mesh == null || viewModel.Material == null)
            {
                return;
            }

            Result<Mesh> mesh = Owner._meshDatabase.Get(viewModel.Mesh);
            Result<Material> material = Owner._materialDatabase.Get(viewModel.Material);
            if (!mesh || !material)
            {
                return;
            }

            //  Create a mesh renderer for the new view model
            var meshRenderer = new MeshRenderer(mesh, material);
            Owner._viewModelEntity.Value.AddOrUpdate(new MeshRendererComponent(meshRenderer));
            Owner._viewModelEntity.Value.AddOrUpdate(new ChildComponent(store.GetUuid(entity))
            {
                LocalPosition = new Vector3(viewModel.Position.X, viewModel.Position.Y, viewModel.Position.Z),
                LocalOrientation = Quaternion.CreateFromYawPitchRoll(viewModel.Rotation.Y * MathS.DEGREES_TO_RADIANS, viewModel.Rotation.X * MathS.DEGREES_TO_RADIANS, viewModel.Rotation.Z * MathS.DEGREES_TO_RADIANS),
                LocalScale = new Vector3(viewModel.Scale.X, viewModel.Scale.Y, viewModel.Scale.Z),
            });
        }
    }

    public void Tick(float delta, DataStore store)
    {
        ForEachAction action = new() { Owner = this };
        store.Query<PlayerComponent, InventoryComponent, ForEachAction>(delta, ref action);
    }
}