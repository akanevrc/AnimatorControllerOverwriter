using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace akanevrc.AnimatorControllerOverwriter.Editor
{
    public class PathIsNotDataPathException : Exception
    {
        public string Path;

        public PathIsNotDataPathException(string path)
        {
            Path = path;
        }
    }

    public class AssetCopyFailureException : Exception
    {
    }

    public static class Util
    {
        public static readonly string WorkFolderPath = "Assets/akanevrc/AnimatorControllerOverwriter/Editor/Work";
        private static readonly string ControllerWorkFilePath = "Assets/akanevrc/AnimatorControllerOverwriter/Editor/Work/tmp.controller";
        private static readonly string AnimWorkFilePath = "Assets/akanevrc/AnimatorControllerOverwriter/Editor/Work/tmp.anim";

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

        public static string ToRelativePath(string absolutePath)
        {
            if (absolutePath.StartsWith(Application.dataPath))
            {
                return $"Assets/{absolutePath.Substring(Application.dataPath.Length)}";
            }
            else
            {
                throw new PathIsNotDataPathException(absolutePath);
            }
        }

        public static Texture2D LoadBuiltInIcon(string name)
        {
            return Resources.FindObjectsOfTypeAll<Texture2D>()
                .Where(x => AssetDatabase.GetAssetPath(x) == "Library/unity editor resources")
                .FirstOrDefault(x => x.name == name);
        }
    }
}
