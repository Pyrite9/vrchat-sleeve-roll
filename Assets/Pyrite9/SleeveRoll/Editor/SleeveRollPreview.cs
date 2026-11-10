// Pyrite9 Sleeve Roll — preview: the rolled sleeves in the Scene view, no build (NDMF preview, on the inspector's 03 ROLL switch)
//   Runs the same calculation as the build (SleeveRollBake in preview mode) on the avatar as it is in the scene. Nothing on the
//   renderers, the scene or the mesh assets changes: the meshes go on NDMF's proxy renderers and are destroyed with the node.
//   Differences from a build, on purpose: no wrist proxy (the rest pose doesn't need it), no FX or menu. An outfit that isn't merged
//   yet (MA Merge Armature) is read through its bone mapping; the proxies give the shape values other previews set (MA Shape Changer).
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace Pyrite9.Sleeve
{
    internal sealed class SleeveRollPreview : IRenderFilter
    {
        /// <summary>On = sleeves shown rolled. Off by default; NDMF keeps its state per project and lists it in its own preview settings</summary>
        internal static readonly TogglablePreviewNode Toggle = TogglablePreviewNode.Create(
            () => "Sleeve Roll (rolled sleeves)", "pyrite9.sleeve-roll/RolledPreview", false);

        /// <summary>Latest result per avatar (instance id) — the inspector shows where the cuff went and any warning; the time field holds the revision</summary>
        static readonly Dictionary<int, SrBuildRecord.Entry> results = new Dictionary<int, SrBuildRecord.Entry>();
        internal static int Revision { get; private set; }

        internal static SrBuildRecord.Entry Result(GameObject root)
            => root != null && Toggle.IsEnabled.Value && results.TryGetValue(root.GetInstanceID(), out var e) ? e : null;

        public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes() { yield return Toggle; }

        public bool IsEnabled(ComputeContext context) => context.Observe(Toggle.IsEnabled);

        // one group per avatar: the picked garments and the meshes the sleeve may uncover (shape keys + forearm bone) — the body's arm comes back with the same switch
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var groups = ImmutableList.CreateBuilder<RenderGroup>();
            foreach (var root in context.GetAvatarRoots())
            {
                if (context.ActiveInHierarchy(root) != true) continue;
                var comps = context.GetComponentsInChildren<SleeveRoll>(root, true);
                if (comps.Length == 0) continue;
                var set = new HashSet<Renderer>();
                foreach (var c in comps)
                {
                    context.Observe(c, x => GarmentKey(x));
                    foreach (var g in c.Garments) if (g != null && g.transform.IsChildOf(root.transform)) set.Add(g);
                }
                if (set.Count == 0) continue;
                var animator = context.GetComponent<Animator>(root);
                if (animator == null || !animator.isHuman) continue;
                var lowerL = animator.GetBoneTransform(SrSide.Lower(true));
                var lowerR = animator.GetBoneTransform(SrSide.Lower(false));
                // a body mesh that comes with an outfit is still on the outfit's bones here — read it through the merge mapping like the build
                var merge = MergeMap(context, root);
                foreach (var r in context.GetComponentsInChildren<SkinnedMeshRenderer>(root, true))
                {
                    if (set.Contains(r)) continue;
                    var mesh = context.Observe(r, x => x.sharedMesh);
                    if (mesh == null || mesh.blendShapeCount == 0) continue;
                    foreach (var b in r.bones)
                    {
                        if (b == null) continue;
                        var to = merge.TryGetValue(b, out var m) ? m : b;
                        if (to == lowerL || to == lowerR) { set.Add(r); break; }
                    }
                }
                groups.Add(RenderGroup.For(set));
            }
            return groups.ToImmutable();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var meshes = new Dictionary<SkinnedMeshRenderer, Mesh>();
            try
            {
                var pairs = proxyPairs.ToList();
                var root = pairs.Count > 0 ? context.GetAvatarRoot(pairs[0].Item1.gameObject) : null;
                var animator = root != null ? context.GetComponent<Animator>(root) : null;
                if (animator == null || !animator.isHuman) return Task.FromResult<IRenderFilterNode>(new Node(meshes));
                var comps = context.GetComponentsInChildren<SleeveRoll>(root, true);
                foreach (var c in comps) context.Observe(c, x => SettingsKey(x));
                var cache = new SleeveRollShape.Cache
                {
                    proxies = new Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer>(),
                    boneMap = MergeMap(context, root),
                };
                foreach (var (original, proxy) in pairs)
                {
                    context.Observe(original, x => x is SkinnedMeshRenderer s ? s.sharedMesh : null);
                    if (original is SkinnedMeshRenderer o && proxy is SkinnedMeshRenderer p) cache.proxies[o] = p;
                }
                var rec = SleeveRollBake.Preview(root, animator, comps, cache, meshes);
                rec.time = (++Revision).ToString();
                results[root.GetInstanceID()] = rec;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                foreach (var m in meshes.Values) if (m != null) Object.DestroyImmediate(m);
                meshes.Clear();
            }
            return Task.FromResult<IRenderFilterNode>(new Node(meshes));
        }

        // outfit bones not merged yet → the avatar's bones (what the build sees after MA)
        static Dictionary<Transform, Transform> MergeMap(ComputeContext context, GameObject root)
            => SleeveRollShape.MergeMap(context.GetComponentsInChildren<ModularAvatarMergeArmature>(root, true));

        static string GarmentKey(SleeveRoll c) => string.Join(",", c.Garments.Select(g => g.GetInstanceID()));

        // every setting the shape depends on
        static string SettingsKey(SleeveRoll c) => string.Join("|", new object[]
        {
            GarmentKey(c), c.left, c.right, c.unhideWhenRolled, c.rollTo, c.startAt, c.margin, c.looseness, c.foldAmplitude, c.foldLength,
            c.bodyMeshes != null ? string.Join(",", c.bodyMeshes.Where(b => b != null).Select(b => b.GetInstanceID())) : "",
        });

        sealed class Node : IRenderFilterNode
        {
            readonly Dictionary<Renderer, (Mesh mesh, int[] on)> shown = new Dictionary<Renderer, (Mesh, int[])>();

            public RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Shapes;

            public Node(Dictionary<SkinnedMeshRenderer, Mesh> meshes)
            {
                foreach (var kv in meshes)
                {
                    var on = new List<int>();
                    foreach (bool left in SrSide.Both)
                        foreach (var name in new[] { SrSide.ShapeName(left), SrSide.UnhideName(left) })
                        {
                            int i = kv.Value.GetBlendShapeIndex(name);
                            if (i >= 0) on.Add(i);
                        }
                    shown[kv.Key] = (kv.Value, on.ToArray());
                }
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (!(proxy is SkinnedMeshRenderer smr) || !shown.TryGetValue(original, out var s)) return;
                smr.sharedMesh = s.mesh;
                foreach (var i in s.on) smr.SetBlendShapeWeight(i, 100f);
            }

            public void Dispose()
            {
                foreach (var s in shown.Values) if (s.mesh != null) Object.DestroyImmediate(s.mesh);
                shown.Clear();
            }
        }
    }
}
