using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.Systems;

/// <summary>Shared first-person view-model transform helpers and active child lookup.</summary>
internal static class ViewModelGeometry
{
    /// <summary>Creates the resting <see cref="ChildComponent"/> transform for a view-model definition.</summary>
    public static ChildComponent CreateRestingChild(in Uuid parent, in ModelDefinition viewModel)
    {
        return new ChildComponent(in parent)
        {
            LocalPosition = new Vector3(viewModel.Position.X, viewModel.Position.Y, viewModel.Position.Z),
            LocalOrientation = Quaternion.CreateFromYawPitchRoll(
                viewModel.Rotation.Y * MathS.DEGREES_TO_RADIANS,
                viewModel.Rotation.X * MathS.DEGREES_TO_RADIANS,
                viewModel.Rotation.Z * MathS.DEGREES_TO_RADIANS
            ),
            LocalScale = new Vector3(viewModel.Scale.X, viewModel.Scale.Y, viewModel.Scale.Z),
        };
    }

    /// <summary>Finds the active rendered view-model <see cref="ChildComponent"/> in <paramref name="store"/>.</summary>
    public static bool TryFindActiveChild(DataStore store, out ChildComponent child)
    {
        FindViewModelChildAction action = default;
        store.Query<ViewModelComponent, MeshRendererComponent, ChildComponent, FindViewModelChildAction>(0f, ref action);
        child = action.Child;
        return action.Found;
    }

    /// <summary>Transforms a view-model-local point into world space matching <see cref="ChildSystem"/>.</summary>
    public static Vector3 ComputeWorldPoint(
        in TransformComponent playerTransform,
        in ChildComponent viewModelChild,
        in Vector3 localPoint
    ) {
        Vector3 worldOrigin = playerTransform.Position + Vector3.Transform(viewModelChild.LocalPosition, playerTransform.Orientation);
        Quaternion worldOrientation = playerTransform.Orientation * viewModelChild.LocalOrientation;
        Vector3 worldScale = playerTransform.Scale * viewModelChild.LocalScale;
        return worldOrigin + Vector3.Transform(localPoint * worldScale, worldOrientation);
    }

    /// <summary>Transforms a view-model-local point from a resting <see cref="ModelDefinition"/> into world space.</summary>
    public static Vector3 ComputeWorldPoint(
        in TransformComponent playerTransform,
        in ModelDefinition viewModel,
        in Vector3 localPoint
    ) {
        ChildComponent child = CreateRestingChild(default, in viewModel);
        return ComputeWorldPoint(in playerTransform, in child, in localPoint);
    }

    private struct FindViewModelChildAction : IForEach<ViewModelComponent, MeshRendererComponent, ChildComponent>
    {
        public bool Found;
        public ChildComponent Child;

        public void Execute(float delta, DataStore store, int entity, in ViewModelComponent viewModel, in MeshRendererComponent renderer, in ChildComponent child)
        {
            Found = true;
            Child = child;
        }
    }
}
