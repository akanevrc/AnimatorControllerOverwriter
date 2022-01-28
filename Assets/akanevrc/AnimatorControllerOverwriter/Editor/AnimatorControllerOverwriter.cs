using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace akanevrc.AnimatorControllerOverwriter.Editor
{
    public class SyncedLayerOverwrittenException : Exception
    {
        public readonly string Name;

        public SyncedLayerOverwrittenException(string name)
        {
            Name = name;
        }
    }

    public class LayerConflictException : Exception
    {
        public readonly string Name;

        public LayerConflictException(string name)
        {
            Name = name;
        }
    }

    public class ParameterConflictException : Exception
    {
        public readonly string Name;

        public ParameterConflictException(string name)
        {
            Name = name;
        }
    }

    public enum SameNameLayerMode
    {
        RaiseError,
        DoNotCopy,
        Replace
    }

    public interface IAnimatorControllerOverwriter
    {
        void Validate
        (
            AnimatorController original,
            AnimatorController overwrite,
            SameNameLayerMode sameNameLayerMode,
            string prefixOriginalLayer,
            string prefixOverwriteLayer,
            bool mergeSameParameters,
            GameObject overwriteObject,
            GameObject originalObject,
            AnimationClipMoveMode animationClipMoveMode
        );

        AnimatorController Generate
        (
            string folderPath,
            AnimatorController original,
            AnimatorController overwrite,
            SameNameLayerMode sameNameLayerMode,
            string prefixOriginalLayer,
            string prefixOverwriteLayer,
            bool mergeSameParameters,
            GameObject overwriteObject,
            GameObject originalObject,
            AnimationClipMoveMode animationClipMoveMode
        );
    }

    public class AnimatorControllerOverwriter : IAnimatorControllerOverwriter
    {
        private class UnityEqualityComparer<T> : IEqualityComparer<T>
            where T : UnityEngine.Object
        {
            public bool Equals(T obj1, T obj2)
            {
                return obj1.GetInstanceID() == obj2.GetInstanceID();
            }

            public int GetHashCode(T obj)
            {
                return obj.GetInstanceID().GetHashCode();
            }
        }

        public IAnimationClipMover AnimationClipMover = new AnimationClipMover();

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
            var path = Util.GetControllerWorkFilePath();

            var result = (AnimatorController)null;
            try
            {
                result = Overwrite
                (
                    false,
                    Util.WorkFolderPath,
                    path,
                    original,
                    overwrite,
                    sameNameLayerMode,
                    prefixOfOriginalLayer,
                    prefixOfOverwriteLayer,
                    mergeSameParameters,
                    overwriteObject,
                    originalObject,
                    animationClipMoveMode
                );
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        public AnimatorController Generate
        (
            string folderPath,
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
            var name =
                original  != null ? $"{original .name}_overwritten.controller" :
                overwrite != null ? $"{overwrite.name}_overwritten.controller" : "new.controller";
            var path = Path.Combine(folderPath, name);
            path     = AssetDatabase.GenerateUniqueAssetPath(path);

            var result = (AnimatorController)null;
            try
            {
                result = Overwrite
                (
                    true,
                    folderPath,
                    path,
                    original,
                    overwrite,
                    sameNameLayerMode,
                    prefixOfOriginalLayer,
                    prefixOfOverwriteLayer,
                    mergeSameParameters,
                    overwriteObject,
                    originalObject,
                    animationClipMoveMode
                );
                return result;
            }
            catch
            {
                AssetDatabase.DeleteAsset(path);
                throw;
            }
        }

        private AnimatorController Overwrite
        (
            bool isGenerating,
            string folderPath,
            string path,
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
            var result = AnimatorController.CreateAnimatorControllerAtPath(path);

            var name =
                original  != null ? $"{original .name}_overwritten" :
                overwrite != null ? $"{overwrite.name}_overwritten" : "new";
            result.name = name;
            result.parameters = OverwriteParameters
                (
                    original  == null ? new AnimatorControllerParameter[0] : original .parameters,
                    overwrite == null ? new AnimatorControllerParameter[0] : overwrite.parameters,
                    mergeSameParameters
                );
            result.layers = OverwriteLayers
                (
                    isGenerating,
                    folderPath,
                    original  == null ? new AnimatorControllerLayer[0] : original .layers,
                    overwrite == null ? new AnimatorControllerLayer[0] : overwrite.layers,
                    sameNameLayerMode,
                    prefixOfOriginalLayer,
                    prefixOfOverwriteLayer,
                    overwriteObject,
                    originalObject,
                    animationClipMoveMode
                );

            var visitor = new AssetVisitor(result);
            visitor.Visit();

            EditorUtility.SetDirty(result);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return result;
        }

        private AnimatorControllerParameter[] OverwriteParameters
        (
            AnimatorControllerParameter[] originals,
            AnimatorControllerParameter[] overwrites,
            bool mergeSameParameters
        )
        {
            foreach (var orig in originals)
            {
                foreach (var over in overwrites)
                {
                    if (orig.name == over.name && !(mergeSameParameters && orig.type == over.type))
                        throw new ParameterConflictException(orig.name);
                }
            }

            var newOriginals  = originals .Select(DuplicateParameter).ToArray();
            var newOverwrites = overwrites.Select(DuplicateParameter).ToArray();

            return newOriginals.Concat(newOverwrites.Where(elem => originals.All(el => el.name != elem.name))).ToArray();
        }

        private AnimatorControllerLayer[] OverwriteLayers
        (
            bool isGenerating,
            string folderPath,
            AnimatorControllerLayer[] originals,
            AnimatorControllerLayer[] overwrites,
            SameNameLayerMode sameNameLayerMode,
            string prefixOfOriginalLayer,
            string prefixOfOverwriteLayer,
            GameObject overwriteObject,
            GameObject originalObject,
            AnimationClipMoveMode animationClipMoveMode
        )
        {
            switch (sameNameLayerMode)
            {
                case SameNameLayerMode.RaiseError:
                {
                    var newOriginals  = originals .Select(DuplicateLayer(prefixOfOriginalLayer , 0                  , isGenerating, false, folderPath, overwriteObject, originalObject, animationClipMoveMode)).ToArray();
                    var newOverwrites = overwrites.Select(DuplicateLayer(prefixOfOverwriteLayer, newOriginals.Length, isGenerating, true , folderPath, overwriteObject, originalObject, animationClipMoveMode)).ToArray();
                    foreach (var orig in newOriginals)
                    {
                        foreach (var over in newOverwrites)
                        {
                            if (orig.name == over.name) throw new LayerConflictException(orig.name);
                        }
                    }
                    return newOriginals.Concat(newOverwrites).ToArray();
                }
                case SameNameLayerMode.DoNotCopy:
                {
                    var newOriginals  = originals .Select(DuplicateLayer(prefixOfOriginalLayer , 0, isGenerating, false, folderPath, overwriteObject, originalObject, animationClipMoveMode)).ToArray();
                    var newOverwrites = overwrites.Select(DuplicateLayer(prefixOfOverwriteLayer, 0, isGenerating, true , folderPath, overwriteObject, originalObject, animationClipMoveMode)).ToArray();
                    var nameToOrig    = newOriginals.ToDictionary(elem => elem.name, elem => elem);
                    var isDeleteds    = newOverwrites.Select(elem => nameToOrig.ContainsKey(elem.name)).ToArray();
                    for (var i = 0; i < newOverwrites.Length; i++)
                    {
                        if (isDeleteds[i] && overwrites.Any(elem => elem.syncedLayerIndex == i))
                            throw new SyncedLayerOverwrittenException(newOverwrites[i].name);
                    }
                    var indices = new int[newOverwrites.Length];
                    for (var (i, baseIndex) = (0, newOriginals.Length); i < indices.Length; i++, baseIndex++)
                    {
                        indices[i] = baseIndex;
                        baseIndex -= isDeleteds[i] ? 1 : 0;
                    }
                    for (var i = 0; i < newOverwrites.Length; i++)
                    {
                        if (isDeleteds[i] || newOverwrites[i].syncedLayerIndex == -1) continue;
                        newOverwrites[i].syncedLayerIndex = indices[overwrites[i].syncedLayerIndex];
                    }
                    return newOriginals.Concat(newOverwrites.Where((_, i) => !isDeleteds[i])).ToArray();
                }
                case SameNameLayerMode.Replace:
                {
                    var newOriginals  = originals .Select(DuplicateLayer(prefixOfOriginalLayer , 0, isGenerating, false, folderPath, overwriteObject, originalObject, animationClipMoveMode)).ToArray();
                    var newOverwrites = overwrites.Select(DuplicateLayer(prefixOfOverwriteLayer, 0, isGenerating, true , folderPath, overwriteObject, originalObject, animationClipMoveMode)).ToArray();
                    var nameToOrig    = newOriginals .Select((elem, i) => (elem, i)).ToDictionary(ei => ei.elem.name, ei => ei);
                    var nameToOver    = newOverwrites.Select((elem, i) => (elem, i)).ToDictionary(ei => ei.elem.name, ei => ei);
                    var isDeleteds    = newOriginals .Select(elem => nameToOver.ContainsKey(elem.name)).ToArray();
                    var isReplacings  = newOverwrites.Select(elem => nameToOrig.ContainsKey(elem.name)).ToArray();
                    for (var i = 0; i < newOriginals.Length; i++)
                    {
                        if (isDeleteds[i] && originals.Any(elem => elem.syncedLayerIndex == i))
                            throw new SyncedLayerOverwrittenException(newOriginals[i].name);
                    }
                    var indices = new int[newOverwrites.Length];
                    for (var (i, baseIndex) = (0, newOriginals.Length); i < indices.Length; i++, baseIndex++)
                    {
                        indices[i] = isReplacings[i] ? nameToOrig[newOverwrites[i].name].i : baseIndex;
                        baseIndex -= isReplacings[i] ? 1 : 0;
                    }
                    for (var i = 0; i < newOverwrites.Length; i++)
                    {
                        if (newOverwrites[i].syncedLayerIndex == -1) continue;
                        newOverwrites[i].syncedLayerIndex = indices[overwrites[i].syncedLayerIndex];
                    }
                    return newOriginals
                        .Select((elem, i) => isDeleteds[i] ? newOverwrites[nameToOver[elem.name].i] : elem)
                        .Concat(newOverwrites.Where((_, i) => !isReplacings[i]))
                        .ToArray();
                }
                default:
                    throw new ArgumentException(nameof(sameNameLayerMode));
            }
        }

        private AnimatorControllerParameter DuplicateParameter(AnimatorControllerParameter src)
        {
            if (src == null) return null;

            var result = new AnimatorControllerParameter();

            result.defaultBool  = src.defaultBool;
            result.defaultFloat = src.defaultFloat;
            result.defaultInt   = src.defaultInt;
            result.name         = src.name;
            result.type         = src.type;

            return result;
        }

        private Func<AnimatorControllerLayer, AnimatorControllerLayer> DuplicateLayer
        (
            string prefix,
            int baseIndex,
            bool isGenerating,
            bool isOverwrite,
            string folderPath,
            GameObject overwriteObject,
            GameObject originalObject,
            AnimationClipMoveMode animationClipMoveMode

        ) =>
        (AnimatorControllerLayer src) =>
        {
            if (src == null) return null;

            var result = new AnimatorControllerLayer();

            result.avatarMask               = src.avatarMask;
            result.blendingMode             = src.blendingMode;
            result.defaultWeight            = src.defaultWeight;
            result.iKPass                   = src.iKPass;
            result.name                     = prefix + src.name;
            result.syncedLayerAffectsTiming = src.syncedLayerAffectsTiming;
            result.syncedLayerIndex         = src.syncedLayerIndex == -1 ? -1 : src.syncedLayerIndex + baseIndex;

            var func = new DuplicateStateMachineFunc
            (
                this,
                isGenerating,
                isOverwrite,
                folderPath,
                overwriteObject,
                originalObject,
                animationClipMoveMode
            );

            result.stateMachine = src.stateMachine == null ? null : func.Invoke(src.stateMachine);

            return result;
        };

        private class DuplicateStateMachineFunc
        {
            private readonly AnimatorControllerOverwriter Parent;
            private readonly bool IsGenerating;
            private readonly bool IsOverwrite;
            private readonly IAnimationClipMover Mover;
            private readonly string FolderPath;
            private readonly GameObject OverwriteObject;
            private readonly GameObject OriginalObject;
            private readonly AnimationClipMoveMode AnimationClipMoveMode;
            private readonly HashSet<AnimatorStateMachine> Exists;
            private readonly Dictionary<AnimatorState, AnimatorState> OldToNewState;
            private readonly Dictionary<AnimatorStateMachine, AnimatorStateMachine> OldToNewStateMachine;

            public DuplicateStateMachineFunc
            (
                AnimatorControllerOverwriter parent,
                bool isGenerating,
                bool isOverwrite,
                string folderPath,
                GameObject overwriteObject,
                GameObject originalObject,
                AnimationClipMoveMode animationClipMoveMode
            )
            {
                Parent                = parent;
                IsGenerating          = isGenerating;
                IsOverwrite           = isOverwrite;
                FolderPath            = folderPath;
                OverwriteObject       = overwriteObject;
                OriginalObject        = originalObject;
                AnimationClipMoveMode = animationClipMoveMode;
                Exists                = new HashSet   <AnimatorStateMachine                      >(new UnityEqualityComparer<AnimatorStateMachine>());
                OldToNewState         = new Dictionary<AnimatorState       , AnimatorState       >(new UnityEqualityComparer<AnimatorState>());
                OldToNewStateMachine  = new Dictionary<AnimatorStateMachine, AnimatorStateMachine>(new UnityEqualityComparer<AnimatorStateMachine>());
            }

            public AnimatorStateMachine Invoke(AnimatorStateMachine src)
            {
                var result = DuplicateStateMachine(src);

                SetTransitions (result, src);
                SetDefaultState(result, src);

                return result;
            }

            private AnimatorStateMachine DuplicateStateMachine(AnimatorStateMachine src)
            {
                if (src == null) return null;

                if (Exists.Contains(src)) throw new ArgumentException("AnimatorStateMachine is structured recursively.", nameof(src));
                Exists.Add(src);

                var result = new AnimatorStateMachine();

                OldToNewStateMachine.Add(src, result);

                result.anyStatePosition           = src.anyStatePosition;
                result.behaviours                 = src.behaviours;
                result.entryPosition              = src.entryPosition;
                result.exitPosition               = src.exitPosition;
                result.hideFlags                  = src.hideFlags;
                result.name                       = src.name;
                result.parentStateMachinePosition = src.parentStateMachinePosition;

                result.anyStateTransitions = new AnimatorStateTransition[0];
                result.entryTransitions    = new AnimatorTransition[0];

                result.stateMachines = src.stateMachines.Select(DuplicateChildStateMachine).ToArray();
                result.states        = src.states       .Select(DuplicateChildState       ).ToArray();

                Exists.Remove(src);
                return result;
            }

            private ChildAnimatorStateMachine DuplicateChildStateMachine(ChildAnimatorStateMachine src)
            {
                var result = new ChildAnimatorStateMachine();

                result.position     = src.position;
                result.stateMachine = src.stateMachine == null ? null : DuplicateStateMachine(src.stateMachine);

                return result;
            }

            private ChildAnimatorState DuplicateChildState(ChildAnimatorState src)
            {
                var result = new ChildAnimatorState();

                result.position = src.position;
                result.state    = src.state == null ? null : DuplicateState(src.state);

                return result;
            }

            private AnimatorState DuplicateState(AnimatorState src)
            {
                if (src == null) return null;

                var result = new AnimatorState();

                OldToNewState.Add(src, result);

                result.behaviours                 = src.behaviours;
                result.cycleOffset                = src.cycleOffset;
                result.cycleOffsetParameter       = src.cycleOffsetParameter;
                result.cycleOffsetParameterActive = src.cycleOffsetParameterActive;
                result.hideFlags                  = src.hideFlags;
                result.iKOnFeet                   = src.iKOnFeet;
                result.mirror                     = src.mirror;
                result.mirrorParameter            = src.mirrorParameter;
                result.mirrorParameterActive      = src.mirrorParameterActive;
                result.name                       = src.name;
                result.speed                      = src.speed;
                result.speedParameter             = src.speedParameter;
                result.speedParameterActive       = src.speedParameterActive;
                result.tag                        = src.tag;
                result.timeParameter              = src.timeParameter;
                result.timeParameterActive        = src.timeParameterActive;
                result.writeDefaultValues         = src.writeDefaultValues;

                result.transitions = new AnimatorStateTransition[0];

                result.motion = new DuplicateMotionFunc
                (
                    Parent,
                    IsGenerating,
                    IsOverwrite,
                    FolderPath,
                    OverwriteObject,
                    OriginalObject,
                    AnimationClipMoveMode
                ).Invoke(src.motion);

                return result;
            }

            private AnimatorStateTransition DuplicateStateTransition(AnimatorStateTransition src)
            {
                if (src == null) return null;

                var result = new AnimatorStateTransition();

                result.canTransitionToSelf = src.canTransitionToSelf;
                result.duration            = src.duration;
                result.exitTime            = src.exitTime;
                result.hasExitTime         = src.hasExitTime;
                result.hasFixedDuration    = src.hasFixedDuration;
                result.hideFlags           = src.hideFlags;
                result.interruptionSource  = src.interruptionSource;
                result.isExit              = src.isExit;
                result.mute                = src.mute;
                result.name                = src.name;
                result.offset              = src.offset;
                result.orderedInterruption = src.orderedInterruption;
                result.solo                = src.solo;

                result.conditions              = src.conditions.Select(Parent.DuplicateCondition).ToArray();
                result.destinationState        = src.destinationState        == null ? null : OldToNewState       [src.destinationState];
                result.destinationStateMachine = src.destinationStateMachine == null ? null : OldToNewStateMachine[src.destinationStateMachine];

                return result;
            }

            private AnimatorTransition DuplicateTransition(AnimatorTransition src)
            {
                if (src == null) return null;

                var result = new AnimatorTransition();

                result.conditions = src.conditions.Select(Parent.DuplicateCondition).ToArray();
                result.hideFlags  = src.hideFlags;
                result.mute       = src.mute;
                result.name       = src.name;
                result.solo       = src.solo;

                result.destinationState        = src.destinationState        == null ? null : OldToNewState       [src.destinationState];
                result.destinationStateMachine = src.destinationStateMachine == null ? null : OldToNewStateMachine[src.destinationStateMachine];

                return result;
            }

            public void SetTransitions(AnimatorStateMachine result, AnimatorStateMachine src)
            {
                result.anyStateTransitions = src.anyStateTransitions.Select(DuplicateStateTransition).ToArray();
                result.entryTransitions    = src.entryTransitions   .Select(DuplicateTransition     ).ToArray();

                for (var i = 0; i < result.states.Length; i++)
                {
                    result.states[i].state.transitions = src.states[i].state.transitions.Select(DuplicateStateTransition).ToArray();
                }

                for (var i = 0; i < result.stateMachines.Length; i++)
                {
                    SetTransitions(result.stateMachines[i].stateMachine, src.stateMachines[i].stateMachine);
                }
            }

            public void SetDefaultState(AnimatorStateMachine result, AnimatorStateMachine src)
            {
                result.defaultState = src.defaultState == null ? null : OldToNewState[src.defaultState];
            }
        }
        
        private class DuplicateMotionFunc
        {
            private readonly AnimatorControllerOverwriter Parent;
            private readonly bool IsGenerating;
            private readonly bool IsOverwrite;
            private readonly string FolderPath;
            private readonly GameObject OverwriteObject;
            private readonly GameObject OriginalObject;
            private readonly AnimationClipMoveMode AnimationClipMoveMode;
            private readonly HashSet<Motion> Exists;

            public DuplicateMotionFunc
            (
                AnimatorControllerOverwriter parent,
                bool isGenerating,
                bool isOverwrite,
                string folderPath,
                GameObject overwriteObject,
                GameObject originalObject,
                AnimationClipMoveMode animationClipMoveMode
            )
            {
                Parent                = parent;
                IsGenerating          = isGenerating;
                IsOverwrite           = isOverwrite;
                FolderPath            = folderPath;
                OverwriteObject       = overwriteObject;
                OriginalObject        = originalObject;
                AnimationClipMoveMode = animationClipMoveMode;
                Exists                = new HashSet<Motion>(new UnityEqualityComparer<Motion>());
            }

            public Motion Invoke(Motion src)
            {
                if (src == null) return null;
                if (src is AnimationClip clip) return DuplicateAnimationClip(clip);

                if (Exists.Contains(src)) throw new ArgumentException("Motion is structured recursively.", nameof(src));
                Exists.Add(src);

                var blendTree = src as BlendTree;
                if (blendTree == null) throw new NotSupportedException($"{src.GetType().Name} is not supported as Moition.");

                var result = DuplicateBlendTree(blendTree);

                Exists.Remove(src);
                return result;
            }

            private AnimationClip DuplicateAnimationClip(AnimationClip src)
            {
                if (src == null) return null;

                if (!IsOverwrite) return src;

                var result = (AnimationClip)null;
                if (IsGenerating)
                {
                    result = Parent.AnimationClipMover.Generate(FolderPath, src, OverwriteObject, OriginalObject, AnimationClipMoveMode);
                }
                else
                {
                    Parent.AnimationClipMover.Validate(src, OverwriteObject, OriginalObject, AnimationClipMoveMode);
                    result = src;
                }

                return result;
            }

            private BlendTree DuplicateBlendTree(BlendTree src)
            {
                if (src == null) return null;

                var result = new BlendTree();

                result.blendParameter         = src.blendParameter;
                result.blendParameterY        = src.blendParameterY;
                result.blendType              = src.blendType;
                result.hideFlags              = src.hideFlags;
                result.maxThreshold           = src.maxThreshold;
                result.minThreshold           = src.minThreshold;
                result.name                   = src.name;
                result.useAutomaticThresholds = src.useAutomaticThresholds;

                result.children = src.children.Select(DuplicateChildMotion).ToArray();

                return result;
            }

            private ChildMotion DuplicateChildMotion(ChildMotion src)
            {
                var result = new ChildMotion();

                result.cycleOffset          = src.cycleOffset;
                result.directBlendParameter = src.directBlendParameter;
                result.mirror               = src.mirror;
                result.position             = src.position;
                result.threshold            = src.threshold;
                result.timeScale            = src.timeScale;

                result.motion = Invoke(src.motion);

                return result;
            }
        }

        private AnimatorCondition DuplicateCondition(AnimatorCondition src)
        {
            var result = new AnimatorCondition();

            result.mode      = src.mode;
            result.parameter = src.parameter;
            result.threshold = src.threshold;

            return result;
        }

        private class AssetVisitor
        {
            private readonly AnimatorController Controller;
            private readonly string Path;

            public AssetVisitor(AnimatorController controller)
            {
                Controller = controller;
                Path       = AssetDatabase.GetAssetPath(Controller);
            }

            public void Visit()
            {
                foreach (var x in Controller.layers) VisitStateMachine(x.stateMachine);
            }

            private void VisitStateMachine(AnimatorStateMachine src)
            {
                if (src == null) return;
                AddSubAsset(src);
                foreach (var x in src.stateMachines      ) VisitStateMachine   (x.stateMachine);
                foreach (var x in src.states             ) VisitState          (x.state);
                foreach (var x in src.anyStateTransitions) VisitStateTransition(x);
                foreach (var x in src.entryTransitions   ) VisitTransition     (x);
            }

            private void VisitState(AnimatorState src)
            {
                if (src == null) return;
                AddSubAsset(src);
                foreach (var x in src.transitions) VisitStateTransition(x);
                VisitMotion(src.motion);
            }

            private void VisitStateTransition(AnimatorStateTransition src)
            {
                if (src == null) return;
                AddSubAsset(src);
            }

            private void VisitTransition(AnimatorTransition src)
            {
                if (src == null) return;
                AddSubAsset(src);
            }

            private void VisitMotion(Motion src)
            {
                if (src == null) return;
                AddSubAsset(src);
                if (src is BlendTree tree) VisitBlendTree(tree);
            }

            private void VisitBlendTree(BlendTree src)
            {
                foreach (var x in src.children) VisitMotion(x.motion);
            }

            private void AddSubAsset(UnityEngine.Object obj)
            {
                EditorUtility.SetDirty(obj);
                if (AssetDatabase.GetAssetPath(obj).Length == 0)
                {
                    obj.hideFlags = HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(obj, Path);
                }
            }
        }
    }
}
