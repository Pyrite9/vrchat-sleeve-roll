// Pyrite9 Sleeve Roll — 계산: 팔뚝 피부 반지름 격자
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pyrite9.Sleeve
{
    public static partial class SleeveRollShape
    {
        // 피부 반지름 (t, θ) — skins에서 아래팔·손 웨이트가 큰 정점의 칸별 최대 축 거리. occupied: 표본이 있는 t 칸. 표본 수는 rep.skinVertices
        static float[,] SampleSkin(SkinnedMeshRenderer cloth, IList<SkinnedMeshRenderer> skins, IDictionary<SkinnedMeshRenderer, Vector3[]> rolled,
            Cache cache, in Forearm f, out bool[] occupied, ref SideReport rep)
        {
            var R = new float[NT, NA];
            occupied = new bool[NT];
            foreach (var skin in skins)
            {
                if (skin == null || skin == cloth || skin.sharedMesh == null) continue;
                var se = cache.Get(skin);
                // 안쪽 옷이 아래팔 본 없이 그 자손(트위스트 등)에만 묶여 있으면(10-75) 바인드 포즈를 유도하고 웨이트도 자손째로 —
                // 아래팔이 본 목록에 있는 메시는 전과 같다
                bool direct = Array.IndexOf(se.bones, f.lower) >= 0;
                if (!LowerBind(se.bones, se.bindposes, f.lower, f.hand, out var bpS, se.boneMap)) continue;
                var toCloth = f.laToMesh * bpS;
                var sw = se.Weights(f.lower, f.hand, !direct);
                Vector3[] sv = null;   // 팔뚝 웨이트가 있는 메시만 정점을 읽는다(머리카락·신발 등은 건너뜀)
                for (int i = 0; i < sw.Length; i++)
                {
                    if (sw[i] <= 0.3f) continue;
                    if (sv == null)
                        sv = rolled != null && rolled.TryGetValue(skin, out var rv) && rv != null && rv.Length == se.mesh.vertexCount
                            ? rv : se.Vertices;
                    f.Cyl(toCloth.MultiplyPoint3x4(sv[i]), out var t, out var r, out var a);
                    int ti = TBin(t), ai = ABin(a);
                    if (r > R[ti, ai]) R[ti, ai] = r;
                    occupied[ti] = true;
                    rep.skinVertices++;
                }
            }
            return R;
        }

        // 손목 쪽에서 팔꿈치 쪽으로 피부가 이어지는 데까지(t) — 옷 밑을 잘라 낸 몸(Lapwing Body2NoInsideMesh)이면 걷은 자리가 빈 팔이 된다
        static float SkinStart(bool[] occupied)
        {
            int top = TBin(1f);
            while (top > 0 && !occupied[top]) top--;
            int from = top, gapBins = Mathf.CeilToInt(SkinGap / (TMax - TMin) * NT), gap = 0;
            for (int i = top - 1; i >= TBin(0f) && gap <= gapBins; i--)
            {
                if (occupied[i]) { from = i; gap = 0; } else gap++;
            }
            float start = occupied[top] ? TMin + (from + 0.5f) * (TMax - TMin) / NT : 1f;
            if (start <= TMin + (TBin(0f) + 0.5f) * (TMax - TMin) / NT + 1e-4f) start = 0f;
            return start;
        }

        // 빈 칸을 메우고, 번진 값과의 최댓값으로 — 피부 표본 사이로 천이 파고들지 않게
        static void SmoothSkin(float[,] R)
        {
            FillSkin(R);
            var Rb = Blur(R, 2);
            for (int i = 0; i < NT; i++) for (int j = 0; j < NA; j++) R[i, j] = Mathf.Max(R[i, j], Rb[i, j]);
        }

        // 빈 칸: 같은 t의 둘레 이웃 → 가장 가까운 t 줄
        static void FillSkin(float[,] R)
        {
            var filled = new bool[NT];
            for (int i = 0; i < NT; i++)
            {
                bool any = false;
                for (int j = 0; j < NA; j++) any |= R[i, j] > 0;
                if (!any) continue;
                filled[i] = true;
                for (int it = 0; it < NA; it++)
                {
                    bool gap = false;
                    var row = new float[NA];
                    for (int j = 0; j < NA; j++) row[j] = R[i, j];
                    for (int j = 0; j < NA; j++)
                        if (row[j] <= 0) { gap = true; R[i, j] = Mathf.Max(row[(j + 1) % NA], row[(j + NA - 1) % NA]); }
                    if (!gap) break;
                }
            }
            for (int i = 0; i < NT; i++)
            {
                if (filled[i]) continue;
                int best = -1;
                for (int d = 1; d < NT && best < 0; d++)
                {
                    if (i - d >= 0 && filled[i - d]) best = i - d;
                    else if (i + d < NT && filled[i + d]) best = i + d;
                }
                if (best >= 0) for (int j = 0; j < NA; j++) R[i, j] = R[best, j];
            }
        }

        // 둘레(순환)·t(끝 고정) 3칸 평균
        static float[,] Blur(float[,] F, int iterations)
        {
            for (int it = 0; it < iterations; it++)
            {
                var G = new float[NT, NA];
                for (int i = 0; i < NT; i++)
                    for (int j = 0; j < NA; j++)
                        G[i, j] = (F[i, j] + F[i, (j + 1) % NA] + F[i, (j + NA - 1) % NA]) / 3f;
                var H = (float[,])G.Clone();
                for (int i = 1; i < NT - 1; i++)
                    for (int j = 0; j < NA; j++)
                        H[i, j] = (G[i - 1, j] + G[i, j] + G[i + 1, j]) / 3f;
                F = H;
            }
            return F;
        }
    }
}
