using NUnit.Framework;
using UnityEngine;

namespace akanevrc.AnimatorControllerOverwriter.Editor.Tests
{
    public class AnimationClipMoverUITest
    {
        private class MockMover : IAnimationClipMover
        {
            private readonly AnimationClipMoverUI UI;

            public MockMover(AnimationClipMoverUI ui)
            {
                UI = ui;
            }

            public void Validate
            (
                AnimationClip clip,
                GameObject overwrite,
                GameObject original,
                AnimationClipMoveMode mode
            )
            {
                Assert.That(clip     , Is.SameAs (UI.OverwriteAnimationClip));
                Assert.That(overwrite, Is.SameAs (UI.OverwriteObject));
                Assert.That(original , Is.SameAs (UI.OriginalObject));
                Assert.That(mode     , Is.EqualTo(UI.AnimationClipMoveMode));
            }

            public AnimationClip Generate
            (
                string path,
                AnimationClip clip,
                GameObject overwrite,
                GameObject original,
                AnimationClipMoveMode mode
            )
            {
                Assert.That(clip     , Is.SameAs (UI.OverwriteAnimationClip));
                Assert.That(overwrite, Is.SameAs (UI.OverwriteObject));
                Assert.That(original , Is.SameAs (UI.OriginalObject));
                Assert.That(mode     , Is.EqualTo(UI.AnimationClipMoveMode));
                return null;
            }
        }

        private AnimationClipMoverUI UI;

        [SetUp]
        public void Init()
        {
            UI = ScriptableObject.CreateInstance<AnimationClipMoverUI>();

            UI.AnimationClipMover      = new MockMover(UI);
            UI.OverwriteAnimationClip  = new AnimationClip();
            UI.AnimationClipUIMoveMode = AnimationClipUIMoveMode.ChildToParent;
            UI.AnimationClipMoveMode   = AnimationClipMoveMode  .ChildToParent;
            UI.OriginalObject          = new GameObject();
            UI.OverwriteObject         = new GameObject();
        }

        [TearDown]
        public void Cleanup()
        {
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
