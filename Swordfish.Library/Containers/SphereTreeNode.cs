using System.Collections.Generic;

using OpenTK.Mathematics;

using Swordfish.Library.Diagnostics;
using Swordfish.Library.Util;

namespace Swordfish.Library.Containers
{
    /// <summary>
    /// A pair of type T objects that are intersecting in the SphereTree
    /// </summary>
    /// <typeparam name="T">type of objects stored in the SphereTree</typeparam>
    public struct SphereTreeObjectPair<T>
    {
        public T A;
        public T B;

        public override bool Equals(object obj)
        {
            if ( !(obj is SphereTreeObjectPair<T>) )
                return false;
            
            SphereTreeObjectPair<T> other = (SphereTreeObjectPair<T>)obj;

            //  A pair is equal if its A-B are contained in this A-B regardless of combination order
            return (this.A.Equals(other.A) || this.A.Equals(other.B)) && (this.B.Equals(other.A) || this.B.Equals(other.B));
        }

        public override int GetHashCode()
        {
            return A.GetHashCode() + B.GetHashCode();
        }
    }

    /// <summary>
    /// A node of a SphereTree
    /// </summary>
    /// <typeparam name="T">type of objects stored in the SphereTree</typeparam>
    internal class SphereTreeNode<T>
    {
        /// <summary>
        /// Maximum number of objects stored per node
        /// </summary>
        private const int MAX_OBJECTS = 8;

        /// <summary>
        /// This node's children nodes
        /// </summary>
        private SphereTreeNode<T>[] children = null;

        /// <summary>
        /// True of this node has children nodes
        /// </summary>
        public bool HasChildren { get => children != null; }

        /// <summary>
        /// Position of this node
        /// </summary>
        public Vector3 position { get; private set; }

        /// <summary>
        /// Size of this node
        /// </summary>
        public float size { get; private set; }

        /// <summary>
        /// Minimum size a node can be in its tree
        /// </summary>
        public float minSize { get; private set; }

        /// <summary>
        /// SphereTreeObjects stored in this node
        /// </summary>
        readonly List<SphereTreeObject> objects = new List<SphereTreeObject>();

        /// <summary>
        /// An object in a sphere tree
        /// </summary>
        public struct SphereTreeObject {
            public Vector3 position;
            public float size;
            public T obj;
        }

        /// <summary>
        /// Initialize a node at a position with a size and its tree's minimum size
        /// </summary>
        /// <param name="pos">position of the node</param>
        /// <param name="size">size of the node</param>
        /// <param name="minSize">minimum size a node can be in the tree</param>
        public SphereTreeNode(Vector3 pos, float size, float minSize)
        {
            this.size = size;
            this.minSize = minSize;
            this.position = pos;
        }

        /// <summary>
        /// Set the node's position, size, and minimum size
        /// </summary>
        /// <param name="pos">position of the node</param>
        /// <param name="size">size of the node</param>
        /// <param name="minSize">minimum size a node can be in the tree</param>
        public void SetValues(Vector3 pos, float size, float minSize)
        {
            this.size = size;
            this.minSize = minSize;
            this.position = pos;
        }

        /// <summary>
        /// Scale and move the node and every node below it by an offset
        /// </summary>
        /// <param name="offset">vector to move the node by</param>
        /// <param name="scale">scale factor of the node</param>
        public void Shift(Vector3 offset, float scale)
        {
            this.size *= scale;
            this.position += offset;

            if (HasChildren)
                foreach (SphereTreeNode<T> child in children)
                    Shift(offset, scale);
        }

        /// <summary>
        /// Check if there are objects in this node or any below it
        /// </summary>
        /// <returns>true if there are any objects; otherwise false</returns>
        public bool HasObjects()
        {
            if (objects.Count > 0) return true;

            if (HasChildren)
                foreach (SphereTreeNode<T> child in children)
                    if (child.HasObjects()) return true;

            return false;
        }

        /// <summary>
        /// Check if a sphere is colliding with any objects at or below this node
        /// </summary>
        /// <param name="pos">position of the sphere</param>
        /// <param name="size">size of the sphere</param>
        /// <returns>true if there is a collision; otherwise false</returns>
        public bool IsColliding(Vector3 pos, float size)
        {
            //  Check the node first
            if (!Intersection.SweepSphereToSphere(this.position, this.size, pos, size))
                return false;

            //  Check the objects in the node
            foreach (SphereTreeObject obj in objects)
                if (Intersection.SweepSphereToSphere(obj.position, obj.size, pos, size))
                    return true;

            //  Check the children in the node
            if (HasChildren)
                foreach (SphereTreeNode<T> child in children)
                    if (child.IsColliding(pos, size))
                        return true;

            return false;
        }

