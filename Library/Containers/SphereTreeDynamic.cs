using System.Collections.Generic;

using OpenTK.Mathematics;

using Swordfish.Library.Diagnostics;

namespace Swordfish.Library.Containers
{
    /// <summary>
    /// A dynamically sizing Octree made up of spherical nodes for detection collision or overlapping objects
    /// </summary>
    /// <typeparam name="T">type of objects stored in the tree</typeparam>
    public class SphereTreeDynamic<T>
    {
        /// <summary>
        /// The number of objects stored in the tree
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Position of the tree
        /// </summary>
        public Vector3 Position { get => root.position; }

        /// <summary>
        /// Size of the tree at the highest level
        /// </summary>
        public float Size { get => root.size; }

        /// <summary>
        /// Minimum size nodes can be in the tree
        /// </summary>
        public float MinimumSize { get => root.minSize; }

        /// <summary>
        /// Root node of the tree
        /// </summary>
        private SphereTreeNode<T> root;

        /// <summary>
        /// Initialize a tree at a position with a size and minimum node size
        /// </summary>
        /// <param name="pos">initial position of the tree</param>
        /// <param name="size">initial size of the tree</param>
        /// <param name="minSize">minimum size of nodes</param>
        public SphereTreeDynamic(Vector3 pos, float size, float minSize)
        {
            if (minSize > size)
            {
                minSize = size;
                Debug.Log($"SphereTree minimum size must be equal-greater than the tree size. Provided: {minSize} Using: {size}", LogType.WARNING);
            }

            Count = 0;
            root = new SphereTreeNode<T>(pos, size, minSize);
        }

        /// <summary>
        /// Attempts to add an object to the tree with provided position and size
        /// </summary>
        /// <param name="obj">an object of the tree's type T</param>
        /// <param name="pos">position of the object</param>
        /// <param name="size">size of the object</param>
        /// <returns>true if object was added; otherwise false</returns>
        public bool TryAdd(T obj, Vector3 pos, float size)
        {
            int resizeAttempts = 0;

            //  Try adding the object, growing the tree on failed attempts
            while ( !root.TryAdd(obj, pos, size) )
            {
                //  ! TODO Dynamic sizing causing stack overflow when redistributing objects
                GrowTree(pos - root.position);
                resizeAttempts++;

                //  Limit # of resize attempts to prevent an unreasonable stack
                if (resizeAttempts > 8)
                {
                    Debug.Log($"SphereTree add failed, unable to grow the tree large enough after {resizeAttempts} attempts", LogType.ERROR);
                    return false;
                }
            }

            //  Object was added
            Count++;
            return true;
        }

        /// <summary>
        /// Attempts to remove an object from the tree
        /// </summary>
        /// <param name="obj">object of tree's type T</param>
        /// <returns>true if object was removed; otherwise false</returns>
        public bool TryRemove(T obj)
        {
            if (root.TryRemove(obj))
            {
                Count--;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Clears the tree of all nodes and objects
        /// </summary>
        public void Clear()
        {
            Count = 0;
            root = new SphereTreeNode<T>(Position, Size, MinimumSize);
        }

        /// <summary>
        /// Expand the tree by moving it in a direction, doubling its size, and mutating nodes appropriately
        /// </summary>
        /// <param name="offset">normal or non-normal direction to move the tree</param>
        private void GrowTree(Vector3 offset)
        {
            Vector3 direction = offset.Normalized();

            if (root.HasObjects())
                root.Shift(direction * root.size * 0.5f, 2);
            else
                root.SetValues(root.position + (direction * root.size * 0.5f), root.size * 2f, MinimumSize);

            // ! redistributing objects is causing a stack overflow, need another method

            root.RedistObjects();
        }

        /// <summary>
        /// Check if a sphere is colliding with the tree
        /// </summary>
        /// <param name="pos">position of the sphere</param>
        /// <param name="size">size of the sphere</param>
        /// <returns>true if there is a collision; otherwise false</returns>
        public bool IsColliding(Vector3 pos, float size) => root.IsColliding(pos, size);

        /// <summary>
        /// Retrieves collisions (if any) between a sphere and the tree
        /// <para/><paramref name="results"/> list is populated with colliding objects
        /// </summary>
        /// <param name="pos">position of the sphere</param>
        /// <param name="size">size of the sphere</param>
        /// <param name="results">list of colliding objects</param>
        /// <returns>true if there is any collisions; otherwise false</returns>
        public bool GetColliding(Vector3 pos, float size, List<T> results)=> root.GetColliding(pos, size, results);

        /// <summary>
        /// Retrieves collisions (if any) in the tree
        /// <para/><paramref name="results"/> list is populated with colliding objects
        /// </summary>
        /// <param name="results">list of colliding objects</param>
        /// <returns>true if there is any collisions; otherwise false</returns>
        public bool CheckForCollisions(List<SphereTreeObjectPair<T>> results) => root.CheckForCollisions(results);

        /// <summary>
        /// Performs an inaccurate, fast sweep using sphere bounding axis to detect possible collisions in the tree
        /// <para/><paramref name="results"/> list is populated with pairs of colliding objects
        /// </summary>
        /// <param name="results">list of colliding objects</param>
        /// <returns>true if there is any collisions; otherwise false</returns>
        public bool SweepForCollisions(List<SphereTreeObjectPair<T>> results) => root.SweepForCollisions(results);
    }
}
