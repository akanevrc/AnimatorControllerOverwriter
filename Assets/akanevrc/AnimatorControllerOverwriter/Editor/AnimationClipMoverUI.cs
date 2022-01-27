using System;
using UnityEditor;
using UnityEngine;

namespace akanevrc.AnimatorControllerOverwriter.Editor
{
    public enum AnimationClipUIMoveMode
    {
        ChildToParent,
        ParentToChild
    }

    public class AnimationClipMoverUI : EditorWindow
    {
        public IAnimationClipMover AnimationClipMover = new AnimationClipMover();
        
        public AnimationClip OverwriteAnimationClip = null;
        public AnimationClipUIMoveMode AnimationClipUIMoveMode = AnimationClipUIMoveMode.ChildToParent;
        public AnimationClipMoveMode AnimationClipMoveMode = AnimationClipMoveMode.ChildToParent;
        public GameObject OriginalObject = null;
        public GameObject OverwriteObject = null;

        public Exception Error = null;
        public string LastRunningProcess = "";
        public Vector2 ScrollPosition = new Vector2(0F, 0F);

        private Texture2D InfoIcon;
        private Texture2D WarnIcon;
        private Texture2D ErrorIcon;

        private bool ButtonsEnabled => OverwriteAnimationClip != null && OriginalObject != null && OverwriteObject != null;

        private void OnEnable()
        {
            InfoIcon  = Util.LoadBuiltInIcon("console.infoicon");
            WarnIcon  = Util.LoadBuiltInIcon("console.warnicon");
            ErrorIcon = Util.LoadBuiltInIcon("console.erroricon");
        }

        [MenuItem("Tools/Move AnimationClip...")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<AnimationClipMoverUI>().Show();
        }

        private void OnGUI()
        {
            ScrollPosition = EditorGUILayout.BeginScrollView(ScrollPosition);

            EditorGUIUtility.labelWidth = 200.0F;

            GUILayout.Label("Move AnimationClip", new GUIStyle(EditorStyles.largeLabel));

            EditorGUILayout.Space();

            OverwriteAnimationClip =
                (AnimationClip)EditorGUILayout.ObjectField
                (
                    "AnimationClip",
                    OverwriteAnimationClip,
                    typeof(AnimationClip),
                    true
                );

            EditorGUILayout.Space();

            AnimationClipMoverGUI();

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(!ButtonsEnabled);

            if (GUILayout.Button("Check validation"))
            {
                try
                {
                    Validate();
                }
                catch (Exception ex)
                {
                    Error = ex;
                }
            }

            if (GUILayout.Button("Generate AnimationClip"))
            {
                var folderPath = EditorUtility.OpenFolderPanel
                (
                    "Select AnimationClip Save Folder",
                    "Assets",
                    ""
                );

                if (!string.IsNullOrEmpty(folderPath))
                {
                    try
                    {
                        Generate(Util.ToRelativePath(folderPath));
                    }
                    catch (Exception ex)
                    {
                        Error = ex;
                    }
                }
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            if (Error is AnimationClipGameObjectNameContainsSlashException nameex)
            {
                GUILayout.Box
                (
                    new GUIContent
                    (
                        $"GameObject name '{nameex.Name}' is invalid because name contains slash character.",
                        ErrorIcon
                    ),
                    new GUIStyle(EditorStyles.helpBox)
                );
            }
            else if (Error is AnimationClipGameObjectIsNotChildException childex)
            {
                GUILayout.Box
                (
                    new GUIContent
                    (
                        $"Parent and child GameObjects are not correct relation.",
                        ErrorIcon
                    ),
                    new GUIStyle(EditorStyles.helpBox)
                );
            }
            else if (Error is PathIsNotDataPathException pathex)
            {
                GUILayout.Box
                (
                    new GUIContent
                    (
                        $"'{pathex.Path}' is not asset data path.",
                        ErrorIcon
                    ),
                    new GUIStyle(EditorStyles.helpBox)
                );
            }
            else if (Error is AssetCopyFailureException copyex)
            {
                GUILayout.Box
                (
                    new GUIContent
                    (
                        $"Fail to copy AnimationClip.",
                        ErrorIcon
                    ),
                    new GUIStyle(EditorStyles.helpBox)
                );
            }
            else if (Error != null)
            {
                GUILayout.Box
                (
                    new GUIContent
                    (
                        $"Unexpected error occured : {Error.Message}\n{Error.StackTrace}",
                        ErrorIcon
                    ),
                    new GUIStyle(EditorStyles.helpBox)
                );
            }
            else if (!string.IsNullOrWhiteSpace(LastRunningProcess))
            {
                GUILayout.Box
                (
                    new GUIContent
                    (
                        $"{LastRunningProcess} succeeded.",
                        InfoIcon
                    ),
                    new GUIStyle(EditorStyles.helpBox)
                );
            }

            EditorGUILayout.EndScrollView();
        }

        private void AnimationClipMoverGUI()
        {
            AnimationClipUIMoveMode = (AnimationClipUIMoveMode)EditorGUILayout.EnumPopup("AnimationClip Move Mode", AnimationClipUIMoveMode);

            switch (AnimationClipUIMoveMode)
            {
                case AnimationClipUIMoveMode.ChildToParent:
                    AnimationClipMoveMode = AnimationClipMoveMode.ChildToParent;
                    break;
                case AnimationClipUIMoveMode.ParentToChild:
                    AnimationClipMoveMode = AnimationClipMoveMode.ParentToChild;
                    break;
                default:
                    AnimationClipMoveMode = AnimationClipMoveMode.Disable;
                    break;
            }

            GUILayout.Box
            (
                new GUIContent
                (
                    "This selection is the movement behaviour mode of AnimationClips' hierarchy.\n" +
                    "Child To Parent : Child AnimationClips will be copied to parent GameObject hierarchy.\n" +
                    "Parent To Child : Parent AnimationClips will be copied to child GameObject hierarchy.",
                    InfoIcon
                ),
                new GUIStyle(EditorStyles.helpBox)
            );

            switch (AnimationClipUIMoveMode)
            {
                case AnimationClipUIMoveMode.ChildToParent:
                    AnimationClipMoverChildToParentGUI();
                    break;
                case AnimationClipUIMoveMode.ParentToChild:
                    AnimationClipMoverParentToChildGUI();
                    break;
            }
        }

        private void AnimationClipMoverChildToParentGUI()
        {
            OverwriteObject = (GameObject)EditorGUILayout.ObjectField("Child GameObject (from)", OverwriteObject, typeof(GameObject), true);
            OriginalObject  = (GameObject)EditorGUILayout.ObjectField("Parent GameObject (to)" , OriginalObject , typeof(GameObject), true);
        }

        private void AnimationClipMoverParentToChildGUI()
        {
            OverwriteObject = (GameObject)EditorGUILayout.ObjectField("Parent GameObject (from)", OverwriteObject, typeof(GameObject), true);
            OriginalObject  = (GameObject)EditorGUILayout.ObjectField("Child GameObject (to)"   , OriginalObject , typeof(GameObject), true);
        }

        public void Validate()
        {
            Error = null;
            LastRunningProcess = "Validation";

            AnimationClipMover.Validate
            (
                OverwriteAnimationClip,
                OverwriteObject,
                OriginalObject,
                AnimationClipMoveMode
            );
        }

        public void Generate(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            
            Error = null;
            LastRunningProcess = "Generation";

            AnimationClipMover.Generate
            (
                folderPath,
                OverwriteAnimationClip,
                OverwriteObject,
                OriginalObject,
                AnimationClipMoveMode
            );
        }
    }
}
