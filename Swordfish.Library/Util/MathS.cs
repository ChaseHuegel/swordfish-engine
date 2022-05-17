using System;

using OpenTK.Mathematics;

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

        public static float Distance (Vector3 firstPosition, Vector3 secondPosition)
        {
            Vector3 heading;

            heading.X = firstPosition.X - secondPosition.X;
            heading.Y = firstPosition.Y - secondPosition.Y;
            heading.Z = firstPosition.Z - secondPosition.Z;

            float distanceSquared = heading.X * heading.X + heading.Y * heading.Y + heading.Z * heading.Z;
            return (float)Math.Sqrt(distanceSquared);
        }

        public static float DistanceUnsquared(Vector3 firstPosition, Vector3 secondPosition)
        {
            return (firstPosition.X - secondPosition.X) * (firstPosition.X - secondPosition.X) +
                    (firstPosition.Y - secondPosition.Y) * (firstPosition.Y - secondPosition.Y) +
                    (firstPosition.Z - secondPosition.Z) * (firstPosition.Z - secondPosition.Z);
        }

        public static Vector3[] CreateEllipse(float a, float b, float h, float k, float theta, int resolution)
        {
            Vector3[] positions = new Vector3[resolution+1];
            Quaternion q = Quaternion.FromAxisAngle(Vector3.UnitY, theta);
            Vector3 center = new Vector3(h,k,0.0f);

            for (int i = 0; i <= resolution; i++) {
                float angle = (float)i / (float)resolution * 2.0f * (float)Math.PI;
                positions[i] = new Vector3(a * (float)Math.Cos(angle), 0.0f, b * (float)Math.Sin(angle));
                positions[i] = q * positions[i] + center;
            }

            return positions;
        }

        public static float Grayscale(Vector4 _color)
        {
            return (_color.X + _color.Y + _color.Z) / 3;
        }

        public static int IndexFrom2D(int _x, int _y, int _width)
        {
            return _x + (_width * _y);
        }
    }
}