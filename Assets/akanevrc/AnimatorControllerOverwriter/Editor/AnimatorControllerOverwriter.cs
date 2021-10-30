using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace akanevrc.AnimatorControllerOverwriter.Editor
{
    public class SyncedLayerOverwritedException : Exception
    {
        public string Name { get; }

        public SyncedLayerOverwritedException(string name)
        {
            Name = name;
        }
    }

    public class LayerConflictException : Exception
    {
        public string Name { get; }

        public LayerConflictException(string name)
        {
            Name = name;
        }
    }

    public class ParameterConflictException : Exception
    {
        public string Name { get; }

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

    public interface IOverwriter
    {
        void Validate
        (
            AnimatorController original,
            AnimatorController overwrite,
            SameNameLayerMode mode,
            string prefixOriginalLayer,
            string prefixOverwriteLayer,
            bool mergeSameParameters
        );

        AnimatorController Generate
        (
            string path,
            AnimatorController original,
            AnimatorController overwrite,
            SameNameLayerMode mode,
            string prefixOriginalLayer,
            string prefixOverwriteLayer,
            bool mergeSameParameters
        );
    }

    public class Overwriter : IOverwriter
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

        public void Validate
        (
            AnimatorController original,
            AnimatorController overwrite,
            SameNameLayerMode mode,
            string prefixOfOriginalLayer,
            string prefixOfOverwriteLayer,
            bool mergeSameParameters
        )
        {
            var result = AnimatorController.CreateAnimatorControllerAtPath($"Assets/{GUID.Generate()}.controller");

            try
            {
                Overwrite
                (
                    result,
                    original,
                    overwrite,
                    mode,
                    prefixOfOriginalLayer,
                    prefixOfOverwriteLayer,
                    mergeSameParameters
                );
            }
            finally
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(result));
            }
        }

        public AnimatorController Generate
        (
            string path,
            AnimatorController original,
            AnimatorController overwrite,
            SameNameLayerMode mode,
            string prefixOfOriginalLayer,
            string prefixOfOverwriteLayer,
            bool mergeSameParameters
        )
        {
            var result = AnimatorController.CreateAnimatorControllerAtPath(path);

            try
            {
                Overwrite
                (
                    result,
                    original,
                    overwrite,
                    mode,
                    prefixOfOriginalLayer,
                    prefixOfOverwriteLayer,
                    mergeSameParameters
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

        private void Overwrite
        (
            AnimatorController result,
            AnimatorController original,
            AnimatorController overwrite,
            SameNameLayerMode mode,
            string prefixOfOriginalLayer,
            string prefixOfOverwriteLayer,
            bool mergeSameParameters
        )
        {
            result.name   = $"{original.name}_overwrited";
            result.parameters = OverwriteParameters
                (
                    original .parameters,
                    overwrite.parameters,
                    mergeSameParameters
                );
            result.layers = OverwriteLayers
                (
                    original .layers,
                    overwrite.layers,
                    mode,
                    prefixOfOriginalLayer,
                    prefixOfOverwriteLayer
                );
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

            return originals.Concat(overwrites.Where(elem => originals.All(el => el.name != elem.name))).ToArray();
        }

        private AnimatorControllerLayer[] OverwriteLayers
        (
            AnimatorControllerLayer[] originals,
            AnimatorControllerLayer[] overwrites,
            SameNameLayerMode mode,
            string prefixOfOriginalLayer,
            string prefixOfOverwriteLayer
        )
        {
            switch (mode)
            {
                case SameNameLayerMode.RaiseError:
                {
                    var newOriginals  = originals .Select(DuplicateLayer(prefixOfOriginalLayer , 0                  )).ToArray();
                    var newOverwrites = overwrites.Select(DuplicateLayer(prefixOfOverwriteLayer, newOriginals.Length)).ToArray();
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
                    var newOriginals  = originals .Select(DuplicateLayer(prefixOfOriginalLayer , 0)).ToArray();
                    var newOverwrites = overwrites.Select(DuplicateLayer(prefixOfOverwriteLayer, 0)).ToArray();
                    var nameToOrig    = newOriginals.ToDictionary(elem => elem.name, elem => elem);
                    var isDeleteds    = newOverwrites.Select(elem => nameToOrig.ContainsKey(elem.name)).ToArray();
                    for (var i = 0; i < newOverwrites.Length; i++)
                    {
                        if (isDeleteds[i] && overwrites.Any(elem => elem.syncedLayerIndex == i))
                            throw new SyncedLayerOverwritedException(newOverwrites[i].name);
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
                    var newOriginals  = originals .Select(DuplicateLayer(prefixOfOriginalLayer , 0)).ToArray();
                    var newOverwrites = overwrites.Select(DuplicateLayer(prefixOfOverwriteLayer, 0)).ToArray();
                    var nameToOrig    = newOriginals .Select((elem, i) => (elem, i)).ToDictionary(ei => ei.elem.name, ei => ei);
                    var nameToOver    = newOverwrites.Select((elem, i) => (elem, i)).ToDictionary(ei => ei.elem.name, ei => ei);
                    var isDeleteds    = newOriginals .Select(elem => nameToOver.ContainsKey(elem.name)).ToArray();
                    var isReplacings  = newOverwrites.Select(elem => nameToOrig.ContainsKey(elem.name)).ToArray();
                    for (var i = 0; i < newOriginals.Length; i++)
                    {
                        if (isDeleteds[i] && originals.Any(elem => elem.syncedLayerIndex == i))
                            throw new SyncedLayerOverwritedException(newOriginals[i].name);
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
                    throw new ArgumentException(nameof(mode));
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

        private Func<AnimatorControllerLayer, AnimatorControllerLayer> DuplicateLayer(string prefix, int baseIndex) => (AnimatorControllerLayer src) =>
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

            var func = new DuplicateStateMachineFunc(this);

            result.stateMachine = src.stateMachine == null ? null : func.Invoke(src.stateMachine);

            return result;
        };

        private class DuplicateStateMachineFunc
        {
            private Overwriter Parent { get; }
            private HashSet<AnimatorStateMachine> Exists { get; }
            private Dictionary<AnimatorState, AnimatorState> OldToNewState { get; }
            private Dictionary<AnimatorStateMachine, AnimatorStateMachine> OldToNewStateMachine { get; }

            public DuplicateStateMachineFunc(Overwriter parent)
            {
                Parent               = parent;
                Exists               = new HashSet   <AnimatorStateMachine                      >(new UnityEqualityComparer<AnimatorStateMachine>());
                OldToNewState        = new Dictionary<AnimatorState       , AnimatorState       >(new UnityEqualityComparer<AnimatorState>());
                OldToNewStateMachine = new Dictionary<AnimatorStateMachine, AnimatorStateMachine>(new UnityEqualityComparer<AnimatorStateMachine>());
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

                var result = ObjectFactory.CreateInstance<AnimatorStateMachine>();

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

                var result = ObjectFactory.CreateInstance<AnimatorState>();

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

                result.motion = new DuplicateMotionFunc(Parent).Invoke(src.motion);

                return result;
            }

            private AnimatorStateTransition DuplicateStateTransition(AnimatorStateTransition src)
            {
                if (src == null) return null;

                var result = ObjectFactory.CreateInstance<AnimatorStateTransition>();

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

                var result = ObjectFactory.CreateInstance<AnimatorTransition>();

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
            private Overwriter Parent { get; }
            private HashSet<Motion> Exists { get; }

            public DuplicateMotionFunc(Overwriter parent)
            {
                Parent = parent;
                Exists = new HashSet<Motion>(new UnityEqualityComparer<Motion>());
            }

            public Motion Invoke(Motion src)
            {
                if (src == null) return null;
                if (src is AnimationClip) return src;

                if (Exists.Contains(src)) throw new ArgumentException("Motion is structured recursively.", nameof(src));
                Exists.Add(src);

                var blendTree = src as BlendTree;
                if (blendTree == null) throw new NotSupportedException($"{src.GetType().Name} is not supported as Moition.");

                var result = DuplicateBlendTree(blendTree);

                Exists.Remove(src);
                return result;
            }

            private BlendTree DuplicateBlendTree(BlendTree src)
            {
                if (src == null) return null;

                var result = ObjectFactory.CreateInstance<BlendTree>();

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
    }
}
