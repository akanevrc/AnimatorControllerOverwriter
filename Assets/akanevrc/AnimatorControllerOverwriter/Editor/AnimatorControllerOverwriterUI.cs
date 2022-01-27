using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace akanevrc.AnimatorControllerOverwriter.Editor
{
    public class AnimatorControllerOverwriterUI : EditorWindow
    {
        public IAnimatorControllerOverwriter Overwriter = new AnimatorControllerOverwriter();
        public IAnimationClipMover AnimationClipMover = new AnimationClipMover();
        
        public AnimatorController OriginalAnimatorController = null;
        public AnimatorController OverwriteAnimatorController = null;
        public SameNameLayerMode SameNameLayerMode = SameNameLayerMode.RaiseError;
        public string PrefixOfOriginalLayer = "";
        public string PrefixOfOverwriteLayer = "";
        public bool MergeSameParameters = false;

        public AnimationClipMoveMode AnimationClipMoveMode = AnimationClipMoveMode.Disable;
        public GameObject OriginalObject = null;
        public GameObject OverwriteObject = null;

        public Exception Error = null;
        public string LastRunningProcess = "";
        public Vector2 ScrollPosition = new Vector2(0F, 0F);

        private Texture2D InfoIcon;
        private Texture2D WarnIcon;
        private Texture2D ErrorIcon;

        private bool ButtonsEnabled => OriginalAnimatorController != null || OverwriteAnimatorController != null;

        private void OnEnable()
        {
            InfoIcon  = Util.LoadBuiltInIcon("console.infoicon");
            WarnIcon  = Util.LoadBuiltInIcon("console.warnicon");
            ErrorIcon = Util.LoadBuiltInIcon("console.erroricon");
        }

        [MenuItem("Tools/Overwirte AnimatorController...")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<AnimatorControllerOverwriterUI>().Show();
        }

        private void OnGUI()
        {
            ScrollPosition = EditorGUILayout.BeginScrollView(ScrollPosition);

            EditorGUIUtility.labelWidth = 200.0F;

            GUILayout.Label("Overwrite AnimatorControllers", new GUIStyle(EditorStyles.largeLabel));

            EditorGUILayout.Space();

            OriginalAnimatorController =
                (AnimatorController)EditorGUILayout.ObjectField
                (
                    "Base AnimatorController",
                    OriginalAnimatorController,
                    typeof(AnimatorController),
                    true
                );
            OverwriteAnimatorController =
                (AnimatorController)EditorGUILayout.ObjectField
                (
                    "Overwriting AnimatorController",
                    OverwriteAnimatorController,
                    typeof(AnimatorController),
                    true
                );

            EditorGUILayout.Space();

            SameNameLayerMode = (SameNameLayerMode)EditorGUILayout.EnumPopup("Same Name Layer", SameNameLayerMode);

            GUILayout.Box
            (
                new GUIContent
                (
                    "This selection will be applied if same name layers exist between base and overwriting.\n" +
                    "Raise Error : Error will occur when layer names conflict.\n" +
                    "Do Not Copy : Overwriting layer will not be copied into generated assets.\n" +
                    "Replace : Overwriting layer will be copied into generated assets, and conflicted base layer will not be copied.",
                    InfoIcon
                ),
                new GUIStyle(EditorStyles.helpBox)
            );

            EditorGUILayout.Space();

            GUILayout.Label("Name prefixes");
            PrefixOfOriginalLayer  = EditorGUILayout.TextField("Base layer"       , PrefixOfOriginalLayer);
            PrefixOfOverwriteLayer = EditorGUILayout.TextField("Overwriting layer", PrefixOfOverwriteLayer);

            GUILayout.Box
            (
                new GUIContent
                (
                    "Name prefix will be added to head of object names.\n" +
                    "For example, base layer prefix is '[Base]', and name of layer in base asset is 'Layer1', " +
                    "then name of corresponding layers are '[Base]Layer1' in generated asset.",
                    InfoIcon
                ),
                new GUIStyle(EditorStyles.helpBox)
            );

            EditorGUILayout.Space();

            MergeSameParameters = GUILayout.Toggle(MergeSameParameters, "Merge same parameters");

            EditorGUILayout.Space();
            
            EditorGUI.indentLevel++;
            AnimationClipMoverGUI();
            EditorGUI.indentLevel--;

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

            if (GUILayout.Button("Generate AnimatorController"))
            {
                var folderPath = EditorUtility.OpenFolderPanel
                (
                    "Select AnimatorController Save Folder",
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

            if (Error is SyncedLayerOverwrittenException soex)
            {
                GUILayout.Box
                (
                    new GUIContent
                    (
                        $"Layer '{soex.Name}' cannot be refered as synced layer because the layer will not be copied into output asset.",
                        ErrorIcon
                    ),
                    new GUIStyle(EditorStyles.helpBox)
                );
            }
            else if (Error is LayerConflictException lcex)
            {
                GUILayout.Box
                (
                    new GUIContent
                    (
                        $"Layer '{lcex.Name}' conflicts which are same names on base and overwriting.",
                        ErrorIcon
                    ),
                    new GUIStyle(EditorStyles.helpBox)
                );
            }
            else if (Error is ParameterConflictException pcex)
            {
                GUILayout.Box
                (
                    new GUIContent
                    (
                        MergeSameParameters ?
                            $"Parameter '{pcex.Name}' conflicts which are same names but different types on base and overwriting." :
                            $"Parameter '{pcex.Name}' conflicts which are same names on base and overwriting.",
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
                        $"Fail to copy AnimatorController.",
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
            EditorGUILayout.LabelField("Move AnimationClips' hierarchy", new GUIStyle(EditorStyles.largeLabel));

            AnimationClipMoveMode = (AnimationClipMoveMode)EditorGUILayout.EnumPopup("AnimationClip Move Mode", AnimationClipMoveMode);

            GUILayout.Box
            (
                new GUIContent
                (
                    "This selection is the movement behaviour mode of AnimationClips' hierarchy.\n" +
                    "Disable : Do not move AnimationClips' hierarchy.\n" +
                    "Child To Parent : Child overwriting AnimationClips will be copied to parent base GameObject hierarchy.\n" +
                    "Parent To Child : Parent overwriting AnimationClips will be copied to child base GameObject hierarchy.",
                    InfoIcon
                ),
                new GUIStyle(EditorStyles.helpBox)
            );

            switch (AnimationClipMoveMode)
            {
                case AnimationClipMoveMode.Disable:
                    AnimationClipMoverDisableGUI();
                    break;
                case AnimationClipMoveMode.ChildToParent:
                    AnimationClipMoverChildToParentGUI();
                    break;
                case AnimationClipMoveMode.ParentToChild:
                    AnimationClipMoverParentToChildGUI();
                    break;
            }
        }

        private void AnimationClipMoverDisableGUI()
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("GameObject (from)", OverwriteObject, typeof(GameObject), true);
            EditorGUILayout.ObjectField("GameObject (to)"  , OriginalObject , typeof(GameObject), true);
            EditorGUI.EndDisabledGroup();
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

            Overwriter.Validate
            (
                OriginalAnimatorController,
                OverwriteAnimatorController,
                SameNameLayerMode,
                PrefixOfOriginalLayer,
                PrefixOfOverwriteLayer,
                MergeSameParameters,
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

            Overwriter.Generate
            (
                folderPath,
                OriginalAnimatorController,
                OverwriteAnimatorController,
                SameNameLayerMode,
                PrefixOfOriginalLayer,
                PrefixOfOverwriteLayer,
                MergeSameParameters,
                OverwriteObject,
                OriginalObject,
                AnimationClipMoveMode
            );
        }
    }
}
