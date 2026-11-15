// Pyrite9 Sleeve Roll — 계산: 옷이 줄여 둔 몸(숨김 경계 D8, 되돌리기 D7·D10)
using System.Collections.Generic;
using UnityEngine;

namespace Pyrite9.Sleeve
{
    public static partial class SleeveRollShape
    {
        /// <summary>
        /// 몸 숨김 되돌리기(D7): 옷이 덮는다고 MA Shape Changer 등이 켜 둔 셰이프(예: Salt LowerArm_OFF=100)가 걷힌 팔뚝까지 숨긴다.
        /// 지금 렌더러에 걸린 셰이프 값들의 합쳐진 정점 이동을, 팔뚝의 tCut 이후(걷은 소맷단 밑 ~ 손목 너머) 정점만 거꾸로 되돌리는 셰이프를 돌려준다.
        /// tCut 경계는 걷힌 천 뭉치 밑이라 늘어난 면이 가려진다. 되돌릴 게 없으면 null.
        /// 되돌리는 건 소매 밑을 숨기던 셰이프만이다 — 셰이프마다, 그 셰이프가 움직이는 팔·손 정점의 과반이 소매가 덮던 구간(t ≤ coverEnd, 걷는 옷들의 소매 끝)에
        /// 있어야 한다. 손톱 모양·손 크기처럼 소매 밖을 움직이는 셰이프, 장갑 밑 손을 숨기는 셰이프는 그대로 둔다(spared) — 전엔 합을 통째로 되돌려
        /// 걷는 동안 손톱이 기본 모양으로 돌아갔다(감사 0.1.3 P1). 빠지는 셰이프가 없으면 전과 같은 값이다
        /// </summary>
        internal static Vector3[] UnhideDelta(SkinnedMeshRenderer body, Animator animator, bool left, float tCut, float coverEnd, Cache cache,
            out int vertices, out string shapes, out string spared)
            => UnhideDelta(body, animator, left, tCut, coverEnd, cache, out vertices, out shapes, out spared, null, null, out _);

        /// <summary>
        /// 되돌리기를 숨김 셰이프별 몫으로 나눈다(옷 토글로 숨김이 풀리면 그 몫만 빠지게 — FX가 그 셰이프를 움직이는 경우).
        /// live(셰이프 이름)가 참인 셰이프는 몫(−그 셰이프의 이동, 되살리는 정점만)을 shares에 따로 담고, 돌려주는 값은 나머지(전체 − 따로 담은 몫 — 고정 숨김).
        /// 몫이 다 0인 셰이프(되살리는 정점을 안 움직임)는 안 담는다. 나머지가 거의 0이면 null. err = 남기는 셰이프들의 합과 나누기 전 값의 최대 차(메시 단위, 나누지 않으면 0).
        /// live가 null이거나 참인 셰이프가 없으면 위 오버로드와 같은 배열이다. vertices는 어느 쪽이든 전체 되살림 정점 수
        /// </summary>
        internal static Vector3[] UnhideDelta(SkinnedMeshRenderer body, Animator animator, bool left, float tCut, float coverEnd, Cache cache,
            out int vertices, out string shapes, out string spared, System.Func<string, bool> live, List<UnhideShare> shares, out float err)
            => UnhideDelta(body, animator, left, tCut, coverEnd, cache, out vertices, out shapes, out spared, live, shares, out err, null, null, out _);

