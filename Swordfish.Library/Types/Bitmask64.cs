using Swordfish.Library.Util;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Types;

// ReSharper disable once UnusedType.Global
public class Bitmask64(in long bits = 0)
{
    public long Bits = bits;
    public readonly int Length = 64;

    public int GetOnes() 
    {
        var v = 0;
        for (var i = 0; i < Length; i++)
        {
            if (Get(i))
            {
                v++;
            }
        }

        return v;
    }

    public int GetZeros() 
    {
        return Length - GetOnes();
    }

    /// <summary>
    /// Set bit at index to 1
    /// </summary>
    /// <returns>Bitmask64 builder</returns>
    public Bitmask64 Set(int index)
    {
        Bitwise64.Set(ref Bits, index);
        return this;
    }

    /// <summary>
    /// Set bit at index to 0
    /// </summary>
    /// <returns>Bitmask64 builder</returns>
    public Bitmask64 Clear(int index)
    {
        Bitwise64.Clear(ref Bits, index);
        return this;
    }

    /// <summary>
    /// Flip bit at index between 0 and 1
    /// </summary>
    public Bitmask64 Flip(int index)
    {
        Bitwise64.Flip(ref Bits, index);
        return this;
    }

    /// <summary>
    /// Get bit state at index
    /// </summary>
    /// <returns>true if bit is 1</returns>
    public bool Get(int index) => Bitwise64.Get(Bits, index);

    /// <summary>
    /// Compares another Bitmask64 to this at index
    /// </summary>
    /// <returns>true if both bitmasks are equal at index</returns>
    public bool Compare(Bitmask64 x, int index) => Bitwise64.Compare(Bits, x.Bits, index);

    //  Indexer
    public bool this[int index] => Get(index);

    //  Casting long to Bitmask64 and Bitmask64 to long
    public static implicit operator Bitmask64(long mask) => new(mask);
    public static implicit operator long(Bitmask64 mask) => mask.Bits;

    public static bool operator!= (Bitmask64 a, Bitmask64 b)
    {
        return a != null && b != null && a.Bits.Equals(b.Bits);
    }

    public static bool operator== (Bitmask64 a, Bitmask64 b)
    {
        return a != null & b != null && a.Bits.Equals(b.Bits);
    }

    public override bool Equals(System.Object obj)
    {
        var bitmask64 = obj as Bitmask64;
        return bitmask64 != null && bitmask64.Bits.Equals(Bits);
    }

    // ReSharper disable once NonReadonlyMemberInGetHashCode
    public override int GetHashCode() => Bits.GetHashCode();
}