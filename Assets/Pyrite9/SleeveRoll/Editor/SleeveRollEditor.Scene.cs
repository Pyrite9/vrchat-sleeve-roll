// Pyrite9 Sleeve Roll — inspector: the avatar this is in, the sleeved garments in it, and the rings drawn in the scene view
//   The avatar lookups (descriptor, every Sleeve Roll, every skinned mesh) are cached until the hierarchy changes — the 300 ms poll
//   and the 50 ms ring check only read the cache. A garment's candidate test (all its bone weights) is remembered per mesh.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Pyrite9.Sleeve
{
    public partial class SleeveRollEditor
    {
        // ---------------------------------------------------------------- avatar cache
        // bumped on any hierarchy change (an object added, removed, moved, renamed, a component added) — the cache reads again then
        static int hierVersion;
        [InitializeOnLoadMethod] static void HookHierarchy() => EditorApplication.hierarchyChanged += () => hierVersion++;

        int cacheVer = -1;
        VRCAvatarDescriptor cDesc;
        SleeveRoll[] cSibs = new SleeveRoll[0];
        SkinnedMeshRenderer[] cSmrs = new SkinnedMeshRenderer[0];
        Animator cAnim;

        void ReadAvatar()
        {
            // a destroyed descriptor also comes with a hierarchy change; the null check covers the frame in between
            if (cacheVer == hierVersion && (cDesc != null || ReferenceEquals(cDesc, null))) return;
            cacheVer = hierVersion;
            cDesc = Cfg.GetComponentInParent<VRCAvatarDescriptor>(true);
            cSibs = cDesc != null ? cDesc.GetComponentsInChildren<SleeveRoll>(true) : new[] { Cfg };
            cSmrs = cDesc != null ? cDesc.GetComponentsInChildren<SkinnedMeshRenderer>(true) : new SkinnedMeshRenderer[0];
            cAnim = cDesc != null ? cDesc.GetComponent<Animator>() : null;
        }

        VRCAvatarDescriptor Desc { get { ReadAvatar(); return cDesc; } }

        // every Sleeve Roll in the avatar, in hierarchy order — the first one with a garment decides the menu
        SleeveRoll[] Siblings() { ReadAvatar(); return cSibs; }

        // the garments without building a list: targets + the old single target (as SleeveRoll.Garments)
        static bool HasGarment(SleeveRoll s)
        {
            if (s == null) return false;
            if (s.target != null) return true;
            if (s.targets != null) foreach (var t in s.targets) if (t != null) return true;
            return false;
        }

        static int GarmentHash(SleeveRoll s)
        {
            unchecked
            {
                if (s == null) return 0;
                int h = s.target != null ? s.target.GetInstanceID() : 17;
                if (s.targets != null) foreach (var t in s.targets) h = h * 31 + (t != null ? t.GetInstanceID() : 0);
                return h;
            }
        }

        // what the picked lists look like across the avatar — the garment block refreshes in place when it changes
        int PickState()
        {
            unchecked
            {
                int h = 7;
                foreach (var s in Siblings()) h = h * 31 + GarmentHash(s);
                return h;
            }
        }

        // the Sleeve Roll whose menu settings the build uses: the first one with a garment picked (as SleeveRollPlugin)
        SleeveRoll MenuOwner()
        {
            var sibs = Siblings();
            foreach (var s in sibs) if (HasGarment(s)) return s;
            return sibs.Length > 0 && sibs[0] != null ? sibs[0] : Cfg;
        }

        // ---------------------------------------------------------------- garment candidates
        // sleeved garments in the avatar: a skinned mesh with weights on a forearm bone and on that side's hand bone. Before the build
        // an outfit may still have its own armature (MA Merge Armature), so its bones are matched by the avatar's bone names too.
        // Body-looking meshes (name has 'body') are left out — the build refuses them anyway (thinner than the skin). Most forearm
        // vertices first, at most 8. The count per renderer is remembered until its mesh, bones or the avatar's bone names change
        struct CandMemo { public int mesh, bones; public string names; public int n; }
        static readonly Dictionary<int, CandMemo> candMemo = new Dictionary<int, CandMemo>();

        List<SkinnedMeshRenderer> Candidates()
        {
            ReadAvatar();
            var anim = cAnim;
            if (anim == null || !anim.isHuman) return new List<SkinnedMeshRenderer>();
            var names = new HashSet<string>();
            var hands = new HashSet<string>();
            foreach (bool left in SrSide.Both)
            {
                var lower = anim.GetBoneTransform(SrSide.Lower(left));
                var hand = anim.GetBoneTransform(SrSide.Hand(left));
                if (lower != null) names.Add(lower.name);
                if (hand != null) hands.Add(hand.name);
            }
            string namesKey = string.Join("/", names) + "|" + string.Join("/", hands);
            var list = new List<(SkinnedMeshRenderer r, int n)>();
            foreach (var r in cSmrs)
            {
                if (r == null || r.sharedMesh == null || r.name.ToLowerInvariant().Contains("body")) continue;
                var bones = r.bones;
                int id = r.GetInstanceID(), meshId = r.sharedMesh.GetInstanceID();
                if (!candMemo.TryGetValue(id, out var m) || m.mesh != meshId || m.bones != bones.Length || m.names != namesKey)
                {
                    m = new CandMemo { mesh = meshId, bones = bones.Length, names = namesKey, n = CountForearm(r.sharedMesh, bones, names, hands) };
                    candMemo[id] = m;
                }
                if (m.n >= 20) list.Add((r, m.n));
            }
            return list.OrderByDescending(x => x.n).Take(8).Select(x => x.r).ToList();
        }

        // forearm vertices (weight ≥ 0.3 on a lower arm bone), 0 when the mesh has no hand weight at all
        static int CountForearm(Mesh mesh, Transform[] bones, HashSet<string> names, HashSet<string> hands)
        {
            bool IsLower(Transform b)
            {
                if (b == null) return false;
                var n = b.name.ToLowerInvariant();
                return names.Contains(b.name) || n.Contains("lowerarm") || n.Contains("forearm");
            }
            bool IsHand(Transform b)
            {
                if (b == null) return false;
                var n = b.name.ToLowerInvariant();
                return hands.Contains(b.name) || n.EndsWith("hand") || n.Contains("hand_") || n.Contains("hand.");
            }
            var lower = new bool[bones.Length];
            var hand = new bool[bones.Length];
            bool anyLowerBone = false, anyHandBone = false;
            for (int i = 0; i < bones.Length; i++)
            {
                lower[i] = IsLower(bones[i]); hand[i] = IsHand(bones[i]);
                anyLowerBone |= lower[i]; anyHandBone |= hand[i];
            }
            if (!anyLowerBone || !anyHandBone) return 0;
            int nLower = 0; bool anyHand = false;
            var counts = mesh.GetBonesPerVertex();
            var weights = mesh.GetAllBoneWeights();
            int k = 0;
            for (int v = 0; v < counts.Length; v++)
            {
                bool onLower = false;
                for (int j = 0; j < counts[v]; j++, k++)
                {
                    var w = weights[k];
                    if (w.weight <= 0f || w.boneIndex >= bones.Length) continue;
                    if (lower[w.boneIndex] && w.weight >= 0.3f) onLower = true;
                    if (hand[w.boneIndex]) anyHand = true;
                }
                if (onLower) nLower++;
            }
            return anyHand ? nLower : 0;
        }

        // ---------------------------------------------------------------- scene view: where the cuff ends up (03 roll)
        // on the rest pose: a gold ring on each rolled forearm at the cuff's place — the last build's value for these garments when there
        // is one (the build may stop at the shrink edge, or go further past a gap in the skin; layered: the outermost), else 'Roll to'; a faint ring
        // where the sleeve starts to move. Hooked on duringSceneGui, not OnSceneGUI: Unity skips OnSceneGUI while the Gizmos button is off.
        // Drawn on Repaint only, no lists per repaint: the build record is read on the poll (ringRec), its value per side kept in ringBuilt
        void OnEnable() => SceneView.duringSceneGui += DrawRings;
        void OnDisable() => SceneView.duringSceneGui -= DrawRings;

        SrBuildRecord.Entry ringRec;
        readonly float[] ringBuilt = { -1f, -1f };   // left, right: where the last build put the cuff of these garments, −1 = no record
        int ringBuiltKey = int.MinValue;
        int ringsDrawn;

        // the last build of this avatar (read on Build, not on every scene event)
        void ReadRecord()
        {
            var d = Desc;
            ringRec = d != null ? SrBuildRecord.Get(d.gameObject.name) : null;
        }

        void ReadRingBuilt(SleeveRoll c)
        {
            var rec = (Desc != null ? SleeveRollPreview.Result(Desc.gameObject) : null) ?? ringRec;
            int k;
            unchecked { k = GarmentHash(c) * 31 + (rec != null ? rec.time.GetHashCode() : 0); }
            if (k == ringBuiltKey) return;
            ringBuiltKey = k;
            ringBuilt[0] = ringBuilt[1] = -1f;
            if (rec == null || rec.garments.Count == 0) return;
            var names = new HashSet<string>(c.Garments.Select(x => x.name));
            foreach (var g in rec.garments)
            {
                if (!names.Contains(g.name)) continue;
                if (g.left.ok) ringBuilt[0] = Mathf.Max(ringBuilt[0], g.left.rolledTo);
                if (g.right.ok) ringBuilt[1] = Mathf.Max(ringBuilt[1], g.right.rolledTo);
            }
        }

        // repaint when what the rings show changed (a hash — no strings, no lists)
        void RepaintRingsIfChanged()
        {
            if (target == null) return;
            var c = Cfg;
            int k;
            unchecked
            {
                k = c.rollTo.GetHashCode() + (c.unhideWhenRolled ? 1 : 2);
                k = k * 31 + c.startAt.GetHashCode();
                k = k * 31 + (c.left ? 1 : 2) + (c.right ? 4 : 8);
                k = k * 31 + GarmentHash(c);
                k = k * 31 + (ringRec != null ? ringRec.time.GetHashCode() : 0);
                k = k * 31 + SleeveRollPreview.Revision + (SleeveRollPreview.Toggle.IsEnabled.Value ? 1 : 0);
            }
            if (k != ringsDrawn) { ringsDrawn = k; SceneView.RepaintAll(); }
        }

        void DrawRings(SceneView view)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            if (target == null || Application.isPlaying) return;
            var c = Cfg;
            if (!c.isActiveAndEnabled) return;
            ReadAvatar();
            var anim = cAnim;
            if (anim == null || !anim.isHuman) return;
            ReadRingBuilt(c);
            var prevZ = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            foreach (bool left in SrSide.Both)
            {
                if (!SrSide.On(c, left)) continue;
                var lower = anim.GetBoneTransform(SrSide.Lower(left));
                var hand = anim.GetBoneTransform(SrSide.Hand(left));
                if (lower == null || hand == null) continue;
                var a = lower.position; var b = hand.position;
                var axis = b - a;
                float len = axis.magnitude;
                if (len < 1e-4f) continue;
                axis /= len;
                float r = len * 0.24f;
                float built = ringBuilt[left ? 0 : 1];
                // preview on: where it really goes, right now. Off: the record may be older than the slider — the slider decides, unless the shrink stops it
                float to = built < 0f ? c.rollTo : SleeveRollPreview.Toggle.IsEnabled.Value ? built : c.unhideWhenRolled ? c.rollTo : Mathf.Max(built, c.rollTo);
                float from = Mathf.Min(c.startAt, to - 0.05f);
                Handles.color = new Color(Gold.r, Gold.g, Gold.b, 0.3f);
                Handles.DrawWireDisc(Vector3.Lerp(a, b, from), axis, r * 0.9f, 1.5f);
                Handles.color = Gold;
                Handles.DrawWireDisc(Vector3.Lerp(a, b, to), axis, r, 2.5f);
            }
            Handles.zTest = prevZ;
        }
    }
}
