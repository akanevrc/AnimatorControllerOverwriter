using UnityEditor;

namespace akanevrc.AnimatorControllerOverwriter.Editor
{
    public static class Util
    {
        private static readonly string WorkFilePath = "Assets/akanevrc/AnimatorControllerOverwriter/Editor/Work/tmp.controller";

        public static string GetWorkFilePath()
        {
            return AssetDatabase.GenerateUniqueAssetPath(WorkFilePath);
        }
    }
}
