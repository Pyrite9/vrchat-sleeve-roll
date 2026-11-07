// Pyrite9 Sleeve Roll — 빌드 앞단(Generating, MA 앞): 구동 FX·메뉴·손목 대리 본 오브젝트
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace Pyrite9.Sleeve
{
    static class SleeveRollDriver
    {
        /// <summary>걷히고 내려오는 시간(초)</summary>
        internal const float BlendSeconds = 0.2f;

        // 지운 정점은 되돌릴 수 없다 — Delete 모드(ShapeChangeType 0) 대상 렌더러 이름만 모아 경고에 쓴다
        public static void ScanDelete(BuildContext ctx, BuildState state)
        {
            if (ctx.AvatarRootObject.GetComponentsInChildren<SleeveRoll>(true).Length == 0) return;
            foreach (var comp in ctx.AvatarRootObject.GetComponentsInChildren<Component>(true))
            {
                if (comp == null || comp.GetType().Name != "ModularAvatarShapeChanger") continue;
                var shapes = new SerializedObject(comp).FindProperty("m_shapes");
                for (int i = 0; shapes != null && i < shapes.arraySize; i++)
                {
                    var el = shapes.GetArrayElementAtIndex(i);
                    var path = el.FindPropertyRelative("Object.referencePath")?.stringValue;
                    var type = el.FindPropertyRelative("ChangeType")?.enumValueIndex ?? 1;
                    if (type == 0 && !string.IsNullOrEmpty(path)) state.deleteTargets.Add(path.Split('/').Last());
                }
            }
        }

        /// <summary>
        /// FX(MA Merge Animator, 절대 경로)에 레이어 하나: Down ↔ Up. Up = 메뉴 토글 Hold 켬(D11 — 그랩은 표정 제스처와 겹쳐 뺐다).
        /// Up 클립: 옷 SleeveUp_L/R 100, 대리 본 제약 GlobalWeight 0, (되돌리기면) 몸 SleeveRoll_Unhide_L/R 100. Down은 그 반대 —
        /// 두 상태가 다 값을 써서 WD 꺼짐·켜짐 어느 쪽이든 맞는다. 셰이프는 Transforming에서 생기지만 이름이 정해져 있어 곡선을 먼저 건다.
        /// 좌우는 같은 Hold·같은 전환으로 움직이고 곡선이 겹치지 않아(쪽마다 다른 셰이프 이름·대리 본) 한 레이어에 같이 둔다 — 쪽마다 레이어를 둔 것과 같은 동작.
        /// 꺼 둔 쪽은 곡선이 없다. 대리 본 오브젝트도 여기서 만든다(경로가 MA 병합 때 있어야 한다) — 쉬는 자세는 Transforming에서 옷 바인드 포즈로
        /// </summary>
        public static void Generate(BuildContext ctx, BuildState state)
        {
            var comps = ctx.AvatarRootObject.GetComponentsInChildren<SleeveRoll>(true).Where(c => c.Garments.Count > 0).ToArray();
            if (comps.Length == 0) return;
            var animator = ctx.AvatarRootObject.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return; // Process가 경고한다

            var clips = new Clips(ctx.AvatarRootTransform);
            var picked = new HashSet<SkinnedMeshRenderer>(comps.SelectMany(c => c.Garments));
            var all = ctx.AvatarRootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            bool anySide = false;
            foreach (bool left in SrSide.Both)
            {
                var sides = comps.Where(c => SrSide.On(c, left)).ToList();
                if (sides.Count == 0) continue;
                anySide = true;
                int before = clips.Count;
                AddSideCurves(clips, sides, left, animator, all, picked);
                state.log.AppendLine($"FX {SrSide.Letter(left)}: {clips.Count - before} curves");
            }

            var controller = NewController(clips, anySide);
            var main = comps[0];
            AddDriver(ctx.AvatarRootTransform, controller, main);
            state.syncedBits = 1;
            state.menu = main.installMenu ? main.MenuLabel : "";
            state.fxLayers = controller.layers.Length;
            state.log.AppendLine($"FX: {controller.layers.Length} layers, menu " +
                                 (main.installMenu ? $"'{main.MenuLabel}'" : $"off — drive '{SleeveRollPlugin.HoldParameter}' yourself") + " (1 synced bit)");
        }

        /// <summary>Down·Up 클립 한 쌍 — 곡선 하나를 두 클립에 같이 넣는다(경로·속성 중복은 무시)</summary>
        sealed class Clips
        {
            public readonly AnimationClip down = new AnimationClip { name = "SleeveRoll_Down", frameRate = 60f };
            public readonly AnimationClip up = new AnimationClip { name = "SleeveRoll_Up", frameRate = 60f };
            readonly Transform root;
            readonly HashSet<string> done = new HashSet<string>();

            public Clips(Transform root) { this.root = root; }
            public int Count => done.Count;

            public void Curve(Transform t, System.Type type, string property, float whenDown, float whenUp)
            {
                var path = AnimationUtility.CalculateTransformPath(t, root);
                if (!done.Add(path + "|" + property)) return;
                AnimationUtility.SetEditorCurve(down, EditorCurveBinding.FloatCurve(path, type, property), Const(whenDown));
                AnimationUtility.SetEditorCurve(up, EditorCurveBinding.FloatCurve(path, type, property), Const(whenUp));
            }

            static AnimationCurve Const(float v) => new AnimationCurve(new Keyframe(0f, v), new Keyframe(1f / 60f, v));
        }

        static void AddSideCurves(Clips clips, List<SleeveRoll> sides, bool left, Animator animator, SkinnedMeshRenderer[] all, HashSet<SkinnedMeshRenderer> picked)
        {
            foreach (var c in sides)
                foreach (var cloth in c.Garments)
                    clips.Curve(cloth.transform, typeof(SkinnedMeshRenderer), "blendShape." + SrSide.ShapeName(left), 0f, 100f);
            var lower = animator.GetBoneTransform(SrSide.Lower(left));
            var hand = animator.GetBoneTransform(SrSide.Hand(left));
            if (sides.Any(c => c.wristProxy) && lower != null && hand != null)
            {
                var proxy = Proxy(lower, hand, Quaternion.Inverse(lower.rotation) * hand.rotation, SrSide.ProxyName(left));
                clips.Curve(proxy, typeof(VRCRotationConstraint), "GlobalWeight", 1f, 0f);
            }
            // 되돌리기(D7)는 어느 몸에 셰이프가 생길지 아직 모른다 — 아래팔 본을 쓰고 블렌드셰이프가 있는 후보 전부에 건다(없는 셰이프 곡선은 아무 일도 안 한다)
            foreach (var c in sides.Where(SleeveRollBake.Unhides))
                foreach (var body in SleeveRollBake.Skins(c, all, picked).Where(b => b.sharedMesh != null && b.sharedMesh.blendShapeCount > 0 && HasWeights(b, lower)))
                    clips.Curve(body.transform, typeof(SkinnedMeshRenderer), "blendShape." + SrSide.UnhideName(left), 0f, 100f);
        }

        /// <summary>정점 하나라도 그 본 웨이트가 있나 — 본 목록만으론 모른다(Salt는 모든 메시가 본 전체를 갖는다)</summary>
        static bool HasWeights(SkinnedMeshRenderer r, Transform bone)
        {
            int i = System.Array.IndexOf(r.bones, bone);
            if (i < 0) return false;
            foreach (var w in r.sharedMesh.GetAllBoneWeights())
                if (w.boneIndex == i && w.weight > 0f) return true;
            return false;
        }

        /// <summary>FX 레이어 — 소매 PhysBone을 유지해도 이 하나(그림자 본 곡선은 SleeveRollPbFx가 같은 상태에 넣는다)</summary>
        internal const string LayerName = "Sleeve Roll";

        static AnimatorController NewController(Clips clips, bool withLayer)
        {
            var controller = new AnimatorController { name = "SleeveRoll" };
            controller.AddParameter(new AnimatorControllerParameter { name = SleeveRollPlugin.HoldParameter, type = AnimatorControllerParameterType.Bool });
            if (!withLayer) return controller;
            var sm = new AnimatorStateMachine { name = LayerName };
            controller.AddLayer(new AnimatorControllerLayer { name = LayerName, stateMachine = sm, defaultWeight = 1f });
            var sDown = AddState(sm, "Down", clips.down);
            var sUp = AddState(sm, "Up", clips.up);
            sm.defaultState = sDown;
            Go(sDown, sUp, AnimatorConditionMode.If);
            Go(sUp, sDown, AnimatorConditionMode.IfNot);
            return controller;
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion)
        {
            var s = sm.AddState(name);
            s.motion = motion;
            s.writeDefaultValues = false;
            return s;
        }

        static void Go(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = BlendSeconds;
            t.AddCondition(mode, 0f, SleeveRollPlugin.HoldParameter);
        }

        // MA 컴포넌트는 아바타 루트 밑 새 오브젝트에 — 설정 컴포넌트가 꺼진 오브젝트에 있어도 병합되게
        static void AddDriver(Transform root, AnimatorController controller, SleeveRoll main)
        {
            var driver = new GameObject("SleeveRoll_Driver");
            driver.transform.SetParent(root, false);
            var merge = driver.AddComponent<ModularAvatarMergeAnimator>();
            merge.animator = controller;
            merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            merge.pathMode = MergeAnimatorPathMode.Absolute;
            merge.matchAvatarWriteDefaults = true;
            merge.deleteAttachedAnimator = false;
            // 메뉴를 끄더라도 파라미터는 동기화로 남긴다 — 다른 메뉴·Contact가 이 이름으로 몰 수 있게
            var parameters = driver.AddComponent<ModularAvatarParameters>();
            parameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = SleeveRollPlugin.HoldParameter,
                syncType = ParameterSyncType.Bool,
                saved = false,
                defaultValue = 0f,
                hasExplicitDefaultValue = true,
            });
            if (!main.installMenu) return;
            var menu = new GameObject(main.MenuLabel);
            menu.transform.SetParent(driver.transform, false);
            menu.AddComponent<ModularAvatarMenuInstaller>();
            var item = menu.AddComponent<ModularAvatarMenuItem>();
            item.Control = new VRCExpressionsMenu.Control
            {
                name = main.MenuLabel,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                value = 1f,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = SleeveRollPlugin.HoldParameter },
                icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(SleeveRollConst.MenuIconGuid)),   // missing only leaves the slot blank
            };
            item.isSynced = true;
            item.isSaved = false;
            item.isDefault = false;
            item.automaticValue = false;
        }

        /// <summary>
        /// 손목 대리 본 — 아래팔 자식, 지금 손과 같은 자세. 손을 소스로 하는 Rotation Constraint(오프셋 0, Locked):
        /// 가중치 1 = 손 회전 그대로(소매 내림), 0 = 쉬는 자세 = 바인드 포즈의 손목(소매 걷음 — FX가 GlobalWeight를 움직인다)
        /// </summary>
        public static Transform Proxy(Transform lower, Transform hand, Quaternion bindLocal, string name)
        {
            var t = lower.Find(name);
            if (t != null)
            {
                // Generating에서 미리 만든 것 — 쉬는 자세만 옷 바인드 포즈로
                var existing = t.GetComponent<VRCRotationConstraint>();
                if (existing != null) existing.RotationAtRest = bindLocal.eulerAngles;
                return t;
            }
            t = new GameObject(name).transform;
            t.SetParent(lower, false);
            t.SetPositionAndRotation(hand.position, hand.rotation);
            t.localScale = Vector3.one;
            CopyRotation(t.gameObject, hand, bindLocal);
            return t;
        }

        /// <summary>source의 회전을 따르는 Rotation Constraint(오프셋 0, Locked) — 가중치 1 = source 그대로, 0 = rest. FX가 GlobalWeight를 움직인다. 대리 본·그림자 본이 같은 구조</summary>
        internal static void CopyRotation(GameObject go, Transform source, Quaternion rest)
        {
            var rc = go.AddComponent<VRCRotationConstraint>();
            rc.Sources.Add(new VRCConstraintSource(source, 1f));
            rc.RotationAtRest = rest.eulerAngles;
            rc.AffectsRotationX = rc.AffectsRotationY = rc.AffectsRotationZ = true;
            rc.GlobalWeight = 1f;
            rc.Locked = true;
            rc.IsActive = true;
        }
    }
}