        /// <summary>
        /// 위 오버로드 + 빌드 때 0인 숨김(0.1.3 B): fxRef(셰이프 이름 → FX가 쓰는 값 중 절댓값이 가장 큰 값, 안 쓰면 0)가 있으면 ZeroShares가 그 셰이프의 몫을
        /// shares에 더한다(zero). 켜진 셰이프의 돌려주는 값·몫·err는 fxRef가 없을 때와 같다. zeroVertices = 그 몫이 새로 되살린 정점 수(vertices에 없는 것만).
        /// notes: 몫을 못 만든 후보의 이유·WARN 줄(부르는 쪽이 로그에)
        /// </summary>
        internal static Vector3[] UnhideDelta(SkinnedMeshRenderer body, Animator animator, bool left, float tCut, float coverEnd, Cache cache,
            out int vertices, out string shapes, out string spared, System.Func<string, bool> live, List<UnhideShare> shares, out float err,
            System.Func<string, float> fxRef, List<string> notes, out int zeroVertices)
        {
            zeroVertices = 0;
            var d = ActiveUnhide(body, animator, left, tCut, coverEnd, cache, out vertices, out shapes, out spared, live, shares, out err, out var restored, out var h);
            if (fxRef != null && shares != null && h != null) zeroVertices = ZeroShares(body, cache, h, restored, tCut, coverEnd, fxRef, shares, notes);
            return d;
        }

        // 켜진 숨김 셰이프의 되돌리기(0.1.3 B 전 UnhideDelta 그대로). restored: 되살린 정점(돌려주는 값이 null이면 null), h: 그 몸·쪽의 ActiveHide
        static Vector3[] ActiveUnhide(SkinnedMeshRenderer body, Animator animator, bool left, float tCut, float coverEnd, Cache cache,
            out int vertices, out string shapes, out string spared, System.Func<string, bool> live, List<UnhideShare> shares, out float err,
            out bool[] restored, out HideInfo h)
        {
            vertices = 0; shapes = ""; spared = ""; err = 0f; restored = null;
            h = ActiveHide(body, animator, left, cache);
            if (h == null || h.none) return null;
            var sum = h.sum; var hidden = h.hidden; shapes = h.shapes;
            List<HidePart> drop = null;
            foreach (var p in h.parts)
            {
                int moved = 0, under = 0;
                for (int v = 0; v < p.delta.Length; v++)
                {
                    if (!h.arm[v] || p.delta[v].magnitude < h.eps) continue;
                    moved++;
                    if (h.t[v] <= coverEnd) under++;
                }
                if (under * 2 < moved) (drop ?? (drop = new List<HidePart>())).Add(p);
            }
            if (drop != null)
            {
                // 뺀 셰이프의 이동을 합에서 덜어 낸다 — 그 셰이프만 움직인 정점은 정확히 0이 되어 숨김에서 빠진다
                sum = (Vector3[])h.sum.Clone();
                foreach (var p in drop)
                    for (int v = 0; v < sum.Length; v++) sum[v] -= p.delta[v];
                hidden = new bool[sum.Length];
                for (int v = 0; v < sum.Length; v++) hidden[v] = h.arm[v] && sum[v].magnitude >= h.eps;
                var kept = new List<string>(); var gone = new List<string>();
                foreach (var p in h.parts) (drop.Contains(p) ? gone : kept).Add(p.name);
                shapes = string.Join(", ", kept); spared = string.Join(", ", gone);
            }
            var outD = new Vector3[sum.Length];
            restored = new bool[sum.Length];
            for (int v = 0; v < outD.Length; v++)
            {
                if (!hidden[v] || h.t[v] < tCut) continue;
                outD[v] = -sum[v];
                restored[v] = true;
                vertices++;
            }
            if (vertices == 0) return null;
            if (live == null || shares == null) return outD;
            int before = shares.Count;
            foreach (var p in h.parts)
            {
                if (drop != null && drop.Contains(p)) continue;
                Vector3[] d = null;
                for (int v = 0; v < outD.Length; v++)
                {
                    if (!hidden[v] || h.t[v] < tCut || p.delta[v] == Vector3.zero) continue;
                    (d ?? (d = new Vector3[outD.Length]))[v] = -p.delta[v];
                }
                if (d != null && live(p.shape)) shares.Add(new UnhideShare { shape = p.shape, built = p.weight, reference = p.weight, delta = d });
            }
            if (shares.Count == before) return outD;
            // 나머지 = 전체 − 따로 담은 몫 — 몫의 합이 늘 전체와 같다(숨김 셰이프가 아닌 셰이프의 아주 작은 이동도 나머지에 남는다)
            var rest = (Vector3[])outD.Clone();
            float most = 0f;
            for (int s = before; s < shares.Count; s++)
                for (int v = 0; v < rest.Length; v++) rest[v] -= shares[s].delta[v];
            for (int v = 0; v < rest.Length; v++) most = Mathf.Max(most, rest[v].magnitude);
            if (most < h.eps * 1e-3f) rest = null;
            // 기록용: 남기는 셰이프들의 합과 나누기 전 셰이프의 최대 차(메시 단위)
            for (int v = 0; v < outD.Length; v++)
            {
                var t = rest != null ? rest[v] : Vector3.zero;
                for (int s = before; s < shares.Count; s++) t += shares[s].delta[v];
                err = Mathf.Max(err, (t - outD[v]).magnitude);
            }
            return rest;
        }

