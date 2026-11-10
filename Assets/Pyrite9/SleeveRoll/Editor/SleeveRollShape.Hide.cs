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
        /// tCut 경계는 걷힌 천 뭉치 밑이라 늘어난 면이 가려진다. 되돌릴 게 없으면 null
        /// </summary>
        internal static Vector3[] UnhideDelta(SkinnedMeshRenderer body, Animator animator, bool left, float tCut, Cache cache,
            out int vertices, out string shapes)
        {
            vertices = 0; shapes = "";
            var h = ActiveHide(body, animator, left, cache);
            if (h == null) return null;
            var outD = new Vector3[h.sum.Length];
            for (int v = 0; v < outD.Length; v++)
            {
                if (!h.hidden[v] || h.t[v] < tCut) continue;
                outD[v] = -h.sum[v];
                vertices++;
            }
            if (vertices == 0) return null;
            shapes = h.shapes;
            return outD;
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
                var verts = en.Vertices;
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
                for (int s = 0; s < 2; s++)
                    if (moved[s]) names[s].Add(mesh.GetBlendShapeName(i) + "=" + w.ToString("0")); // 팔뚝을 움직인 셰이프만 로그에
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

        // 셰이프 값 w(0~100)일 때의 정점 이동 — 프레임이 여러 개면 사이를 선형 보간. a·b는 프레임 읽기용 작업 배열(정점 수)
        static bool FrameDelta(Mesh mesh, int shape, float w, Vector3[] outV, Vector3[] a, Vector3[] b)
        {
            int fc = mesh.GetBlendShapeFrameCount(shape);
            if (fc == 0) return false;
            int n = mesh.vertexCount;
            int f1 = 0;
            while (f1 < fc - 1 && mesh.GetBlendShapeFrameWeight(shape, f1) < w) f1++;
            float w1 = mesh.GetBlendShapeFrameWeight(shape, f1);
            mesh.GetBlendShapeFrameVertices(shape, f1, b, null, null);
            if (f1 == 0)
            {
                float k = w1 != 0 ? w / w1 : 0f;
                for (int v = 0; v < n; v++) outV[v] = b[v] * k;
                return true;
            }
            float w0 = mesh.GetBlendShapeFrameWeight(shape, f1 - 1);
            mesh.GetBlendShapeFrameVertices(shape, f1 - 1, a, null, null);
            float s = Mathf.Approximately(w1, w0) ? 1f : (w - w0) / (w1 - w0);
            for (int v = 0; v < n; v++) outV[v] = Vector3.LerpUnclamped(a[v], b[v], s);
            return true;
        }
    }
}