        /// <summary>
        /// Retrieves collisions (if any) between a sphere and objects at or below this node
        /// <para/><paramref name="results"/> list is populated with colliding objects
        /// </summary>
        /// <param name="pos">position of the sphere</param>
        /// <param name="size">size of the sphere</param>
        /// <param name="results">list of colliding objects</param>
        /// <returns>true if there is any collisions; otherwise false</returns>
        public bool GetColliding(Vector3 pos, float size, List<T> results)
        {
            //  Check the node
            if (!Intersection.SweepSphereToSphere(this.position, this.size, pos, size))
                return false;

            //  Check the objects in the node
            foreach (SphereTreeObject obj in objects)
                if (Intersection.SweepSphereToSphere(obj.position, obj.size, pos, size))
                    results.Add(obj.obj);

            //  Check the children in the node
            if (HasChildren)
                foreach (SphereTreeNode<T> child in children)
                    child.GetColliding(pos, size, results);

            return results.Count > 0;
        }

        /// <summary>
        /// Retrieves collisions (if any) at or below this node
        /// <para/><paramref name="results"/> list is populated with colliding objects
        /// </summary>
        /// <param name="results">list of colliding objects</param>
        /// <returns>true if there is any collisions; otherwise false</returns>
        public bool CheckForCollisions(List<SphereTreeObjectPair<T>> results)
        {
            if (HasObjects())
            {
                for (int i = 0; i < objects.Count; i++)
                {
                    SphereTreeObject obj = objects[i];

                    //  Check each object in the node against each other
                    for (int n = i + 1; n < objects.Count; n++)
                    {
                        SphereTreeObject obj2 = objects[n];

                        if (Intersection.SphereToSphere(obj.position, obj.size, obj2.position, obj2.size))
                            results.Add(new SphereTreeObjectPair<T>() { A = obj.obj, B = obj2.obj });
                    }
                }
            }

            //  Check the children in the node
            if (HasChildren)
                foreach (SphereTreeNode<T> child in children)
                    child.CheckForCollisions(results);

            return results.Count > 0;
        }

        /// <summary>
        /// Performs an inaccurate, fast sweep using sphere bounding axis to detect possible collisions at or below this node
        /// <para/><paramref name="results"/> list is populated with pairs of colliding objects
        /// </summary>
        /// <param name="results">list of colliding objects</param>
        /// <returns>true if there is any collisions; otherwise false</returns>
        public bool SweepForCollisions(List<SphereTreeObjectPair<T>> results)
        {
            //  Check the objects in the node against each other
            if (HasObjects())
            {
                for (int i = 0; i < objects.Count; i++)
                for (int n = i + 1; n < objects.Count; n++)
                {
                    SphereTreeObject obj = objects[i];
                    SphereTreeObject obj2 = objects[n];

                    if (Intersection.SweepSphereToSphere(obj.position, obj.size, obj2.position, obj2.size))
                        results.Add(new SphereTreeObjectPair<T>() { A = obj.obj, B = obj2.obj });
                }
            }

            //  Check the children in the node
            if (HasChildren)
                foreach (SphereTreeNode<T> child in children)
                    child.SweepForCollisions(results);

            return results.Count > 0;
        }