        /// <summary>
        /// 빌드 때 0인 숨김(0.1.3 B) — 몸 셰이프 중 빌드 때 렌더러 값이 0(|w| &lt; 0.01)이고 FX가 0 아닌 값을 쓰는 것(re = fxRef, 절댓값이 가장 큰 값)이
        /// 이쪽 팔뚝을 숨기면, re만큼의 이동을 되돌리는 몫(UnhideShare zero)을 shares에 더한다 — FX가 그 셰이프를 켜는 동안만 걷힌 팔이 돌아온다
        /// (SleeveRollUnhideFx: 미러 = 값 ÷ re, 기본값 0). 켜진 셰이프의 합·나머지와는 따로(블렌드셰이프는 선형 합).
        /// 숨김 판정: 켜진 셰이프와 같은 규칙(이쪽 팔·손 정점을 eps 넘게 움직이고 그 과반이 t ≤ coverEnd) + 팔뚝 구간(0 ≤ t ≤ 1)에서 움직인 정점이 있고
        /// 그 이동량 가중 '축 거리가 줄어드는' 비율 ≥ SleeveRollConst.ZeroHideInward(0인 채 FX가 켜는 팔 굵기·근육 셰이프를 집지 않게) — 켜진 셰이프엔 이 조건이 없다.
        /// 팔·손을 안 움직이는 셰이프(얼굴 등)는 말없이 건너뛰고, 움직였지만 탈락한 셰이프는 notes에 이유 한 줄. 몫 정점은 켜진 몫과 같은 규칙(팔 정점, 이동 ≥ eps, t ≥ tCut).
        /// 프레임이 여럿이면 몫은 만들고 WARN 줄(값에 선형이라고 본다). 숨김 경계(HiddenEdge, shrink 해제 끔)엔 안 넣는다(B-d2). 새로 되살린 정점 수를 돌려준다
        /// </summary>
        static int ZeroShares(SkinnedMeshRenderer body, Cache cache, HideInfo h, bool[] restored, float tCut, float coverEnd,
            System.Func<string, float> fxRef, List<UnhideShare> shares, List<string> notes)
        {
            var en = cache.Get(body);
            var mesh = en.mesh;
            int n = mesh.vertexCount, added = 0;
            if (restored == null) restored = new bool[n];
            Vector3[] bv = null, dv = null, fa = null, fb = null;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                var name = mesh.GetBlendShapeName(i);
                if (OwnShape(name)) continue;
                float built = en.shapes.GetBlendShapeWeight(i);
                if (Mathf.Abs(built) >= 0.01f) continue;   // 켜진 셰이프는 ActiveUnhide(지금 규칙 그대로)
                float re = fxRef(name);
                if (Mathf.Abs(re) < 0.01f) continue;      // FX가 안 쓰거나 0만 쓴다
                if (dv == null) { bv = en.BaseVertices; dv = new Vector3[n]; fa = new Vector3[n]; fb = new Vector3[n]; }
                if (!FrameDelta(mesh, i, re, dv, fa, fb)) continue;
                int moved = 0, under = 0, fore = 0;
                double wAll = 0, wIn = 0;
                for (int v = 0; v < n; v++)
                {
                    if (!h.arm[v]) continue;
                    float m = dv[v].magnitude;
                    if (m < h.eps) continue;
                    moved++;
                    if (h.t[v] <= coverEnd) under++;
                    if (h.t[v] < 0f || h.t[v] > 1f) continue;
                    fore++;
                    wAll += m;
                    if (h.Radial(bv[v] + dv[v]) < h.Radial(bv[v])) wIn += m;
                }
                if (moved == 0) continue;   // 이쪽 팔·손을 안 움직인다(얼굴·몸통 등)
                string why = under * 2 < moved ? "not under the sleeve"
                    : fore == 0 ? "no forearm vertices"
                    : wIn < SleeveRollConst.ZeroHideInward * wAll ? $"inward {wIn / wAll:0.00}" : null;
                if (why != null)
                {
                    notes?.Add($"[{name}] driven by the FX (up to {re:0.##}) but not a forearm hide ({why}) — left as set");
                    continue;
                }
                Vector3[] d = null;
                int nv = 0;
                for (int v = 0; v < n; v++)
                {
                    if (!h.arm[v] || h.t[v] < tCut || dv[v].magnitude < h.eps) continue;
                    (d ?? (d = new Vector3[n]))[v] = -dv[v];
                    nv++;
                    if (!restored[v]) { restored[v] = true; added++; }
                }
                if (d == null)
                {
                    notes?.Add($"[{name}] driven by the FX (up to {re:0.##}) hides the arm only above where the sleeve rolls — left as set");
                    continue;
                }
                int fc = mesh.GetBlendShapeFrameCount(i);
                if (fc > 1) notes?.Add($"WARN '{name}' (built at 0, the FX sets up to {re:0.##}) has {fc} frames — its share of the restored arm assumes the shape moves in a straight line with its value");
                shares.Add(new UnhideShare { shape = name, built = built, reference = re, delta = d, zero = true, vertices = nv });
            }
            return added;
        }

