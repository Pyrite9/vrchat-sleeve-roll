// Pyrite9 Sleeve Roll — 계산: 걷힌 반지름 다듬기(이웃 평균 10-35, 소맷단 턱 10-36)
using System.Collections.Generic;
using UnityEngine;

namespace Pyrite9.Sleeve
{
    public static partial class SleeveRollShape
    {
        /// <summary>걷힌 정점 반지름을 메시 이웃 쪽으로 다듬는 횟수·비율(10-35)</summary>
        const int RadiusSmoothPasses = 2;
        const float RadiusSmoothRate = 0.5f;

        /// <summary>걷힌 겉면 턱 다듬기(10-36) — 축 방향 흐림 σ, 턱으로 볼 오름(1 mm 사이), 겉면으로 볼 깊이, 안쪽 겹을 겉면 밑에 남길 틈(전부 mm)</summary>
        const float LipSigmaMm = 3f, LipRiseMm = 0.6f, LipOuterMm = 0.8f, LipInnerGapMm = 0.4f;
        /// <summary>겉면을 찍는 칸 — 축 방향 mm, 둘레 칸 수(2.5도)</summary>
        const float LipBinMm = 0.5f;
        const int LipSectors = 144;
        /// <summary>둘레 방향 흐림 — σ² 칸(σ 3칸 = 7.5도)과 반지름 칸</summary>
        const float LipRoundSigmaSq = 9f;
        const int LipRoundReach = 6;
        /// <summary>다듬기 시작 — 전환이 끝난 자리에서 이만큼 지나 이 폭에 걸쳐 켠다(팔뚝 길이 비율). 전환 구간은 압축으로 메시가 뒤로 접혀 축 방향 윤곽이 없다</summary>
        const float LipStartLead = 0.03f, LipStartFade = 0.05f;

        /// <summary>겉면 칸의 빈 값 — 면이 지나지 않은 칸. 칸은 평평한 배열 [t 칸 × 둘레 칸 + 둘레 칸]</summary>
        const float NoSurface = float.MinValue;

        // 압축된 띠에서 고리 간격이 1~2 mm로 좁아지면 고리끼리 몇 mm만 달라도 칼날처럼 선다(10-35) — 걷힌 정도만큼 반지름을 이웃 평균 쪽으로 옮긴다. 자리(t)·각도는 그대로
        static void SmoothRadii(Cache.Entry ce, Sleeve sl, Roll roll)
        {
            var idx = sl.idx;
            var key = sl.key;
            int count = sl.welded;
            var nb = new List<int>[count];
            for (int i = 0; i < count; i++) nb[i] = new List<int>(8);
            var tris = ce.Triangles;
            for (int i = 0; i < tris.Length; i += 3)
                for (int e = 0; e < 3; e++)
                {
                    int p = key[tris[i + e]], q = key[tris[i + (e + 1) % 3]];
                    if (p < 0 || q < 0 || p == q) continue;
                    if (!nb[p].Contains(q)) nb[p].Add(q);
                    if (!nb[q].Contains(p)) nb[q].Add(p);
                }
            var r = new float[count];
            var w = new float[count];
            for (int k = 0; k < idx.Length; k++)
            {
                int m = key[idx[k]];
                r[m] = roll.radii[k];
                w[m] = Mathf.Max(w[m], roll.weight[k]);
            }
            for (int pass = 0; pass < RadiusSmoothPasses; pass++)
            {
                var next = (float[])r.Clone();
                for (int m = 0; m < count; m++)
                {
                    var around = nb[m];
                    if (w[m] <= 0f || around.Count == 0) continue;
                    float sum = 0f;
                    for (int j = 0; j < around.Count; j++) sum += r[around[j]];
                    next[m] = r[m] + RadiusSmoothRate * w[m] * (sum / around.Count - r[m]);
                }
                r = next;
            }
            for (int k = 0; k < idx.Length; k++)
                if (roll.weight[k] > 0f) roll.radii[k] = r[key[idx[k]]];
        }

