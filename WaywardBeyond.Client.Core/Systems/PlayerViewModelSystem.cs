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
) : EntitySystem<PlayerComponent, InventoryComponent>
{
    private readonly PlayerData _playerData = playerData;
    private readonly IAssetDatabase<Material> _materialDatabase = materialDatabase;
    private readonly IAssetDatabase<Mesh> _meshDatabase = meshDatabase;

    private Entity? _viewModelEntity;
    private ModelDefinition _currentViewModel;
    
    protected override void OnTick(float delta, DataStore store, int entity, ref PlayerComponent player, ref InventoryComponent inventory)
    {
        //  Create the entity if it doesn't exist yet
        if (!_viewModelEntity.HasValue)
        {
            _viewModelEntity = new Entity(store.Alloc(), store);
            _viewModelEntity.Value.AddOrUpdate(new IdentifierComponent("PlayerViewModel"));
            _viewModelEntity.Value.AddOrUpdate(new TransformComponent());
        }
        
        Result<ItemSlot> mainHandResult = _playerData.GetMainHand(store, entity, inventory);
        ModelDefinition viewModel;
        if (mainHandResult.Success)
        {
            viewModel = mainHandResult.Value.Item.ViewModel ?? default;
        }
        else
        {
            viewModel = default;
        }

        if (viewModel.Equals(_currentViewModel))
        {
            //  Do nothing if the view model isn't changing
            return;
        }
        
        _currentViewModel = viewModel;
        
        //  Cleanup any pre-existing mesh renderer
        MeshRendererComponent? meshRendererComponent = _viewModelEntity.Value.Get<MeshRendererComponent>();
        if (meshRendererComponent != null)
        {
            if (meshRendererComponent.Value.MeshRenderer != null)
            {
                meshRendererComponent.Value.MeshRenderer.Dispose();
                meshRendererComponent.Value.MeshRenderer.Mesh.Dispose();
            }

            _viewModelEntity.Value.Remove<MeshRendererComponent>();
        }
        
        //  Attempt to resolve resources for the view model
        if (viewModel.Mesh == null || viewModel.Material == null)
        {
            return;
        }
        
        Result<Mesh> mesh = _meshDatabase.Get(viewModel.Mesh);
        Result<Material> material = _materialDatabase.Get(viewModel.Material);
        if (!mesh || !material)
        {
            return;
        }
        
        //  Create a mesh renderer for the new view model
        var meshRenderer = new MeshRenderer(mesh, material);
        _viewModelEntity.Value.AddOrUpdate(new MeshRendererComponent(meshRenderer));
        _viewModelEntity.Value.AddOrUpdate(new ChildComponent(entity)
        {
            LocalPosition = new Vector3(viewModel.Position.X, viewModel.Position.Y, viewModel.Position.Z),
            LocalOrientation = Quaternion.CreateFromYawPitchRoll(viewModel.Rotation.Y * MathS.DEGREES_TO_RADIANS, viewModel.Rotation.X * MathS.DEGREES_TO_RADIANS, viewModel.Rotation.Z * MathS.DEGREES_TO_RADIANS),
            LocalScale = new Vector3(viewModel.Scale.X, viewModel.Scale.Y, viewModel.Scale.Z),
        });
    }
}