        /// <summary>
        /// 안쪽 겹은 걷힌 소매가 내준 자리까지만 푼다(0.1.3 A-2c) — 소매가 되돌린 피부보다 가는 새 경로에서만(SideReport.releasedInner).
        /// 걷힌 소매(rolledCloth, 접혀 숨은 정점은 NaN)의 칸별 최소 반지름 Rin을 그 옷의 팔뚝 틀로 재고, 되돌리기 정점 v마다
        /// 입은 반지름 r0·다 풀린 반지름 r1(같은 틀, full = 되돌리기 이동 전체)로 풀기 비율 s = clamp((Rin − margin − r0) / (r1 − r0), 0, 1),
        /// r1 ≤ Rin − margin이면 1. 둘레 칸마다 걷힌 소매가 걸친 t 범위 밖(소매 끝 너머 = 드러난 자리)은 1. 틀을 못 구하면 null(줄이지 않음).
        /// Rin은 채운 뒤 이웃 칸(3×3, 둘레는 돌아감)의 최솟값으로 바꾼다 — SkinAt의 칸 사이 보간이 그 칸의 최솟값보다 커져 점처럼 뚫리지 않게
        /// </summary>
        internal static float[] ReleaseRatio(SkinnedMeshRenderer body, Vector3[] full, SkinnedMeshRenderer cloth, Vector3[] rolledCloth, Animator animator, bool left,
            Cache cache, float marginMeters)
        {
            var ce = cache.Get(cloth);
            if (GetForearm(animator, left, ce.bones, ce.bindposes, out var f, ce.boneMap) != null) return null;
            var w = ce.Weights(f.lower, null, true);
            var Rin = new float[NT, NA];
            var tLo = new float[NA]; var tHi = new float[NA];
            for (int j = 0; j < NA; j++) { tLo[j] = float.MaxValue; tHi[j] = float.MinValue; }
            bool any = false;
            for (int i = 0; i < rolledCloth.Length && i < w.Length; i++)
            {
                if (w[i] <= 0.3f || float.IsNaN(rolledCloth[i].x)) continue;
                f.Cyl(rolledCloth[i], out var t, out var r, out var a);
                int ti = TBin(t), ai = ABin(a);
                if (Rin[ti, ai] <= 0f || r < Rin[ti, ai]) Rin[ti, ai] = r;
                tLo[ai] = Mathf.Min(tLo[ai], t); tHi[ai] = Mathf.Max(tHi[ai], t);
                any = true;
            }
            if (!any) return null;
            FillSkin(Rin);
            var Rf = (float[,])Rin.Clone();
            for (int i = 0; i < NT; i++)
                for (int j = 0; j < NA; j++)
                {
                    float m = Rf[i, j];
                    for (int di = -1; di <= 1; di++)
                    {
                        int ii = i + di;
                        if (ii < 0 || ii >= NT) continue;
                        for (int dj = -1; dj <= 1; dj++) m = Mathf.Min(m, Rf[ii, ((j + dj) % NA + NA) % NA]);
                    }
                    Rin[i, j] = m;
                }
            var be = cache.Get(body);
            if (!LowerBind(be.bones, be.bindposes, f.lower, f.hand, out var bpS, be.boneMap)) return null;
            var toCloth = f.laToMesh * bpS;
            var worn = be.Vertices;
            float margin = marginMeters / f.mPerUnit;
            var s = new float[full.Length];
            for (int v = 0; v < s.Length; v++)
            {
                s[v] = 1f;
                if (full[v] == Vector3.zero) continue;
                f.Cyl(toCloth.MultiplyPoint3x4(worn[v]), out _, out var r0, out _);
                f.Cyl(toCloth.MultiplyPoint3x4(worn[v] + full[v]), out var t1, out var r1, out var a1);
                int aj = ABin(a1);
                if (t1 < tLo[aj] || t1 > tHi[aj]) continue;
                float allow = SkinAt(Rin, t1, a1) - margin;
                if (r1 <= allow) continue;
                s[v] = r1 - r0 > 1e-7f ? Mathf.Clamp01((allow - r0) / (r1 - r0)) : 1f;
            }
            return s;
        }

