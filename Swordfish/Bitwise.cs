using System;

namespace Swordfish
{

public class Bit
{
    public static void Flip(ref int value, int index)
    {
        if (IsSet(value, index))
            Set(ref value, index);
        else
            Clear(ref value, index);
    }

    public static void Set(ref int value, int index) { value = 1 << index; }
    public static void Clear(ref int value, int index) { value = ~(1 << index); }

    public static bool IsSet(int value, int index) { return (value & (1 << index)) == 0; }

    public static bool Compare(int a, int b, int index) { return IsSet(a, index) && IsSet(b, index); }
}

[Serializable]
public class BitMask
{
    public int bits;
    public readonly int Length = 32;

    public int Ones {
        get
        {
            int v = 0;
            for (int i = 0; i < Length; i++)
                if (IsSet(i)) v++;

            return v;
        }
    }

    public int Zeros {
        get => Length - Ones;
    }

    public BitMask(int bits = 0)
    {
        this.bits = bits;
    }

    public BitMask Set(int index)
    {
        Bit.Set(ref bits, index);
        return this;
    }

    public BitMask Clear(int index)
    {
        Bit.Clear(ref bits, index);
        return this;
    }

    public void Flip(int index) => Bit.Flip(ref bits, index);
    public bool IsSet(int index) => Bit.IsSet(bits, index);
    public bool Compare(BitMask x, int index) => Bit.Compare(bits, x.bits, index);

    public bool this[int index]
    {
        get => IsSet(index);
    }

    public static implicit operator BitMask(int mask)
    {
        return new BitMask(mask);
    }

    public static implicit operator int(BitMask mask)
    {
        return mask.bits;
    }

    public static bool operator!= (BitMask a, BitMask b)
    {
        if (b == null) return false;

        for (int i = 0; i < 32; i++)
            if (Bit.Compare(a, b, i))
                return true;

        return false;
    }

    public static bool operator== (BitMask a, BitMask b)
    {
        if (b == null) return false;

        for (int i = 0; i < 32; i++)
            if (!Bit.Compare(a, b, i))
                return false;

        return true;
    }

    public override bool Equals(System.Object obj)
    {
        BitMask bitmask = obj as BitMask;

        if (bitmask == null)
        {
            return false;
        }

        return bitmask.bits.Equals(this.bits);
    }

    public override int GetHashCode()
    {
        return bits.GetHashCode();
    }
}

}