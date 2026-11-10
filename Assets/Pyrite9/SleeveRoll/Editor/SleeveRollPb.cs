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
            /// <summary>The root's subtree, parents first: bones plus the ones its Ignore Transforms leave out. Those are not simulated but still ride on the chain, so cloth on them must follow the shadows too (docs 10-70).
            /// It stops at a bone another PhysBone simulates (that chain sorts its own bones), and once kept, at a bone an animation or a constraint moves (a shadow copies rotation only)</summary>
            public List<Transform> reach;
            /// <summary>Reach bones that carry rolled cloth, over every garment</summary>
            public readonly HashSet<Transform> hit = new HashSet<Transform>();
            public SkinnedMeshRenderer cloth;
            public readonly List<Transform> shadows = new List<Transform>();
            public readonly HashSet<SkinnedMeshRenderer> garments = new HashSet<SkinnedMeshRenderer>();
        }

        readonly GameObject root;
        readonly Animator animator;
        readonly SleeveRollShape.Cache cache;
        readonly StringBuilder log;
        readonly List<(VRCPhysBone pb, Transform root, List<Transform> bones, List<Transform> reach)> all = new List<(VRCPhysBone, Transform, List<Transform>, List<Transform>)>();
        readonly Dictionary<VRCPhysBone, Chain> candidates = new Dictionary<VRCPhysBone, Chain>();
        readonly Dictionary<(SkinnedMeshRenderer, bool), string> fails = new Dictionary<(SkinnedMeshRenderer, bool), string>();
        readonly HashSet<Transform> keep = new HashSet<Transform>();
        readonly HashSet<Transform> humanoid = new HashSet<Transform>();
        /// <summary>Bones an animation or a constraint moves, left off the shadows with everything under them (Undriven)</summary>
        readonly HashSet<Transform> driven = new HashSet<Transform>();

        public readonly List<Chain> Chains = new List<Chain>();
        /// <summary>Bones of kept chains — their weights must not move to the wrist proxy</summary>
        public ICollection<Transform> Keep => keep;

        public SleeveRollPb(GameObject root, Animator animator, SleeveRollShape.Cache cache, StringBuilder log)
        {
            this.root = root; this.animator = animator; this.cache = cache; this.log = log;
            var upperL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var upperR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var simulated = new HashSet<Transform>();
            foreach (var pb in root.GetComponentsInChildren<VRCPhysBone>(true))
            {
                if (!pb.enabled) continue;   // off in the prefab: it does not move, nothing to keep — and its bones are free for another chain's reach (docs 10-70)
                var rt = pb.rootTransform != null ? pb.rootTransform : pb.transform;
                if (!(upperL != null && rt.IsChildOf(upperL)) && !(upperR != null && rt.IsChildOf(upperR))) continue;   // hair, tails: never a sleeve
                var ignored = new HashSet<Transform>((pb.ignoreTransforms ?? new List<Transform>()).Where(t => t != null));
                var bones = new List<Transform>();
                Affected(rt, ignored, bones);
                if (bones.Count == 0) continue;
                simulated.UnionWith(bones);
                all.Add((pb, rt, bones, null));
            }
            for (int i = 0; i < all.Count; i++)
            {
                var reach = new List<Transform>();
                Reach(all[i].root, new HashSet<Transform>(all[i].bones), simulated, reach);
                all[i] = (all[i].pb, all[i].root, all[i].bones, reach);
            }
        }

        // a bone left out by Ignore Transforms that another PhysBone simulates is that one's — stop there
        static void Reach(Transform t, HashSet<Transform> own, HashSet<Transform> simulated, List<Transform> into)
        {
            if (!own.Contains(t) && simulated.Contains(t)) return;
            into.Add(t);
            for (int i = 0; i < t.childCount; i++) Reach(t.GetChild(i), own, simulated, into);
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
            foreach (var (pb, rt, bones, reach) in all)
            {
                if (!rt.IsChildOf(upper)) continue;
                if (!rt.IsChildOf(lower))
                {
                    if (SleeveRollShape.Reaches(cache, cloth, animator, left, bones, ReachT, out var t))
                        fails[(cloth, left)] = $"a PhysBone under the upper arm ({rt.name}) holds this sleeve down to t={t:F2} — it cannot be rolled";
                    continue;
                }
                var hit = reach.Where(rolled.Contains).ToList();
                if (hit.Count == 0) continue;
                if (!candidates.TryGetValue(pb, out var c))
                    candidates[pb] = c = new Chain { pb = pb, left = left, root = rt, lower = lower, hand = hand, bones = bones, reach = reach, cloth = cloth, underHand = rt.IsChildOf(hand) };
                c.garments.Add(cloth);
                c.hit.UnionWith(hit);
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
                // only bones its Ignore Transforms leave out carry rolled cloth: those are an extra, never a reason to fail this side — left as 0.1.1 did (docs 10-70)
                bool direct = c.bones.Any(c.hit.Contains);
                if (why != null && !direct)
                {
                    log.AppendLine($"  {c.root.name}: PhysBone left as it is ({why}) — only bones it leaves out carry rolled cloth");
                    continue;
                }
                if (why == null)
                {
                    c.reach = Undriven(c, animated);
                    if (!direct && !c.reach.Any(c.hit.Contains))
                    {
                        log.AppendLine($"  {c.root.name}: PhysBone left as it is — the bones it leaves out that carry rolled cloth are moved by an animation or a constraint");
                        continue;
                    }
                    Chains.Add(c);
                    foreach (var b in c.reach) if (!humanoid.Contains(b)) keep.Add(b);
                    log.AppendLine($"  {c.root.name}: PhysBone kept — still while rolled");
                }
                else if (c.underHand)
                    log.AppendLine($"WARN {c.root.name}: this sleeve's PhysBone stops moving ({why})");
                else
                    foreach (var g in c.garments)
                        fails[(g, c.left)] = $"a PhysBone on the forearm ({c.root.name}) holds this sleeve and cannot be held still while rolled ({why})";
            }
            // two kept PhysBones moving the same bone — the outfit's physics fights itself (docs 10-68 F2, F3a). A shared root is left out: two PhysBones splitting one root with Ignore Transforms roll fine (10-68), so there is nothing to tell
            var warned = new HashSet<(int, int)>();
            for (int x = 0; x < Chains.Count; x++)
                for (int y = 0; y < Chains.Count; y++)
                {
                    if (x == y) continue;
                    var c = Chains[x]; var o = Chains[y];
                    var b = c.bones.FirstOrDefault(t => t != c.root && o.bones.Contains(t));
                    if (b != null && warned.Add((System.Math.Min(x, y), System.Math.Max(x, y))))
                        log.AppendLine($"WARN {c.root.name}: another PhysBone ({o.root.name}) also moves {b.name} — the two fight over it; the sleeve still rolls");
                }
        }

        public string FailReason(SkinnedMeshRenderer cloth, bool left) => fails.TryGetValue((cloth, left), out var m) ? m : null;

        public int KeptOn(SkinnedMeshRenderer cloth, bool left) => Chains.Count(c => c.left == left && c.garments.Contains(cloth));

        // a bone the PhysBone leaves out that an animation or a constraint moves keeps its own motion: a shadow would copy only its rotation. It and everything under it stay off the shadows (docs 10-70)
        List<Transform> Undriven(Chain c, HashSet<string> animated)
        {
            var off = new HashSet<Transform>();
            var kept = new List<Transform>();
            foreach (var b in c.reach)   // parents first
            {
                if (off.Contains(b.parent)) { off.Add(b); driven.Add(b); continue; }
                if (!c.bones.Contains(b) && (animated.Contains("T|" + Path(b)) || b.GetComponent<UnityEngine.Animations.IConstraint>() != null || b.GetComponent<VRC.Dynamics.VRCConstraintBase>() != null))
                {
                    off.Add(b); driven.Add(b);
                    log.AppendLine($"  {c.root.name}: {b.name} stays off the shadows — an animation or a constraint moves it");
                    continue;
                }
                kept.Add(b);
            }
            return kept;
        }

        // English goes to the build record. null = safe
        string Blocked(Chain c, HashSet<string> animated, HashSet<Object> merged)
        {
            var pb = c.pb;
            if (c.bones.Any(humanoid.Contains)) return "it moves a body bone";
            if (c.bones.Any(b => !b.IsChildOf(c.lower))) return "it also moves bones outside this forearm";
            if (pb.isAnimated) return "Is Animated is on";
            if (pb.maxStretch > 0f || pb.maxSquish > 0f) return "Max Stretch or Max Squish is set";
            if (merged.Contains(pb)) return "an Avatar Optimizer Merge PhysBone uses it";
            if (animated.Contains("E|" + Path(pb.transform))) return "an animation already turns it on and off";
            if (c.bones.Any(b => animated.Contains("T|" + Path(b)))) return "an animation already moves its bones";
            var ce = cache.Get(c.cloth);
            foreach (var b in c.bones)
            {
                // real bones: a chain's parent may be an outfit bone MA kept (docs 10-73) — compare against what the renderer really uses
                int i = System.Array.IndexOf(ce.rawBones, b), pi = System.Array.IndexOf(ce.rawBones, b.parent);
                if (i < 0 || pi < 0) continue;
                var m = ce.rawBindposes[pi] * ce.rawBindposes[i].inverse;
                float meters = Vector3.Distance(b.localPosition, m.GetColumn(3)) * b.parent.lossyScale.x;
                if (Quaternion.Angle(b.localRotation, m.rotation) > MaxAngle || meters > MaxMeters) return "its rest pose differs from the mesh's bind pose";
            }
            return null;
        }

        /// <summary>
        /// After the weights moved: chains under the hand go under the wrist proxy (when there is one), then each chain gets shadow bones and the rolled garments skin to them.
        /// A shadow is a Rotation Constraint with the real bone as its source: weight 1 = the PhysBone's rotation (sleeve down), 0 = its rest = the bind pose (rolled) — the same mechanism as the wrist proxy.
        /// FX never writes the PhysBone or its bones: writing the bones worked in Unity Play but in VRChat the rolled cloth stayed bent (docs 10-38).
        /// One shadow per bone, shared by every chain, and a chain's shadows hang under the shadow of the real parent when it has one (docs 10-68):
        /// two PhysBones splitting one root get one root shadow, and a PhysBone that starts inside another kept chain stays where it is instead of moving under the proxy.
        /// </summary>
        public void Reparent()
        {
            var shadowOf = new Dictionary<Transform, Transform>();
            // a chain that starts inside another kept chain: every bone from its root's parent up to the outermost kept bone around it needs a shadow,
            // even with no cloth on it, or the inner shadows would hang from a real bone that the outer PhysBone keeps moving while rolled
            var between = new Dictionary<SleeveRollPb.Chain, List<Transform>>();
            var forced = new HashSet<Transform>();
            // a chain under a bone an animation or a constraint moves stays on it: under the proxy it would lose that motion, and a shadow would copy only the rotation (docs 10-70)
            var underDriven = new HashSet<SleeveRollPb.Chain>();
            foreach (var c in Chains)
            {
                var path = new List<Transform>();
                int top = -1;
                for (var t = c.root.parent; t != null && t != c.lower; t = t.parent)
                {
                    if (driven.Contains(t)) { underDriven.Add(c); top = -1; break; }
                    path.Add(t);
                    if (keep.Contains(t)) top = path.Count - 1;
                }
                if (top < 0) continue;
                path.RemoveRange(top + 1, path.Count - top - 1);
                path.Reverse();   // parents first
                between[c] = path;
                forced.UnionWith(path);
            }
            foreach (var c in Chains.OrderBy(c => Depth(c.root)))   // outer chains first, so an inner root finds its parent's shadow (stable: same order as before otherwise)
            {
                var ce = cache.Get(c.cloth);
                int hi = System.Array.IndexOf(ce.bones, c.hand);
                bool inside = between.ContainsKey(c);
                bool stay = inside || underDriven.Contains(c);
                var proxy = c.underHand && !stay ? c.lower.Find(SrSide.ProxyName(c.left)) : null;
                if (proxy != null && c.root.parent != proxy)
                {
                    c.root.SetParent(proxy, true);
                    log.AppendLine($"  {c.root.name}: moved under {proxy.name}");
                }
                if (inside) log.AppendLine($"  {c.root.name}: inside another kept PhysBone — stays where it is");
                else if (underDriven.Contains(c)) log.AppendLine($"  {c.root.name}: under a bone an animation or a constraint moves — stays where it is");
                // a bone with no cloth on it or below it (usually the chain's end) needs no shadow: nothing would follow it
                var needed = new HashSet<Transform>();
                foreach (var g in c.garments) needed.UnionWith(SleeveRollShape.WeightedBones(g));
                var reach = c.reach.Where(keep.Contains).ToList();
                needed.IntersectWith(reach);
                needed.UnionWith(reach.Where(forced.Contains));
                for (int k = reach.Count - 1; k >= 0; k--)   // children come last, so walk up from them
                    if (needed.Contains(reach[k]) && reach[k] != c.root) needed.Add(reach[k].parent);
                // bones on the way in that no chain moves (an outer PhysBone's Ignore Transforms): their shadow just carries the inner one along
                if (inside)
                    foreach (var t in between[c])
                    {
                        if (shadowOf.ContainsKey(t)) continue;
                        int i = System.Array.IndexOf(ce.rawBones, t), pi = System.Array.IndexOf(ce.rawBones, t.parent);
                        var rest = i >= 0 && pi >= 0 ? (ce.rawBindposes[pi] * ce.rawBindposes[i].inverse).rotation : t.localRotation;
                        var s = Shadow(t, shadowOf.TryGetValue(t.parent, out var ps) ? ps : t.parent, rest);
                        shadowOf[t] = s;
                        c.shadows.Add(s);
                    }
                foreach (var b in reach)   // parents come first
                {
                    if (!needed.Contains(b) || shadowOf.ContainsKey(b)) continue;   // another chain made it already
                    int i = System.Array.IndexOf(ce.rawBones, b);
                    bool onProxy = b == c.root && b.parent == proxy && proxy != null;
                    // the proxy's bind pose is the avatar hand's (retargeted, like the proxy's own — docs 10-73); any other parent is a real bone
                    int pi = onProxy ? hi : System.Array.IndexOf(ce.rawBones, b.parent);
                    var parentBp = pi < 0 ? default : onProxy ? ce.bindposes[pi] : ce.rawBindposes[pi];
                    var rest = i >= 0 && pi >= 0 ? (parentBp * ce.rawBindposes[i].inverse).rotation : b.localRotation;
                    var s = Shadow(b, shadowOf.TryGetValue(b.parent, out var ps) ? ps : b.parent, rest);
                    shadowOf[b] = s;
                    c.shadows.Add(s);
                }
                log.AppendLine($"  {c.root.name}: {c.shadows.Count} shadow bones");
            }
            // a garment moves to the shadows of the chains that roll it — not to a chain that only holds another garment
            foreach (var c in Chains)
            {
                var mine = new HashSet<Transform>(c.reach.Where(keep.Contains));
                if (between.TryGetValue(c, out var way)) mine.UnionWith(way);
                foreach (var g in c.garments)
                {
                    var bones = g.bones;
                    for (int i = 0; i < bones.Length; i++)
                        if (bones[i] != null && mine.Contains(bones[i]) && shadowOf.TryGetValue(bones[i], out var s)) bones[i] = s;
                    g.bones = bones;
                }
            }
        }

        static int Depth(Transform t)
        {
            int d = 0;
            for (; t != null; t = t.parent) d++;
            return d;
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
