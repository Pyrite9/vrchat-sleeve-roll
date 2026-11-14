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
        {
            vertices = 0; shapes = ""; spared = ""; err = 0f;
            var h = ActiveHide(body, animator, left, cache);
            if (h == null) return null;
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
            for (int v = 0; v < outD.Length; v++)
            {
                if (!hidden[v] || h.t[v] < tCut) continue;
                outD[v] = -sum[v];
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
                if (d != null && live(p.shape)) shares.Add(new UnhideShare { shape = p.shape, weight = p.weight, delta = d });
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

        /// <summary>되돌리기의 숨김 셰이프 하나 몫 — FX가 그 셰이프를 움직일 때 따로 둔다(SleeveRollUnhideFx). weight = 빌드 때 렌더러 값</summary>
        internal sealed class UnhideShare
        {
            public string shape;
            public float weight;
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
            if (h == null) return false;
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
                var h = new HideInfo { t = new float[n], hidden = new bool[n], arm = new bool[n] };
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
                if (!any) hs[s] = null;
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
