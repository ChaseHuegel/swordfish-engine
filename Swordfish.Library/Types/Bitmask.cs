using Swordfish.Library.Util;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Types;

// ReSharper disable once UnusedType.Global
public class Bitmask(int bits = 0)
{
    public int Bits = bits;
    public readonly int Length = 32;
    
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
    /// <returns>bitmask builder</returns>
    public Bitmask Set(int index)
    {
        Bitwise.Set(ref Bits, index);
        return this;
    }

    /// <summary>
    /// Set bit at index to 0
    /// </summary>
    /// <returns>bitmask builder</returns>
    public Bitmask Clear(int index)
    {
        Bitwise.Clear(ref Bits, index);
        return this;
    }

    /// <summary>
    /// Flip bit at index between 0 and 1
    /// </summary>
    public Bitmask Flip(int index)
    {
        Bitwise.Flip(ref Bits, index);
        return this;
    }

    /// <summary>
    /// Get bit state at index
    /// </summary>
    /// <returns>true if bit is 1</returns>
    public bool Get(int index) => Bitwise.Get(Bits, index);

    /// <summary>
    /// Compares another bitmask to this at index
    /// </summary>
    /// <returns>true if both bitmasks are equal at index</returns>
    public bool Compare(Bitmask x, int index) => Bitwise.Compare(Bits, x.Bits, index);

    //  Indexer
    public bool this[int index] => Get(index);

    //  Casting int to bitmask and bitmask to int
    public static implicit operator Bitmask(int mask) => new(mask);
    public static implicit operator int(Bitmask mask) => mask.Bits;

    public static bool operator!= (Bitmask a, Bitmask b)
    {
        return a != null && b != null && a.Bits.Equals(b.Bits);
    }

    public static bool operator== (Bitmask a, Bitmask b)
    {
        return a != null && b != null && a.Bits.Equals(b.Bits);
    }

    //  Equals overrides
    public override bool Equals(System.Object obj)
    {
        var bitmask = obj as Bitmask;
        return bitmask != null && bitmask.Bits.Equals(Bits);
    }

    // ReSharper disable once NonReadonlyMemberInGetHashCode
    public override int GetHashCode() => Bits.GetHashCode();
}