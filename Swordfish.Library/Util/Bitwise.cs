namespace Swordfish.Library.Util
{
    public static class Bitwise
    {
        /// <summary>
        /// Flip bit at index between 0 and 1
        /// </summary>
        /// <param name="value"></param>
        /// <param name="index"></param>
        public static void Flip(ref int value, int index)
        {
            if (Get(value, index))
                Set(ref value, index);
            else
                Clear(ref value, index);
        }

        /// <summary>
        /// Set bit at index to 1
        /// </summary>
        /// <param name="value"></param>
        /// <param name="index"></param>
        public static void Set(ref int value, int index) { value = 1 << index; }

        /// <summary>
        /// Set bit at index to 0
        /// </summary>
        /// <param name="value"></param>
        /// <param name="index"></param>
        public static void Clear(ref int value, int index) { value = ~(1 << index); }

        /// <summary>
        /// Get bit state at index
        /// </summary>
        /// <param name="value"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static bool Get(int value, int index) { return (value & (1 << index)) == 0; }

        /// <summary>
        /// Compare two bits at index
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="index"></param>
        /// <returns>true if both bits are equal at index</returns>
        public static bool Compare(int a, int b, int index) { return Get(a, index) == Get(b, index); }
    }
}
