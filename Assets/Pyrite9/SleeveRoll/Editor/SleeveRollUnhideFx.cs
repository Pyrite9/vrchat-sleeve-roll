// Pyrite9 Sleeve Roll — the restored arm follows a hide shape that FX animates (an outfit toggle, MA reactive components). Runs after the shapes
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Pyrite9.Sleeve
{
    /// <summary>
    /// The unhide shape brings back the arm that a hide shape (say an outfit's MA Shape Changer) shrinks under the sleeve, as that shape was set at build.
    /// When FX turns that hide shape off (the outfit toggled off), a fixed unhide would push the bare arm outward. So a hide shape FX animates gets its own
    /// share of the unhide (SleeveRoll_Unhide_L__&lt;shape&gt;, SleeveRollBake), and its weight here follows: share = Rolled × (the hide shape now / at build).
    ///  - every FX clip that writes the hide shape also writes a float parameter (Live) = its value / the build value, key by key — default 1
    ///  - the Down and Up states of the 'Sleeve Roll' layer write Rolled 0 / 1
    ///  - one layer at the end (write defaults on, one state): a Direct blend tree [One → all shares 0] + [Live → 1D on Rolled: 0 → share 0, 1 → share 100].
    ///    Write defaults on, so a Direct tree does not add up frame after frame on avatars with write defaults off; the One child writes 0 when every Live is 0
    /// Parameters are read one frame after they are written, so a share lags its hide shape by one frame. Hide shapes FX never writes keep the old fixed path
    /// (SleeveRoll_Unhide_L/R in the Up state) and this class does nothing — the controller stays as it was.
    /// </summary>
    static class SleeveRollUnhideFx
    {
        internal const string LayerName = "Sleeve Roll Unhide";
        internal const string LivePrefix = "Pyrite9/SleeveRoll/Live/", RolledParameter = "Pyrite9/SleeveRoll/Rolled", OneParameter = "Pyrite9/SleeveRoll/One";
        const string StateName = "Live", TreeName = "SleeveRoll_Unhide_Live", ZeroClip = "SleeveRoll_Unhide_Zero", OffClip = "SleeveRoll_Unhide_Off_", OnClip = "SleeveRoll_Unhide_On_";
        const float Short = 1f / 60f;

        /// <summary>
        /// Which (body, shape) FX animates — read from the built controllers on the descriptor (after MA: its reactive layers are in there). Reads only.
        /// A hide shape another playable layer animates gets a warning: only FX is followed.
        /// The controllers are read the first time the answer is asked — only when there is a hidden arm to bring back — so an avatar without one is
        /// not read at all. If reading them fails, one warning and "not animated" for this build: the old fixed unhide, the rest of the build goes on
        /// </summary>
        internal static System.Func<SkinnedMeshRenderer, string, bool> Scan(GameObject root, StringBuilder log)
        {
            HashSet<string> fx = null;
            Dictionary<string, string> other = null;
            bool failed = false;
            var warned = new HashSet<string>();
            return (body, shape) =>
            {
                if (failed) return false;
                try
                {
                    if (fx == null) Read(root, out fx, out other);
                    var key = AnimationUtility.CalculateTransformPath(body.transform, root.transform) + "|blendShape." + shape;
                    if (other.TryGetValue(key, out var where) && warned.Add(key))
                        log.AppendLine($"WARN {body.name}: hide shape '{shape}' is animated in the {where} layer — the arm brought back under a rolled sleeve follows it only in FX");
                    return fx.Contains(key);
                }
                catch (System.Exception e)
                {
                    failed = true;
                    log.AppendLine($"WARN FX: could not read the animator controllers ({e.GetType().Name}: {e.Message}) — the arm an outfit hides is brought back as it was at build, even when the outfit is toggled off");
                    return false;
                }
            };
        }

        static void Read(GameObject root, out HashSet<string> fx, out Dictionary<string, string> other)
        {
            fx = new HashSet<string>();
            other = new Dictionary<string, string>();
            var desc = root.GetComponent<VRCAvatarDescriptor>();
            if (desc == null) return;
            var layers = (desc.baseAnimationLayers ?? new VRCAvatarDescriptor.CustomAnimLayer[0]).Concat(desc.specialAnimationLayers ?? new VRCAvatarDescriptor.CustomAnimLayer[0]);
            foreach (var l in layers)
            {
                if (l.isDefault || l.animatorController == null) continue;
                bool isFx = l.type == VRCAvatarDescriptor.AnimLayerType.FX;
                foreach (var clip in l.animatorController.animationClips.Distinct())
                {
                    if (clip == null) continue;
                    foreach (var b in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (b.type != typeof(SkinnedMeshRenderer) || !b.propertyName.StartsWith("blendShape.")) continue;
                        var key = b.path + "|" + b.propertyName;
                        if (isFx) fx.Add(key);
                        else if (!other.ContainsKey(key)) other[key] = l.type.ToString();
                    }
                }
            }
        }

        /// <summary>True when the layer went in (the build record counts it)</summary>
        public static bool Add(BuildContext ctx, IList<SleeveRollBake.LiveUnhide> shares, StringBuilder log)
        {
            // only here, not as a pass requirement: an avatar without such a hide shape keeps its controller exactly as it was
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
                    log.AppendLine($"WARN FX: layer '{SleeveRollDriver.LayerName}' not found — the arm an outfit hides is not brought back while rolled");
                    return false;
                }
                var paths = asc.ObjectPathRemapper;
                var clips = new HashSet<VirtualClip>();
                foreach (var l in fx.Layers)
                    if (l.StateMachine != null)
                        foreach (var st in l.StateMachine.AllStates()) Collect(st.Motion, clips);

                var zero = VirtualClip.Create(ZeroClip);
                var children = new List<VirtualBlendTree.VirtualChildMotion>
                {
                    new VirtualBlendTree.VirtualChildMotion { Motion = zero, DirectBlendParameter = OneParameter },
                };
                int n = 0;
                // one parameter per (body, hide shape) — the number keeps two bodies of the same name apart; both sides share it (same shape, same value)
                foreach (var g in shares.GroupBy(u => (u.body, u.hide)))
                {
                    var first = g.First();
                    var param = LivePrefix + n + "/" + first.hide;
                    var path = paths.GetVirtualPathForObject(first.body.transform);
                    var prop = "blendShape." + first.hide;
                    int mirrored = 0;
                    foreach (var clip in clips)
                    {
                        var cv = clip.GetFloatCurve(path, typeof(SkinnedMeshRenderer), prop);
                        if (cv == null) continue;
                        clip.SetFloatCurve("", typeof(Animator), param, Scaled(cv, 1f / first.weight));
                        mirrored++;
                    }
                    fx.SetParameter(param, new AnimatorControllerParameter { name = param, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
                    var off = VirtualClip.Create(OffClip + n);
                    var on = VirtualClip.Create(OnClip + n);
                    foreach (var u in g)
                    {
                        var bp = paths.GetVirtualPathForObject(u.body.transform);
                        zero.SetFloatCurve(bp, typeof(SkinnedMeshRenderer), "blendShape." + u.shape, Const(0f));
                        off.SetFloatCurve(bp, typeof(SkinnedMeshRenderer), "blendShape." + u.shape, Const(0f));
                        on.SetFloatCurve(bp, typeof(SkinnedMeshRenderer), "blendShape." + u.shape, Const(100f));
                    }
                    var rolled = VirtualBlendTree.Create(TreeName + "_" + n);
                    rolled.BlendType = BlendTreeType.Simple1D;
                    rolled.BlendParameter = RolledParameter;
                    rolled.UseAutomaticThresholds = false;
                    rolled.Children = new List<VirtualBlendTree.VirtualChildMotion>
                    {
                        new VirtualBlendTree.VirtualChildMotion { Motion = off, Threshold = 0f },
                        new VirtualBlendTree.VirtualChildMotion { Motion = on, Threshold = 1f },
                    }.ToImmutableList();
                    children.Add(new VirtualBlendTree.VirtualChildMotion { Motion = rolled, DirectBlendParameter = param });
                    log.AppendLine($"FX: {first.body.name} '{first.hide}' (built at {first.weight:0.##}) → '{param}' in {mirrored} clips — drives {string.Join(", ", g.Select(u => u.shape))}");
                    if (mirrored == 0) log.AppendLine($"WARN FX: no curve for '{first.hide}' on {first.body.name} found — its share of the restored arm stays on while rolled");
                    n++;
                }

                fx.SetParameter(RolledParameter, new AnimatorControllerParameter { name = RolledParameter, type = AnimatorControllerParameterType.Float, defaultFloat = 0f });
                fx.SetParameter(OneParameter, new AnimatorControllerParameter { name = OneParameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
                down.SetFloatCurve("", typeof(Animator), RolledParameter, Const(0f));
                up.SetFloatCurve("", typeof(Animator), RolledParameter, Const(1f));

                var tree = VirtualBlendTree.Create(TreeName);
                tree.BlendType = BlendTreeType.Direct;
                tree.Children = children.ToImmutableList();
                var last = fx.AddLayer(LayerPriority.Default, LayerName);
                last.DefaultWeight = 1f;
                var state = last.StateMachine.AddState(StateName, tree);
                state.WriteDefaultValues = true;
                last.StateMachine.DefaultState = state;
                log.AppendLine($"FX: layer '{LayerName}' (write defaults on, last), {n} hide shapes followed, '{RolledParameter}' in '{SleeveRollDriver.LayerName}'");
                return true;
            }
            finally
            {
                ctx.DeactivateExtensionContext<AnimatorServicesContext>();
                ctx.DeactivateExtensionContext<VirtualControllerContext>();
            }
        }

        static void Collect(VirtualMotion m, HashSet<VirtualClip> clips)
        {
            if (m is VirtualClip c) clips.Add(c);
            else if (m is VirtualBlendTree bt)
                foreach (var ch in bt.Children) Collect(ch.Motion, clips);
        }

        // every key and tangent by the same factor — not only constant curves
        static AnimationCurve Scaled(AnimationCurve c, float s)
        {
            var keys = c.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                k.value *= s;
                k.inTangent *= s;
                k.outTangent *= s;
                keys[i] = k;
            }
            return new AnimationCurve(keys) { preWrapMode = c.preWrapMode, postWrapMode = c.postWrapMode };
        }

        static AnimationCurve Const(float v) => new AnimationCurve(new Keyframe(0f, v), new Keyframe(Short, v));
    }
}
