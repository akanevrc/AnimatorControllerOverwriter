using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace akanevrc.AnimatorControllerOverwriter.Editor.Tests
{
    public class AnimatorControllerOverwriterTest
    {
        private class TestBehaviour : StateMachineBehaviour
        {
        }

        private Overwriter Overwriter { get; } = new Overwriter();

        private List<UnityEngine.Object> Assets { get; set; } = null;
        private AnimationClip[] AnimationClipPool { get; set; } = null;
        private AvatarMask[] AvatarMaskPool { get; set; } = null;
        private StateMachineBehaviour[] BehaviourPool { get; set; } = null;
        private AnimatorControllerParameter[] ParameterPool { get; set; } = null;
        private List<AnimatorStateMachine> StateMachines { get; set; } = null;
        private List<AnimatorState> States { get; set; } = null;
        private AnimatorController Original { get; set; } = null;
        private AnimatorController Overwrite { get; set; } = null;
        private TestRandom Random { get; set; } = null;

        [SetUp]
        public void Init()
        {
            Random = new TestRandom(DateTime.Now.Ticks);

            Assets            = new List<UnityEngine.Object>();
            AnimationClipPool = GenerateArray(GenerateAnimationClip, 1, 5);
            AvatarMaskPool    = GenerateArray(GenerateAvatarMask   , 1, 5);
            BehaviourPool     = GenerateArray(GenerateBehaviour    , 1, 5);
            
            ParameterPool = GenerateArray(GenerateParameter , 0, 5);
            Original      = GenerateAnimatorController();

            ParameterPool = GenerateArray(GenerateParameter , 0, 5);
            Overwrite     = GenerateAnimatorController();
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var asset in Assets)
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(asset));
            }
            Assets            = null;
            AnimationClipPool = null;
            AvatarMaskPool    = null;
            BehaviourPool     = null;
            ParameterPool     = null;
            StateMachines     = null;
            States            = null;
            Original          = null;
            Overwrite         = null;
        }

        private AnimatorController GenerateAnimatorController()
        {
            var result = AnimatorController.CreateAnimatorControllerAtPath($"Assets/{GUID.Generate()}.controller");
            Assets.Add(result);
            
            SetProperties
            (
                result,
                "hideFlags",
                "layers",
                "parameters"
            );

            result.hideFlags  = HideFlags.None;
            result.parameters = GenerateParameters();

            result.layers = GenerateArray(GenerateLayer, 2, 5);

            SetSyncedLayerIndeices(result);

            return result;
        }

        private AnimatorControllerLayer GenerateLayer()
        {
            var result = new AnimatorControllerLayer();

            SetProperties
            (
                result,
                "avatarMask",
                "stateMachine",
                "syncedLayerIndex"
            );

            StateMachines = new List<AnimatorStateMachine>();
            States        = new List<AnimatorState>();

            result.avatarMask       = Random.Pick(AvatarMaskPool);
            result.syncedLayerIndex = -1;

            result.stateMachine = GenerateStateMachine(2)();

            SetTransitions(result.stateMachine);
            SetDefaultState();

            return result;
        }

        private AnimatorControllerParameter[] GenerateParameters()
        {
            return ParameterPool.ToArray();
        }

        private AnimatorControllerParameter GenerateParameter()
        {
            var result = new AnimatorControllerParameter();

            SetProperties(result);

            return result;
        }

        private Func<AnimatorStateMachine> GenerateStateMachine(int maxDepth) => () =>
        {
            var result = ObjectFactory.CreateInstance<AnimatorStateMachine>();
            StateMachines.Add(result);

            SetProperties
            (
                result,
                "anyStateTransitions",
                "behaviours",
                "defaultState",
                "entryTransitions",
                "stateMachines",
                "states"
            );

            result.behaviours = Random.PickSome(BehaviourPool);

            result.anyStateTransitions = new AnimatorStateTransition[0];
            result.entryTransitions    = new AnimatorTransition[0];

            result.stateMachines = maxDepth == 0 ? new ChildAnimatorStateMachine[0] : GenerateArray(GenerateChildStateMachine(maxDepth - 1), 0, 2);
            result.states        = GenerateArray(GenerateChildState, 0, 5);

            return result;
        };

        private Func<ChildAnimatorStateMachine> GenerateChildStateMachine(int maxDepth) => () =>
        {
            var result = new ChildAnimatorStateMachine();

            SetProperties
            (
                result,
                "stateMachine"
            );

            result.stateMachine = GenerateStateMachine(maxDepth)();

            return result;
        };

        private ChildAnimatorState GenerateChildState()
        {
            var result = new ChildAnimatorState();

            SetProperties
            (
                result,
                "state"
            );

            result.state = GenerateState();

            return result;
        }

        private AnimatorState GenerateState()
        {
            var result = ObjectFactory.CreateInstance<AnimatorState>();
            States.Add(result);

            SetProperties
            (
                result,
                "behaviours",
                "cycleOffsetParameter",
                "cycleOffsetParameterActive",
                "mirrorParameter",
                "mirrorParameterActive",
                "motion",
                "speedParameter",
                "speedParameterActive",
                "timeParameter",
                "timeParameterActive",
                "transitions"
            );

            var floatParameters = ParameterPool.Where(p => p.type == AnimatorControllerParameterType.Float).ToArray();
            var boolParameters  = ParameterPool.Where(p => p.type == AnimatorControllerParameterType.Bool ).ToArray();

            result.behaviours  = Random.PickSome(BehaviourPool);

            result.cycleOffsetParameterActive = floatParameters.Length == 0 ? false : Random.NextBool();
            result.mirrorParameterActive      = boolParameters .Length == 0 ? false : Random.NextBool();
            result.speedParameterActive       = floatParameters.Length == 0 ? false : Random.NextBool();
            result.timeParameterActive        = floatParameters.Length == 0 ? false : Random.NextBool();

            result.cycleOffsetParameter = result.cycleOffsetParameterActive ? Random.Pick(floatParameters)?.name : null;
            result.mirrorParameter      = result.mirrorParameterActive      ? Random.Pick(boolParameters )?.name : null;
            result.speedParameter       = result.speedParameterActive       ? Random.Pick(floatParameters)?.name : null;
            result.timeParameter        = result.timeParameterActive        ? Random.Pick(floatParameters)?.name : null;

            result.motion =
                Random.NextBool() ? (Motion)GenerateBlendTree(2)() :
                Random.NextBool() ? (Motion)Random.Pick(AnimationClipPool) : null;

            result.transitions = new AnimatorStateTransition[0];

            return result;
        }

        private Func<BlendTree> GenerateBlendTree(int maxDepth) => () =>
        {
            var result = ObjectFactory.CreateInstance<BlendTree>();

            SetProperties
            (
                result,
                "blendParameter",
                "blendParameterY",
                "children"
            );

            var floatParameters = ParameterPool.Where(p => p.type == AnimatorControllerParameterType.Float).ToArray();
            
            result.blendParameter =
                result.blendType == BlendTreeType.Direct
                    ? null : Random.Pick(floatParameters)?.name;
            result.blendParameterY =
                result.blendType == BlendTreeType.Direct || result.blendType == BlendTreeType.Simple1D
                    ? null : Random.Pick(floatParameters)?.name;

            result.children = maxDepth == 0 ? new ChildMotion[0] : GenerateArray(GenerateChildMotion(maxDepth - 1), 1, 3);

            return result;
        };

        private Func<ChildMotion> GenerateChildMotion(int maxDepth) => () =>
        {
            var result = new ChildMotion();

            SetProperties
            (
                result,
                "directBlendParameter",
                "motion",
                "timeScale"
            );

            var floatParameters = ParameterPool.Where(p => p.type == AnimatorControllerParameterType.Float).ToArray();

            result.directBlendParameter = Random.Pick(floatParameters)?.name;
            result.timeScale            = Random.NextFloat(0.5F, 2.0F);

            result.motion = Random.NextBool() ? (Motion)GenerateBlendTree(maxDepth)() : (Motion)Random.Pick(AnimationClipPool);

            return result;
        };

        private AnimatorStateTransition GenerateStateTransition(UnityEngine.Object dest)
        {
            var result = ObjectFactory.CreateInstance<AnimatorStateTransition>();

            SetProperties
            (
                result,
                "conditions",
                "destinationState",
                "destinationStateMachine",
                "isExit"
            );

            result.isExit = dest == null;

            result.conditions = GenerateArray(GenerateCondition, 0, 3);

            result.destinationState        = dest as AnimatorState;
            result.destinationStateMachine = dest as AnimatorStateMachine;

            return result;
        }

        private AnimatorTransition GenerateTransition(UnityEngine.Object dest)
        {
            var result = ObjectFactory.CreateInstance<AnimatorTransition>();

            SetProperties
            (
                result,
                "conditions",
                "destinationState",
                "destinationStateMachine",
                "isExit"
            );

            result.isExit = dest == null;

            result.conditions = GenerateArray(GenerateCondition, 0, 3);

            result.destinationState        = dest as AnimatorState;
            result.destinationStateMachine = dest as AnimatorStateMachine;

            return result;
        }

        private AnimatorCondition GenerateCondition()
        {
            var result = new AnimatorCondition();

            SetProperties
            (
                result,
                "parameter"
            );

            result.parameter = Random.Pick(ParameterPool)?.name;

            return result;
        }

        private AnimationClip GenerateAnimationClip()
        {
            var result = ObjectFactory.CreateInstance<AnimationClip>();
            return result;
        }

        private AvatarMask GenerateAvatarMask()
        {
            return ObjectFactory.CreateInstance<AvatarMask>();
        }

        private StateMachineBehaviour GenerateBehaviour()
        {
            return ObjectFactory.CreateInstance<TestBehaviour>();
        }

        private T[] GenerateArray<T>(Func<T> generator, int minLength, int maxLength)
        {
            return Enumerable.Repeat(0, Random.NextInt(minLength, maxLength + 1)).Select(_ => generator()).ToArray();
        }

        private void SetProperties<T>(T obj, params string[] excludes)
        {
            foreach (var p in typeof(T).GetProperties())
            {
                if (!p.CanRead || !p.CanWrite || excludes.Contains(p.Name)) continue;

                if (p.PropertyType == typeof(int))
                {
                    p.GetSetMethod().Invoke(obj, new object[] { Random.NextInt(0, 100) });
                }
                else if (p.PropertyType == typeof(float))
                {
                    p.GetSetMethod().Invoke(obj, new object[] { Random.NextFloat(0.0F, 1.0F) });
                }
                else if (p.PropertyType == typeof(bool))
                {
                    p.GetSetMethod().Invoke(obj, new object[] { Random.NextBool() });
                }
                else if (p.PropertyType.IsEnum)
                {
                    p.GetSetMethod().Invoke(obj, new object[] { Random.NextEnum(p.PropertyType) });
                }
                else if (p.PropertyType == typeof(Vector2))
                {
                    p.GetSetMethod().Invoke(obj, new object[] { new Vector2
                        (
                            Random.NextFloat(0.0F, 1.0F),
                            Random.NextFloat(0.0F, 1.0F)
                        ) });
                }
                else if (p.PropertyType == typeof(Vector3))
                {
                    p.GetSetMethod().Invoke(obj, new object[] { new Vector3
                        (
                            Random.NextFloat(0.0F, 1.0F),
                            Random.NextFloat(0.0F, 1.0F),
                            Random.NextFloat(0.0F, 1.0F)
                        ) });
                }
                else if (p.PropertyType == typeof(string))
                {
                    p.GetSetMethod().Invoke(obj, new object[] { GUID.Generate().ToString() });
                }
            }
        }

        private void SetSyncedLayerIndeices(AnimatorController src)
        {
            var syncs = new List<int>();
            var roots = new List<int>();

            roots.Add(0);

            for (var i = 1; i < src.layers.Length; i++)
            {
                if (Random.NextBool())
                {
                    syncs.Add(i);
                }
                else
                {
                    roots.Add(i);
                }
            }

            for (var (i, j) = (0, 0); i < syncs.Count; i++, j = (j + 1) % roots.Count)
            {
                src.layers[syncs[i]].syncedLayerIndex = roots[j];
            }
        }

        private void SetTransitions(AnimatorStateMachine src)
        {
            var anyStateDests =
                src.stateMachines.Select(child => (UnityEngine.Object)child.stateMachine)
                    .Concat(src.states.Select(child => (UnityEngine.Object)child.state))
                    .ToArray();
            var entryDests =
                src.stateMachines.Select(child => (UnityEngine.Object)child.stateMachine)
                    .Concat(src.states.Select(child => (UnityEngine.Object)child.state))
                    .Concat(StateMachines)
                    .Concat(States)
                    .ToArray();
            var stateDests =
                src.stateMachines.Select(child => (UnityEngine.Object)child.stateMachine)
                    .Concat(src.states.Select(child => (UnityEngine.Object)child.state))
                    .Concat(StateMachines)
                    .Concat(States)
                    .Concat(new UnityEngine.Object[] { null })
                    .ToArray();

            src.anyStateTransitions = Random.PickSome(anyStateDests).Select(GenerateStateTransition).ToArray();
            src.entryTransitions    = Random.PickSome(entryDests   ).Select(GenerateTransition     ).ToArray();

            foreach (var child in src.states)
            {
                child.state.transitions =
                    Random.PickSome(stateDests)
                        .Where(dest => dest?.GetInstanceID() != child.state.GetInstanceID())
                        .Select(GenerateStateTransition)
                        .ToArray();
            }

            foreach (var child in src.stateMachines)
            {
                SetTransitions(child.stateMachine);
            }
        }

        private void SetDefaultState()
        {
            if (StateMachines.Count > 0)
            {
                var stateMachine = Random.Pick(StateMachines.ToArray());
                if (stateMachine.states.Length > 0) stateMachine.defaultState = Random.Pick(stateMachine.states).state;
            }
        }

        [Test]
        public void ValidateToBeSuccess()
        {
            Assert.That
            (
                () => Overwriter.Validate(Original, Overwrite, "", "", false),
                Throws.Nothing
            );
        }

        [Test]
        public void ValidateToBeFailure()
        {
            var layer1 = GenerateLayer();
            var layer2 = GenerateLayer();

            layer1.name = "Same Name";
            layer2.name = "Same Name";

            Original .AddLayer(layer1);
            Overwrite.AddLayer(layer2);
            
            Assert.That
            (
                () => Overwriter.Validate(Original, Overwrite, "", "", false),
                Throws.TypeOf<LayerConflictException>()
            );
        }

        [Test]
        public void GenerateToBeSuccessWhenDoNotMergeSameParameters()
        {
            var path = $"Assets/{GUID.Generate()}.controller";

            try
            {
                var result = Overwriter.Generate(path, Original, Overwrite, "[Original]", "[Overwrite]", false);

                Assert.That(AssetDatabase.GetAssetPath(result), Is.EqualTo(path));
                new AssertDuplicationFunc(this, "[Original]", "[Overwrite]", false).Invoke(result);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void GenerateToBeSuccessWhenMergeSameParameters()
        {
            var param1 = GenerateParameter();
            var param2 = GenerateParameter();

            param1.name = "Same Name";
            param2.name = "Same Name";

            param1.type = AnimatorControllerParameterType.Int;
            param2.type = AnimatorControllerParameterType.Int;

            Original .AddParameter(param1);
            Overwrite.AddParameter(param2);

            var path = $"Assets/{GUID.Generate()}.controller";

            try
            {
                var result = Overwriter.Generate(path, Original, Overwrite, "[Original]", "[Overwrite]", true);

                Assert.That(AssetDatabase.GetAssetPath(result), Is.EqualTo(path));
                new AssertDuplicationFunc(this, "[Original]", "[Overwrite]", false).Invoke(result);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void GenerateToBeFailureWhenLayerConflicts()
        {
            var layer1 = GenerateLayer();
            var layer2 = GenerateLayer();

            layer1.name = "Same Name";
            layer2.name = "Same Name";

            Original .AddLayer(layer1);
            Overwrite.AddLayer(layer2);

            var path = $"Assets/{GUID.Generate()}.controller";

            try
            {
                Assert.That
                (
                    () => Overwriter.Generate(path, Original, Overwrite, "[Original]", "[Overwrite]", false),
                    Throws.TypeOf<LayerConflictException>()
                );
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void GenerateToBeFailureWhenParameterNameConflicts()
        {
            var param1 = GenerateParameter();
            var param2 = GenerateParameter();

            param1.name = "Same Name";
            param2.name = "Same Name";

            param1.type = AnimatorControllerParameterType.Int;
            param2.type = AnimatorControllerParameterType.Int;

            Original .AddParameter(param1);
            Overwrite.AddParameter(param2);
            
            var path = $"Assets/{GUID.Generate()}.controller";

            try
            {
                Assert.That
                (
                    () => Overwriter.Generate(path, Original, Overwrite, "[Original]", "[Overwrite]", false),
                    Throws.TypeOf<ParameterConflictException>()
                );
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void GenerateToBeFailureWhenParameterTypeConflicts()
        {
            var param1 = GenerateParameter();
            var param2 = GenerateParameter();

            param1.name = "Same Name";
            param2.name = "Same Name";

            param1.type = AnimatorControllerParameterType.Int;
            param2.type = AnimatorControllerParameterType.Float;

            Original .AddParameter(param1);
            Overwrite.AddParameter(param2);
            
            var path = $"Assets/{GUID.Generate()}.controller";

            try
            {
                Assert.That
                (
                    () => Overwriter.Generate(path, Original, Overwrite, "[Original]", "[Overwrite]", true),
                    Throws.TypeOf<ParameterConflictException>()
                );
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private class AssertDuplicationFunc
        {
            private AnimatorControllerOverwriterTest Parent { get; }
            private string PrefixOfOriginalLayer { get; }
            private string PrefixOfOverwriteLayer { get; }
            private bool MergeSameParameters { get; }

            public AssertDuplicationFunc
            (
                AnimatorControllerOverwriterTest parent,
                string prefixOfOriginalLayer,
                string prefixOfOverwriteLayer,
                bool mergeSameParameters
            )
            {
                Parent                 = parent;
                PrefixOfOriginalLayer  = prefixOfOriginalLayer;
                PrefixOfOverwriteLayer = prefixOfOverwriteLayer;
                MergeSameParameters    = mergeSameParameters;
            }

            public void Invoke(AnimatorController result)
            {
                Assert.That(result.name     , Is.EqualTo($"{Parent.Original.name}_overwrited"));
                Assert.That(result.hideFlags, Is.EqualTo(HideFlags.None));

                var concatLayers = Parent.Original.layers.Concat(Parent.Overwrite.layers).ToArray();
                Assert.That(result.layers.Length, Is.EqualTo(concatLayers.Length));

                var concatParameters =
                    Parent.Original.parameters.Concat
                    (
                        Parent.Overwrite.parameters.Where(elem => Parent.Original.parameters.All(el => el.name != elem.name))
                    ).ToArray();
                Assert.That(result.parameters.Length, Is.EqualTo(concatParameters.Length));

                for (var i = 0; i < result.layers.Length; i++)
                {
                    AssertValue(typeof(AnimatorControllerLayer[]), -1, i, "", result.layers[i], concatLayers[i]);
                }

                for (var i = 0; i < result.parameters.Length; i++)
                {
                    AssertValue(typeof(AnimatorControllerParameter[]), -1, i, "", result.parameters[i], concatParameters[i]);
                }
            }

            private void AssertProperties(int index, object result, object original)
            {
                Assert.That(result.GetType(), Is.EqualTo(original.GetType()));

                foreach (var p in result.GetType().GetProperties())
                {
                    if (!p.CanRead || !p.CanWrite) continue;

                    var getMethod = p.GetGetMethod();
                    AssertValue(result.GetType(), index, -1, p.Name, getMethod.Invoke(result, new object[0]), getMethod.Invoke(original, new object[0]));
                }
            }

            private void AssertValue
            (
                Type parentType,
                int parentIndex,
                int index,
                string propertyName,
                dynamic result,
                dynamic original)
            {
                if (ReferenceEquals(result, null) || ReferenceEquals(original, null))
                {
                    Assert.That(result  , Is.Null);
                    Assert.That(original, Is.Null);
                    return;
                }

                Type type = result.GetType();

                if (typeof(AnimatorControllerLayer).IsAssignableFrom(parentType) && propertyName == "name")
                {
                    if (parentIndex < Parent.Original.layers.Length)
                    {
                        Assert.That(result, Is.EqualTo(PrefixOfOriginalLayer + original));
                    }
                    else
                    {
                        Assert.That(result, Is.EqualTo(PrefixOfOverwriteLayer + original));
                    }
                }
                else if
                (
                    typeof(AnimatorStateMachine).IsAssignableFrom(parentType) &&
                    (
                        propertyName == "defaultState"
                    ) ||
                    typeof(AnimatorTransitionBase).IsAssignableFrom(parentType) &&
                    (
                        propertyName == "destinationState" ||
                        propertyName == "destinationStateMachine"
                    )
                )
                {
                    Assert.That(result.GetInstanceID(), Is.Not.EqualTo(original.GetInstanceID()));
                }
                else if (type.IsArray)
                {
                    for (var i = 0; i < result.Length; i++)
                    {
                        AssertValue(type, index, i, "", result[i], original[i]);
                    }
                }
                else if
                (
                    type == typeof(int)     ||
                    type == typeof(float)   ||
                    type == typeof(bool)    ||
                    type == typeof(string)  ||
                    type == typeof(Vector2) ||
                    type == typeof(Vector3) ||
                    type.IsEnum
                )
                {
                    Assert.That(result, Is.EqualTo(original));
                }
                else if
                (
                    typeof(ChildAnimatorStateMachine).IsAssignableFrom(type) ||
                    typeof(ChildAnimatorState       ).IsAssignableFrom(type) ||
                    typeof(ChildMotion              ).IsAssignableFrom(type) ||
                    typeof(AnimatorCondition        ).IsAssignableFrom(type)
                )
                {
                    AssertProperties(index, result, original);
                }
                else if
                (
                    typeof(AnimatorControllerLayer    ).IsAssignableFrom(type) ||
                    typeof(AnimatorControllerParameter).IsAssignableFrom(type)
                )
                {
                    Assert.That(result, Is.Not.SameAs(original));
                    AssertProperties(index, result, original);
                }
                else if
                (
                    typeof(AnimatorStateMachine   ).IsAssignableFrom(type) ||
                    typeof(AnimatorState          ).IsAssignableFrom(type) ||
                    typeof(BlendTree              ).IsAssignableFrom(type) ||
                    typeof(AnimatorStateTransition).IsAssignableFrom(type) ||
                    typeof(AnimatorTransition     ).IsAssignableFrom(type)
                )
                {
                    Assert.That(result.GetInstanceID(), Is.Not.EqualTo(original.GetInstanceID()));
                    AssertProperties(index, result, original);
                }
                else if
                (
                    typeof(AnimationClip        ).IsAssignableFrom(type) ||
                    typeof(AvatarMask           ).IsAssignableFrom(type) ||
                    typeof(StateMachineBehaviour).IsAssignableFrom(type)
                )
                {
                    Assert.That(result.GetInstanceID(), Is.EqualTo(original.GetInstanceID()));
                }
                else
                {
                    throw new NotSupportedException($"Unexpected type has been found : {type}");
                }
            }
        }
    }
}
