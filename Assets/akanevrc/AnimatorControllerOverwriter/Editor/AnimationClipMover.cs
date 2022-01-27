using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace akanevrc.AnimatorControllerOverwriter.Editor
{
    public class AnimationClipGameObjectNameContainsSlashException : Exception
    {
        public readonly string Name;

        public AnimationClipGameObjectNameContainsSlashException(string name)
        {
            Name = name;
        }
    }

    public class AnimationClipGameObjectIsNotChildException : Exception
    {
    }

    public enum AnimationClipMoveMode
    {
        Disable,
        ChildToParent,
        ParentToChild
    }

    public interface IAnimationClipMover
    {
        void Validate
        (
            AnimationClip clip,
            GameObject overwrite,
            GameObject original,
            AnimationClipMoveMode mode
        );

        AnimationClip Generate
        (
            string folderPath,
            AnimationClip clip,
            GameObject overwrite,
            GameObject original,
            AnimationClipMoveMode mode
        );
    }

    public class AnimationClipMover : IAnimationClipMover
    {
        public void Validate
        (
            AnimationClip clip,
            GameObject overwrite,
            GameObject original,
            AnimationClipMoveMode mode
        )
        {
            var path = Util.GetControllerWorkFilePath();

            try
            {
                Move
                (
                    path,
                    clip,
                    overwrite,
                    original,
                    mode
                );
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        public AnimationClip Generate
        (
            string folderPath,
            AnimationClip clip,
            GameObject overwrite,
            GameObject original,
            AnimationClipMoveMode mode
        )
        {
            var path = Path.Combine(folderPath, $"{clip.name}_moved.anim");
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            try
            {
                var result = Move
                (
                    path,
                    clip,
                    overwrite,
                    original,
                    mode
                );
                AssetDatabase.Refresh();
                return result;
            }
            catch
            {
                AssetDatabase.DeleteAsset(path);
                throw;
            }
        }

        public AnimationClip Move
        (
            string path,
            AnimationClip clip,
            GameObject overwrite,
            GameObject original,
            AnimationClipMoveMode mode
        )
        {
            switch (mode)
            {
                case AnimationClipMoveMode.Disable:
                    return clip;
                case AnimationClipMoveMode.ChildToParent:
                    return MoveCore(path, clip, overwrite, original, ConvertPathAsChildToParent(overwrite, original));
                case AnimationClipMoveMode.ParentToChild:
                    return MoveCore(path, clip, overwrite, original, ConvertPathAsParentToChild(overwrite, original));
                default:
                    throw new ArgumentException(nameof(mode));
            }
        }

        private AnimationClip MoveCore
        (
            string path,
            AnimationClip clip,
            GameObject overwrite,
            GameObject original,
            Func<string, (bool success, string path)> convertPath
        )
        {
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(clip), path))
            {
                throw new AssetCopyFailureException();
            }

            var newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            foreach (var binding in AnimationUtility.GetCurveBindings(newClip).ToArray())
            {
                var curve   = AnimationUtility.GetEditorCurve(newClip, binding);
                var newPath = convertPath(binding.path);
                if (newPath.success)
                {
                    var newBinding = EditorCurveBinding.FloatCurve(newPath.path, binding.type, binding.propertyName);
                    AnimationUtility.SetEditorCurve(newClip, binding   , null);
                    AnimationUtility.SetEditorCurve(newClip, newBinding, curve);
                }
            }

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(newClip).ToArray())
            {
                var curve   = AnimationUtility.GetObjectReferenceCurve(newClip, binding);
                var newPath = convertPath(binding.path);
                if (newPath.success)
                {
                    var newBinding = EditorCurveBinding.PPtrCurve(newPath.path, binding.type, binding.propertyName);
                    AnimationUtility.SetObjectReferenceCurve(newClip, binding   , null);
                    AnimationUtility.SetObjectReferenceCurve(newClip, newBinding, curve);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return newClip;
        }

        private Func<string, (bool success, string path)> ConvertPathAsChildToParent(GameObject overwrite, GameObject original)
        {
            var rootPath = GetPath(original, overwrite);

            return (string path) =>
            {
                if (rootPath == "")
                {
                    return (true, path);
                }
                else if (path == "")
                {
                    return (true, rootPath);
                }
                else
                {
                    return (true, $"{rootPath}/{path}");
                }
            };
        }

        private Func<string, (bool success, string path)> ConvertPathAsParentToChild(GameObject overwrite, GameObject original)
        {
            var rootPath = GetPath(overwrite, original);

            return (string path) =>
            {
                if (rootPath == "")
                {
                    return (true, path);
                }
                else if (path.StartsWith($"{rootPath}/"))
                {
                    return (true, path.Substring(rootPath.Length + 1));
                }
                else
                {
                    return (false, null);
                }
            };
        }

        private string GetPath(GameObject parent, GameObject child)
        {
            if (child.transform.IsChildOf(parent.transform))
            {
                var objs = new Stack<GameObject>();

                while (parent != null && child != null && parent != child)
                {
                    if (child.name.Contains("/"))
                    {
                        throw new AnimationClipGameObjectNameContainsSlashException(child.name);
                    }
                    objs.Push(child);
                    child = child.transform.parent.gameObject;
                }

                if (parent == null || child == null)
                {
                    throw new AnimationClipGameObjectIsNotChildException();
                }

                return string.Join("/", objs.Select(x => x.name));
            }
            else
            {
                throw new AnimationClipGameObjectIsNotChildException();
            }
        }
    }
}
