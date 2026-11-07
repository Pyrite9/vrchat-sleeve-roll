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

        public SleeveRollBake(GameObject root, Animator animator, StringBuilder log, SrBuildRecord.Entry rec, HashSet<string> deleteTargets)
            : this(root, animator, log, rec, deleteTargets, new SleeveRollShape.Cache(), null) { }

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
                if (i < jobs.Count - 1) rolled[j.cloth] = RolledVertices(j.mesh);
            }
            SortPhysBones(jobs);
            foreach (var j in jobs) Finish(j);
            if (previewMeshes == null) pb.Reparent();
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
                               $"forearm={r.forearmMeters:F3}m end={r.sleeveEnd:F2} r={r.clothRadiusMm:F1}mm skin={r.skinRadiusMm:F1}mm {r.message}");
                if (!string.IsNullOrEmpty(r.note)) log.AppendLine($"  {j.cloth.name} {side}: {r.note}");
                var gs = r.left ? j.g.left : j.g.right;
                gs.ok = r.ok; gs.rolledTo = r.rolledTo; gs.moved = r.movedVertices; gs.message = r.message;
                gs.stopped = r.left ? stoppedL : stoppedR;
                if (!r.ok) log.AppendLine($"WARN {j.cloth.name} {side}: {r.message}");
            }
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
        static Vector3[] RolledVertices(Mesh mesh)
        {
            var v = mesh.vertices;
            var d = new Vector3[v.Length];
            foreach (bool left in SrSide.Both)
            {
                int si = mesh.GetBlendShapeIndex(SrSide.ShapeName(left));
                if (si < 0) continue;
                mesh.GetBlendShapeFrameVertices(si, mesh.GetBlendShapeFrameCount(si) - 1, d, null, null);
                for (int i = 0; i < v.Length; i++) v[i] += d[i];
            }
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
            if (!SleeveRollShape.BindLocalRotation(j.cloth, lower, hand, out var rest))
            {
                log.AppendLine($"  {j.cloth.name} {side}: no hand weights — wrist proxy skipped");
                return;
            }
            var proxy = SleeveRollDriver.Proxy(lower, hand, rest, SrSide.ProxyName(r.left));
            int moved = SleeveRollShape.Rebind(j.cloth, j.mesh, hand, proxy, r.roll, pb.Keep);
            (r.left ? j.g.left : j.g.right).rebound = moved;
            log.AppendLine($"  {j.cloth.name} {side}: wrist proxy {proxy.name}, rebound {moved} vertices");
        }

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
            var d = SleeveRollShape.UnhideDelta(body, animator, left, tCut, cache, out int nv, out string shapes);
            if (d == null) return;
            BodyCopy(body, previewCopy).AddBlendShapeFrame(name, 100f, d, null, null);
            (left ? j.g.left : j.g.right).unhide += nv;
            log.AppendLine($"  {body.name} {SrSide.Letter(left)}: {name} restores {nv} vertices hidden by [{shapes}]");
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

        /// <summary>몸 숨김 되돌리기(D7)를 걸까 — 숨김 경계 너머로 걷거나(경계에서 안 멈춤), 걷힌 동안 되살리기를 켰을 때(D10)</summary>
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

        // 꺼 둔 쪽 이름의 셰이프가 옷에 원래 있으면 빼고 다시 만든다(블렌드셰이프 하나만 지우는 API가 없다). Build가 꺼 둔 쪽을 안 넣으니 보통은 바로 돌아간다
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