        /// <summary>
        /// Attempts to remove an object at or below this node
        /// </summary>
        /// <param name="obj">object of tree's type T</param>
        /// <returns>true if object was removed; otherwise false</returns>
        public bool TryRemove(T obj)
        {
            bool wasRemoved = false;

            //  Try removing from this node then fallback to children (if any)
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].obj.Equals(obj))
                {
                    objects.RemoveAt(i);
                    wasRemoved = true;
                    break;
                }
            }

            if (HasChildren)
            {
                //  If we haven't removed the object, check the children
                for (int i = 0; !wasRemoved && i < MAX_OBJECTS; i++)
                    wasRemoved = children[i].TryRemove(obj);

                //  Try consuming children if the object was removed
                //  This will clear up empty or unnecessary nodes
                if (wasRemoved) TryConsumeChildren();
            }

            //  Was it removed?
            return wasRemoved;
        }

        /// <summary>
        /// Attempts to add an object at or below this node with provided position and size
        /// </summary>
        /// <param name="obj">an object of the tree's type T</param>
        /// <param name="pos">position of the object</param>
        /// <param name="size">size of the object</param>
        /// <returns>true if object was added; otherwise false</returns>
        public bool TryAdd(T obj, Vector3 pos, float size)
        {
            //  Don't add if the obj isn't in bounds of this node
            if (!Intersection.SweepSphereToSphere(this.position, this.size, pos, size))
                return false;

            Add(obj, pos, size);
            return true;
        }

        /// <summary>
        /// Forcibly adds an object at or below this node
        /// </summary>
        /// <param name="obj">an object of the tree's type T</param>
        /// <param name="pos">position of the object</param>
        /// <param name="size">size of the object</param>
        private void Add(T obj, Vector3 pos, float size)
        {
            //  If this node isn't filled with objects or children would be too small
            if (objects.Count < MAX_OBJECTS || (this.size * 0.5f) < minSize)
            {
                //  Add the object at this level
                objects.Add(
                    new SphereTreeObject()
                    {
                        position = pos,
                        size = size,
                        obj = obj
                    }
                );

                //  Early out, we aren't diving into children yet
                return;
            }

            //  If there are no children, create them and push down the hierarchy
            if (!HasChildren)
            {
                CreateChildren();

                //  Push all objects to the nearest children
                foreach (SphereTreeObject thisObj in objects)
                {
                    int child = GetNearestChild(thisObj.position);
                    children[child].Add(thisObj.obj, thisObj.position, thisObj.size);
                }
                objects.Clear();
            }

            //  Push the added object down the hierarchy
            int nearestChild = GetNearestChild(pos);
            children[nearestChild].Add(obj, pos, size);
        }

        /// <summary>
        /// Gets the index of the nearest child to a position
        /// </summary>
        /// <param name="pos">position to check</param>
        /// <returns>index of the nearest child</returns>
        private int GetNearestChild(Vector3 pos)
        {
            int index = 0;

            index += pos.X <= this.position.X ? 0 : 1;
            index += pos.Y >= this.position.Y ? 0 : 4;
            index += pos.Z <= this.position.Z ? 0 : 2;

            return index;
        }

        /// <summary>
        /// Generates child nodes for this node
        /// </summary>
        private void CreateChildren()
        {
            float offset = size / 4f;
            float childSize = size / 2f;

            children = new SphereTreeNode<T>[8];

            //  Upper 4
            children[0] = new SphereTreeNode<T>( position + new Vector3(-offset, offset, -offset), childSize, minSize );
            children[1] = new SphereTreeNode<T>( position + new Vector3(offset, offset, -offset), childSize, minSize );
            children[2] = new SphereTreeNode<T>( position + new Vector3(-offset, offset, offset), childSize, minSize );
            children[3] = new SphereTreeNode<T>( position + new Vector3(offset, offset, offset), childSize, minSize );

            //  Lower 4
            children[4] = new SphereTreeNode<T>( position + new Vector3(-offset, -offset, -offset), childSize, minSize );
            children[5] = new SphereTreeNode<T>( position + new Vector3(offset, -offset, -offset), childSize, minSize );
            children[6] = new SphereTreeNode<T>( position + new Vector3(-offset, -offset, offset), childSize, minSize );
            children[7] = new SphereTreeNode<T>( position + new Vector3(offset, -offset, offset), childSize, minSize );
        }

        /// <summary>
        /// Redistribute objects to the hierarchy by pulled all to the top and pushing back down
        /// <para/>This should be called AFTER any changing to node size/position to reallocate objects appropriately
        /// <para/>WARNING: This is destructive. Any objects that are out of bounds or can't be pushed to a child are lost
        /// </summary>
        public void RedistObjects()
        {
            //  ! TODO Stack overflow
            PullObjects();
            PushObjects();
        }

        /// <summary>
        /// Pull all objects from lower nodes to this node, ignoring size limitations
        /// </summary>
        private void PullObjects()
        {
            if (!HasChildren) return;

            //  ! TODO Stack overflow

            foreach (SphereTreeNode<T> child in children)
            {
                child.PullObjects();
                objects.AddRange(child.objects);
            }

            foreach (SphereTreeNode<T> child in children)
                child.objects.Clear();
        }

        /// <summary>
        /// Pushes all objects in this node back down the hierarchy into appropriate children
        /// <para/>WARNING: This is destructive. Any object that can't be allocated in a child is lost
        /// </summary>
        private void PushObjects()
        {
            if (!HasChildren) return;

            //  ! TODO Stack overflow

            foreach (SphereTreeObject thisObj in objects)
            {
                int child = GetNearestChild(thisObj.position);
                children[child].TryAdd(thisObj.obj, thisObj.position, thisObj.size);
            }

            //  TODO make this non destructive without creating garbage data

            objects.Clear();
        }

        /// <summary>
        /// Attempts to consume all children into this node
        /// <para/>IF there is room to store all objects below this node and children are dead end branches
        /// </summary>
        /// <returns></returns>
        private bool TryConsumeChildren()
        {
            int numOfObjects = objects.Count;

            //  If any child has children, then assume too many objects to consume
            if (HasChildren)
            {
                foreach (SphereTreeNode<T> child in children)
                    if (child.HasChildren)
                        return false;
                    else
                        numOfObjects += child.objects.Count;
            }

            //  If too many objects, don't consume
            if (numOfObjects > MAX_OBJECTS)
                return false;
            //  ...Otherwise, consume the children and their objects
            else
                ConsumeChildren();

            return true;
        }

        /// <summary>
        /// Pulls all objects below this node and removes all children
        /// </summary>
        private void ConsumeChildren()
        {
            if (!HasChildren)
            {
                Debug.Log($"SphereTree attempted consuming NULL child nodes", LogType.WARNING);
                return;
            }

            //  Pull all objects from children
            foreach (SphereTreeNode<T> child in children)
                foreach (SphereTreeObject obj in child.objects)
                    objects.Add(obj);

            //  Remove the children
            children = null;
        }
    }
}
