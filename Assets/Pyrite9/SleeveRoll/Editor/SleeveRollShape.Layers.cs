// Pyrite9 Sleeve Roll — 계산: 소매 겹 두께(안감·소맷단 접힘)
using System.Collections.Generic;
using UnityEngine;

namespace Pyrite9.Sleeve
{
    public static partial class SleeveRollShape
    {
        /// <summary>
        /// 소매 정점마다 겹 두께(메시 단위): 정점에서 축으로 곧게(같은 t·θ) 쏜 선이 maxDepth 안에서 만나는 소매 면 중 가장 깊은 것까지의 거리, 없으면 0.
        /// 가장 깊은 것까지라 세 겹이면 바깥 겹이 두 겹 몫을 갖는다 — 걷은 뒤에도 겹 사이 거리가 그대로(남는 헐렁함이 겹마다 같아짐).
        /// 삼각형은 (t, θ) 칸에 걸치는 범위로 미리 나눠 정점 칸만 본다(선이 축으로 곧게라 맞는 점은 같은 칸)
        /// </summary>
        static float[] Layers(Cache.Entry ce, Sleeve sl, in Forearm f, float maxDepth, float minDepth)
        {
            var verts = ce.Vertices; var tris = ce.Triangles;
            var idx = sl.idx; var T = sl.T; var A = sl.A; var inSleeve = sl.member;
            int n = idx.Length;
            var depth = new float[n];
            // 같은 위치 정점(UV 이음새)은 한 정점으로 — 제 면을 맞히지 않게
            var key = sl.key; int welded = sl.welded;
            var bins = new List<int>[NT * NA];
            // 정점 원통 좌표는 정점마다 한 번만 — 삼각형마다 다시 재면 Atan2가 6배
            var ct = new float[verts.Length]; var ca = new float[verts.Length]; var known = new bool[verts.Length];
            for (int k = 0; k < n; k++) { ct[idx[k]] = T[k]; ca[idx[k]] = A[k]; known[idx[k]] = true; }
            float tScale = NT / (TMax - TMin), aScale = NA / (2f * Mathf.PI);
            for (int c = 0; c + 2 < tris.Length; c += 3)
            {
                int i0 = tris[c], i1 = tris[c + 1], i2 = tris[c + 2];
                if (!inSleeve[i0] && !inSleeve[i1] && !inSleeve[i2]) continue;
                KnowCyl(f, verts, i0, ct, ca, known); KnowCyl(f, verts, i1, ct, ca, known); KnowCyl(f, verts, i2, ct, ca, known);
                float ta = ct[i0], tb = ct[i1], tc = ct[i2], aa = ca[i0], ab = ca[i1], ac = ca[i2];
                // 각도는 첫 꼭짓점 기준으로 펴서 범위를 잡는다(−π/π 경계)
                if (ab - aa > Mathf.PI) ab -= 2f * Mathf.PI; else if (aa - ab > Mathf.PI) ab += 2f * Mathf.PI;
                if (ac - aa > Mathf.PI) ac -= 2f * Mathf.PI; else if (aa - ac > Mathf.PI) ac += 2f * Mathf.PI;
                float aMin = Mathf.Min(aa, Mathf.Min(ab, ac)), aMax = Mathf.Max(aa, Mathf.Max(ab, ac));
                if (aMax - aMin > Mathf.PI) continue;   // 축을 감싸는 면 — 소매 겹이 아니다
                int t0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(ta, Mathf.Min(tb, tc)) - TMin) * tScale), 0, NT - 1);
                int t1 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(ta, Mathf.Max(tb, tc)) - TMin) * tScale), 0, NT - 1);
                int a0 = Mathf.FloorToInt((aMin + Mathf.PI) * aScale), a1 = Mathf.FloorToInt((aMax + Mathf.PI) * aScale);
                for (int ti = t0; ti <= t1; ti++)
                    for (int aj = a0; aj <= a1; aj++)
                    {
                        int b = ti * NA + ((aj % NA) + NA) % NA;
                        (bins[b] ?? (bins[b] = new List<int>())).Add(c);
                    }
            }
            for (int k = 0; k < n; k++)
            {
                int i = idx[k];
                var list = bins[TBin(T[k]) * NA + ABin(A[k])];
                if (list == null) continue;
                var p = verts[i];
                var w = p - f.e;
                var radial = w - f.ax * Vector3.Dot(w, f.ax);
                float rl = radial.magnitude;
                if (rl < 1e-9f) continue;
                var dir = -radial / rl;
                float best = 0f, limit = Mathf.Min(maxDepth, rl);
                int own0 = key[i];
                for (int e = 0; e < list.Count; e++)
                {
                    int c = list[e];
                    int i0 = tris[c], i1 = tris[c + 1], i2 = tris[c + 2];
                    if (key[i0] == own0 || key[i1] == own0 || key[i2] == own0) continue;   // 제 면
                    float d = Ray(p, dir, verts[i0], verts[i1], verts[i2]);
                    if (d > minDepth && d <= limit && d > best) best = d;
                }
                depth[k] = best;
            }
            // 이웃 한 칸 최댓값으로 넓힌다 — 겹친 덧단(커프스 단추 자리) 가장자리 정점은 선이 틈으로 빠져 아래 겹을 못 맞히는데,
            // 그 정점만 0이면 덧단 끝이 아래 겹 쪽으로 내려앉아 틈이 벌어진다(Kipfel 셔츠, 10-15)
            var own = new float[welded];
            for (int k = 0; k < n; k++) own[key[idx[k]]] = Mathf.Max(own[key[idx[k]]], depth[k]);
            var grown = (float[])own.Clone();
            for (int c = 0; c + 2 < tris.Length; c += 3)
            {
                int k0 = key[tris[c]], k1 = key[tris[c + 1]], k2 = key[tris[c + 2]];
                if (k0 < 0 || k1 < 0 || k2 < 0) continue;
                float m = Mathf.Max(own[k0], Mathf.Max(own[k1], own[k2]));
                if (m > grown[k0]) grown[k0] = m;
                if (m > grown[k1]) grown[k1] = m;
                if (m > grown[k2]) grown[k2] = m;
            }
            for (int k = 0; k < n; k++) depth[k] = grown[key[idx[k]]];
            return depth;
        }

        static void KnowCyl(in Forearm f, Vector3[] verts, int i, float[] ct, float[] ca, bool[] known)
        {
            if (known[i]) return;
            f.Cyl(verts[i], out ct[i], out _, out ca[i]);
            known[i] = true;
        }

        // 선(p + dir·d)이 삼각형과 만나는 d(양면), 안 만나면 −1 — Möller–Trumbore.
        // float로 풀어 쓰지 않는다: Mono에선 끝자리가 달라져(겹 두께 ±1 ULP) 빌드 결과가 바뀐다(10-40)
        static float Ray(in Vector3 p, in Vector3 dir, in Vector3 a, in Vector3 b, in Vector3 c)
        {
            var e1 = b - a; var e2 = c - a;
            var h = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, h);
            if (Mathf.Abs(det) < 1e-12f) return -1f;
            float inv = 1f / det;
            var s = p - a;
            float bu = Vector3.Dot(s, h) * inv;
            if (bu < 0f || bu > 1f) return -1f;
            var q = Vector3.Cross(s, e1);
            float bv = Vector3.Dot(dir, q) * inv;
            if (bv < 0f || bu + bv > 1f) return -1f;
            return Vector3.Dot(e2, q) * inv;
        }
    }
}