        // 덧댄 소맷단 띠처럼 원본에서 완만하던 턱은 축 방향만 많이 줄면 칼날로 선다. 이웃 평균(SmoothRadii)으론 안 사라진다(10-36).
        // 걷힌 겉면을 (t, 둘레) 칸으로 찍어 축 방향으로 흐린 면을 만들고, 손목 쪽으로 가파르게 솟는 턱 둘레에서만 겉면 정점을 그 면으로 옮긴다.
        // 턱이 없는 자리는 그대로 둔다 — 소맷단 끝까지 흐리면 겉면이 안쪽 겹 밑으로 내려가 비친다
        static void SmoothLips(Cache.Entry ce, Sleeve sl, Roll roll, float L, float mPerUnit, float tStart)
        {
            var idx = sl.idx;
            float[] radii = roll.radii, t2s = roll.t2s, rollW = roll.weight;
            float tMin = float.MaxValue, tMax = float.MinValue;
            for (int k = 0; k < idx.Length; k++)
                if (rollW[k] > 0f) { tMin = Mathf.Min(tMin, t2s[k]); tMax = Mathf.Max(tMax, t2s[k]); }
            if (tMin >= tMax) return;
            float mm = 0.001f / mPerUnit;
            float binT = LipBinMm * mm / L;
            int nt = Mathf.CeilToInt((tMax - tMin) / binT) + 2, na = LipSectors;
            float sigmaBins = LipSigmaMm / LipBinMm;
            var roundKernel = Gaussian(LipRoundReach, LipRoundSigmaSq);
            var outer = OuterSurface(ce, sl, roll, tMin, binT, nt, na);
            var lip = LipMask(outer, nt, na, LipRiseMm * mm, Mathf.CeilToInt(2.5f * sigmaBins), roundKernel);
            if (lip == null) return;   // 턱이 없는 옷 — 흐린 면을 만들 필요가 없다
            var blur = BlurAlongArm(outer, nt, na, sigmaBins, roundKernel);
            float binA = 2f * Mathf.PI / na, depth = LipOuterMm * mm, gap = LipInnerGapMm * mm;
            for (int k = 0; k < idx.Length; k++)
            {
                if (rollW[k] <= 0f) continue;
                float on = Smooth((t2s[k] - tStart) / LipStartFade);
                if (on <= 0f) continue;
                int ti = Mathf.Clamp(RoundEven((t2s[k] - tMin) / binT), 0, nt - 1);
                int ai = Wrap(RoundEven(sl.A[k] / binA), na);
                on *= lip[ti * na + ai];
                if (on <= 0f) continue;
                float top = float.MinValue, sum = 0f, cnt = 0f;
                for (int di = -1; di <= 1; di++)
                {
                    int q = ti + di;
                    if (q < 0 || q >= nt) continue;
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        int cell = q * na + Wrap(ai + dj, na);
                        if (outer[cell] == NoSurface) continue;
                        top = Mathf.Max(top, outer[cell]);
                        sum += blur[cell]; cnt += 1f;
                    }
                }
                if (cnt <= 0f) continue;
                float surface = sum / cnt, r = radii[k];
                // 겉면 판정은 부드럽게 — 칸 경계에서 겉·안이 갈리면 이웃끼리 튄다. 안쪽 겹은 새 겉면 밑에 남긴다
                float target = Mathf.Lerp(Mathf.Min(r, surface - gap), surface, Smooth((r - (top - 2f * depth)) / depth));
                target = Mathf.Max(target, Mathf.Min(roll.floors[k], r));
                radii[k] = r + (target - r) * on * rollW[k];
            }
        }

        static int Wrap(int i, int n) => (i % n + n) % n;

        // Mathf.RoundToInt와 같은 값(.5는 짝수로) — 칸 찍기가 표본마다 두 번 부른다
        static int RoundEven(float v)
        {
            int i = (int)v;
            float f = v - i;
            if (f > 0.5f) return i + 1;
            if (f < -0.5f) return i - 1;
            if (f == 0.5f) return (i & 1) == 0 ? i : i + 1;
            if (f == -0.5f) return (i & 1) == 0 ? i : i - 1;
            return i;
        }

        // [−reach, reach] 칸의 가우스 무게
        static float[] Gaussian(int reach, float sigmaSq)
        {
            var g = new float[2 * reach + 1];
            for (int d = -reach; d <= reach; d++) g[d + reach] = Mathf.Exp(-0.5f * d * d / sigmaSq);
            return g;
        }

