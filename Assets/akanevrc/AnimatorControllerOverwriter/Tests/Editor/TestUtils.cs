using System;
using System.Linq;

namespace akanevrc.AnimatorControllerOverwriter.Editor.Tests
{
    internal class TestRandom
    {
        private readonly Random Random;

        public TestRandom(long seed)
        {
            Random = new Random(unchecked((int)seed));
        }

        public int NextInt(int min, int max)
        {
            return Random.Next(min, max);
        }

        public float NextFloat(float min, float max)
        {
            return (float)Random.NextDouble() * (max - min) + min;
        }

        public bool NextBool()
        {
            return Random.Next(2) == 0;
        }

        public int NextEnum(Type enumType)
        {
            if (!enumType.IsEnum) throw new ArgumentException(nameof(enumType));

            return Pick(enumType.GetEnumValues().Cast<int>().ToArray());
        }

        public T Pick<T>(T[] array)
        {
            return array.Length == 0 ? default : array[NextInt(0, array.Length)];
        }

        public T[] PickSome<T>(T[] array)
        {
            return array.Where(_ => NextBool()).ToArray();
        }
    }
}
