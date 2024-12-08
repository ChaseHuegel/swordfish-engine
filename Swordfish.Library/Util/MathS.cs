using System;
using MemorizedCos = FastMath.MemoizedCos;
using MemorizedSin = FastMath.MemoizedSin;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Util;

public static class MathS
{
    public const float DEGREES_TO_RADIANS = MathF.PI / 180f;
    public const float RADIANS_FULL_REVOLUTION = MathF.PI * 2;
    public const float RADIANS_HALF_REVOLUTION = MathF.PI;

    public static readonly Random Random = new();
    private static readonly MemorizedSin _fastSin = MemorizedSin.ConstructByMaxError(0.001f);
    private static readonly MemorizedCos _fastCos = MemorizedCos.ConstructByMaxError(0.001f);

    public static float Sin(float value)
    {
        return _fastSin.CalculateUnbound(value);
    }

    public static float Cos(float value)
    {
        return _fastCos.CalculateUnbound(value);
    }

    public static float Lerp(float start, float end, float value)
    {
        return start * (1 - value) + end * value;
    }

    public static double PingPong(double time, double max)
    {
        return max - Math.Abs(Repeat(time, max * 2) - max);
    }

    public static double Repeat(double time, double max)
    {
        return Math.Clamp(time - Math.Floor(time / max) * max, 0, max);
    }

    public static float Slerp(float start, float end, float value)
    {
        return (float)Math.Pow(end * Math.Pow(start, -1), value) * start;
    }

    public static int GetOverflow(int value, int min, int max)
    {
        if (value < min)
        {
            return value - min;
        }

        if (value > max)
        {
            return value - max;
        }

        return 0;
    }

    public static float GetOverflow(float value, float min, float max)
    {
        if (value < min)
        {
            return value - min;
        }

        if (value > max)
        {
            return value - max;
        }

        return 0.0f;
    }

    public static int WrapInt(int value, int rangeMin, int rangeMax)
    {
        int range = rangeMax - rangeMin + 1;

        if (value < rangeMin)
        {
            value += range * ((rangeMin - value) / range + 1);
        }

        return rangeMin + (value - rangeMin) % range;
    }

    public static float RangeToRange(float input, float low, float high, float newLow, float newHigh)
    {
        return ((input - low) / (high - low)) * (newHigh - newLow) + newLow;
    }

    public static int IndexFrom2D(int x, int y, int width)
    {
        return x + (width * y);
    }

    public static sbyte Clamp(sbyte value, sbyte min, sbyte max)
    {
        return value < min ? min : value > max ? max : value;
    }

    public static byte Clamp(byte value, byte min, byte max)
    {
        return value < min ? min : value > max ? max : value;
    }

    public static short Clamp(short value, short min, short max)
    {
        return value < min ? min : value > max ? max : value;
    }

    public static ushort Clamp(ushort value, ushort min, ushort max)
    {
        return value < min ? min : value > max ? max : value;
    }

    public static int Clamp(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }

    public static uint Clamp(uint value, uint min, uint max)
    {
        return value < min ? min : value > max ? max : value;
    }

    public static long Clamp(long value, long min, long max)
    {
        return value < min ? min : value > max ? max : value;
    }

    public static ulong Clamp(ulong value, ulong min, ulong max)
    {
        return value < min ? min : value > max ? max : value;
    }

    public static float Clamp(float value, float min, float max)
    {
        return value < min ? min : value > max ? max : value;
    }

    public static double Clamp(double value, double min, double max)
    {
        return value < min ? min : value > max ? max : value;
    }

    public static decimal Clamp(decimal value, decimal min, decimal max)
    {
        return value < min ? min : value > max ? max : value;
    }
}