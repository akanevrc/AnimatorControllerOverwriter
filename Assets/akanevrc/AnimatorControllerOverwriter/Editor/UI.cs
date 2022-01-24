using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace akanevrc.AnimatorControllerOverwriter.Editor
{
    public class UI : EditorWindow
    {
        public IAnimatorControllerOverwriter Overwriter = new AnimatorControllerOverwriter();
        
        public AnimatorController OriginalAnimatorController = null;
        public AnimatorController OverwriteAnimatorController = null;
        public SameNameLayerMode SameNameLayerMode = SameNameLayerMode.RaiseError;
        public string PrefixOfOriginalLayer = "";
        public string PrefixOfOverwriteLayer = "";
        public bool MergeSameParameters = false;
        public Exception Error = null;
        public string LastRunningProcess = "";

        private Texture2D InfoIcon { get; set; }
        private Texture2D WarnIcon { get; set; }
        private Texture2D ErrorIcon { get; set; }

        private bool ButtonsEnabled =>  OriginalAnimatorController != null && OverwriteAnimatorController != null;

        private void OnEnable()
        {
            InfoIcon  = LoadBuiltInIcon("console.infoicon");
            WarnIcon  = LoadBuiltInIcon("console.warnicon");
            ErrorIcon = LoadBuiltInIcon("console.erroricon");
        }

        private Texture2D LoadBuiltInIcon(string name)
        {
            return Resources.FindObjectsOfTypeAll<Texture2D>()
                .Where(x => AssetDatabase.GetAssetPath(x) == "Library/unity editor resources")
                .FirstOrDefault(x => x.name == name);
        }

        [MenuItem("Tools/Overwirte AnimatorController...")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<UI>().Show();
        }

        private void OnGUI()
        {
            EditorGUIUtility.labelWidth = 200.0F;

            GUILayout.Label("Overwrite AnimatorControllers", new GUIStyle(EditorStyles.largeLabel));

            EditorGUILayout.Space();

            OriginalAnimatorController =
                (AnimatorController)EditorGUILayout.ObjectField
                (
                    "Base AnimatorController",
                    OriginalAnimatorController,
                    typeof(AnimatorController),
                    false
                );
            OverwriteAnimatorController =
                (AnimatorController)EditorGUILayout.ObjectField
                (
                    "Overwriting AnimatorController",
                    OverwriteAnimatorController,
                    typeof(AnimatorController),
                    false
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
                var path = EditorUtility.SaveFilePanelInProject
                (
                    "Save AnimatorController",
                    $"{OriginalAnimatorController.name}_overwritten.controller",
                    "controller",
                    "Enter a name of new AnimatorController."
                );

                try
                {
                    Generate(path);
                }
                catch (Exception ex)
                {
                    Error = ex;
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
                MergeSameParameters
            );
        }

        public void Generate(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            
            Error = null;
            LastRunningProcess = "Generation";

            Overwriter.Generate
            (
                path,
                OriginalAnimatorController,
                OverwriteAnimatorController,
                SameNameLayerMode,
                PrefixOfOriginalLayer,
                PrefixOfOverwriteLayer,
                MergeSameParameters
            );
        }
    }
}
