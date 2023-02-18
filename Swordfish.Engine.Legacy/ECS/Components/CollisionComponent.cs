namespace Swordfish.Engine.ECS
{
    [Component]
    public struct CollisionComponent
    {
        public float size;
        public bool colliding;
        public bool broadHit;
        public float skin;
    }
}
