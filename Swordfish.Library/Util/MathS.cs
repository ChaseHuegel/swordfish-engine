using System;

namespace Swordfish.Library.Util
{
	public class MathS
	{
        public static float Lerp(float start, float end, float value)
        {
            return start + value * (end - start);
        }

        public static float Slerp(float start, float end, float value)
        {
            return (float)Math.Pow(end * Math.Pow(start, -1), value) * start;
        }

        public static int GetOverflow(int value, int min, int max)
        {
            if (value < min) return value - min;
            if (value > max) return value - max;

            return 0;
        }

        public static float GetOverflow(float value, float min, float max)
        {
            if (value < min) return value - min;
            if (value > max) return value - max;

            return 0.0f;
        }

        public static int WrapInt(int _value, int _rangeMin, int _rangeMax)
        {
            int range = _rangeMax - _rangeMin + 1;

            if (_value < _rangeMin)
            {
                _value += range * ((_rangeMin - _value) / range + 1);
            }

            return _rangeMin + (_value - _rangeMin) % range;
        }

        public static float RangeToRange(float _input, float _low, float _high, float _newLow, float _newHigh)
        {
            return ((_input - _low) / (_high - _low)) * (_newHigh - _newLow) + _newLow;
        }

        public static int IndexFrom2D(int _x, int _y, int _width)
        {
            return _x + (_width * _y);
        }
    }
}