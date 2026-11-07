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
    /// </summary>
    static class SleeveRollPbFx
    {
        const float Short = 1f / 60f;

        public static void Add(BuildContext ctx, IList<SleeveRollPb.Chain> chains, StringBuilder log)
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
                    log.AppendLine($"WARN FX: layer '{SleeveRollDriver.LayerName}' not found — kept PhysBones will not hold still while rolled");
                    return;
                }
                var paths = asc.ObjectPathRemapper;
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
            finally
            {
                ctx.DeactivateExtensionContext<AnimatorServicesContext>();
                ctx.DeactivateExtensionContext<VirtualControllerContext>();
            }
        }

        static void Curve(VirtualClip clip, string path, System.Type type, string prop, float v)
            => clip.SetFloatCurve(path, type, prop, new AnimationCurve(new Keyframe(0f, v), new Keyframe(Short, v)));
    }
}
