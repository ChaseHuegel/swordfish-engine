using System;

namespace Swordfish
{

public class Bit
{
    public static void Set(ref int value, int index) { value = 1 << index; }
    public static void Clear(ref int value, int index) { value = ~(1 << index); }

    public static bool IsSet(int value, int index) { return (value & (1 << index)) == 0; }

    public static bool Compare(int a, int b, int index) { return IsSet(a, index) && IsSet(b, index); }
}

[Serializable]
public class BitMask
{
    public int bits;

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

    public bool IsSet(int index) => Bit.IsSet(bits, index);
    public bool Compare(BitMask x, int index) => Bit.Compare(bits, x.bits, index);

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
        for (int i = 0; i < 32; i++)
            if (Bit.Compare(a, b, i))
                return true;

        return false;
    }

    public static bool operator== (BitMask a, BitMask b)
    {
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