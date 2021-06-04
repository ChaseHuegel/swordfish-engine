﻿using Swordfish.Util;

namespace Swordfish.Containers
{
    public class Bitmask
    {
        public int bits;
        public readonly int Length = 32;

        /// <summary>
        /// Number of 1 bits
        /// </summary>
        public int Ones {
            get
            {
                int v = 0;
                for (int i = 0; i < Length; i++)
                    if (Get(i)) v++;

                return v;
            }
        }

        /// <summary>
        /// Number of 0 bits
        /// </summary>
        public int Zeros {
            get => Length - Ones;
        }

        public Bitmask(int bits = 0)
        {
            this.bits = bits;
        }

        /// <summary>
        /// Set bit at index to 1
        /// </summary>
        /// <param name="index"></param>
        /// <returns>bitmask builder</returns>
        public Bitmask Set(int index)
        {
            Bitwise.Set(ref bits, index);
            return this;
        }

        /// <summary>
        /// Set bit at index to 0
        /// </summary>
        /// <param name="index"></param>
        /// <returns>bitmask builder</returns>
        public Bitmask Clear(int index)
        {
            Bitwise.Clear(ref bits, index);
            return this;
        }

        /// <summary>
        /// Flip bit at index between 0 and 1
        /// </summary>
        /// <param name="index"></param>
        public Bitmask Flip(int index)
        {
            Bitwise.Flip(ref bits, index);
            return this;
        }

        /// <summary>
        /// Get bit state at index
        /// </summary>
        /// <param name="index"></param>
        /// <returns>true if bit is 1</returns>
        public bool Get(int index) => Bitwise.Get(bits, index);

        /// <summary>
        /// Compares another bitmask to this at index
        /// </summary>
        /// <param name="x"></param>
        /// <param name="index"></param>
        /// <returns>true if both bitmasks are equal at index</returns>
        public bool Compare(Bitmask x, int index) => Bitwise.Compare(bits, x.bits, index);

        //  Indexer
        public bool this[int index]
        {
            get => Get(index);
        }

        //  Casting int to bitmask and bitmask to int
        public static implicit operator Bitmask(int mask) => new Bitmask(mask);
        public static implicit operator int(Bitmask mask) => mask.bits;

        /// <summary>
        /// Compare if two bitmasks are not matching
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator!= (Bitmask a, Bitmask b)
        {
            if (b == null) return false;

            return a.bits.Equals(b.bits);
        }

        /// <summary>
        /// Compare if two bitmasks are matching
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator== (Bitmask a, Bitmask b)
        {
            if (b == null) return false;

            return a.bits.Equals(b.bits);
        }

        //  Equals overrides
        public override bool Equals(System.Object obj)
        {
            Bitmask bitmask = obj as Bitmask;

            if (bitmask == null) return false;

            return bitmask.bits.Equals(this.bits);
        }

        public override int GetHashCode() => bits.GetHashCode();
    }
}