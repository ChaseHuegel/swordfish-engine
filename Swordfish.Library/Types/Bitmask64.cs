using Swordfish.Library.Util;

namespace Swordfish.Library.Types
{
    public class Bitmask64
    {
        public long bits;
        public readonly int Length = 64;

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

        public Bitmask64(long bits = 0)
        {
            this.bits = bits;
        }

        /// <summary>
        /// Set bit at index to 1
        /// </summary>
        /// <param name="index"></param>
        /// <returns>Bitmask64 builder</returns>
        public Bitmask64 Set(int index)
        {
            Bitwise64.Set(ref bits, index);
            return this;
        }

        /// <summary>
        /// Set bit at index to 0
        /// </summary>
        /// <param name="index"></param>
        /// <returns>Bitmask64 builder</returns>
        public Bitmask64 Clear(int index)
        {
            Bitwise64.Clear(ref bits, index);
            return this;
        }

        /// <summary>
        /// Flip bit at index between 0 and 1
        /// </summary>
        /// <param name="index"></param>
        public Bitmask64 Flip(int index)
        {
            Bitwise64.Flip(ref bits, index);
            return this;
        }

        /// <summary>
        /// Get bit state at index
        /// </summary>
        /// <param name="index"></param>
        /// <returns>true if bit is 1</returns>
        public bool Get(int index) => Bitwise64.Get(bits, index);

        /// <summary>
        /// Compares another Bitmask64 to this at index
        /// </summary>
        /// <param name="x"></param>
        /// <param name="index"></param>
        /// <returns>true if both bitmasks are equal at index</returns>
        public bool Compare(Bitmask64 x, int index) => Bitwise64.Compare(bits, x.bits, index);

        //  Indexer
        public bool this[int index]
        {
            get => Get(index);
        }

        //  Casting long to Bitmask64 and Bitmask64 to long
        public static implicit operator Bitmask64(long mask) => new Bitmask64(mask);
        public static implicit operator long(Bitmask64 mask) => mask.bits;

        /// <summary>
        /// Compare if two bitmasks are not matching
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator!= (Bitmask64 a, Bitmask64 b)
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
        public static bool operator== (Bitmask64 a, Bitmask64 b)
        {
            if (b == null) return false;

            return a.bits.Equals(b.bits);
        }

        public override bool Equals(System.Object obj)
        {
            Bitmask64 Bitmask64 = obj as Bitmask64;

            if (Bitmask64 == null)
            {
                return false;
            }

            return Bitmask64.bits.Equals(this.bits);
        }

        public override int GetHashCode() => bits.GetHashCode();
    }
}