        /// <summary>
        /// 되돌리기의 숨김 셰이프 하나 몫 — FX가 그 셰이프를 움직일 때 따로 둔다(SleeveRollUnhideFx). built = 빌드 때 렌더러 값,
        /// reference = 몫(delta)이 되돌리는 셰이프 값(켜진 셰이프는 built와 같다, 빌드 때 0인 셰이프는 FX가 쓰는 값 중 절댓값이 가장 큰 값 — zero, 0.1.3 B)
        /// </summary>
        internal sealed class UnhideShare
        {
            public string shape;
            public float built, reference;
            public bool zero;
            public int vertices;   // zero 몫만(로그용)
            public Vector3[] delta;
        }

        /// <summary>
        /// 숨김 경계(D8, 관리자 2026-11-04): 옷 밑이라 줄어든 팔뚝 정점 중 가장 손목 쪽 t(edge)와, 그 너머 처음 안 숨겨진 고리의 t(next).
        /// 소맷단을 next 바로 너머까지만 걷으면 숨겨진 팔(과 edge~next 사이 늘어난 면)이 안 드러난다. 숨김이 없으면 false
        /// </summary>
        internal static bool HiddenEdge(SkinnedMeshRenderer body, Animator animator, bool left, Cache cache, out float edge, out float next, out string shapes)
        {
            edge = next = 0f; shapes = "";
            var h = ActiveHide(body, animator, left, cache);
            if (h == null || h.none) return false;
            edge = float.MinValue;
            for (int v = 0; v < h.t.Length; v++)
                if (h.hidden[v] && h.arm[v] && h.t[v] < 1.2f) edge = Mathf.Max(edge, h.t[v]);
            if (edge == float.MinValue) return false;
            next = float.MaxValue;
            for (int v = 0; v < h.t.Length; v++)
                if (h.arm[v] && !h.hidden[v] && h.t[v] > edge) next = Mathf.Min(next, h.t[v]);
            if (next == float.MaxValue) next = edge;
            shapes = h.shapes;
            return true;
        }

