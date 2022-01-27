using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace akanevrc.AnimatorControllerOverwriter.Editor.Tests
{
    public class AnimatorControllerOverwriterUITest
    {
        private class MockOverwriter : IAnimatorControllerOverwriter
        {
            private readonly AnimatorControllerOverwriterUI UI;

            public MockOverwriter(AnimatorControllerOverwriterUI ui)
            {
                UI = ui;
            }

            public void Validate
            (
                AnimatorController original,
                AnimatorController overwrite,
                SameNameLayerMode sameNameLayerMode,
                string prefixOfOriginalLayer,
                string prefixOfOverwriteLayer,
                bool mergeSameParameters,
                GameObject overwriteObject,
                GameObject originalObject,
                AnimationClipMoveMode animationClipMoveMode
            )
            {
                Assert.That(original              , Is.SameAs (UI.OriginalAnimatorController));
                Assert.That(overwrite             , Is.SameAs (UI.OverwriteAnimatorController));
                Assert.That(sameNameLayerMode     , Is.EqualTo(UI.SameNameLayerMode));
                Assert.That(prefixOfOriginalLayer , Is.EqualTo(UI.PrefixOfOriginalLayer));
                Assert.That(prefixOfOverwriteLayer, Is.EqualTo(UI.PrefixOfOverwriteLayer));
                Assert.That(mergeSameParameters   , Is.EqualTo(UI.MergeSameParameters));
                Assert.That(overwriteObject       , Is.EqualTo(UI.OverwriteObject));
                Assert.That(originalObject        , Is.EqualTo(UI.OriginalObject));
                Assert.That(animationClipMoveMode , Is.EqualTo(UI.AnimationClipMoveMode));
            }

            public AnimatorController Generate
            (
                string path,
                AnimatorController original,
                AnimatorController overwrite,
                SameNameLayerMode mode,
                string prefixOfOriginalLayer,
                string prefixOfOverwriteLayer,
                bool mergeSameParameters,
                GameObject overwriteObject,
                GameObject originalObject,
                AnimationClipMoveMode animationClipMoveMode
            )
            {
                Assert.That(original              , Is.SameAs (UI.OriginalAnimatorController));
                Assert.That(overwrite             , Is.SameAs (UI.OverwriteAnimatorController));
                Assert.That(mode                  , Is.EqualTo(UI.SameNameLayerMode));
                Assert.That(prefixOfOriginalLayer , Is.EqualTo(UI.PrefixOfOriginalLayer));
                Assert.That(prefixOfOverwriteLayer, Is.EqualTo(UI.PrefixOfOverwriteLayer));
                Assert.That(mergeSameParameters   , Is.EqualTo(UI.MergeSameParameters));
                Assert.That(overwriteObject       , Is.EqualTo(UI.OverwriteObject));
                Assert.That(originalObject        , Is.EqualTo(UI.OriginalObject));
                Assert.That(animationClipMoveMode , Is.EqualTo(UI.AnimationClipMoveMode));
                return null;
            }
        }

        private AnimatorControllerOverwriterUI UI;

        [SetUp]
        public void Init()
        {
            UI = ScriptableObject.CreateInstance<AnimatorControllerOverwriterUI>();
            
            UI.Overwriter                  = new MockOverwriter(UI);
            UI.OriginalAnimatorController  = AnimatorController.CreateAnimatorControllerAtPath(TestUtil.GetControllerWorkFilePath());
            UI.OverwriteAnimatorController = AnimatorController.CreateAnimatorControllerAtPath(TestUtil.GetControllerWorkFilePath());
            UI.SameNameLayerMode           = SameNameLayerMode.Replace;
            UI.PrefixOfOriginalLayer       = "[Original]";
            UI.PrefixOfOverwriteLayer      = "[Overwrite]";
            UI.MergeSameParameters         = true;
        }

        [TearDown]
        public void Cleanup()
        {
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(UI.OriginalAnimatorController));
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(UI.OverwriteAnimatorController));
        }

        [Test]
        public void ValidateToBeCalledSuccessfully()
        {
            UI.Validate();
        }

        [Test]
        public void GenerateToBeCalledSuccessfully()
        {
            UI.Generate("");
        }
    }
}