        // 칸마다 걷힌 면의 가장 큰 반지름
        static float[] OuterSurface(Cache.Entry ce, Sleeve sl, Roll roll, float tMin, float binT, int nt, int na)
        {
            var idx = sl.idx;
            float[] radii = roll.radii, t2s = roll.t2s, rollW = roll.weight, A = sl.A;
            var kOf = new int[ce.Vertices.Length];
            for (int i = 0; i < kOf.Length; i++) kOf[i] = -1;
            for (int k = 0; k < idx.Length; k++) if (rollW[k] > 0f) kOf[idx[k]] = k;
            float binA = 2f * Mathf.PI / na;
            var e = new float[nt * na];
            for (int i = 0; i < e.Length; i++) e[i] = NoSurface;
            var tris = ce.Triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                int k0 = kOf[tris[i]], k1 = kOf[tris[i + 1]], k2 = kOf[tris[i + 2]];
                if (k0 < 0 || k1 < 0 || k2 < 0) continue;
                // 둘레가 ±π에서 끊기지 않게 첫 정점 기준으로 펼친다
                float a0 = A[k0];
                float a1 = a0 + Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, A[k1] * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float a2 = a0 + Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, A[k2] * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float t0 = t2s[k0], t1 = t2s[k1], t2 = t2s[k2], r0 = radii[k0], r1 = radii[k1], r2 = radii[k2];
                float dt = Mathf.Max(Mathf.Abs(t1 - t0), Mathf.Max(Mathf.Abs(t2 - t0), Mathf.Abs(t2 - t1)));
                float da = Mathf.Max(Mathf.Abs(a1 - a0), Mathf.Max(Mathf.Abs(a2 - a0), Mathf.Abs(a2 - a1)));
                int n = Mathf.CeilToInt(Mathf.Max(dt / binT, da / binA)) + 1;
                for (int u = 0; u <= n; u++)
                {
                    float b0 = (float)u / n;
                    for (int v = 0; v <= n - u; v++)
                    {
                        float b1 = (float)v / n, b2 = 1f - b0 - b1;
                        int ti = Mathf.Clamp(RoundEven((b0 * t0 + b1 * t1 + b2 * t2 - tMin) / binT), 0, nt - 1);
                        int cell = ti * na + Wrap(RoundEven((b0 * a0 + b1 * a1 + b2 * a2) / binA), na);
                        float r = b0 * r0 + b1 * r1 + b2 * r2;
                        if (r > e[cell]) e[cell] = r;
                    }
                }
            }
            return e;
        }

        // 축 방향 가우스(빈 칸은 빼고 정규화) 뒤 둘레 방향으로 조금
        static float[] BlurAlongArm(float[] e, int nt, int na, float sigmaBins, float[] roundKernel)
        {
            int rad = Mathf.CeilToInt(2.5f * sigmaBins);
            var kernel = Gaussian(rad, sigmaBins * sigmaBins);
            var along = new float[e.Length];
            for (int i = 0; i < nt; i++)
                for (int j = 0; j < na; j++)
                {
                    int cell = i * na + j;
                    if (e[cell] == NoSurface) { along[cell] = NoSurface; continue; }
                    float sum = 0f, wt = 0f;
                    for (int q = Mathf.Max(i - rad, 0), end = Mathf.Min(i + rad, nt - 1); q <= end; q++)
                    {
                        float v = e[q * na + j];
                        if (v == NoSurface) continue;
                        float g = kernel[q - i + rad];
                        sum += g * v; wt += g;
                    }
                    along[cell] = sum / wt;
                }
            var round = new float[e.Length];
            for (int i = 0; i < nt; i++)
            {
                int row = i * na;
                for (int j = 0; j < na; j++)
                {
                    if (along[row + j] == NoSurface) { round[row + j] = NoSurface; continue; }
                    float sum = 0f, wt = 0f;
                    for (int d = -LipRoundReach; d <= LipRoundReach; d++)
                    {
                        float v = along[row + WrapNear(j + d, na)];
                        if (v == NoSurface) continue;
                        float g = roundKernel[d + LipRoundReach];
                        sum += g * v; wt += g;
                    }
                    round[row + j] = sum / wt;
                }
            }
            return round;
        }

        // n 한 바퀴 안쪽으로 벗어난 칸만
        static int WrapNear(int i, int n) => i < 0 ? i + n : i >= n ? i - n : i;

        // 손목 쪽으로 1 mm 사이에 rise 넘게 솟는 칸 둘레가 1, reach 칸에 걸쳐 0으로. 솟는 칸이 없으면 null
        static float[] LipMask(float[] e, int nt, int na, float rise, int reach, float[] roundKernel)
        {
            int oneMm = Mathf.RoundToInt(1f / LipBinMm);
            var ramp = new float[2 * reach + 1];
            for (int d = -reach; d <= reach; d++) ramp[d + reach] = Smooth(1f - Mathf.Abs(d) / (float)(reach + 1));
            var mask = new float[e.Length];
            var rowHit = new bool[nt];
            bool any = false;
            for (int j = 0; j < na; j++)
                for (int i = 0; i + oneMm < nt; i++)
                {
                    float low = e[i * na + j], high = e[(i + oneMm) * na + j];
                    if (low == NoSurface || high == NoSurface || high - low < rise) continue;
                    any = true;
                    for (int q = Mathf.Max(i + oneMm / 2 - reach, 0), end = Mathf.Min(i + oneMm / 2 + reach, nt - 1); q <= end; q++)
                    {
                        float g = ramp[q - (i + oneMm / 2) + reach];
                        int cell = q * na + j;
                        if (g > mask[cell]) mask[cell] = g;
                        rowHit[q] = true;
                    }
                }
            if (!any) return null;
            float wt = 0f;
            foreach (float g in roundKernel) wt += g;
            var round = new float[e.Length];
            for (int i = 0; i < nt; i++)
            {
                if (!rowHit[i]) continue;   // 줄 전체가 0이면 흐려도 0
                int row = i * na;
                for (int j = 0; j < na; j++)
                {
                    float sum = 0f;
                    for (int d = -LipRoundReach; d <= LipRoundReach; d++)
                        sum += roundKernel[d + LipRoundReach] * mask[row + WrapNear(j + d, na)];
                    round[row + j] = sum / wt;
                }
            }
            return round;
        }
    }
}