        internal sealed class HideInfo
        {
            public Vector3[] sum;      // 좌우가 같은 배열을 나눠 쓴다(셰이프 합은 쪽과 무관) — 읽기만
            public float[] t;
            public bool[] hidden, arm;
            public string shapes;
            public float eps;          // 이만큼은 움직여야 숨김으로 본다(메시 단위)
            /// <summary>켜진 셰이프가 숨긴 정점이 없다(좌표만 — 빌드 때 0인 숨김 ZeroShares가 쓴다, 0.1.3 B). 전엔 이때 HideInfo가 null이었다</summary>
            public bool none;
            public Vector3 e, ax;      // 팔꿈치·아래팔 축(단위, 메시 공간) — t·Radial
            public float L;
            /// <summary>메시 공간 점의 아래팔 축 거리</summary>
            public float Radial(Vector3 p) { var d = p - e; return (d - ax * Vector3.Dot(d, ax)).magnitude; }
            /// <summary>이쪽 팔·손 정점을 움직인 셰이프 하나씩(shapes에 이름이 나가는 것) — 되돌리기가 소매 밑인지 따로 본다</summary>
            public readonly List<HidePart> parts = new List<HidePart>();
        }

        internal sealed class HidePart
        {
            public string name;        // 로그용 '이름=값'
            public string shape;       // 셰이프 이름
            public float weight;       // 빌드 때 렌더러 값
            public Vector3[] delta;    // 지금 값에서의 정점 이동(읽기만)
        }

        // 지금 렌더러에 걸린 셰이프 값들의 합쳐진 정점 이동 + 팔뚝 좌표. 팔뚝에서 움직인 정점이 없으면 null.
        // 숨김 경계(Shape)와 되돌리기(Finish)가 같은 몸을 묻는다 — 좌우를 한 번에 구해 cache에 둔다(몸 메시 사본이 생기면 다시 구함)
        static HideInfo ActiveHide(SkinnedMeshRenderer body, Animator animator, bool left, Cache cache)
        {
            var mesh = body.sharedMesh;
            if (mesh == null || mesh.blendShapeCount == 0) return null;
            var en = cache.Get(body);
            if (en.hide == null) en.hide = ActiveHideBoth(body, en, animator);
            return en.hide[left ? 0 : 1];
        }

