// Pyrite9 Sleeve Roll — PhysBone sleeves (docs 10-33): keep the sleeve's physics while it is down, stop it while rolled
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Pyrite9.Sleeve
{
    /// <summary>
    /// Sorts the PhysBones that hold a rolled sleeve.
    /// Under the forearm or hand, if safe: kept — the PhysBone keeps running, the cloth skins to shadow bones that copy it, and FX blends the shadows to the bind pose while rolled (SleeveRollPbFx). A chain under the hand also moves under the wrist proxy, so the rolled cloth ignores the wrist.
    /// Under the hand, not safe: the physics stops for good and the weights go to the proxy (WARN). Under the forearm, not safe, or under the upper arm reaching the cuff: this side is not rolled (WARN).
    /// Nothing changes for an avatar without such a PhysBone: no layer, no re-virtualised controller.
    /// </summary>
    sealed class SleeveRollPb
    {
        /// <summary>Largest difference between a bone's prefab pose and the bind pose that still counts as the same (the rolled shape is computed from the bind pose)</summary>
        const float MaxAngle = 0.5f, MaxMeters = 0.0005f;
        /// <summary>A PhysBone under the upper arm holding cloth this far down the forearm (0 elbow ~ 1 wrist) is a sleeve we cannot roll</summary>
        const float ReachT = 0.5f;

        internal sealed class Chain
        {
            public VRCPhysBone pb;
            public bool left, underHand;
            public Transform root, lower, hand;
            public List<Transform> bones;
            public SkinnedMeshRenderer cloth;
            public readonly List<Transform> shadows = new List<Transform>();
            public readonly HashSet<SkinnedMeshRenderer> garments = new HashSet<SkinnedMeshRenderer>();
        }

        readonly GameObject root;
        readonly Animator animator;
        readonly SleeveRollShape.Cache cache;
        readonly StringBuilder log;
        readonly List<(VRCPhysBone pb, Transform root, List<Transform> bones)> all = new List<(VRCPhysBone, Transform, List<Transform>)>();
        readonly Dictionary<VRCPhysBone, Chain> candidates = new Dictionary<VRCPhysBone, Chain>();
        readonly Dictionary<(SkinnedMeshRenderer, bool), string> fails = new Dictionary<(SkinnedMeshRenderer, bool), string>();
        readonly HashSet<Transform> keep = new HashSet<Transform>();
        readonly HashSet<Transform> humanoid = new HashSet<Transform>();

        public readonly List<Chain> Chains = new List<Chain>();
        /// <summary>Bones of kept chains — their weights must not move to the wrist proxy</summary>
        public ICollection<Transform> Keep => keep;

        public SleeveRollPb(GameObject root, Animator animator, SleeveRollShape.Cache cache, StringBuilder log)
        {
            this.root = root; this.animator = animator; this.cache = cache; this.log = log;
            var upperL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var upperR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            foreach (var pb in root.GetComponentsInChildren<VRCPhysBone>(true))
            {
                if (!pb.enabled) continue;   // off in the prefab: it does not move, nothing to keep
                var rt = pb.rootTransform != null ? pb.rootTransform : pb.transform;
                if (!(upperL != null && rt.IsChildOf(upperL)) && !(upperR != null && rt.IsChildOf(upperR))) continue;   // hair, tails: never a sleeve
                var ignored = new HashSet<Transform>((pb.ignoreTransforms ?? new List<Transform>()).Where(t => t != null));
                var bones = new List<Transform>();
                Affected(rt, ignored, bones);
                if (bones.Count > 0) all.Add((pb, rt, bones));
            }
        }

        static void Affected(Transform t, HashSet<Transform> ignored, List<Transform> into)
        {
            if (ignored.Contains(t)) return;
            into.Add(t);
            for (int i = 0; i < t.childCount; i++) Affected(t.GetChild(i), ignored, into);
        }

        /// <summary>A rolled side of one garment. roll: how much each vertex rolls (SideReport.roll)</summary>
        public void Consider(SkinnedMeshRenderer cloth, bool left, float[] roll)
        {
            if (all.Count == 0 || roll == null) return;
            var upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var lower = animator.GetBoneTransform(SrSide.Lower(left));
            var hand = animator.GetBoneTransform(SrSide.Hand(left));
            if (upper == null || lower == null || hand == null) return;
            var rolled = SleeveRollShape.BonesOnRolled(cache, cloth, roll);
            foreach (var (pb, rt, bones) in all)
            {
                if (!rt.IsChildOf(upper)) continue;
                if (!rt.IsChildOf(lower))
                {
                    if (SleeveRollShape.Reaches(cache, cloth, animator, left, bones, ReachT, out var t))
                        fails[(cloth, left)] = $"a PhysBone under the upper arm ({rt.name}) holds this sleeve down to t={t:F2} — it cannot be rolled";
                    continue;
                }
                if (!bones.Any(rolled.Contains)) continue;
                if (!candidates.TryGetValue(pb, out var c))
                    candidates[pb] = c = new Chain { pb = pb, left = left, root = rt, lower = lower, hand = hand, bones = bones, cloth = cloth, underHand = rt.IsChildOf(hand) };
                c.garments.Add(cloth);
            }
        }

        public void Decide()
        {
            if (candidates.Count == 0) return;
            foreach (HumanBodyBones b in System.Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (b == HumanBodyBones.LastBone) continue;
                var t = animator.GetBoneTransform(b);
                if (t != null) humanoid.Add(t);
            }
            var animated = AnimatedPaths();
            var merged = MergedPhysBones();
            foreach (var c in candidates.Values)
            {
                var why = Blocked(c, animated, merged);
                if (why == null)
                {
                    Chains.Add(c);
                    foreach (var b in c.bones) keep.Add(b);
                    log.AppendLine($"  {c.root.name}: PhysBone kept — still while rolled");
                }
                else if (c.underHand)
                    log.AppendLine($"WARN {c.root.name}: this sleeve's PhysBone stops moving ({why})");
                else
                    foreach (var g in c.garments)
                        fails[(g, c.left)] = $"a PhysBone on the forearm ({c.root.name}) holds this sleeve and cannot be held still while rolled ({why})";
            }
        }

        public string FailReason(SkinnedMeshRenderer cloth, bool left) => fails.TryGetValue((cloth, left), out var m) ? m : null;

        public int KeptOn(SkinnedMeshRenderer cloth, bool left) => Chains.Count(c => c.left == left && c.garments.Contains(cloth));

        // English goes to the build record. null = safe
        string Blocked(Chain c, HashSet<string> animated, HashSet<Object> merged)
        {
            var pb = c.pb;
            if (c.bones.Any(humanoid.Contains)) return "it moves a body bone";
            if (c.bones.Any(b => !b.IsChildOf(c.lower))) return "it also moves bones outside this forearm";
            if (pb.GetComponents<VRCPhysBone>()[0] != pb) return "a second PhysBone on one object";
            if (pb.isAnimated) return "Is Animated is on";
            if (pb.maxStretch > 0f || pb.maxSquish > 0f) return "Max Stretch or Max Squish is set";
            if (merged.Contains(pb)) return "an Avatar Optimizer Merge PhysBone uses it";
            if (animated.Contains("E|" + Path(pb.transform))) return "an animation already turns it on and off";
            if (c.bones.Any(b => animated.Contains("T|" + Path(b)))) return "an animation already moves its bones";
            var ce = cache.Get(c.cloth);
            foreach (var b in c.bones)
            {
                int i = System.Array.IndexOf(ce.bones, b), pi = System.Array.IndexOf(ce.bones, b.parent);
                if (i < 0 || pi < 0) continue;
                var m = ce.bindposes[pi] * ce.bindposes[i].inverse;
                float meters = Vector3.Distance(b.localPosition, m.GetColumn(3)) * b.parent.lossyScale.x;
                if (Quaternion.Angle(b.localRotation, m.rotation) > MaxAngle || meters > MaxMeters) return "its rest pose differs from the mesh's bind pose";
            }
            return null;
        }

        /// <summary>
        /// After the weights moved: chains under the hand go under the wrist proxy (when there is one), then each chain gets shadow bones and the rolled garments skin to them.
        /// A shadow is a Rotation Constraint with the real bone as its source: weight 1 = the PhysBone's rotation (sleeve down), 0 = its rest = the bind pose (rolled) — the same mechanism as the wrist proxy.
        /// FX never writes the PhysBone or its bones: writing the bones worked in Unity Play but in VRChat the rolled cloth stayed bent (docs 10-38).
        /// </summary>
        public void Reparent()
        {
            foreach (var c in Chains)
            {
                var ce = cache.Get(c.cloth);
                int hi = System.Array.IndexOf(ce.bones, c.hand);
                var proxy = c.underHand ? c.lower.Find(SrSide.ProxyName(c.left)) : null;
                if (proxy != null)
                {
                    c.root.SetParent(proxy, true);
                    log.AppendLine($"  {c.root.name}: moved under {proxy.name}");
                }
                // a bone with no cloth on it or below it (usually the chain's end) needs no shadow: nothing would follow it
                var needed = new HashSet<Transform>();
                foreach (var g in c.garments) needed.UnionWith(SleeveRollShape.WeightedBones(g));
                for (int k = c.bones.Count - 1; k >= 0; k--)   // children come last, so walk up from them
                    if (needed.Contains(c.bones[k]) && c.bones[k] != c.root) needed.Add(c.bones[k].parent);
                var map = new Dictionary<Transform, Transform>();
                foreach (var b in c.bones)   // parents come first
                {
                    if (!needed.Contains(b)) continue;
                    int i = System.Array.IndexOf(ce.bones, b);
                    int pi = b == c.root && proxy != null ? hi : System.Array.IndexOf(ce.bones, b.parent);   // the proxy's bind pose is the hand's
                    var rest = i >= 0 && pi >= 0 ? (ce.bindposes[pi] * ce.bindposes[i].inverse).rotation : b.localRotation;
                    map[b] = Shadow(b, b == c.root ? b.parent : map[b.parent], rest);
                    c.shadows.Add(map[b]);
                }
                foreach (var g in c.garments)
                {
                    var bones = g.bones;
                    for (int i = 0; i < bones.Length; i++)
                        if (bones[i] != null && map.TryGetValue(bones[i], out var s)) bones[i] = s;
                    g.bones = bones;
                }
                log.AppendLine($"  {c.root.name}: {c.shadows.Count} shadow bones");
            }
        }

        static Transform Shadow(Transform bone, Transform parent, Quaternion rest)
        {
            var t = new GameObject("SleeveRoll_Shadow_" + bone.name).transform;
            t.SetParent(parent, false);
            t.localPosition = bone.localPosition;
            t.localRotation = bone.localRotation;
            t.localScale = bone.localScale;
            SleeveRollDriver.CopyRotation(t.gameObject, bone, rest);
            return t;
        }

        string Path(Transform t) => AnimationUtility.CalculateTransformPath(t, root.transform);

        // "E|path" PhysBone enabled, "T|path" transform — in any playable layer of the avatar
        HashSet<string> AnimatedPaths()
        {
            var set = new HashSet<string>();
            var d = root.GetComponent<VRCAvatarDescriptor>();
            if (d == null) return set;
            var seen = new HashSet<AnimationClip>();
            foreach (var layer in d.baseAnimationLayers.Concat(d.specialAnimationLayers))
            {
                if (layer.animatorController == null) continue;
                foreach (var clip in layer.animatorController.animationClips)
                {
                    if (clip == null || !seen.Add(clip)) continue;
                    foreach (var b in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (b.type == typeof(Transform)) set.Add("T|" + b.path);
                        else if (b.type == typeof(VRCPhysBone) && b.propertyName == "m_Enabled") set.Add("E|" + b.path);
                    }
                }
            }
            return set;
        }

        // Avatar Optimizer is optional — found by type name, so the package stays free of it
        HashSet<Object> MergedPhysBones()
        {
            var set = new HashSet<Object>();
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null || comp.GetType().Name != "MergePhysBone") continue;
                var it = new SerializedObject(comp).GetIterator();
                while (it.Next(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null) set.Add(it.objectReferenceValue);
            }
            return set;
        }
    }
}
