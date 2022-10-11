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
            ShiftOffset = (int)Math.Round(ChunkOffset / 2d, MidpointRounding.AwayFromZero);

            Data = new object[Size * ChunkOffset];

            Count = 0;
            HighestPtr = 0;
            LowestPtr = 0;
            SecondLowestPtr = 0;
            RecycledPtrs = new Queue<int>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(int ptr = NullPtr)
        {
            if (ptr == NullPtr)
                ptr = AllocatePtr();

            Data[ptr << ShiftOffset] = true;
            for (int i = 1; i <= ChunkSize; i++)
                Data[(ptr << ShiftOffset) + i] = null;

            return ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(object[] data, int ptr = NullPtr)
        {
            if (data.Length != ChunkSize)
                throw new ArgumentException("Data length must be equal to chunk count.");

            if (ptr == NullPtr)
                ptr = AllocatePtr();

            Data[ptr << ShiftOffset] = true;
            for (int i = 0; i < ChunkSize; i++)
                Data[(ptr << ShiftOffset) + i + 1] = data[i];

            return ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(Dictionary<int, object> chunks, int ptr = NullPtr)
        {
            if (ptr == NullPtr)
                ptr = AllocatePtr();

            Data[ptr << ShiftOffset] = true;
            for (int i = 1; i <= ChunkSize; i++)
                Data[(ptr << ShiftOffset) + i] = chunks.TryGetValue(i, out object chunk) ? chunk : null;

            return ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int ptr, int chunkIndex, object chunk)
        {
            Data[(ptr << ShiftOffset) + chunkIndex + 1] = chunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int ptr)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            Data[ptr << ShiftOffset] = null;
            for (int i = 1; i <= ChunkSize; i++)
                Data[(ptr << ShiftOffset) + i] = null;

            FreePtr(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object[] Get(int ptr)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            object[] chunks = new object[ChunkSize];
            Array.Copy(Data, (ptr << ShiftOffset) + 1, chunks, 0, ChunkSize);
            return chunks;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetAt(int ptr, int chunkIndex)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            return Data[(ptr << ShiftOffset) + chunkIndex + 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref object GetRefAt(int ptr, int chunkIndex)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            return ref Data[(ptr << ShiftOffset) + chunkIndex + 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int ptr)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            return Data[ptr << ShiftOffset] is true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAt(int ptr, int chunkIndex)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            int index = (ptr << ShiftOffset) + chunkIndex + 1;
            return Data[index] != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int[] All()
        {
            int[] ptrs = new int[Count];
            int ptrIndex = 0;
            for (int i = LowestPtr; i < HighestPtr; i++)
            {
                if (Data[i << ShiftOffset] != null)
                    ptrs[ptrIndex++] = i;
            }

            return ptrs;
        }

        public void ForEach(int ptr, Action<object> action)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            for (int i = 1; i <= ChunkSize; i++)
                action.Invoke(Data[(ptr << ShiftOffset) + i]);
        }

        public IEnumerable<object> EnumerateAt(int ptr)
        {
            if (ptr == NullPtr)
                throw new NullReferenceException();

            ptr = ptr << ShiftOffset;
            for (int i = 1; i <= ChunkSize; i++)
                yield return Data[ptr + i];
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
    }
}