        static HideInfo[] ActiveHideBoth(SkinnedMeshRenderer body, Cache.Entry en, Animator animator)
        {
            var mesh = en.mesh;
            int n = mesh.vertexCount;
            var hs = new HideInfo[2];
            var eps = new float[2];
            for (int s = 0; s < 2; s++)
            {
                if (GetForearm(animator, s == 0, en.bones, en.bindposes, out var f, en.boneMap) != null) continue;
                var wArm = en.Weights(f.lower, null, true);
                bool anyArm = false;   // 팔뚝 웨이트가 없는 메시(머리·신발 등)는 팔뚝 정점이 없다 → 셰이프를 안 읽고 null
                for (int v = 0; v < n && !anyArm; v++) anyArm = wArm[v] >= 0.5f;
                if (!anyArm) continue;
                var verts = en.BaseVertices;   // 셰이프 이동은 아래에서 따로 잰다 — 기본 정점 기준(입은 모양 아님)
                var e = f.e; var ax = f.ax; float L = f.L;
                var h = new HideInfo { t = new float[n], hidden = new bool[n], arm = new bool[n], e = e, ax = ax, L = L };
                for (int v = 0; v < n; v++)
                {
                    var d = verts[v] - e;
                    float t = Vector3.Dot(d, ax) / L;
                    h.t[v] = t;
                    // 팔뚝 원통 안쪽만(손가락 끝·몸통 정점 제외)
                    h.arm[v] = wArm[v] >= 0.5f && (d - ax * (t * L)).magnitude < L * 0.6f;
                }
                eps[s] = L * 0.002f;
                h.eps = eps[s];
                hs[s] = h;
            }
            if (hs[0] == null && hs[1] == null) return hs;

            var sum = new Vector3[n];
            var dv = new Vector3[n]; var fa = new Vector3[n]; var fb = new Vector3[n];
            var names = new[] { new List<string>(), new List<string>() };
            var moved = new bool[2];
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                float w = en.shapes.GetBlendShapeWeight(i);
                if (Mathf.Abs(w) < 0.01f) continue;
                if (!FrameDelta(mesh, i, w, dv, fa, fb)) continue;
                moved[0] = moved[1] = false;
                for (int v = 0; v < n; v++)
                {
                    sum[v] += dv[v];
                    for (int s = 0; s < 2; s++)
                        if (hs[s] != null && !moved[s]) moved[s] = hs[s].arm[v] && dv[v].magnitude >= eps[s];
                }
                HidePart part = null;
                for (int s = 0; s < 2; s++)
                {
                    if (!moved[s]) continue;
                    names[s].Add(mesh.GetBlendShapeName(i) + "=" + w.ToString("0")); // 팔뚝을 움직인 셰이프만 로그에
                    if (part == null) part = new HidePart { name = names[s][names[s].Count - 1], shape = mesh.GetBlendShapeName(i), weight = w, delta = (Vector3[])dv.Clone() };
                    hs[s].parts.Add(part);
                }
            }
            for (int s = 0; s < 2; s++)
            {
                var h = hs[s];
                if (h == null) continue;
                bool any = false;
                for (int v = 0; v < n; v++)
                {
                    h.hidden[v] = h.arm[v] && sum[v].magnitude >= eps[s];
                    any |= h.hidden[v];
                }
                h.sum = sum;
                h.shapes = string.Join(", ", names[s]);
                h.none = !any;   // 켜진 숨김은 없어도 좌표는 둔다(0.1.3 B) — HiddenEdge·ActiveUnhide는 전처럼 '숨김 없음'으로 본다
            }
            return hs;
        }

        // 셰이프 값 w(0~100)일 때의 정점 이동 — 프레임이 여러 개면 사이를 선형 보간. a·b는 프레임 읽기용 작업 배열(정점 수).
        // outN이 있으면 노멀 이동도 같은 보간으로(an·bn 작업 배열) — 입은 모양(Cache.Entry.Wear)
        static bool FrameDelta(Mesh mesh, int shape, float w, Vector3[] outV, Vector3[] a, Vector3[] b,
            Vector3[] outN = null, Vector3[] an = null, Vector3[] bn = null)
        {
            int fc = mesh.GetBlendShapeFrameCount(shape);
            if (fc == 0) return false;
            int n = mesh.vertexCount;
            int f1 = 0;
            while (f1 < fc - 1 && mesh.GetBlendShapeFrameWeight(shape, f1) < w) f1++;
            float w1 = mesh.GetBlendShapeFrameWeight(shape, f1);
            mesh.GetBlendShapeFrameVertices(shape, f1, b, outN != null ? bn : null, null);
            if (f1 == 0)
            {
                float k = w1 != 0 ? w / w1 : 0f;
                for (int v = 0; v < n; v++) outV[v] = b[v] * k;
                if (outN != null) for (int v = 0; v < n; v++) outN[v] = bn[v] * k;
                return true;
            }
            float w0 = mesh.GetBlendShapeFrameWeight(shape, f1 - 1);
            mesh.GetBlendShapeFrameVertices(shape, f1 - 1, a, outN != null ? an : null, null);
            float s = Mathf.Approximately(w1, w0) ? 1f : (w - w0) / (w1 - w0);
            for (int v = 0; v < n; v++) outV[v] = Vector3.LerpUnclamped(a[v], b[v], s);
            if (outN != null) for (int v = 0; v < n; v++) outN[v] = Vector3.LerpUnclamped(an[v], bn[v], s);
            return true;
        }
    }
}
