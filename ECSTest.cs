using System;
using OpenTK.Mathematics;
using Swordfish;
using Swordfish.ECS;
using Swordfish.Rendering;

public class ECSTest
{
    [Component]
    public struct TransformComponent
    {
        public Vector3 position;
    }

    [Component]
    public struct RenderComponent
    {
        public Mesh mesh;
    }

    [ComponentSystem(typeof(TransformComponent))]
    public class TransformSystem : IComponentSystem
    {
        public void Start()
        {
            Debug.Log("Transform start");
        }

        public void Destroy()
        {
            Debug.Log("Transform destroy");
        }

        public void Update()
        {
            Debug.Log("Transform update");
        }
    }
}