using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace akanevrc.AnimatorControllerOverwriter.Editor.Tests
{
    public class UITest
    {
        private class MockOverwriter : IOverwriter
        {
            private UI UI { get; }

            public MockOverwriter(UI ui)
            {
                UI = ui;
            }

            public void Validate
            (
                AnimatorController original,
                AnimatorController overwrite,
                string prefixOfOriginalLayer,
                string prefixOfOverwriteLayer,
                bool mergeSameParameters
            )
            {
                Assert.That(original , Is.SameAs(UI.OriginalAnimatorController));
                Assert.That(overwrite, Is.SameAs(UI.OverwriteAnimatorController));
                Assert.That(prefixOfOriginalLayer , Is.EqualTo(UI.PrefixOfOriginalLayer));
                Assert.That(prefixOfOverwriteLayer, Is.EqualTo(UI.PrefixOfOverwriteLayer));
                Assert.That(mergeSameParameters   , Is.EqualTo(UI.MergeSameParameters));
            }

            public AnimatorController Generate
            (
                string path,
                AnimatorController original,
                AnimatorController overwrite,
                string prefixOfOriginalLayer,
                string prefixOfOverwriteLayer,
                bool mergeSameParameters
            )
            {
                Assert.That(original , Is.SameAs(UI.OriginalAnimatorController));
                Assert.That(overwrite, Is.SameAs(UI.OverwriteAnimatorController));
                Assert.That(prefixOfOriginalLayer , Is.EqualTo(UI.PrefixOfOriginalLayer));
                Assert.That(prefixOfOverwriteLayer, Is.EqualTo(UI.PrefixOfOverwriteLayer));
                Assert.That(mergeSameParameters   , Is.EqualTo(UI.MergeSameParameters));
                return null;
            }
        }

        private UI UI { get; set; }

        [SetUp]
        public void Init()
        {
            UI = ScriptableObject.CreateInstance<UI>();
            
            UI.Overwriter                  = new MockOverwriter(UI);
            UI.OriginalAnimatorController  = AnimatorController.CreateAnimatorControllerAtPath($"Assets/{GUID.Generate()}.controller");
            UI.OverwriteAnimatorController = AnimatorController.CreateAnimatorControllerAtPath($"Assets/{GUID.Generate()}.controller");
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
