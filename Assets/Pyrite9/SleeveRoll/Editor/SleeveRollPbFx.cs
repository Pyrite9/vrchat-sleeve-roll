// Pyrite9 Sleeve Roll — FX curves for kept PhysBones. Runs after the shapes, because the shadow bones exist only then
using System.Collections.Generic;
using System.Linq;
using System.Text;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace Pyrite9.Sleeve
{
    /// <summary>
    /// The shadow constraints' GlobalWeight goes into the Down and Up states of the existing 'Sleeve Roll' layer: Down (default, shadow bones copy the PhysBone) ↔ Up (Hold on, shadow bones at the bind pose).
    /// Same Hold, same blend and no shared curves, so a layer of its own would do the same work for one more layer. The PhysBone itself keeps running, untouched.
    /// The wrist proxies' scale goes into the same states (audit P2): Down = the hand bone's scale, Up = the hand's scale in the garment's bind pose — only on avatars that scale the hand.
    /// So do the finger shadows (audit P3): GlobalWeight Down 1 / Up 0 like the PhysBone shadows, plus their position and scale when the bind pose differs from the bone as it is.
    /// And the follow shadows (0.1.3-beta4, VRC Parent Constraint): GlobalWeight Down 1 / Up 0.
    /// </summary>
    static class SleeveRollPbFx
    {
        const float Short = 1f / 60f;

        public static void Add(BuildContext ctx, IList<SleeveRollPb.Chain> chains, IList<(Transform proxy, Vector3 down, Vector3 up)> proxyScales,
            IList<SleeveRollBake.FingerShadow> fingers, IList<SleeveRollBake.FollowShadow> follows, StringBuilder log)
        {
            // only here, not as a pass requirement: an avatar without a kept PhysBone keeps its controller exactly as it was
            var asc = ctx.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            try
            {
                var fx = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
                var layer = fx.Layers.FirstOrDefault(l => l.Name == SleeveRollDriver.LayerName && l.StateMachine != null);
                var states = layer != null ? layer.StateMachine.AllStates().ToList() : new List<VirtualState>();
                var down = states.FirstOrDefault(st => st.Name == "Down")?.Motion as VirtualClip;
                var up = states.FirstOrDefault(st => st.Name == "Up")?.Motion as VirtualClip;
                if (down == null || up == null)
                {
                    if (chains.Count > 0) log.AppendLine($"WARN FX: layer '{SleeveRollDriver.LayerName}' not found — kept PhysBones will not hold still while rolled");
                    if (proxyScales.Count > 0) log.AppendLine($"WARN FX: layer '{SleeveRollDriver.LayerName}' not found — the wrist proxy keeps the hand scale while rolled");
                    if (fingers.Count > 0) log.AppendLine($"WARN FX: layer '{SleeveRollDriver.LayerName}' not found — the rolled cuff keeps following the fingers");
                    if (follows.Count > 0) log.AppendLine($"WARN FX: layer '{SleeveRollDriver.LayerName}' not found — the rolled cuff keeps following bones that move on their own");
                    return;
                }
                var paths = asc.ObjectPathRemapper;
                if (chains.Count > 0)
                {
                    int curves = 0;
                    foreach (var t in chains.SelectMany(c => c.shadows))
                    {
                        var path = paths.GetVirtualPathForObject(t);
                        Curve(down, path, typeof(VRCRotationConstraint), "GlobalWeight", 1f);
                        Curve(up, path, typeof(VRCRotationConstraint), "GlobalWeight", 0f);
                        curves++;
                    }
                    log.AppendLine($"FX: layer '{layer.Name}', {chains.Count} PhysBones, {curves} shadow curves");
                }
                foreach (var (proxy, sDown, sUp) in proxyScales)
                {
                    var path = paths.GetVirtualPathForObject(proxy);
                    Curve(down, path, typeof(Transform), "m_LocalScale.x", sDown.x);
                    Curve(down, path, typeof(Transform), "m_LocalScale.y", sDown.y);
                    Curve(down, path, typeof(Transform), "m_LocalScale.z", sDown.z);
                    Curve(up, path, typeof(Transform), "m_LocalScale.x", sUp.x);
                    Curve(up, path, typeof(Transform), "m_LocalScale.y", sUp.y);
                    Curve(up, path, typeof(Transform), "m_LocalScale.z", sUp.z);
                }
                if (proxyScales.Count > 0) log.AppendLine($"FX: layer '{layer.Name}', wrist proxy scale curves on {string.Join(", ", proxyScales.Select(p => p.proxy.name))}");
                foreach (var f in fingers)
                {
                    var path = paths.GetVirtualPathForObject(f.shadow);
                    Curve(down, path, typeof(VRCRotationConstraint), "GlobalWeight", 1f);
                    Curve(up, path, typeof(VRCRotationConstraint), "GlobalWeight", 0f);
                    if (f.PosCurve) Vec(down, up, path, "m_LocalPosition", f.downPos, f.upPos);
                    if (f.ScaleCurve) Vec(down, up, path, "m_LocalScale", f.downScale, f.upScale);
                }
                if (fingers.Count > 0)
                    log.AppendLine($"FX: layer '{layer.Name}', {fingers.Count} finger shadow curves" +
                                   (fingers.Any(f => f.PosCurve || f.ScaleCurve) ? $", position/scale on {string.Join(", ", fingers.Where(f => f.PosCurve || f.ScaleCurve).Select(f => f.shadow.name))}" : ""));
                foreach (var f in follows)
                {
                    var path = paths.GetVirtualPathForObject(f.shadow);
                    Curve(down, path, typeof(VRCParentConstraint), "GlobalWeight", 1f);
                    Curve(up, path, typeof(VRCParentConstraint), "GlobalWeight", 0f);
                }
                if (follows.Count > 0) log.AppendLine($"FX: layer '{layer.Name}', {follows.Count} follow shadow curves");
            }
            finally
            {
                ctx.DeactivateExtensionContext<AnimatorServicesContext>();
                ctx.DeactivateExtensionContext<VirtualControllerContext>();
            }
        }

        static void Vec(VirtualClip down, VirtualClip up, string path, string prop, Vector3 d, Vector3 u)
        {
            Curve(down, path, typeof(Transform), prop + ".x", d.x);
            Curve(down, path, typeof(Transform), prop + ".y", d.y);
            Curve(down, path, typeof(Transform), prop + ".z", d.z);
            Curve(up, path, typeof(Transform), prop + ".x", u.x);
            Curve(up, path, typeof(Transform), prop + ".y", u.y);
            Curve(up, path, typeof(Transform), prop + ".z", u.z);
        }

        static void Curve(VirtualClip clip, string path, System.Type type, string prop, float v)
            => clip.SetFloatCurve(path, type, prop, new AnimationCurve(new Keyframe(0f, v), new Keyframe(Short, v)));
    }
}
