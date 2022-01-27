using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace akanevrc.AnimatorControllerOverwriter.Editor.Tests
{
    internal static class TestUtil
    {
        public static readonly string WorkFolderPath = "Assets/akanevrc/AnimatorControllerOverwriter/Tests/Editor/Work";
        private static readonly string ControllerWorkFilePath = "Assets/akanevrc/AnimatorControllerOverwriter/Tests/Editor/Work/tmp.controller";
        private static readonly string AnimWorkFilePath = "Assets/akanevrc/AnimatorControllerOverwriter/Tests/Editor/Work/tmp.anim";

        public static string GetControllerWorkFilePath()
        {
            return AssetDatabase.GenerateUniqueAssetPath(ControllerWorkFilePath);
        }

        public static string GetAnimWorkFilePath()
        {
            return AssetDatabase.GenerateUniqueAssetPath(AnimWorkFilePath);
        }

        public static void CleanupWorkFolder()
        {
            foreach (var assetPath in Directory.GetFiles(WorkFolderPath).Where(x => Path.GetExtension(x) != ".txt" && Path.GetExtension(x) != ".meta"))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }
    }

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
