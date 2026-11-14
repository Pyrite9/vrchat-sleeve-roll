// Pyrite9 Sleeve Roll — 빌드 뒷단(Transforming, MA 뒤): 옷마다 셰이프를 넣고, 손목 대리 본으로 웨이트를 옮기고, 몸 숨김을 되돌리는 셰이프를 넣는다
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Pyrite9.Sleeve
{
    /// <summary>
    /// 겹쳐 입은 옷: 팔뚝 둘레가 가는 옷(안쪽)부터 걷고, 바깥 옷은 몸 + 안쪽 옷의 '걷힌' 모양을 피부로 잰다 — 바깥 소맷단이 안쪽 소맷단 위에 얹힌다.
    /// 고른 옷끼리는 서로의 걷기 전 모양을 피부로 보지 않는다(안 그러면 안쪽 옷이 바깥 옷보다 가늘어 '몸 같음'으로 실패). 대리 본으로 웨이트를
    /// 옮기는 건 셰이프를 다 만든 뒤 — 안쪽 옷을 피부로 잴 때 손 웨이트가 그대로여야 한다
    /// </summary>
    sealed class SleeveRollBake
    {
        /// <summary>숨김 경계 너머 첫 고리에서 소맷단을 얼마나 더 내려 덮을지(팔뚝 길이 비율) — 경계 사이 늘어난 면 가리기</summary>
        const float HiddenOverlap = 0.03f;

        readonly GameObject root;
        readonly Animator animator;
        readonly StringBuilder log;
        readonly SrBuildRecord.Entry rec;
        readonly HashSet<string> deleteTargets;
        // 옷마다·쪽마다 같은 몸 메시들을 다시 읽지 않게(정점·웨이트·숨김 셰이프 합) — 이 빌드 동안만
        readonly SleeveRollShape.Cache cache;
        // 미리 보기: 렌더러를 안 건드리고 만든 메시를 여기에 둔다. null = 실제 빌드
        readonly Dictionary<SkinnedMeshRenderer, Mesh> previewMeshes;
        readonly HashSet<Mesh> bodyCopies = new HashSet<Mesh>();
        // 안쪽 옷의 걷힌 정점 — 바깥 옷이 피부로 잰다
        readonly Dictionary<SkinnedMeshRenderer, Vector3[]> rolled = new Dictionary<SkinnedMeshRenderer, Vector3[]>();
        SleeveRollPb pb;
        // 쪽마다(0 왼쪽, 1 오른쪽) 걷는 옷들의 소매 끝 t 중 가장 손 쪽 — 그 너머 피부는 걷기 전에도 드러나 있었다
        readonly float[] cover = { float.MinValue, float.MinValue };
        // 손목 대리 본 스케일(감사 P2), 쪽마다: 내림 = 손 본 스케일, 걷음 = 대리 본으로 웨이트를 옮긴 첫 옷의 바인드 포즈 손 스케일(아래팔 기준)
        readonly Vector3[] downScale = { Vector3.one, Vector3.one };
        readonly Vector3?[] upScale = new Vector3?[2];
        readonly string[] upFrom = new string[2];

        /// <summary>대리 본 스케일 곡선(FX의 Down·Up) — Run 뒤에 부르는 쪽이 SleeveRollPbFx로 넣는다. 스케일이 1인 아바타는 비어 있다</summary>
        internal readonly List<(Transform proxy, Vector3 down, Vector3 up)> proxyScales = new List<(Transform, Vector3, Vector3)>();

        /// <summary>
        /// 손가락 그림자 본(감사 P3) — 대리 본 밑, 진짜 손가락 본마다 하나(아바타 전체, 옷 여럿이 같이). 내림 = 진짜 손가락 회전 복사(Constraint GlobalWeight 1),
        /// 걷음 = 그 본을 처음 쓴 옷의 바인드 포즈 자세(GlobalWeight 0 + 쉬는 회전, 위치·스케일이 지금과 다를 때만 Transform 곡선). Run 뒤에 SleeveRollPbFx가 넣는다
        /// </summary>
        internal sealed class FingerShadow
        {
            public Transform shadow, bone;
            public bool left;
            public string from;   // 걷음 자세를 정한 옷
            public Quaternion upRot;
            public Vector3 downPos, upPos, downScale, upScale;
            public bool PosCurve => !Near(downPos, upPos, 1e-5f);
            public bool ScaleCurve => !Near(downScale, upScale, 1e-4f);
        }
        internal readonly List<FingerShadow> fingerShadows = new List<FingerShadow>();
        readonly Dictionary<Transform, FingerShadow> fingerShadowOf = new Dictionary<Transform, FingerShadow>();
        readonly HashSet<(string, bool, Transform)> fingerWarned = new HashSet<(string, bool, Transform)>();

        /// <summary>
        /// 따라가기 그림자(0.1.3-beta4) — 아래팔에 대해 움직이는 본(PhysBone 서브트리·그걸 소스로 한 Constraint 본, SleeveRollPb.MovingBones)마다 그쪽 아래팔 밑에 하나(옷 여럿이 같이).
        /// VRC Parent Constraint 소스 = 그 본: 내림(GlobalWeight 1) = 그 본과 월드 일치(원래대로 흔들림), 걷음(0) = 아래팔 기준 바인드 관계(쉬는 값)에 고정 — 걷힌 정점이 아래팔 웨이트처럼 따라간다
        /// </summary>
        internal sealed class FollowShadow
        {
            public Transform shadow, bone;
            public bool left;
            public string from;   // 쉬는 값을 정한 옷
            public Quaternion upRot;
            public Vector3 upPos, scale;
        }
        internal readonly List<FollowShadow> followShadows = new List<FollowShadow>();
        readonly Dictionary<(Transform, bool), FollowShadow> followOf = new Dictionary<(Transform, bool), FollowShadow>();
        readonly HashSet<(string, bool, Transform)> followWarned = new HashSet<(string, bool, Transform)>();

        /// <summary>
        /// FX가 이 몸의 이 숨김 셰이프를 움직이나(옷 토글·MA 반응형) — 빌드만(SleeveRollUnhideFx.Scan), null = 나누지 않음(미리 보기).
        /// 움직이는 셰이프의 되돌리기 몫은 따로 셰이프로 두고(liveUnhides), 걷은 동안 그 셰이프의 지금 값을 따라간다(Run 뒤에 SleeveRollUnhideFx.Add)
        /// </summary>
        internal System.Func<SkinnedMeshRenderer, string, bool> live;

        /// <summary>숨김 셰이프 하나 몫의 되돌리기 셰이프 — 몸·쪽·숨김 셰이프·빌드 때 값</summary>
        internal sealed class LiveUnhide
        {
            public SkinnedMeshRenderer body;
            public bool left;
            public string hide, shape;
            public float weight;
        }
        internal readonly List<LiveUnhide> liveUnhides = new List<LiveUnhide>();
        readonly HashSet<(SkinnedMeshRenderer, bool)> unhideDone = new HashSet<(SkinnedMeshRenderer, bool)>();

        public SleeveRollBake(GameObject root, Animator animator, StringBuilder log, SrBuildRecord.Entry rec, HashSet<string> deleteTargets,
            Dictionary<Transform, Transform> boneMap = null, HashSet<Transform> known = null)
            : this(root, animator, log, rec, deleteTargets, new SleeveRollShape.Cache { boneMap = boneMap, known = known }, null) { }

        SleeveRollBake(GameObject root, Animator animator, StringBuilder log, SrBuildRecord.Entry rec, HashSet<string> deleteTargets,
            SleeveRollShape.Cache cache, Dictionary<SkinnedMeshRenderer, Mesh> previewMeshes)
        {
            this.root = root; this.animator = animator; this.log = log; this.rec = rec; this.deleteTargets = deleteTargets;
            this.cache = cache; this.previewMeshes = previewMeshes;
        }

        /// <summary>
        /// 인스펙터 미리 보기(SleeveRollPreview) — 빌드와 같은 계산을 씬의 아바타에. 렌더러는 그대로, 메시는 meshes에(지우는 건 부르는 쪽).
        /// 대리 본은 안 만든다(쉬는 자세라 필요 없음). 결과·경고는 빌드 기록과 같은 모양으로 돌려준다
        /// </summary>
        internal static SrBuildRecord.Entry Preview(GameObject root, Animator animator, SleeveRoll[] comps, SleeveRollShape.Cache cache, Dictionary<SkinnedMeshRenderer, Mesh> meshes)
        {
            var log = new StringBuilder();
            var rec = new SrBuildRecord.Entry { avatar = root.name, version = SleeveRollConst.Version, built = true };
            new SleeveRollBake(root, animator, log, rec, new HashSet<string>(), cache, meshes).BuildAll(comps);
            foreach (var l in log.ToString().Split('\n'))
                if (l.StartsWith("WARN ")) rec.warnings.Add(l.Substring(5).TrimEnd('\r'));
            return rec;
        }

        /// <summary>옷 하나 — 어느 Sleeve Roll 설정으로, 걷은 결과(대리 본·되살리기는 셰이프를 다 만든 뒤에)</summary>
        sealed class Job
        {
            public SleeveRoll c;
            public SkinnedMeshRenderer cloth;
            public float radius;
            public SrBuildRecord.Garment g;
            public List<SkinnedMeshRenderer> bodies, skins;   // 몸(숨김 경계·되돌리기) / 피부로 잴 것 = 몸 + 안쪽 옷
            public SleeveRollShape.Settings sL, sR;
            public Mesh mesh;
            public List<SleeveRollShape.SideReport> reports;
        }

        /// <summary>돌려준 체인은 부르는 쪽이 FX 레이어로(SleeveRollPbFx) — 옷이 없으면 null</summary>
        public SleeveRollPb Run(SleeveRoll[] comps)
        {
            BuildAll(comps);
            DropUnusedProxies();
            if (pb != null) ProxyScaleCurves();
            LogFingerShadows();
            LogFollowShadows();
            return pb;
        }

        void BuildAll(SleeveRoll[] comps)
        {
            var jobs = CollectJobs(comps);
            if (jobs.Count == 0) return;
            jobs = jobs.OrderBy(j => float.IsNaN(j.radius) ? float.MaxValue : j.radius).ToList();
            var all = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var picked = new HashSet<SkinnedMeshRenderer>(jobs.Select(j => j.cloth));
            if (jobs.Count > 1)
                log.AppendLine("  layers, inner → outer: " + string.Join(" → ", jobs.Select(j => float.IsNaN(j.radius) ? j.cloth.name : $"{j.cloth.name} ({j.radius * 1000f:F1} mm)")));
            for (int i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                j.g = new SrBuildRecord.Garment { name = j.cloth.name, over = i > 0 ? jobs[i - 1].cloth.name : "" };
                j.g.left.on = j.c.left; j.g.right.on = j.c.right;
                rec.garments.Add(j.g);
                // 몸(고른 옷 빼고) + 이미 걷은 안쪽 옷(걷힌 정점으로 잰다)
                j.bodies = Skins(j.c, all, picked);
                j.skins = new List<SkinnedMeshRenderer>(j.bodies);
                for (int k = 0; k < i; k++) j.skins.Add(jobs[k].cloth);
                Shape(j);
                // 가장 바깥 옷은 아무도 안 잰다
                if (i < jobs.Count - 1) rolled[j.cloth] = RolledVertices(cache.Get(j.cloth).Vertices, j.mesh, j.reports);
            }
            SortPhysBones(jobs);
            // 소매가 덮던 구간의 끝(쪽마다, 걷는 옷 중 가장 긴 소매) — 되돌리기는 그 밑을 숨기던 셰이프만 되돌린다
            foreach (var j in jobs)
                foreach (var r in j.reports)
                    if (SrSide.On(j.c, r.left) && r.ok) cover[r.left ? 0 : 1] = Mathf.Max(cover[r.left ? 0 : 1], r.sleeveEnd);
            foreach (var j in jobs) Finish(j);
            if (previewMeshes == null)
            {
                ScaleProxies();   // 체인을 대리 본 밑으로 옮기기 전에 — SetParent가 체인의 월드 스케일을 지킨다
                pb.Reparent();
                Follow(jobs);     // 대리 본·손가락 그림자·체인 그림자로 옮긴 뒤의 본 목록에서 — 그것들은 아래팔 밑이라 겹치지 않는다
            }
        }

        // 따라가기 그림자(0.1.3-beta4): 걷히는 정점에 웨이트가 있는, 아래팔에 대해 움직이는 본 → 그 웨이트만 아래팔 밑 그림자로. 실제 빌드만, 대리 본 설정과 무관
        void Follow(List<Job> jobs)
        {
            HashSet<Transform> moving = null;
            foreach (var j in jobs)
                foreach (var r in j.reports)
                {
                    if (j.mesh == null || !SrSide.On(j.c, r.left) || !r.ok || r.roll == null) continue;
                    if (moving == null) moving = cache.Moving(animator);   // 셰이프 계산이 쓴 것과 같은 집합(셰이프 전에 구해 둠, 0.1.3-beta5)
                    if (moving.Count == 0) return;
                    FollowSide(j, r, moving);
                }
        }

        void FollowSide(Job j, SleeveRollShape.SideReport r, HashSet<Transform> moving)
        {
            var lower = animator.GetBoneTransform(SrSide.Lower(r.left));
            var hand = animator.GetBoneTransform(SrSide.Hand(r.left));
            if (lower == null) return;
            var bones = j.cloth.bones;   // 지금 본 — Rebind·그림자 교체 뒤
            var bp = j.mesh.bindposes;
            var cand = new bool[bones.Length];
            bool any = false;
            for (int i = 0; i < bones.Length && i < bp.Length; i++)
                any |= cand[i] = bones[i] != null && moving.Contains(bones[i]) && !bones[i].IsChildOf(lower);
            if (!any) return;
            var hit = new bool[bones.Length];
            any = false;
            var per = j.mesh.GetBonesPerVertex();
            var bw = j.mesh.GetAllBoneWeights();
            int c = 0;
            for (int v = 0; v < per.Length; v++)
                for (int k = 0; k < per[v]; k++, c++)
                    if (v < r.roll.Length && r.roll[v] > 0f && bw[c].weight > 0f && bw[c].boneIndex < cand.Length && cand[bw[c].boneIndex])
                        any = hit[bw[c].boneIndex] = true;
            if (!any) return;
            // 아래팔 칸의 바인드 포즈(계산용 — BindLocalRotation과 같은 방식)
            var ce = cache.Get(j.cloth);
            if (!SleeveRollShape.LowerBind(ce.bones, ce.bindposes, lower, hand, out var bpLower, ce.boneMap)) return;
            var slotTo = new Transform[bones.Length];
            var names = new List<string>();
            for (int i = 0; i < bones.Length; i++)
            {
                if (!hit[i]) continue;
                slotTo[i] = FollowShadowFor(j.cloth.name, r.left, bones[i], lower, bpLower * bp[i].inverse);
                names.Add(bones[i].name);
            }
            int moved = SleeveRollShape.MoveToFollow(j.cloth, j.mesh, r.roll, slotTo);
            log.AppendLine($"  {j.cloth.name} {SrSide.Letter(r.left)}: {moved} rolled vertices follow the forearm off bones that move on their own ({string.Join(", ", names)})");
        }

        /// <summary>
        /// 본 b의 따라가기 그림자 — 아바타에 쪽·본마다 하나. 쉬는 값 = L(아래팔 바인드 · b 바인드⁻¹)의 위치·회전, localScale = L 스케일. 다른 옷의 L이 다르면 처음 옷을 따르고 WARN
        /// </summary>
        Transform FollowShadowFor(string cloth, bool left, Transform b, Transform lower, Matrix4x4 m)
        {
            var upRot = m.rotation;
            Vector3 upPos = m.GetColumn(3);
            var scale = Snap(m.lossyScale);
            if (followOf.TryGetValue((b, left), out var fs))
            {
                float deg = Quaternion.Angle(fs.upRot, upRot);
                float meters = Vector3.Distance(fs.upPos, upPos) * lower.lossyScale.x;
                if ((deg > SleeveRollPb.MaxAngle || meters > SleeveRollPb.MaxMeters || !Near(fs.scale, scale, 1e-3f)) && followWarned.Add((cloth, left, b)))
                    log.AppendLine($"WARN {cloth} {SrSide.Letter(left)}: its bind pose holds {b.name} {deg:0.#}° and {meters * 1000f:0.#} mm off {fs.from}'s, scale {Fmt(scale)} vs {Fmt(fs.scale)} — the follow shadow follows {fs.from}, the rolled cuff of {cloth} may sit off");
                return fs.shadow;
            }
            var t = new GameObject(UniqueChild(lower, "SleeveRoll_Follow_" + b.name)).transform;
            t.SetParent(lower, false);
            t.SetPositionAndRotation(b.position, b.rotation);   // 내림(기본 상태) 자리
            t.localScale = scale;
            var pc = t.gameObject.AddComponent<VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint>();
            pc.Sources.Add(new VRC.Dynamics.VRCConstraintSource(b, 1f));
            pc.PositionAtRest = upPos;
            pc.RotationAtRest = upRot.eulerAngles;
            pc.AffectsPositionX = pc.AffectsPositionY = pc.AffectsPositionZ = true;
            pc.AffectsRotationX = pc.AffectsRotationY = pc.AffectsRotationZ = true;
            pc.GlobalWeight = 1f;
            pc.Locked = true;
            pc.IsActive = true;
            fs = new FollowShadow { shadow = t, bone = b, left = left, from = cloth, upRot = upRot, upPos = upPos, scale = scale };
            followOf[(b, left)] = fs;
            followShadows.Add(fs);
            return t;
        }

        // 같은 이름 형제가 있으면(다른 옷의 같은 이름 본) 뒤에 번호 — FX 경로가 겹치지 않게
        static string UniqueChild(Transform parent, string name)
        {
            if (parent.Find(name) == null) return name;
            for (int n = 2; ; n++)
                if (parent.Find($"{name}_{n}") == null) return $"{name}_{n}";
        }

        void LogFollowShadows()
        {
            foreach (bool left in SrSide.Both)
            {
                var mine = followShadows.Where(f => f.left == left).ToList();
                if (mine.Count == 0) continue;
                log.AppendLine($"  follow shadows {SrSide.Letter(left)}: {mine.Count} ({string.Join(", ", mine.Select(f => $"{f.shadow.name} ← {f.bone.name}"))})");
            }
        }

        // PhysBone 소매(10-33). 셰이프를 다 만든 뒤, 웨이트를 옮기기 전 — 걷을 수 없는 쪽은 여기서 실패로 돌린다
        void SortPhysBones(List<Job> jobs)
        {
            pb = new SleeveRollPb(root, animator, cache, log);
            foreach (var j in jobs)
                foreach (var r in j.reports)
                    if (SrSide.On(j.c, r.left) && r.ok) pb.Consider(j.cloth, r.left, r.roll);
            pb.Decide();
            foreach (var j in jobs)
                for (int i = 0; i < j.reports.Count; i++)
                {
                    var r = j.reports[i];
                    if (!SrSide.On(j.c, r.left) || !r.ok) continue;
                    var gs = r.left ? j.g.left : j.g.right;
                    gs.physBones = pb.KeptOn(j.cloth, r.left);
                    var why = pb.FailReason(j.cloth, r.left);
                    if (why == null) continue;
                    r.ok = false; r.message = why;
                    j.reports[i] = r;
                    RemoveShape(j.mesh, r.left);
                    gs.ok = false; gs.message = why; gs.moved = 0; gs.physBones = 0;
                    log.AppendLine($"WARN {j.cloth.name} {SrSide.Letter(r.left)}: {why}");
                }
        }

        List<Job> CollectJobs(SleeveRoll[] comps)
        {
            var jobs = new List<Job>();
            foreach (var c in comps)
            {
                var garments = c.Garments;
                if (garments.Count == 0) { log.AppendLine($"WARN {c.name}: no garment picked — this Sleeve Roll does nothing"); continue; }
                foreach (var cloth in garments)
                {
                    // 다른 아바타의 렌더러(컴포넌트를 복사해 붙인 경우 등) — 빌드 사본 밖 오브젝트라 메시를 바꾸면 씬이 바뀐다. 인스펙터도 미리 경고한다
                    if (!cloth.transform.IsChildOf(root.transform)) { log.AppendLine($"WARN {cloth.name}: not inside this avatar — skipped"); continue; }
                    if (cloth.sharedMesh == null) { log.AppendLine($"WARN {cloth.name}: no mesh — skipped"); continue; }
                    if (jobs.Any(j => j.cloth == cloth)) { log.AppendLine($"WARN {cloth.name}: picked in two Sleeve Rolls — the first one's settings are used"); continue; }
                    jobs.Add(new Job { c = c, cloth = cloth, radius = SleeveRollShape.SleeveRadius(cloth, animator, cache) });
                }
            }
            return jobs;
        }

        void Shape(Job j)
        {
            var c = j.c;
            var s0 = SleeveRollShape.Settings.Default;
            s0.tTarget = c.rollTo;
            s0.t0 = Mathf.Min(c.startAt, c.rollTo - 0.05f);
            s0.margin = c.margin;
            s0.snug = c.looseness;
            s0.foldAmplitude = c.foldAmplitude;
            s0.foldLength = Mathf.Max(0.005f, c.foldLength);
            j.sL = j.sR = s0;
            bool stoppedL = !c.unhideWhenRolled && StopAtHidden(j, true, ref j.sL);
            bool stoppedR = !c.unhideWhenRolled && StopAtHidden(j, false, ref j.sR);

            j.reports = new List<SleeveRollShape.SideReport>();
            // 꺼 둔 쪽은 계산하지 않고 셰이프도 안 넣는다(보고는 빈 채로 남음)
            j.mesh = SleeveRollShape.Build(j.cloth, j.skins, rolled, animator, j.sL, j.sR, c.left, c.right, cache, j.reports);
            if (previewMeshes != null) previewMeshes[j.cloth] = j.mesh;
            else { j.cloth.sharedMesh = j.mesh; cache.Adopt(j.cloth, j.mesh); }
            foreach (var r in j.reports)
            {
                string side = SrSide.Letter(r.left);
                // 옷에 원래 같은 이름 셰이프가 있던 경우만 실제로 지운다(Build는 꺼 둔 쪽을 안 넣는다)
                if (!SrSide.On(c, r.left)) { RemoveShape(j.mesh, r.left); log.AppendLine($"  {j.cloth.name} {side}: off"); continue; }
                log.AppendLine($"  {j.cloth.name} {side}: ok={r.ok} sleeve={r.clothVertices} moved={r.movedVertices} skin={r.skinVertices} " +
                               $"forearm={r.forearmMeters:F3}m end={r.sleeveEnd:F2} r={r.clothRadiusMm:F1}mm skin={r.skinRadiusMm:F1}mm" +
                               (r.worn != null ? $" worn [{r.worn}]" + (r.folded > 0 ? $" folded={r.folded}" : "") : "") + $" {r.message}");
                if (!string.IsNullOrEmpty(r.note)) log.AppendLine($"  {j.cloth.name} {side}: {r.note}");
                var gs = r.left ? j.g.left : j.g.right;
                gs.ok = r.ok; gs.rolledTo = r.rolledTo; gs.moved = r.movedVertices; gs.message = r.message;
                gs.stopped = r.left ? stoppedL : stoppedR;
                if (!r.ok) log.AppendLine($"WARN {j.cloth.name} {side}: {r.message}");
                // 걷긴 했지만 피부가 늦게 시작해 조금만 — 실패와 같은 안내(인스펙터 안내 표의 "has forearm skin only from")
                else if (r.skinShort)
                    log.AppendLine($"WARN {j.cloth.name} {side}: the body mesh has forearm skin only from t={r.skinFrom:F2} — rolled only to t={r.rolledTo:F2}; use a body mesh with the whole forearm to roll it further");
            }
            WarnAnimatedShapes(j);
        }

        HashSet<string> animatedPaths;

        // 소매를 움직이는 셰이프를 아바타 애니메이션이 바꾸면 — 걷기는 빌드 때 값의 입은 모양으로 계산했다(0.1.3-beta6). 걷은 쪽이 있는 옷만.
        // 빌드 때 값이 0이 아닌 셰이프는 WARN(입은 모양에 들어가 있다), 0인 셰이프는 일반 줄 — 벤더 메뉴 토글(꺼진 채 빌드)이 흔해 WARN이면 잡음.
        // 미리 보기는 이 로그의 WARN 줄만 인스펙터 경고로 올리므로 같은 규칙이 된다
        void WarnAnimatedShapes(Job j)
        {
            if (j.mesh == null || !j.reports.Any(r => SrSide.On(j.c, r.left) && r.ok)) return;
            if (animatedPaths == null) animatedPaths = SleeveRollPb.AnimatedPaths(root);
            var key = "S|" + UnityEditor.AnimationUtility.CalculateTransformPath(j.cloth.transform, root.transform) + "|blendShape.";
            if (!animatedPaths.Any(p => p.StartsWith(key))) return;
            var names = SleeveRollShape.SleeveShapes(cache.Get(j.cloth), animator, n => animatedPaths.Contains(key + n));
            string Fmt(IEnumerable<(string name, float weight)> s) => string.Join(", ", s.Select(x => x.name + "=" + x.weight.ToString("0")));
            var on = names.Where(x => Mathf.Abs(x.weight) >= 0.01f).ToList();   // 입은 모양과 같은 문턱(Cache.Entry.Wear)
            var off = names.Where(x => Mathf.Abs(x.weight) < 0.01f).ToList();
            if (on.Count > 0)
                log.AppendLine($"WARN {j.cloth.name}: an animation changes {Fmt(on)}, which move the sleeve — the roll is made for the values at build time");
            if (off.Count > 0)
                log.AppendLine($"  {j.cloth.name}: an animation changes {Fmt(off)} (built at 0 — if a menu turns them on, the rolled sleeve may not fit)");
        }

        // shrink를 해제하지 않을 때(D16): 그쪽 몸 메시들의 shrink 경계 중 가장 손목 쪽 → 그 너머 첫 고리 + 덮개 여유까지만. 슬라이더가 더 손목 쪽이면 슬라이더대로(false). shrink가 없어도 false
        bool StopAtHidden(Job j, bool left, ref SleeveRollShape.Settings s)
        {
            float best = float.MinValue; string why = "";
            foreach (var body in j.bodies)
            {
                if (body == null || body.sharedMesh == null) continue;
                if (!SleeveRollShape.HiddenEdge(body, animator, left, cache, out var edge, out var next, out var shapes)) continue;
                float t = next + HiddenOverlap;
                if (t > best) { best = t; why = $"{body.name} hidden to t={edge:F2}, next ring t={next:F2} [{shapes}]"; }
            }
            if (best == float.MinValue || best <= j.c.rollTo) return false;
            s.tTarget = Mathf.Min(best, 1f);
            s.t0 = Mathf.Min(j.c.startAt, s.tTarget - 0.05f);
            log.AppendLine($"  {j.cloth.name} {SrSide.Letter(left)}: stop at hidden skin → roll to t={s.tTarget:F2} ({why})");
            return true;
        }

        // 양쪽 셰이프를 다 켠 정점(토글 하나로 같이 걷힌다)
        // worn: 그 옷의 입은 모양 — 걷기 셰이프는 그 위에 더하는 이동이다(0.1.3-beta6).
        // 셰이프로 접혀 피부 안에 숨은 정점(SideReport.hid)은 NaN — 겉면이 아니라 피부로 안 잰다(SampleSkin이 건너뜀).
        // 축 가까이 모인 정점은 둘레 각도가 부동소수 잡음에 흔들려, 재면 바깥 옷 피부 격자에 얇은 칸(구멍)이 아무 데나 생긴다
        static Vector3[] RolledVertices(Vector3[] worn, Mesh mesh, List<SleeveRollShape.SideReport> reports)
        {
            var v = (Vector3[])worn.Clone();
            var d = new Vector3[v.Length];
            foreach (bool left in SrSide.Both)
            {
                int si = mesh.GetBlendShapeIndex(SrSide.ShapeName(left));
                if (si < 0) continue;
                mesh.GetBlendShapeFrameVertices(si, mesh.GetBlendShapeFrameCount(si) - 1, d, null, null);
                for (int i = 0; i < v.Length; i++) v[i] += d[i];
            }
            if (reports != null)
                foreach (var r in reports)
                    if (r.hid != null)
                        for (int i = 0; i < v.Length && i < r.hid.Length; i++)
                            if (r.hid[i]) v[i] = new Vector3(float.NaN, float.NaN, float.NaN);
            return v;
        }

        /// <summary>손목 대리 본으로 웨이트 옮기기(D6)·몸 숨김 되돌리기(D7·D10) — 모든 옷의 셰이프를 만든 뒤</summary>
        void Finish(Job j)
        {
            if (j.mesh == null) return;
            if (j.c.wristProxy && previewMeshes == null)
                foreach (var r in j.reports)
                    if (SrSide.On(j.c, r.left) && r.ok) MoveToProxy(j, r);
            foreach (var body in j.bodies)
            {
                if (body == null || body.sharedMesh == null) continue;
                foreach (bool left in SrSide.Both) AddUnhide(j, body, left);
            }
            foreach (var mr in j.bodies.Where(b => b != null && deleteTargets.Contains(b.name)))
                log.AppendLine($"WARN {mr.name}: an MA Shape Changer in Delete mode removes vertices at build — a rolled sleeve may show a gap there");
        }

        void MoveToProxy(Job j, SleeveRollShape.SideReport r)
        {
            string side = SrSide.Letter(r.left);
            var lower = animator.GetBoneTransform(SrSide.Lower(r.left));
            var hand = animator.GetBoneTransform(SrSide.Hand(r.left));
            // 옷의 손·아래팔이 MA가 남긴 의상 본이어도 아바타 본으로 본다(계산용 본·바인드 포즈 — 문의 10-73)
            var ce = cache.Get(j.cloth);
            if (!SleeveRollShape.BindLocalRotation(ce, lower, hand, out var rest))
            {
                log.AppendLine($"  {j.cloth.name} {side}: no hand weights — wrist proxy skipped");
                return;
            }
            var proxy = SleeveRollDriver.Proxy(lower, hand, rest, SrSide.ProxyName(r.left));
            FingerSlots(j, r, hand, proxy, ce, out var slotTo, out var slotBind);
            int moved = SleeveRollShape.Rebind(j.cloth, j.mesh, hand, proxy, r.roll, ce, pb.Keep, slotTo, slotBind);
            (r.left ? j.g.left : j.g.right).rebound = moved;
            log.AppendLine($"  {j.cloth.name} {side}: wrist proxy {proxy.name}, rebound {moved} vertices");
            if (moved > 0 && SleeveRollShape.BindLocalScale(ce, lower, hand, out var k)) NoteUpScale(j.cloth.name, r.left, Snap(k));
        }

        // 걷은 동안의 대리 본 스케일은 쪽마다 하나 — 그쪽 첫 옷의 것을 쓰고, 바인드 포즈 손 스케일이 다른 옷은 걷은 소맷단이 어긋날 수 있다
        void NoteUpScale(string cloth, bool left, Vector3 k)
        {
            int i = left ? 0 : 1;
            if (upScale[i] == null) { upScale[i] = k; upFrom[i] = cloth; return; }
            var first = upScale[i].Value;
            if (Mathf.Abs(k.x - first.x) > 1e-3f || Mathf.Abs(k.y - first.y) > 1e-3f || Mathf.Abs(k.z - first.z) > 1e-3f)
                log.AppendLine($"WARN {cloth} {SrSide.Letter(left)}: its bind pose scales the hand by {Fmt(k)}, {upFrom[i]} by {Fmt(first)} — the wrist proxy follows {upFrom[i]}, the rolled cuff of {cloth} may sit off");
        }

        /// <summary>
        /// 걷히는 웨이트가 갈 손가락 그림자(감사 P3). 옷의 실제 본 중 손의 엄격한 자손이고, 걷히는 정점에 0.01 이상 실리고(BonesOnRolled와 같은 문턱),
        /// 유지하는 PhysBone 체인 본이 아닌 본마다 — 손 쪽으로 올라가며 처음 만나는 휴머노이드 손가락 본(자기 포함)의 그림자가 칸이다.
        /// 그런 조상이 없는 본(손바닥 쪽 본, ScaleProxy, MA가 남긴 의상 손, 제약 사본, 손가락이 매핑 안 된 아바타)은 지금처럼 대리 본으로.
        /// 칸의 바인드 포즈 = 그 손가락의 실제 바인드 포즈 — 손가락 밑 본이 합쳐질 때도 그 손가락 것(바인드 자세에선 정확). 해당 본이 없으면 둘 다 null(Rebind가 전과 같음)
        /// </summary>
        void FingerSlots(Job j, SleeveRollShape.SideReport r, Transform hand, Transform proxy, SleeveRollShape.Cache.Entry ce, out Transform[] slotTo, out Matrix4x4[] slotBind)
        {
            slotTo = null; slotBind = null;
            if (r.roll == null) return;
            var fingers = Fingers(r.left);
            if (fingers.Count == 0) return;
            var bones = j.cloth.bones;   // 지금 본 — 다른 쪽 Rebind가 붙인 칸까지(웨이트 인덱스가 이것을 가리킨다)
            var bp = j.mesh.bindposes;
            var onRolled = new bool[bones.Length];
            var per = j.mesh.GetBonesPerVertex();
            var bw = j.mesh.GetAllBoneWeights();
            int c = 0;
            for (int v = 0; v < per.Length; v++)
                for (int k = 0; k < per[v]; k++, c++)
                    if (v < r.roll.Length && r.roll[v] > 0f && bw[c].weight >= 0.01f && bw[c].boneIndex < onRolled.Length) onRolled[bw[c].boneIndex] = true;
            // 대리 본 칸의 바인드 포즈(Rebind와 같은 값) — 맨 위 손가락 그림자의 걷음 자세는 이것 기준
            if (!SleeveRollShape.LowerBind(ce.bones, ce.bindposes, hand, null, out var bpHand, ce.boneMap)) return;
            for (int i = 0; i < bones.Length && i < bp.Length; i++)
            {
                var b = bones[i];
                if (b == null || b == hand || !onRolled[i] || !b.IsChildOf(hand) || pb.Keep.Contains(b)) continue;
                Transform f = null;
                for (var t = b; t != null && t != hand; t = t.parent)
                    if (fingers.Contains(t)) { f = t; break; }
                if (f == null || !FingerBind(bones, bp, f, out var bpF)) continue;
                var s = FingerShadowFor(j.cloth.name, r.left, f, hand, proxy, bones, bp, bpHand);
                if (s == null) continue;
                if (slotTo == null) { slotTo = new Transform[bones.Length]; slotBind = new Matrix4x4[bones.Length]; }
                slotTo[i] = s;
                slotBind[i] = bpF;
            }
        }

        /// <summary>휴머노이드 손가락 본(그쪽 손 엄지·검지·중지·약지·소지 × Proximal·Intermediate·Distal)</summary>
        HashSet<Transform> Fingers(bool left)
        {
            var set = new HashSet<Transform>();
            int from = (int)(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            for (int k = 0; k < 15; k++)
            {
                var t = animator.GetBoneTransform((HumanBodyBones)(from + k));
                if (t != null) set.Add(t);
            }
            return set;
        }

        /// <summary>이 옷의 x 바인드 포즈(실제 본 기준) — 본 목록에 있으면 그것, 없으면 x 밑 첫 본에서 유도(LowerBind와 같은 식, 지금 자세 = 바인드 가정)</summary>
        static bool FingerBind(Transform[] bones, Matrix4x4[] bp, Transform x, out Matrix4x4 m)
        {
            m = default;
            int i = System.Array.IndexOf(bones, x);
            if (i >= 0 && i < bp.Length) { m = bp[i]; return true; }
            for (int k = 0; k < bones.Length && k < bp.Length; k++)
                if (bones[k] != null && bones[k] != x && bones[k].IsChildOf(x))
                {
                    m = x.worldToLocalMatrix * bones[k].localToWorldMatrix * bp[k];
                    return true;
                }
            return false;
        }

        /// <summary>
        /// 본 x의 손가락 그림자 — 아바타에 본 하나당 하나. 부모가 손이면 대리 본 밑, 아니면 부모의 그림자 밑(사이 본은 웨이트가 없어도 만든다).
        /// 내림 = 진짜 본의 지금 로컬 값 + 회전 복사, 걷음 = (부모 칸 바인드 · x 바인드⁻¹)의 회전·위치·스케일 — 그 본을 처음 쓴 옷 것. 다른 옷의 값이 다르면 WARN
        /// </summary>
        Transform FingerShadowFor(string cloth, bool left, Transform x, Transform hand, Transform proxy, Transform[] bones, Matrix4x4[] bp, Matrix4x4 bpHand)
        {
            Transform parent;
            Matrix4x4 bpParent;
            if (x.parent == hand) { parent = proxy; bpParent = bpHand; }   // 대리 본 칸의 바인드 포즈 = 손(계산용, Rebind와 같음)
            else
            {
                parent = FingerShadowFor(cloth, left, x.parent, hand, proxy, bones, bp, bpHand);
                if (parent == null || !FingerBind(bones, bp, x.parent, out bpParent)) return null;
            }
            if (!FingerBind(bones, bp, x, out var bpX)) return null;
            var m = bpParent * bpX.inverse;
            var upRot = m.rotation;
            var upScale = m.lossyScale;
            Vector3 upPos = m.GetColumn(3);
            if (fingerShadowOf.TryGetValue(x, out var fs))
            {
                float deg = Quaternion.Angle(fs.upRot, upRot);
                float meters = Vector3.Distance(fs.upPos, upPos) * x.parent.lossyScale.x;
                if ((deg > SleeveRollPb.MaxAngle || meters > SleeveRollPb.MaxMeters || !Near(fs.upScale, upScale, 1e-3f)) && fingerWarned.Add((cloth, left, x)))
                    log.AppendLine($"WARN {cloth} {SrSide.Letter(left)}: its bind pose holds {x.name} {deg:0.#}° and {meters * 1000f:0.#} mm off {fs.from}'s, scale {Fmt(upScale)} vs {Fmt(fs.upScale)} — the finger shadow follows {fs.from}, the rolled cuff of {cloth} may sit off");
                return fs.shadow;
            }
            var s = SleeveRollPb.Shadow(x, parent, upRot);
            fs = new FingerShadow { shadow = s, bone = x, left = left, from = cloth, upRot = upRot, upPos = upPos, upScale = upScale, downPos = x.localPosition, downScale = x.localScale };
            fingerShadowOf[x] = fs;
            fingerShadows.Add(fs);
            return s;
        }

        void LogFingerShadows()
        {
            foreach (bool left in SrSide.Both)
            {
                var mine = fingerShadows.Where(f => f.left == left).ToList();
                if (mine.Count == 0) continue;
                log.AppendLine($"  {SrSide.ProxyName(left)}: {mine.Count} finger shadow bones ({string.Join(", ", mine.Select(f => f.bone.name))})");
                foreach (var f in mine.Where(f => f.PosCurve || f.ScaleCurve))
                    log.AppendLine($"  {f.shadow.name}: position {FmtP(f.downPos)} scale {Fmt(f.downScale)} while down, position {FmtP(f.upPos)} scale {Fmt(f.upScale)} while rolled");
            }
        }

        static string FmtP(Vector3 v) => $"({v.x:0.#####}, {v.y:0.#####}, {v.z:0.#####})";

        /// <summary>
        /// 손 크기를 본 스케일로 바꾼 아바타(감사 P2): 내린 동안 대리 본 스케일 = 손 본 스케일(옮긴 정점·체인이 손과 똑같이). 체인을 대리 본 밑으로 옮기기 전에 넣는다.
        /// 스케일이 1이면 아무것도 안 한다 — 출력이 전과 같다
        /// </summary>
        void ScaleProxies()
        {
            foreach (bool left in SrSide.Both)
            {
                var lower = animator.GetBoneTransform(SrSide.Lower(left));
                var hand = animator.GetBoneTransform(SrSide.Hand(left));
                var proxy = lower != null ? lower.Find(SrSide.ProxyName(left)) : null;
                if (proxy == null || hand == null) continue;
                var s = HandScale(lower, hand);
                downScale[left ? 0 : 1] = s;
                if (s != Vector3.one) proxy.localScale = s;
            }
        }

        /// <summary>
        /// 남은 대리 본마다 FX 곡선 값 — 내림 = 손 본 스케일, 걷음 = 바인드 포즈 손 스케일(걷은 모양은 바인드 포즈에서 아래팔 기준으로 계산했다).
        /// 웨이트를 옮긴 옷이 없고 체인만 옮긴 쪽은 그 체인을 건 옷의 바인드 포즈로, 그것도 없으면 걷음 = 내림. 둘이 같으면 곡선이 필요 없다
        /// </summary>
        void ProxyScaleCurves()
        {
            foreach (bool left in SrSide.Both)
            {
                int i = left ? 0 : 1;
                var lower = animator.GetBoneTransform(SrSide.Lower(left));
                var hand = animator.GetBoneTransform(SrSide.Hand(left));
                var proxy = lower != null ? lower.Find(SrSide.ProxyName(left)) : null;
                if (proxy == null || hand == null) continue;
                var s = downScale[i];
                var k = upScale[i];
                if (k == null)
                {
                    var chain = pb.Chains.FirstOrDefault(c => c.left == left && c.root != null && c.root.parent == proxy && c.cloth != null);
                    if (chain != null && SleeveRollShape.BindLocalScale(cache.Get(chain.cloth), lower, hand, out var kc)) k = Snap(kc);
                }
                var up = k ?? s;
                if (s == Vector3.one && up == Vector3.one) continue;
                log.AppendLine($"  {proxy.name}: hand bone scale {Fmt(s)} — proxy scale {Fmt(s)} while down, {Fmt(up)} while rolled");
                if (!Near(s, up, 1e-4f)) proxyScales.Add((proxy, s, up));
            }
        }

        /// <summary>손 본의 아래팔 기준 스케일 — 손에서 아래팔 앞까지 부모 사슬 localScale의 성분별 곱. 1에서 1e-4 안이거나 손이 아래팔 밑이 아니면 정확히 1</summary>
        static Vector3 HandScale(Transform lower, Transform hand)
        {
            if (!hand.IsChildOf(lower)) return Vector3.one;
            var s = Vector3.one;
            for (var t = hand; t != lower; t = t.parent) s = Vector3.Scale(s, t.localScale);
            return Snap(s);
        }

        // FBX가 남긴 0.9999999 같은 값은 정확히 1로 — 스케일 1인 아바타의 출력이 바뀌지 않게
        static Vector3 Snap(Vector3 s) => Near(s, Vector3.one, 1e-4f) ? Vector3.one : s;

        static bool Near(Vector3 a, Vector3 b, float eps) => Mathf.Abs(a.x - b.x) <= eps && Mathf.Abs(a.y - b.y) <= eps && Mathf.Abs(a.z - b.z) <= eps;

        static string Fmt(Vector3 v) => $"({v.x:0.###}, {v.y:0.###}, {v.z:0.###})";

        // 몸 숨김 되돌리기(D7·D10) — 걷힌 팔뚝을 숨기던 셰이프를 거꾸로. 경계는 천이 움직이기 시작하는 자리(덜 걷힌 소매 밑)
        void AddUnhide(Job j, SkinnedMeshRenderer body, bool left)
        {
            // FX가 되돌리기 곡선을 넣는 조건(SleeveRollDriver)과 같게
            if (!SrSide.On(j.c, left) || !Unhides(j.c)) return;
            if (!j.reports.Any(r => r.left == left && r.ok)) return;
            // 천이 움직이기 시작하는 자리(t0)부터 손목 쪽 전부 — 소맷단 근처만 되살리면 소매 구멍 안 깊은 데서 가는 팔이 보인다(10-8).
            // 걷은 자리는 계산이 피부 끊김 때문에 더 밀었을 수 있다 — 보고값으로
            float rolledTo = j.reports.First(r => r.left == left && r.ok).rolledTo;
            float tCut = Mathf.Min((left ? j.sL : j.sR).t0, rolledTo - 0.05f);
            var name = SrSide.UnhideName(left);
            Mesh previewCopy = null;
            if (previewMeshes != null) previewMeshes.TryGetValue(body, out previewCopy);
            if ((previewCopy != null ? previewCopy : body.sharedMesh).GetBlendShapeIndex(name) >= 0) return; // 다른 SleeveRoll이 이미 넣음
            if (unhideDone.Contains((body, left))) return;   // 몫으로만 나눠 넣어 위 이름이 없는 경우
            var shares = live != null ? new List<SleeveRollShape.UnhideShare>() : null;
            var d = SleeveRollShape.UnhideDelta(body, animator, left, tCut, cover[left ? 0 : 1], cache, out int nv, out string shapes, out string spared,
                live != null ? (System.Func<string, bool>)(k => live(body, k)) : null, shares, out float err);
            if (nv == 0)
            {
                if (spared.Length > 0) log.AppendLine($"  {body.name} {SrSide.Letter(left)}: nothing to restore — [{spared}] left as set (not under the sleeve)");
                return;
            }
            unhideDone.Add((body, left));
            var mesh = BodyCopy(body, previewCopy);
            if (d != null) mesh.AddBlendShapeFrame(name, 100f, d, null, null);
            (left ? j.g.left : j.g.right).unhide += nv;
            log.AppendLine($"  {body.name} {SrSide.Letter(left)}: {name} restores {nv} vertices hidden by [{shapes}]" +
                           (spared.Length > 0 ? $"; [{spared}] left as set (not under the sleeve)" : ""));
            if (shares == null || shares.Count == 0) return;
            foreach (var s in shares)
            {
                var sn = name + "__" + s.shape;
                if (mesh.GetBlendShapeIndex(sn) >= 0) continue;
                mesh.AddBlendShapeFrame(sn, 100f, s.delta, null, null);
                liveUnhides.Add(new LiveUnhide { body = body, left = left, hide = s.shape, shape = sn, weight = s.weight });
            }
            log.AppendLine($"  {body.name} {SrSide.Letter(left)}: split by hide shape — {string.Join(", ", shares.Select(s => name + "__" + s.shape))} follow their shape in FX" +
                           (d != null ? $", {name} keeps the rest" : $", nothing fixed left ({name} not added)") + $", max |sum − one shape| {err * 1000f:0.######} mm");
        }

        // 셰이프를 넣을 몸 메시 사본 — 처음 넣을 때 만든다. 미리 보기는 렌더러를 안 건드린다
        Mesh BodyCopy(SkinnedMeshRenderer body, Mesh previewCopy)
        {
            if (previewCopy != null) return previewCopy;
            if (previewMeshes == null && bodyCopies.Contains(body.sharedMesh)) return body.sharedMesh;
            var copy = Object.Instantiate(cache.Get(body).mesh);
            copy.name = copy.name.Replace("(Clone)", "_SleeveRoll");
            if (previewMeshes != null) previewMeshes[body] = copy;
            else
            {
                body.sharedMesh = copy;
                bodyCopies.Add(copy);
                cache.Adopt(body, copy);
            }
            return copy;
        }

        /// <summary>몸 숨김 되돌리기(D7)를 걸까 — 'shrink 해제' 체크 하나(D16). 끄면 shrink를 두고 경계에서 멈춘다(StopAtHidden)</summary>
        public static bool Unhides(SleeveRoll c) => c.unhideWhenRolled;

        /// <summary>피부를 잴 메시 — 지정한 몸 메시, 아니면 고른 옷(어느 Sleeve Roll이든)을 뺀 스킨드 메시 전부. 자동일 땐 꺼 둔 오브젝트는 뺀다(꺼진 옷 변형을 피부로 재면 소매가 부푼다)</summary>
        public static List<SkinnedMeshRenderer> Skins(SleeveRoll c, IEnumerable<SkinnedMeshRenderer> all, ICollection<SkinnedMeshRenderer> picked)
            => c.bodyMeshes != null && c.bodyMeshes.Any(s => s != null)
                ? c.bodyMeshes.Where(s => s != null && !picked.Contains(s)).ToList()
                : all.Where(s => !picked.Contains(s) && s.gameObject.activeInHierarchy).ToList();

        /// <summary>Generating에서 만든 대리 본 중 옮겨진 정점이 없고 옮겨 온 체인도 없는 것(그쪽 실패·꺼짐)은 뺀다 — FX 곡선은 빈 경로가 되어 아무 일도 안 한다</summary>
        void DropUnusedProxies()
        {
            var used = new HashSet<Transform>(root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .SelectMany(r => r.bones).Where(b => b != null));
            foreach (bool left in SrSide.Both)
            {
                var lower = animator.GetBoneTransform(SrSide.Lower(left));
                var name = SrSide.ProxyName(left);
                var p = lower != null ? lower.Find(name) : null;
                if (p == null || used.Contains(p) || p.childCount > 0) continue;
                Object.DestroyImmediate(p.gameObject);
                log.AppendLine($"  {name}: no vertices bound — removed");
            }
        }

        // 그쪽 이름의 셰이프를 빼고 다시 만든다(블렌드셰이프 하나만 지우는 API가 없다) — 꺼 둔 쪽 이름이 옷에 원래 있을 때, 그리고 PhysBone 판정에서 실패로 돌린 쪽(SortPhysBones).
        // Build가 꺼 둔 쪽을 안 넣으니 꺼 둔 쪽은 보통 바로 돌아간다
        static void RemoveShape(Mesh mesh, bool left)
        {
            var name = SrSide.ShapeName(left);
            if (mesh.GetBlendShapeIndex(name) < 0) return;
            var keep = new List<(string n, float w, Vector3[] v, Vector3[] nn, Vector3[] tt)>();
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                var sn = mesh.GetBlendShapeName(i);
                if (sn == name) continue;
                for (int f = 0; f < mesh.GetBlendShapeFrameCount(i); f++)
                {
                    var v = new Vector3[mesh.vertexCount]; var nn = new Vector3[mesh.vertexCount]; var tt = new Vector3[mesh.vertexCount];
                    mesh.GetBlendShapeFrameVertices(i, f, v, nn, tt);
                    keep.Add((sn, mesh.GetBlendShapeFrameWeight(i, f), v, nn, tt));
                }
            }
            mesh.ClearBlendShapes();
            foreach (var k in keep) mesh.AddBlendShapeFrame(k.n, k.w, k.v, k.nn, k.tt);
        }
    }
}
