using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Swordfish.Library.Types
{
    public class ChunkedDataStore
    {
        public const int NullPtr = -1;

        public int Size { get; }
        public int ChunkSize { get; }
        public int Count { get; private set; }

        private readonly object[] Data;
        private volatile int HighestPtr;
        private volatile int LowestPtr;
        private volatile int SecondLowestPtr;
        private readonly Queue<int> RecycledPtrs;
        private readonly int ChunkOffset;
        private readonly int ShiftOffset;

        public ChunkedDataStore(int size, int chunkSize)
        {
            if (chunkSize < 1)
                throw new ArgumentException($"{nameof(chunkSize)} must be greater than 0.");

            Size = size;
            ChunkSize = chunkSize;
            ChunkOffset = chunkSize + 1;
            ShiftOffset = (int)Math.Round(chunkSize / 2d, MidpointRounding.AwayFromZero);

            Data = new object[Size * ChunkOffset];

            Count = 0;
            HighestPtr = 0;
            LowestPtr = 0;
            SecondLowestPtr = 0;
            RecycledPtrs = new Queue<int>();
        }

        public int Add(int ptr = NullPtr)
        {
            if (ptr == NullPtr)
                ptr = AllocatePtr();

            Data[GetChunkPtr(ptr)] = true;
            for (int i = 1; i <= ChunkSize; i++)
                Data[GetChunkPtr(ptr) + i] = null;

            return ptr;
        }

        public int Add(object[] data, int ptr = NullPtr)
        {
            if (data.Length != ChunkSize)
                throw new ArgumentException("Data length must be equal to chunk count.");

            if (ptr == NullPtr)
                ptr = AllocatePtr();

            Data[GetChunkPtr(ptr)] = true;
            for (int i = 0; i < ChunkSize; i++)
                Data[GetChunkPtr(ptr) + i + 1] = data[i];

            return ptr;
        }

        public int Add(Dictionary<int, object> chunks, int ptr = NullPtr)
        {
            if (ptr == NullPtr)
                ptr = AllocatePtr();

            Data[GetChunkPtr(ptr)] = true;
            for (int i = 1; i <= ChunkSize; i++)
                Data[GetChunkPtr(ptr) + i] = chunks.TryGetValue(i, out object chunk) ? chunk : null;

            return ptr;
        }

        public void Set(int ptr, int chunkIndex, object chunk)
        {
            Data[GetChunkPtr(ptr) + chunkIndex + 1] = chunk;
        }

        public void Remove(int ptr)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            Data[GetChunkPtr(ptr)] = null;
            for (int i = 1; i <= ChunkSize; i++)
                Data[GetChunkPtr(ptr) + i] = null;

            FreePtr(ptr);
        }

        public object[] Get(int ptr)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            object[] chunks = new object[ChunkSize];
            Array.Copy(Data, GetChunkPtr(ptr) + 1, chunks, 0, ChunkSize);
            return chunks;
        }

        public object GetAt(int ptr, int chunkIndex)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            return Data[GetChunkPtr(ptr) + chunkIndex + 1];
        }

        public ref object GetRefAt(int ptr, int chunkIndex)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            return ref Data[GetChunkPtr(ptr) + chunkIndex + 1];
        }

        public bool Has(int ptr)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            return Data[GetChunkPtr(ptr)] is true;
        }

        public bool HasAt(int ptr, int chunkIndex)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            int index = GetChunkPtr(ptr) + chunkIndex + 1;
            return Data[index] != null;
        }

        public int[] All()
        {
            int[] ptrs = new int[Count];
            int ptrIndex = 0;
            for (int i = LowestPtr; i < HighestPtr; i++)
            {
                if (Data[GetChunkPtr(i)] != null)
                    ptrs[ptrIndex++] = i;
            }

            return ptrs;
        }

        public void ForEach(int ptr, Action<object> action)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            for (int i = 1; i <= ChunkSize; i++)
                action.Invoke(Data[GetChunkPtr(ptr) + i]);
        }

        public IEnumerable<object> EnumerateAt(int ptr)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            ptr = GetChunkPtr(ptr);
            for (int i = 1; i <= ChunkSize; i++)
            {
                object value = Data[ptr + i];
                if (value != null)
                    yield return value;
            }
        }

        private int AllocatePtr()
        {
            bool anyRecycledPtrs = RecycledPtrs.Count != 0;

            if (HighestPtr > Size && !anyRecycledPtrs)
                throw new OutOfMemoryException($"Exceeded maximum chunk allocations ({Size}).");

            int ptr = anyRecycledPtrs ? RecycledPtrs.Dequeue() : HighestPtr++;

            if (ptr < LowestPtr)
                LowestPtr = ptr;

            Count++;
            return ptr;
        }

        private void FreePtr(int ptr)
        {
            RecycledPtrs.Enqueue(ptr);
            Count--;

            if (SecondLowestPtr < LowestPtr)
                SecondLowestPtr = LowestPtr;

            if (ptr == LowestPtr)
                LowestPtr = SecondLowestPtr == ptr ? LowestPtr + 1 : SecondLowestPtr;

            if (SecondLowestPtr < ptr)
                SecondLowestPtr = ptr;

            if (ptr == SecondLowestPtr)
                SecondLowestPtr++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetChunkPtr(int ptr) => ptr << ShiftOffset;
    }
}