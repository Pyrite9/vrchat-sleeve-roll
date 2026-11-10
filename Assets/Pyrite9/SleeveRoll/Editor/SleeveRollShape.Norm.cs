// Pyrite9 Sleeve Roll — 처진 소매 정규화. 쉬는 자세에서 소맷단이 팔 축 밖으로 처진 소매를 축으로 되돌린 뒤 걷는다.
// Python 함수 하나 = 여기 함수 하나, 이름도 같게 둔다. 전부 double, 길이는 m(메시 좌표 × mPerUnit)이라 [m] 상수를 그대로 쓴다.
// 이동이 정확히 0이면 TryRoll이 false를 돌려주고 기존 RollDeltas를 그대로 탄다(기존 식은 한 글자도 안 바꿨다 — Mono float가 달라진다).
// 옷 이름 분기 없음. 움직인 소매에는 RecenterCuff·ringLoose·cuffNormals·tight·wall이 끼어들지 않는다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pyrite9.Sleeve
{
    public static partial class SleeveRollShape
    {
        internal readonly struct D3
        {
            public readonly double x, y, z;
            public D3(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
            public D3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
            public static D3 operator +(D3 a, D3 b) => new D3(a.x + b.x, a.y + b.y, a.z + b.z);
            public static D3 operator -(D3 a, D3 b) => new D3(a.x - b.x, a.y - b.y, a.z - b.z);
            public static D3 operator -(D3 a) => new D3(-a.x, -a.y, -a.z);
            public static D3 operator *(D3 a, double k) => new D3(a.x * k, a.y * k, a.z * k);
            public static D3 operator /(D3 a, double k) => new D3(a.x / k, a.y / k, a.z / k);
            public double Dot(D3 b) => x * b.x + y * b.y + z * b.z;
            public D3 Cross(D3 b) => new D3(y * b.z - z * b.y, z * b.x - x * b.z, x * b.y - y * b.x);
            public double Norm => Math.Sqrt(x * x + y * y + z * z);
            public bool IsZero => x == 0.0 && y == 0.0 && z == 0.0;
        }

        internal readonly struct D2
        {
            public readonly double x, y;
            public D2(double x, double y) { this.x = x; this.y = y; }
        }

        static class Norm6
        {
            // ---------------- 걷기: 기존 C# 값 그대로 ----------------
            const double THICK_MAX = 0.006, LAYER_MIN = 0.0002, SHORT_MARGIN = 0.02, MEMBER_W = 0.01, WELD_R = 2e-5, MIN_SPAN = 0.05;
            const double T_MIN = -0.2, T_MAX = 2.0, F_END = 0.5;
            const int RADII_SMOOTH = 2;
            const double RADII_RELAX = 0.5, ROLL_LEN_BASE = 0.30, TAPER_LEAD = 0.15, EASE_SLOPE_PEAK = 1.6875, MIN_EASE_SLOPE = 0.05;
            const double PULL_FROM = 1.5, PULL_SPAN = 0.7, PULL_MAX = 0.30, PULL_ELBOW = 0.5, PULL_RAMP = 0.45, PULL_W_MIN = 0.01;
            const double LIP_SIGMA = 3.0, LIP_RISE = 0.6, LIP_OUTER = 0.8, LIP_GAP = 0.4, LIP_BIN = 0.5, LIP_ROUND_SQ = 9.0, LIP_LEAD = 0.03, LIP_FADE = 0.05, LIP_REACH = 2.5, LIP_TOP = 2.0;
            const int LIP_SECTORS = 144, LIP_ROUND_REACH = 6;
            // ---------------- 트리거·고리 ----------------
            const int REGION_MIN = 16;
            const double MAIN_T = 0.2, FAR_A = 0.2, FAR_B = 0.7, HOLE_A = 0.25, HOLE_B = 0.4, SPAN_GUARD = 0.001, EDGE_GUARD = 1e-4, PIN = 1e5, PCG_EPS = 1e-6;
            const int PCG_ITERS = 6000, LEVELS = 96, RING_SEG_MIN = 3, RING_MIN = 4;
            const double TOP_LEVEL = 1e-3, LEVEL_MIX = 0.5;
            // ---------------- 위반·곧게 펴기 ----------------
            const double CONTOUR_GUARD = 1e-6, ZETA = 0.44, ZETA_L = 0.047, KAPPA = 3.0, OFF_POW = 2.0, RHO_EASES = 2.0, OFFSHOOT_EPS = 0.001, OFFSHOOT_POW = 4, SHAPE_GUARD = 1e-9, RING_PER_COST = 1.0;
            const int SHAPE_SMOOTH = 2, DV_SMOOTH = 2;
            // ---------------- 단조 반지름·복원 ----------------
            const double SOFT = 0.016, EPS = 0.10, CONE_SPAN = 2.0, ROUND_A = 0.1, ROUND_B = 0.22, DETILT = 0.6, SLOPE_MAX = 0.8, U_A = 0.005, U_B = 0.02, WRAP_POW = 0.5, W_SCALE = 1.0, PBD_WS_MIN = 1.0, MIN_CLEAR = 0.002, EPS_FLOOR = 0.0005, STRETCH_TOL = 0.15, PBD_RELAX = 0.5, PBD_STEP = 0.002;
            const double ELBOW_T = 0.1;   // Python에선 표시용 묶음에 있지만 t0e·t0w·rs에 쓴다
            const int PBD_ITERS = 30;

            /// <summary>빌드 전체에서 정규화 계산(이동 0으로 끝난 것 포함)에 쓴 시간 — 측정 도구용</summary>
            public static double TotalNormMs;

            internal sealed class In
            {
                public D3[] v0; public int[] tris; public double[] wL, wU;
                public D3 e, sh, ax, u, v; public double L; public bool hasUpper;
                public double[,] Rs, Rd;
                public double tT, t0, ease, margin, snug, foldL, foldA, wLow, wHigh;
                public double t0w, cone;   // norm_core가 움직일 때 정한다(Python c["t0w"], c["cone"])
            }

            internal sealed class Info
            {
                public double hole, neck, gate, vmax, straight, u, sagLen, deltaMax, radius; public string warn;
                public int rings, pcgIters, repairIters; public double repairMm, pull; public bool moved;
                public override string ToString() => $"hole {hole:F3} neck {neck:F3} gate {gate:F3} v {vmax:F3} straight {straight:F3} u {u:F3} sag {sagLen:F3} {warn}move {deltaMax * 1000:F1}mm rings {rings} pcg {pcgIters} pull {pull:F3} repair {repairIters}it {repairMm:F2}mm";
            }

            internal sealed class Out
            {
                public D3[] rolled; public double[] rollW; public int movedVerts; public double skinMedian;
            }

            // ================ 기초 ================
            static double smooth01(double x) { x = x < 0.0 ? 0.0 : x > 1.0 ? 1.0 : x; return x * x * (3.0 - 2.0 * x); }
            static double clip01(double x) => x < 0.0 ? 0.0 : x > 1.0 ? 1.0 : x;
            static double ramp(double x, double lo, double hi) => smooth01((x - lo) / Math.Max(hi - lo, SPAN_GUARD));
            static int Mod(int i, int n) { int m = i % n; return m < 0 ? m + n : m; }

            static void cyl(In d, D3 p, out double t, out double r, out double a)
            {
                var q = p - d.e;
                double x = q.Dot(d.u), y = q.Dot(d.v);
                t = q.Dot(d.ax) / d.L;
                r = Math.Sqrt(x * x + y * y);
                a = Math.Atan2(y, x);
            }

            static D2 to2d(In d, D3 p) { var q = p - d.e; return new D2(q.Dot(d.u), q.Dot(d.v)); }

            static double triangle_area(D3 a, D3 b, D3 c) => 0.5 * (b - a).Cross(c - a).Norm;

            /// <summary>옮기는 경로의 위치 묶기: 반경 WELD_R 안 정점의 연결 성분. 격자 해시(칸 = 반경)로 같은 칸과 이웃 13칸만 본다. 라벨은 성분의 가장 작은 정점 순서. 일반 경로(기존 C#)는 이걸 안 쓰고 자기 묶기를 그대로 쓴다</summary>
            static int[] weld_union(D3[] V, int[] subset, out int count)
            {
                int n = subset != null ? subset.Length : V.Length;
                var cx = new long[n]; var cy = new long[n]; var cz = new long[n];
                long minx = long.MaxValue, miny = long.MaxValue, minz = long.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    var p = V[subset != null ? subset[i] : i];
                    cx[i] = (long)Math.Floor(p.x / WELD_R); cy[i] = (long)Math.Floor(p.y / WELD_R); cz[i] = (long)Math.Floor(p.z / WELD_R);
                    minx = Math.Min(minx, cx[i]); miny = Math.Min(miny, cy[i]); minz = Math.Min(minz, cz[i]);
                }
                long maxc = 0;
                for (int i = 0; i < n; i++)
                {
                    cx[i] -= minx; cy[i] -= miny; cz[i] -= minz;
                    maxc = Math.Max(maxc, Math.Max(cx[i], Math.Max(cy[i], cz[i])));
                }
                long M = maxc + 2;
                var cells = new Dictionary<long, List<int>>(n);
                for (int i = 0; i < n; i++)
                {
                    long id = cx[i] * M * M + cy[i] * M + cz[i];
                    if (!cells.TryGetValue(id, out var l)) { l = new List<int>(1); cells.Add(id, l); }
                    l.Add(i);
                }
                var par = new int[n];
                for (int i = 0; i < n; i++) par[i] = i;
                double r2 = WELD_R * WELD_R;
                void Join(int a, int b)
                {
                    var q = V[subset != null ? subset[a] : a] - V[subset != null ? subset[b] : b];
                    if (q.Dot(q) > r2) return;
                    int ra = Find(par, a), rb = Find(par, b);
                    if (ra < rb) par[rb] = ra; else par[ra] = rb;
                }
                foreach (var kv in cells)
                {
                    var A = kv.Value;
                    for (int i = 0; i < A.Count; i++)
                        for (int j = i + 1; j < A.Count; j++) Join(A[i], A[j]);
                    for (int o = 0; o < 13; o++)
                    {
                        if (!cells.TryGetValue(kv.Key + WeldDx[o] * M * M + WeldDy[o] * M + WeldDz[o], out var B)) continue;
                        foreach (int a in A) foreach (int b in B) Join(a, b);
                    }
                }
                // 뿌리가 곧 성분의 가장 작은 정점이라 앞에서부터 번호를 매기면 np.unique(root)와 같다
                var label = new int[n];
                count = 0;
                for (int i = 0; i < n; i++)
                {
                    int root = Find(par, i);
                    label[i] = root == i ? count++ : label[root];
                }
                return label;
            }

            static readonly int[] WeldDx = { 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 1 };
            static readonly int[] WeldDy = { 0, 1, 0, 1, -1, 0, 0, 1, 1, 1, 1, -1, -1 };
            static readonly int[] WeldDz = { 0, 0, 1, 0, 0, 1, -1, 1, -1, 1, -1, 1, -1 };

            /// <summary>무방향 변(정렬)과 사용 횟수. tr은 평평한 삼각형 배열</summary>
            static void unique_edges(int[] tr, int nk, out int[] ea, out int[] eb, out int[] cnt)
            {
                var codes = new long[tr.Length];
                for (int i = 0; i + 2 < tr.Length; i += 3)
                    for (int j = 0; j < 3; j++)
                    {
                        int a = tr[i + j], b = tr[i + (j + 1) % 3];
                        codes[i + j] = a < b ? a * (long)nk + b : b * (long)nk + a;
                    }
                Array.Sort(codes);
                var la = new List<int>(); var lb = new List<int>(); var lc = new List<int>();
                for (int i = 0; i < codes.Length; i++)
                {
                    if (i > 0 && codes[i] == codes[i - 1]) { lc[lc.Count - 1]++; continue; }
                    la.Add((int)(codes[i] / nk)); lb.Add((int)(codes[i] % nk)); lc.Add(1);
                }
                ea = la.ToArray(); eb = lb.ToArray(); cnt = lc.ToArray();
            }

            // ================ 피부 표 ================
            static double[,] dilate_skin(double[,] Rs)
            {
                var G = new double[NT, NA];
                for (int i = 0; i < NT; i++)
                    for (int j = 0; j < NA; j++)
                        G[i, j] = Math.Max(Math.Max(Rs[i, j], Rs[i, Mod(j - 1, NA)]), Rs[i, Mod(j + 1, NA)]);
                var H = new double[NT, NA];
                for (int i = 0; i < NT; i++)
                    for (int j = 0; j < NA; j++)
                    {
                        double m = G[i, j];
                        if (i > 0) m = Math.Max(m, G[i - 1, j]);
                        if (i < NT - 1) m = Math.Max(m, G[i + 1, j]);
                        H[i, j] = m;
                    }
                return H;
            }

            static double skin_at(double[,] R, double t, double a)
            {
                double ft = (t - T_MIN) / (T_MAX - T_MIN) * NT - 0.5;
                int t0 = Math.Max(0, Math.Min(NT - 1, (int)Math.Floor(ft)));
                int t1 = Math.Min(t0 + 1, NT - 1);
                double ut = clip01(ft - t0);
                double fa = (a + Math.PI) / (2.0 * Math.PI) * NA - 0.5;
                int a0 = (int)Math.Floor(fa);
                double ua = fa - a0;
                int a1 = Mod(a0 + 1, NA);
                a0 = Mod(a0, NA);
                return (R[t0, a0] * (1 - ua) + R[t0, a1] * ua) * (1 - ut) + (R[t1, a0] * (1 - ua) + R[t1, a1] * ua) * ut;
            }

            static int t_bin(double t) => Math.Max(0, Math.Min(NT - 1, (int)((t - T_MIN) / (T_MAX - T_MIN) * NT)));
            static int a_bin(double a) => Mod((int)((a + Math.PI) / (2.0 * Math.PI) * NA), NA);

            static double sleeve_end(In d, D3[] V)
            {
                double best = -1e9; bool any = false;
                for (int i = 0; i < V.Length; i++)
                {
                    if (smooth01((d.wL[i] - d.wLow) / (d.wHigh - d.wLow)) <= F_END) continue;
                    cyl(d, V[i], out var t, out _, out _);
                    if (!any || t > best) { best = t; any = true; }
                }
                return best;
            }

            // ================ 걷기 사상 ================
            sealed class Map
            {
                public double t0, ease, end, pull, endPull, tT, kComp, taper, radialStart, kEven; public bool even;
            }

            static Map roll_map(In d, double end, double endPull, double t0, double ease)
            {
                var m = new Map { t0 = t0, ease = ease, end = end, endPull = endPull };
                m.pull = d.hasUpper ? PULL_MAX * clip01((endPull - PULL_FROM) / PULL_SPAN) : 0.0;   // hasUpper: C#에만 있는 조건(위팔 본이 메시에 없으면 당김 없음)
                m.tT = d.tT + m.pull;
                m.kComp = (m.tT - t0) / (end - t0);
                m.taper = clip01((m.tT - t0) / ROLL_LEN_BASE - 1.0);
                m.radialStart = t0 - TAPER_LEAD * m.taper;
                m.kEven = (m.tT - t0 - ease * 0.5) / (end - t0 - ease * 0.5);
                m.even = 1.0 - (1.0 - m.kComp) * EASE_SLOPE_PEAK < MIN_EASE_SLOPE && m.kEven > 0.0;
                return m;
            }

            static double even_roll(Map m, double t)
            {
                double x = (t - m.t0) / m.ease;
                return x < 1.0 ? m.t0 + m.ease * (x + (m.kEven - 1.0) * (x * x * x - 0.5 * x * x * x * x)) : m.t0 + m.ease * (1.0 + m.kEven) * 0.5 + (t - m.t0 - m.ease) * m.kEven;
            }

            static double map_full(Map m, double t) => t <= m.t0 ? t : m.even ? even_roll(m, t) : m.t0 + (t - m.t0) * m.kComp;
            static double map_ease_end(Map m) => m.even ? even_roll(m, m.t0 + m.ease) : m.t0 + m.ease * m.kComp;

            // ================ 소매 영역·위상 ================
            sealed class Region
            {
                public int[] key; public D3[] P; public int nk; public double[] tk; public bool[] has, main;
                public int[] tr, ea, eb, ecount;
            }

            static int Find(int[] par, int x)
            {
                while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; }
                return x;
            }

            static Region build_region(In d)
            {
                var v = d.v0;
                var key = weld_union(v, null, out int nk);
                var P = new D3[nk];
                for (int i = 0; i < v.Length; i++) P[key[i]] = v[i];
                var w = new double[nk];
                for (int i = 0; i < v.Length; i++) w[key[i]] = Math.Max(w[key[i]], d.wL[i]);
                var trList = new List<int>(d.tris.Length);
                for (int i = 0; i + 2 < d.tris.Length; i += 3)
                {
                    int a = key[d.tris[i]], b = key[d.tris[i + 1]], c = key[d.tris[i + 2]];
                    if (a == b || b == c || a == c) continue;
                    if (w[a] > MEMBER_W && w[b] > MEMBER_W && w[c] > MEMBER_W) { trList.Add(a); trList.Add(b); trList.Add(c); }
                }
                var tr = trList.ToArray();
                unique_edges(tr, nk, out var ue0, out var ue1, out var cnt);
                var par = new int[nk];
                for (int i = 0; i < nk; i++) par[i] = i;
                for (int i = 0; i < ue0.Length; i++)
                {
                    int ra = Find(par, ue0[i]), rb = Find(par, ue1[i]);
                    if (ra != rb) par[ra] = rb;
                }
                var root = new int[nk];
                for (int i = 0; i < nk; i++) root[i] = Find(par, i);
                var has = new bool[nk];
                for (int i = 0; i < ue0.Length; i++) { has[ue0[i]] = true; has[ue1[i]] = true; }
                var tk = new double[nk];
                for (int i = 0; i < nk; i++) tk[i] = (P[i] - d.e).Dot(d.ax) / d.L;
                var counts = new SortedDictionary<int, int>();
                for (int i = 0; i < nk; i++)
                {
                    if (!has[i]) continue;
                    counts.TryGetValue(root[i], out int c);
                    counts[root[i]] = c + (tk[i] > MAIN_T ? 1 : 0);
                }
                int best = -1, bc = -1;
                foreach (var kv in counts) if (kv.Value > bc) { bc = kv.Value; best = kv.Key; }
                var main = new bool[nk];
                if (best >= 0) for (int i = 0; i < nk; i++) main[i] = root[i] == best && has[i];
                var fa = new List<int>(); var fb = new List<int>(); var fc = new List<int>();
                for (int i = 0; i < ue0.Length; i++)
                    if (main[ue0[i]] && main[ue1[i]]) { fa.Add(ue0[i]); fb.Add(ue1[i]); fc.Add(cnt[i]); }
                var mt = new List<int>(tr.Length);
                for (int i = 0; i + 2 < tr.Length; i += 3)
                    if (main[tr[i]] && main[tr[i + 1]] && main[tr[i + 2]]) { mt.Add(tr[i]); mt.Add(tr[i + 1]); mt.Add(tr[i + 2]); }
                return new Region { key = key, P = P, nk = nk, tk = tk, has = has, main = main, tr = mt.ToArray(), ea = fa.ToArray(), eb = fb.ToArray(), ecount = fc.ToArray() };
            }

            /// <summary>경계 변의 연결 성분과 그 둘레</summary>
            static List<KeyValuePair<int[], double>> boundary_loops(Region R)
            {
                var par = new Dictionary<int, int>();
                int F(int x)
                {
                    if (!par.ContainsKey(x)) par[x] = x;
                    while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; }
                    return x;
                }
                for (int i = 0; i < R.ea.Length; i++)
                {
                    if (R.ecount[i] != 1) continue;
                    int ra = F(R.ea[i]), rc = F(R.eb[i]);
                    if (ra != rc) par[ra] = rc;
                }
                var comps = new SortedDictionary<int, List<int>>();
                foreach (var x in new List<int>(par.Keys))
                {
                    int r = F(x);
                    if (!comps.TryGetValue(r, out var l)) comps[r] = l = new List<int>();
                    l.Add(x);
                }
                var perim = new Dictionary<int, double>();
                for (int i = 0; i < R.ea.Length; i++)
                {
                    if (R.ecount[i] != 1) continue;
                    int r = F(R.ea[i]);
                    perim.TryGetValue(r, out double s);
                    perim[r] = s + (R.P[R.ea[i]] - R.P[R.eb[i]]).Norm;
                }
                var o = new List<KeyValuePair<int[], double>>();
                foreach (var kv in comps) { kv.Value.Sort(); o.Add(new KeyValuePair<int[], double>(kv.Value.ToArray(), perim[kv.Key])); }
                return o;
            }

            static void classify_loops(In d, Region R, out double[] kapSrc, out double[] kapSnk, out double hole)
            {
                kapSrc = new double[R.nk]; kapSnk = new double[R.nk]; hole = 0.0;
                foreach (var kv in boundary_loops(R))
                {
                    var l = kv.Key;
                    double sum = 0.0;
                    foreach (int x in l) sum += R.tk[x];
                    double tb = sum / l.Length;
                    double far = ramp(tb, FAR_A, FAR_B);
                    double size = ramp(kv.Value / d.L, HOLE_A, HOLE_B);
                    double h = far * size;
                    hole = Math.Max(hole, h);
                    foreach (int x in l)
                    {
                        kapSrc[x] = Math.Max(kapSrc[x], (1.0 - far) * PIN);
                        kapSnk[x] = Math.Max(kapSnk[x], h * PIN);
                    }
                }
            }

            static double[] harmonic_field(Region R, double[] kapSrc, double[] kapSnk, Info info)
            {
                int nk = R.nk, ne = R.ea.Length;
                var w = new double[ne];
                var dg = new double[nk];
                for (int i = 0; i < ne; i++)
                {
                    w[i] = 1.0 / Math.Max((R.P[R.ea[i]] - R.P[R.eb[i]]).Norm, EDGE_GUARD);
                    dg[R.ea[i]] += w[i]; dg[R.eb[i]] += w[i];
                }
                for (int i = 0; i < nk; i++) dg[i] += kapSrc[i] + kapSnk[i];
                var on = R.main;
                var x = new double[nk]; var r = new double[nk]; var z = new double[nk]; var pp = new double[nk]; var Ap = new double[nk];
                for (int i = 0; i < nk; i++)
                {
                    r[i] = on[i] ? kapSnk[i] : 0.0;
                    z[i] = on[i] ? r[i] / Math.Max(dg[i], 1e-12) : 0.0;
                    pp[i] = z[i];
                }
                double rz = 0.0, rr = 0.0;
                for (int i = 0; i < nk; i++) { rz += r[i] * z[i]; rr += r[i] * r[i]; }
                double nrm = Math.Max(Math.Sqrt(rr), 1e-12);
                int it = 0;
                for (; it < PCG_ITERS; it++)
                {
                    for (int i = 0; i < nk; i++) Ap[i] = dg[i] * pp[i];
                    for (int i = 0; i < ne; i++) { int a = R.ea[i], b = R.eb[i]; Ap[a] -= w[i] * pp[b]; Ap[b] -= w[i] * pp[a]; }
                    double pAp = 0.0;
                    for (int i = 0; i < nk; i++) { if (!on[i]) Ap[i] = 0.0; pAp += pp[i] * Ap[i]; }
                    double al = rz / Math.Max(pAp, 1e-30);
                    rr = 0.0;
                    for (int i = 0; i < nk; i++) { x[i] += al * pp[i]; r[i] -= al * Ap[i]; rr += r[i] * r[i]; }
                    if (Math.Sqrt(rr) < PCG_EPS * nrm) { it++; break; }
                    double rz2 = 0.0;
                    for (int i = 0; i < nk; i++) { z[i] = on[i] ? r[i] / Math.Max(dg[i], 1e-12) : 0.0; rz2 += r[i] * z[i]; }
                    double beta = rz2 / Math.Max(rz, 1e-30);
                    for (int i = 0; i < nk; i++) pp[i] = z[i] + beta * pp[i];
                    rz = rz2;
                }
                info.pcgIters = it;
                return x;
            }

            /// <summary>np.interp 자리 찾기: xp[j] &lt;= x &lt; xp[j+1]. 왼쪽 밖 -1, 오른쪽 밖 n</summary>
            static int locate(double[] xp, double x)
            {
                int n = xp.Length;
                if (x < xp[0]) return -1;
                if (x > xp[n - 1]) return n;
                int lo = 0, hi = n - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi + 1) >> 1;
                    if (xp[mid] <= x) lo = mid; else hi = mid - 1;
                }
                return lo;
            }

            static double interp(double x, double[] xp, double[] fp)
            {
                int j = locate(xp, x), n = xp.Length;
                if (j < 0) return fp[0];
                if (j >= n) return fp[n - 1];
                if (j == n - 1 || xp[j] == x) return fp[j];
                return (fp[j + 1] - fp[j]) / (xp[j + 1] - xp[j]) * (x - xp[j]) + fp[j];
            }

            static double[] contour_levels(Region R, double[] S)
            {
                int m = R.tr.Length / 3;
                var sc = new double[m]; var area = new double[m]; var order = new int[m];
                for (int i = 0; i < m; i++)
                {
                    int a = R.tr[i * 3], b = R.tr[i * 3 + 1], c = R.tr[i * 3 + 2];
                    sc[i] = (S[a] + S[b] + S[c]) / 3.0;
                    area[i] = 0.5 * (R.P[b] - R.P[a]).Cross(R.P[c] - R.P[a]).Norm;
                    order[i] = i;
                }
                Array.Sort(order, (x, y) => { int q = sc[x].CompareTo(sc[y]); return q != 0 ? q : x.CompareTo(y); });
                var cum = new double[m]; var sSorted = new double[m]; var g = new double[m];
                double acc = 0.0;
                for (int i = 0; i < m; i++) { acc += area[order[i]]; cum[i] = acc; sSorted[i] = sc[order[i]]; }
                for (int i = 0; i < m; i++) { cum[i] /= acc; g[i] = LEVEL_MIX * cum[i] + (1.0 - LEVEL_MIX) * sSorted[i]; }
                double top = 1.0 - TOP_LEVEL;
                double gHi = top < sSorted[m - 1] ? interp(top, sSorted, g) : g[m - 1] + (1.0 - LEVEL_MIX) * (top - sSorted[m - 1]);
                var lev = new double[LEVELS];
                for (int i = 0; i < LEVELS; i++) lev[i] = interp(gHi * (i + 0.5) / (LEVELS - 0.5), g, sSorted);
                lev[LEVELS - 1] = top >= sSorted[m - 1] ? Math.Min(top, Math.Max(lev[LEVELS - 1], sSorted[m - 1])) : top;
                return lev;
            }

            sealed class Comp { public D3[] a, b; public double per; public D3 ctr; public D3[] chord; }

            struct Seg { public D3 p0, p1; public long k0, k1; }

            static List<Comp> contour_components(Region R, double[] S, double lv)
            {
                var tri = R.tr; var P = R.P; long nk = R.nk;
                var par = new Dictionary<long, long>();
                long F(long x)
                {
                    if (!par.ContainsKey(x)) par[x] = x;
                    while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; }
                    return x;
                }
                var segs = new List<Seg>();
                var vtx = new int[3];
                var pts = new D3[2]; var keys = new long[2];
                for (int ti = 0; ti + 2 < tri.Length; ti += 3)
                {
                    int cnt = (S[tri[ti]] > lv ? 1 : 0) + (S[tri[ti + 1]] > lv ? 1 : 0) + (S[tri[ti + 2]] > lv ? 1 : 0);
                    if (cnt != 1 && cnt != 2) continue;
                    vtx[0] = tri[ti]; vtx[1] = tri[ti + 1]; vtx[2] = tri[ti + 2];
                    int np = 0;
                    for (int j = 0; j < 3; j++)
                    {
                        int a = vtx[j], b = vtx[(j + 1) % 3];
                        double sa = S[a], sb = S[b];
                        if ((sa > lv) != (sb > lv) && np < 2)
                        {
                            pts[np] = P[a] + (P[b] - P[a]) * ((lv - sa) / (sb - sa));
                            keys[np] = a < b ? a * nk + b : b * nk + a;
                            np++;
                        }
                    }
                    if (np != 2) continue;
                    segs.Add(new Seg { p0 = pts[0], p1 = pts[1], k0 = keys[0], k1 = keys[1] });
                    long ra = F(keys[0]), rb = F(keys[1]);
                    if (ra != rb) par[ra] = rb;
                }
                var groups = new Dictionary<long, List<Seg>>();
                var groupOrder = new List<List<Seg>>();
                foreach (var sg in segs)
                {
                    long r = F(sg.k0);
                    if (!groups.TryGetValue(r, out var l)) { groups[r] = l = new List<Seg>(); groupOrder.Add(l); }
                    l.Add(sg);
                }
                var o = new List<Comp>();
                foreach (var lst in groupOrder)
                {
                    if (lst.Count < RING_SEG_MIN) continue;
                    var a = new D3[lst.Count]; var b = new D3[lst.Count];
                    double per = 0.0; var sum = new D3(0, 0, 0);
                    for (int i = 0; i < lst.Count; i++)
                    {
                        a[i] = lst[i].p0; b[i] = lst[i].p1;
                        double l = (b[i] - a[i]).Norm;
                        per += l;
                        sum = sum + (a[i] + b[i]) * 0.5 * l;
                    }
                    if (per <= 0.0) continue;
                    var use = new SortedDictionary<long, int>(); var pos = new Dictionary<long, D3>();
                    foreach (var sg in lst)
                    {
                        use.TryGetValue(sg.k0, out int u0); use[sg.k0] = u0 + 1; pos[sg.k0] = sg.p0;
                        use.TryGetValue(sg.k1, out int u1); use[sg.k1] = u1 + 1; pos[sg.k1] = sg.p1;
                    }
                    var ends = new List<D3>();
                    foreach (var kv in use) if (kv.Value == 1) ends.Add(pos[kv.Key]);
                    o.Add(new Comp { a = a, b = b, per = per, ctr = sum / per, chord = ends.Count == 2 ? ends.ToArray() : null });
                }
                return o;
            }

            sealed class Ring { public double level; public Comp comp; }

            static List<Ring> ring_chain(Region R, double[] S)
            {
                var rings = new List<Ring>();
                Comp prev = null;
                foreach (double lv in contour_levels(R, S))
                {
                    var o = contour_components(R, S, lv);
                    if (o.Count == 0) continue;
                    Comp best = null;
                    if (prev == null)
                    {
                        foreach (var c in o) if (best == null || c.per > best.per) best = c;
                    }
                    else
                    {
                        double r0 = prev.per / (2.0 * Math.PI), bv = double.MaxValue;
                        foreach (var c in o)
                        {
                            double q = (c.ctr - prev.ctr).Norm / r0 + RING_PER_COST * Math.Abs(Math.Log(c.per / prev.per));
                            if (q < bv) { bv = q; best = c; }
                        }
                    }
                    prev = best;
                    rings.Add(new Ring { level = lv, comp = best });
                }
                return rings;
            }

            // ================ 고리 기하 ================
            static double point_clearance(D2 q, D2[] a2, D2[] b2, D2[] chord2)
            {
                double c = double.MaxValue;
                int hits = 0;
                int n = a2.Length;
                for (int i = 0; i < n + (chord2 != null ? 1 : 0); i++)
                {
                    D2 pa = i < n ? a2[i] : chord2[0], pb = i < n ? b2[i] : chord2[1];
                    if (i < n)
                    {
                        double abx = pb.x - pa.x, aby = pb.y - pa.y;
                        double t = clip01(((q.x - pa.x) * abx + (q.y - pa.y) * aby) / Math.Max(abx * abx + aby * aby, 1e-14));
                        double dx = pa.x + abx * t - q.x, dy = pa.y + aby * t - q.y;
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        if (dist < c) c = dist;
                    }
                    double ux = pa.x - q.x, uy = pa.y - q.y, vx = pb.x - q.x, vy = pb.y - q.y;
                    if ((uy > 0.0) != (vy > 0.0))
                    {
                        double x = ux - uy * (vx - ux) / (vy - uy);
                        if (x > 0.0) hits++;
                    }
                }
                return hits % 2 != 0 ? c : -c;
            }

            static double[,] binomial_smooth(double[,] tab, int iters)
            {
                int n = tab.GetLength(0), m = tab.GetLength(1);
                for (int it = 0; it < iters; it++)
                {
                    var G = (double[,])tab.Clone();
                    for (int i = 1; i < n - 1; i++)
                        for (int j = 0; j < m; j++)
                            G[i, j] = (tab[i - 1, j] + 2.0 * tab[i, j] + tab[i + 1, j]) / 4.0;
                    tab = G;
                }
                return tab;
            }

            static double[,] ring_whiten(In d, List<Ring> rings, D2[] ctr2)
            {
                int n = rings.Count;
                var whiten = new double[n, 4];
                for (int i = 0; i < n; i++)
                {
                    var cp = rings[i].comp;
                    double xx = SHAPE_GUARD, xy = 0.0, yy = SHAPE_GUARD, sum = 0.0;
                    for (int k = 0; k < cp.a.Length; k++)
                    {
                        double wg = (cp.b[k] - cp.a[k]).Norm;
                        var q = to2d(d, (cp.a[k] + cp.b[k]) * 0.5);
                        double x = q.x - ctr2[i].x, y = q.y - ctr2[i].y;
                        xx += x * wg * x; xy += x * wg * y; yy += y * wg * y;
                        sum += wg;
                    }
                    double den = Math.Max(sum, SHAPE_GUARD);
                    xx /= den; xy /= den; yy /= den;
                    // 2x2 대칭 고유분해(eigh와 같은 행렬 함수 — 고유벡터 부호·순서에 안 민감하다)
                    double mid = (xx + yy) * 0.5, dif = (xx - yy) * 0.5, rad = Math.Sqrt(dif * dif + xy * xy);
                    double l2 = mid + rad, l1 = mid - rad;
                    l1 = Math.Max(l1, SHAPE_GUARD); l2 = Math.Max(l2, SHAPE_GUARD);
                    double gm = Math.Sqrt(l1 * l2), f1 = Math.Sqrt(gm / l1), f2 = Math.Sqrt(gm / l2);
                    double cx, cy;
                    if (rad <= 0.0) { cx = 1.0; cy = 0.0; }
                    else if (dif >= 0.0) { cx = dif + rad; cy = xy; }
                    else { cx = xy; cy = rad - dif; }
                    double cn = Math.Sqrt(cx * cx + cy * cy);
                    cx /= cn; cy /= cn;
                    whiten[i, 0] = f2 * cx * cx + f1 * cy * cy;
                    whiten[i, 1] = (f2 - f1) * cx * cy;
                    whiten[i, 2] = whiten[i, 1];
                    whiten[i, 3] = f2 * cy * cy + f1 * cx * cx;
                }
                return whiten;
            }

            static double[] field_on_offshoots(Region R, double[] S)
            {
                var Sk = (double[])S.Clone();
                var mainIdx = new List<int>();
                for (int i = 0; i < R.nk; i++) if (R.main[i]) mainIdx.Add(i);
                double eps2 = OFFSHOOT_EPS * OFFSHOOT_EPS;
                var d2 = new double[mainIdx.Count];
                for (int x = 0; x < R.nk; x++)
                {
                    if (!R.has[x] || R.main[x]) continue;
                    double mn = double.MaxValue;
                    for (int i = 0; i < d2.Length; i++)
                    {
                        var q = R.P[mainIdx[i]] - R.P[x];
                        d2[i] = q.Dot(q);
                        if (d2[i] < mn) mn = d2[i];
                    }
                    double sc = Math.Max(mn, eps2), sw = 0.0, ss = 0.0;
                    for (int i = 0; i < d2.Length; i++)
                    {
                        double w = Math.Pow(d2[i] / sc + 1e-9, -OFFSHOOT_POW);
                        sw += w; ss += w * S[mainIdx[i]];
                    }
                    Sk[x] = ss / sw;
                }
                return Sk;
            }

            static double[] table_col(double[,] tab, int col)
            {
                var c = new double[tab.GetLength(0)];
                for (int i = 0; i < c.Length; i++) c[i] = tab[i, col];
                return c;
            }

            static double Median(double[] a)
            {
                var s = (double[])a.Clone();
                Array.Sort(s);
                int n = s.Length;
                return n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) * 0.5;
            }

            // ================ 정규화 ================
            /// <summary>정점별 이동 off(Vn = v0 - off). 안 옮기면 null</summary>
            internal static D3[] norm_core(In d, Info info)
            {
                var V0 = d.v0;
                d.t0w = d.t0; d.cone = d.ease;
                double end = sleeve_end(d, V0);
                if (end <= d.tT + SHORT_MARGIN) return null;
                var R = build_region(d);
                int mainCount = 0;
                for (int i = 0; i < R.nk; i++) if (R.main[i]) mainCount++;
                if (mainCount < REGION_MIN) return null;
                classify_loops(d, R, out var kapSrc, out var kapSnk, out double hole);
                info.hole = hole;
                double srcMax = 0.0;
                for (int i = 0; i < R.nk; i++) srcMax = Math.Max(srcMax, kapSrc[i]);
                if (hole <= 0.0 || srcMax <= 0.0) { info.warn = "no_hole "; return null; }
                var S = harmonic_field(R, kapSrc, kapSnk, info);
                var rings = ring_chain(R, S);
                int n = rings.Count;
                info.rings = n;
                if (n < RING_MIN) return null;
                var sLev = new double[n]; var tbar = new double[n]; var ctr2 = new D2[n]; var radius = new double[n];
                double rMin = double.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    var cp = rings[i].comp;
                    sLev[i] = rings[i].level;
                    tbar[i] = (cp.ctr - d.e).Dot(d.ax) / d.L;
                    ctr2[i] = to2d(d, cp.ctr);
                    radius[i] = cp.per / (2.0 * Math.PI);
                    rMin = Math.Min(rMin, radius[i]);
                }
                double neck = 2.0 * Math.PI * rMin / d.L;
                double gate = hole * ramp(neck, HOLE_A, HOLE_B);
                info.neck = neck; info.gate = gate;
                if (gate <= 0.0) return null;
                // 고리마다 위반: 쉬는 자세의 고리 기하만 본다. 위반이 하나도 없으면 이동 정확히 0
                var v = new double[n]; var q = new double[n]; var cn = new double[n];
                double vmax = double.MinValue; bool anyV = false;
                for (int i = 0; i < n; i++)
                {
                    var cp = rings[i].comp;
                    cn[i] = Math.Sqrt(ctr2[i].x * ctr2[i].x + ctr2[i].y * ctr2[i].y);
                    var a2 = new D2[cp.a.Length]; var b2 = new D2[cp.a.Length];
                    for (int k = 0; k < a2.Length; k++) { a2[k] = to2d(d, cp.a[k]); b2[k] = to2d(d, cp.b[k]); }
                    D2[] ch = cp.chord == null ? null : new[] { to2d(d, cp.chord[0]), to2d(d, cp.chord[1]) };
                    double sc = Math.Max(point_clearance(ctr2[i], a2, b2, ch), 0.0);
                    q[i] = sc / radius[i];
                    double need = Math.Min(radius[i] * ZETA, d.L * ZETA_L) * ramp(tbar[i], FAR_A, FAR_B);
                    v[i] = Math.Max(Math.Min(need, sc) - point_clearance(new D2(0.0, 0.0), a2, b2, ch), 0.0);
                    if (v[i] != 0.0) anyV = true;
                    vmax = Math.Max(vmax, v[i] / radius[i]);
                }
                info.vmax = vmax;
                if (!anyV) return null;
                // 믿을 수 없는 재료 비율 u = (트임 벽 고리 + 떨어진 덩어리) 면적 / 전체
                var conf = new double[n];
                for (int i = 0; i < n; i++) conf[i] = ramp(q[i], ROUND_A, ROUND_B);
                double uBad = 0.0;
                for (int i = 0; i + 2 < R.tr.Length; i += 3)
                {
                    int a = R.tr[i], b = R.tr[i + 1], c = R.tr[i + 2];
                    uBad += (1.0 - interp((S[a] + S[b] + S[c]) / 3.0, sLev, conf)) * triangle_area(R.P[a], R.P[b], R.P[c]);
                }
                double uOff = 0.0, uAll = 0.0;
                for (int i = 0; i + 2 < d.tris.Length; i += 3)
                {
                    int a = R.key[d.tris[i]], b = R.key[d.tris[i + 1]], c = R.key[d.tris[i + 2]];
                    if (!R.has[a] || !R.has[b] || !R.has[c] || a == b || b == c || a == c) continue;
                    double ar = triangle_area(R.P[a], R.P[b], R.P[c]);
                    uAll += ar;
                    if (!(R.main[a] && R.main[b] && R.main[c])) uOff += ar;
                }
                double u = (uBad + uOff) / uAll;
                double sk = double.MinValue;
                for (int i = 0; i < n; i++) sk = Math.Max(sk, KAPPA * gate * v[i] / Math.Max(cn[i], CONTOUR_GUARD));
                double straight = clip01(sk) * (1.0 - ramp(u, U_A, U_B));
                info.straight = straight; info.u = u;
                if (straight <= 0.0) return null;
                // 팔을 못 두른 재료 길이
                double sagLen = 0.0;
                for (int i = 1; i < n; i++)
                {
                    double offRatio = clip01(cn[i] / radius[i]);
                    double arc = (rings[i].comp.ctr - rings[i - 1].comp.ctr).Norm / d.L;
                    sagLen += Math.Pow(offRatio, OFF_POW) * arc;
                }
                sagLen *= straight;
                // 시작 원뿔: 풍선 반지름을 피부 위까지 떨어뜨리는 길이. 걷는 구간이 이보다 짧으면 걷기 시작을 앞당긴다. 일반 경로는 t0 그대로
                int i0 = 0;
                for (int i = 0; i < n; i++) if (tbar[i] >= d.t0) { i0 = i; break; }
                int ti0 = t_bin(d.t0);
                double rowMean = 0.0;
                for (int j = 0; j < NA; j++) rowMean += d.Rs[ti0, j];
                rowMean /= NA;
                double drop = radius[i0] - rowMean - Math.Max(d.margin, MIN_CLEAR);
                d.cone = Math.Max(drop, 0.0) / SLOPE_MAX * 1.5 / d.L;
                double t0e = Math.Max(d.t0, Math.Min(ELBOW_T, d.tT - MIN_SPAN));
                d.t0w = Math.Max(Math.Min(t0e, d.tT - CONE_SPAN * d.cone), Math.Min(t0e, ELBOW_T));
                var dl0 = new double[n, 2];
                for (int i = 0; i < n; i++)
                {
                    double rhoT = ramp(tbar[i], d.t0w, d.t0w + RHO_EASES * d.ease);
                    double k = rhoT * straight;
                    dl0[i, 0] = ctr2[i].x * k; dl0[i, 1] = ctr2[i].y * k;
                }
                var dl = binomial_smooth(dl0, DV_SMOOTH);
                var whiten = ring_whiten(d, rings, ctr2);
                var W0 = new double[n, 4];
                var rhoA = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double dn = Math.Sqrt(dl[i, 0] * dl[i, 0] + dl[i, 1] * dl[i, 1]);
                    double rho = clip01(dn / Math.Max(cn[i], CONTOUR_GUARD));
                    rhoA[i] = rho;
                    W0[i, 0] = (1.0 - rho) + whiten[i, 0] * rho;
                    W0[i, 1] = whiten[i, 1] * rho;
                    W0[i, 2] = whiten[i, 2] * rho;
                    W0[i, 3] = (1.0 - rho) + whiten[i, 3] * rho;
                }
                var Weff = binomial_smooth(W0, SHAPE_SMOOTH);
                // 정점: s로 표를 읽는다
                var Sk = field_on_offshoots(R, S);
                var dl1 = table_col(dl, 0); var dl2 = table_col(dl, 1);
                var cx0 = new double[n]; var cy0 = new double[n];
                for (int i = 0; i < n; i++) { cx0[i] = ctr2[i].x; cy0[i] = ctr2[i].y; }
                var w00 = table_col(Weff, 0); var w01 = table_col(Weff, 1); var w10 = table_col(Weff, 2); var w11 = table_col(Weff, 3);
                var d3 = new D3[R.nk];
                for (int x = 0; x < R.nk; x++)
                {
                    if (!R.has[x]) continue;
                    double s = Sk[x];
                    double dvx = interp(s, sLev, dl1), dvy = interp(s, sLev, dl2);
                    var p2 = to2d(d, R.P[x]);
                    double xx = p2.x - interp(s, sLev, cx0), xy = p2.y - interp(s, sLev, cy0);
                    double a = interp(s, sLev, w00), b = interp(s, sLev, w01), c = interp(s, sLev, w10), e = interp(s, sLev, w11);
                    double dwx = xx - (a * xx + b * xy), dwy = xy - (c * xx + e * xy);
                    // 기울어진 고리를 축에 수직으로 세운다(비율 DETILT * rho)
                    d3[x] = d.u * (dvx + dwx) + d.v * (dvy + dwy) + d.ax * ((R.tk[x] - interp(s, sLev, tbar)) * d.L * DETILT * interp(s, sLev, rhoA));
                }
                var off = new D3[V0.Length];
                bool moved = false;
                double dm = 0.0;
                for (int i = 0; i < V0.Length; i++)
                {
                    if (d.wL[i] <= MEMBER_W) continue;
                    off[i] = d3[R.key[i]];
                    if (!off[i].IsZero) moved = true;
                    dm = Math.Max(dm, off[i].Norm);
                }
                if (!moved) return null;
                // 기울어진 소맷단이 끝 t를 늘린 몫도 팔을 두르지 않는다
                var Vn = new D3[V0.Length];
                for (int i = 0; i < V0.Length; i++) Vn[i] = V0[i] - off[i];
                double tbarMax = double.MinValue;
                foreach (double t in tbar) tbarMax = Math.Max(tbarMax, t);
                sagLen += straight * Math.Max(sleeve_end(d, Vn) - tbarMax, 0.0);
                info.radius = Median(radius); info.sagLen = sagLen; info.deltaMax = dm; info.moved = true;
                return off;
            }

            // ================ 걷기 ================
            static double[] layers(In d, D3[] V, int[] idx, double[] T, double[] A, int[] key, bool[] member)
            {
                var tris = d.tris;
                int nv = V.Length;
                var tk = new double[nv]; var ak = new double[nv];
                for (int i = 0; i < nv; i++) cyl(d, V[i], out tk[i], out _, out ak[i]);
                var bins = new List<int>[NT * NA];
                double tScale = NT / (T_MAX - T_MIN), aScale = NA / (2.0 * Math.PI);
                for (int c = 0; c + 2 < tris.Length; c += 3)
                {
                    int f0 = tris[c], f1 = tris[c + 1], f2 = tris[c + 2];
                    if (!member[f0] && !member[f1] && !member[f2]) continue;
                    double a0v = ak[f0], a1v = ak[f1], a2v = ak[f2];
                    if (a1v - a0v > Math.PI) a1v -= 2.0 * Math.PI; else if (a0v - a1v > Math.PI) a1v += 2.0 * Math.PI;
                    if (a2v - a0v > Math.PI) a2v -= 2.0 * Math.PI; else if (a0v - a2v > Math.PI) a2v += 2.0 * Math.PI;
                    double aMin = Math.Min(a0v, Math.Min(a1v, a2v)), aMax = Math.Max(a0v, Math.Max(a1v, a2v));
                    if (aMax - aMin > Math.PI) continue;
                    double tMn = Math.Min(tk[f0], Math.Min(tk[f1], tk[f2])), tMx = Math.Max(tk[f0], Math.Max(tk[f1], tk[f2]));
                    int t0 = Math.Max(0, Math.Min(NT - 1, (int)Math.Floor((tMn - T_MIN) * tScale)));
                    int t1 = Math.Max(0, Math.Min(NT - 1, (int)Math.Floor((tMx - T_MIN) * tScale)));
                    int a0 = (int)Math.Floor((aMin + Math.PI) * aScale), a1 = (int)Math.Floor((aMax + Math.PI) * aScale);
                    for (int ti = t0; ti <= t1; ti++)
                        for (int aj = a0; aj <= a1; aj++)
                        {
                            int b = ti * NA + Mod(aj, NA);
                            (bins[b] ?? (bins[b] = new List<int>())).Add(c);
                        }
                }
                int n = idx.Length;
                var depth = new double[n];
                for (int k = 0; k < n; k++)
                {
                    var lst = bins[t_bin(T[k]) * NA + a_bin(A[k])];
                    if (lst == null) continue;
                    var q = V[idx[k]];
                    var w = q - d.e;
                    var radial = w - d.ax * w.Dot(d.ax);
                    double rl = radial.Norm;
                    if (rl < 1e-9) continue;
                    var dr = -radial / rl;
                    double limit = Math.Min(THICK_MAX, rl), best = 0.0;
                    int own0 = key[idx[k]];
                    foreach (int c in lst)
                    {
                        int i0 = tris[c], i1 = tris[c + 1], i2 = tris[c + 2];
                        if (key[i0] == own0 || key[i1] == own0 || key[i2] == own0) continue;
                        var A0 = V[i0];
                        var e1 = V[i1] - A0; var e2 = V[i2] - A0;
                        var h = dr.Cross(e2);
                        double det = e1.Dot(h);
                        if (Math.Abs(det) < 1e-12) continue;
                        double inv = 1.0 / det;
                        var s = q - A0;
                        double bu = s.Dot(h) * inv;
                        if (bu < 0.0 || bu > 1.0) continue;
                        var qq = s.Cross(e1);
                        double bv = qq.Dot(dr) * inv;
                        if (bv < 0.0 || bu + bv > 1.0) continue;
                        double dd = e2.Dot(qq) * inv;
                        if (dd > LAYER_MIN && dd <= limit && dd > best) best = dd;
                    }
                    depth[k] = best;
                }
                int count = 0;
                foreach (int kk in key) if (kk + 1 > count) count = kk + 1;
                var own = new double[count];
                for (int k = 0; k < n; k++) own[key[idx[k]]] = Math.Max(own[key[idx[k]]], depth[k]);
                var grown = (double[])own.Clone();
                for (int c = 0; c + 2 < tris.Length; c += 3)
                {
                    int k0 = key[tris[c]], k1 = key[tris[c + 1]], k2 = key[tris[c + 2]];
                    if (k0 < 0 || k1 < 0 || k2 < 0) continue;
                    double m = Math.Max(own[k0], Math.Max(own[k1], own[k2]));
                    if (m > grown[k0]) grown[k0] = m;
                    if (m > grown[k1]) grown[k1] = m;
                    if (m > grown[k2]) grown[k2] = m;
                }
                var o = new double[n];
                for (int k = 0; k < n; k++) o[k] = grown[key[idx[k]]];
                return o;
            }

            static double[] smooth_radii(int[] tris, int[] idx, int[] key, int count, double[] radii, double[] weight)
            {
                var r = new double[count]; var w = new double[count];
                for (int k = 0; k < idx.Length; k++) r[key[idx[k]]] = radii[k];
                for (int k = 0; k < idx.Length; k++) w[key[idx[k]]] = Math.Max(w[key[idx[k]]], weight[k]);
                var codes = new List<long>(tris.Length);
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    int a = key[tris[i]], b = key[tris[i + 1]], c = key[tris[i + 2]];
                    if (a < 0 || b < 0 || c < 0) continue;   // Python: 세 정점 다 소매인 면만(기존 C#은 변 단위 — 포트를 따른다)
                    if (a != b) codes.Add(a < b ? a * (long)count + b : b * (long)count + a);
                    if (b != c) codes.Add(b < c ? b * (long)count + c : c * (long)count + b);
                    if (c != a) codes.Add(c < a ? c * (long)count + a : a * (long)count + c);
                }
                codes.Sort();
                var ea = new List<int>(); var eb = new List<int>();
                for (int i = 0; i < codes.Count; i++)
                {
                    if (i > 0 && codes[i] == codes[i - 1]) continue;
                    ea.Add((int)(codes[i] / count)); eb.Add((int)(codes[i] % count));
                }
                var cnt = new double[count];
                for (int i = 0; i < ea.Count; i++) { cnt[ea[i]] += 1.0; cnt[eb[i]] += 1.0; }
                for (int it = 0; it < RADII_SMOOTH; it++)
                {
                    var s = new double[count];
                    for (int i = 0; i < ea.Count; i++) { s[ea[i]] += r[eb[i]]; s[eb[i]] += r[ea[i]]; }
                    var nx = (double[])r.Clone();
                    for (int m = 0; m < count; m++)
                        if (w[m] > 0.0 && cnt[m] > 0.0) nx[m] = r[m] + RADII_RELAX * w[m] * (s[m] / cnt[m] - r[m]);
                    r = nx;
                }
                var o = (double[])radii.Clone();
                for (int k = 0; k < idx.Length; k++) if (weight[k] > 0.0) o[k] = r[key[idx[k]]];
                return o;
            }

            const double NO = -1e30;

            static double[] smooth_lips(int[] tris, int nv, int[] idx, double[] A, double[] radii, double[] t2s, double[] rollW, double[] floors, double L, double tStart)
            {
                int n = idx.Length;
                double tMin = double.MaxValue, tMax = double.MinValue;
                bool any = false;
                for (int k = 0; k < n; k++)
                    if (rollW[k] > 0.0) { any = true; tMin = Math.Min(tMin, t2s[k]); tMax = Math.Max(tMax, t2s[k]); }
                if (!any || tMin >= tMax) return radii;
                const double mm = 0.001;
                double binT = LIP_BIN * mm / L;
                int nt = (int)Math.Ceiling((tMax - tMin) / binT) + 2;
                int na = LIP_SECTORS;
                double sb = LIP_SIGMA / LIP_BIN;
                int rr = LIP_ROUND_REACH;
                var rk = new double[2 * rr + 1];
                double rkSum = 0.0;
                for (int k = -rr; k <= rr; k++) { rk[k + rr] = Math.Exp(-0.5 * k * k / LIP_ROUND_SQ); rkSum += rk[k + rr]; }
                var kOf = new int[nv];
                for (int i = 0; i < nv; i++) kOf[i] = -1;
                for (int k = 0; k < n; k++) if (rollW[k] > 0.0) kOf[idx[k]] = k;
                double binA = 2.0 * Math.PI / na;
                var E = new double[nt * na];
                for (int i = 0; i < E.Length; i++) E[i] = NO;
                double TwoPi = 2.0 * Math.PI;
                for (int c = 0; c + 2 < tris.Length; c += 3)
                {
                    int k0 = kOf[tris[c]], k1 = kOf[tris[c + 1]], k2 = kOf[tris[c + 2]];
                    if (k0 < 0 || k1 < 0 || k2 < 0) continue;
                    double a0 = A[k0];
                    double a1 = a0 + (PyMod(A[k1] - a0 + Math.PI, TwoPi) - Math.PI);
                    double a2 = a0 + (PyMod(A[k2] - a0 + Math.PI, TwoPi) - Math.PI);
                    double t0 = t2s[k0], t1 = t2s[k1], t2 = t2s[k2], r0 = radii[k0], r1 = radii[k1], r2 = radii[k2];
                    double dt = Math.Max(Math.Abs(t1 - t0), Math.Max(Math.Abs(t2 - t0), Math.Abs(t2 - t1)));
                    double da = Math.Max(Math.Abs(a1 - a0), Math.Max(Math.Abs(a2 - a0), Math.Abs(a2 - a1)));
                    int m = (int)Math.Ceiling(Math.Max(dt / binT, da / binA)) + 1;
                    for (int u = 0; u <= m; u++)
                        for (int v = 0; v <= m - u; v++)
                        {
                            double b0 = (double)u / m, b1 = (double)v / m, b2 = 1.0 - b0 - b1;
                            int ti = (int)Math.Round((b0 * t0 + b1 * t1 + b2 * t2 - tMin) / binT);
                            ti = ti < 0 ? 0 : ti > nt - 1 ? nt - 1 : ti;
                            int cell = ti * na + Mod((int)Math.Round((b0 * a0 + b1 * a1 + b2 * a2) / binA), na);
                            double r = b0 * r0 + b1 * r1 + b2 * r2;
                            if (r > E[cell]) E[cell] = r;
                        }
                }
                int one = (int)Math.Round(1.0 / LIP_BIN);
                int reach = (int)Math.Ceiling(LIP_REACH * sb);
                var rampT = new double[2 * reach + 1];
                for (int q = -reach; q <= reach; q++) rampT[q + reach] = smooth01(1.0 - Math.Abs(q) / (double)(reach + 1));
                var mask = new double[nt * na];
                bool hitAny = false;
                for (int i = 0; i + one < nt; i++)
                    for (int j = 0; j < na; j++)
                    {
                        double lo = E[i * na + j], hi = E[(i + one) * na + j];
                        if (lo == NO || hi == NO || hi - lo < LIP_RISE * mm) continue;
                        hitAny = true;
                        int c = i + one / 2;
                        for (int q = Math.Max(c - reach, 0), e = Math.Min(c + reach, nt - 1); q <= e; q++)
                        {
                            double g = rampT[q - c + reach];
                            if (g > mask[q * na + j]) mask[q * na + j] = g;
                        }
                    }
                if (!hitAny) return radii;
                var roundM = new double[nt * na];
                for (int i = 0; i < nt; i++)
                    for (int j = 0; j < na; j++)
                    {
                        double s = 0.0;
                        for (int k = -rr; k <= rr; k++) s += rk[k + rr] * mask[i * na + Mod(j + k, na)];
                        roundM[i * na + j] = s / rkSum;
                    }
                var ker = new double[2 * reach + 1];
                for (int k = -reach; k <= reach; k++) ker[k + reach] = Math.Exp(-0.5 * k * k / (sb * sb));
                var along = new double[nt * na];
                for (int i = 0; i < nt; i++)
                    for (int j = 0; j < na; j++)
                    {
                        if (E[i * na + j] == NO) { along[i * na + j] = NO; continue; }
                        double num = 0.0, den = 0.0;
                        for (int k = -reach; k <= reach; k++)
                        {
                            int q = i + k;
                            if (q < 0 || q >= nt) continue;
                            double v = E[q * na + j];
                            if (v == NO) continue;
                            num += ker[k + reach] * v; den += ker[k + reach];
                        }
                        along[i * na + j] = num / den;
                    }
                var blur = new double[nt * na];
                for (int i = 0; i < nt; i++)
                    for (int j = 0; j < na; j++)
                    {
                        if (along[i * na + j] == NO) { blur[i * na + j] = NO; continue; }
                        double num = 0.0, den = 0.0;
                        for (int k = -rr; k <= rr; k++)
                        {
                            double v = along[i * na + Mod(j + k, na)];
                            if (v == NO) continue;
                            num += rk[k + rr] * v; den += rk[k + rr];
                        }
                        blur[i * na + j] = num / den;
                    }
                double depth = LIP_OUTER * mm, gap = LIP_GAP * mm;
                var o = (double[])radii.Clone();
                for (int k = 0; k < n; k++)
                {
                    if (rollW[k] <= 0.0) continue;
                    double on = smooth01((t2s[k] - tStart) / LIP_FADE);
                    if (on <= 0.0) continue;
                    int ti = (int)Math.Round((t2s[k] - tMin) / binT);
                    ti = ti < 0 ? 0 : ti > nt - 1 ? nt - 1 : ti;
                    int ai = Mod((int)Math.Round(A[k] / binA), na);
                    on *= roundM[ti * na + ai];
                    if (on <= 0.0) continue;
                    double top = -1e30, s = 0.0; int cnt = 0;
                    for (int di = -1; di <= 1; di++)
                    {
                        int q = ti + di;
                        if (q < 0 || q >= nt) continue;
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            int cc = q * na + Mod(ai + dj, na);
                            if (E[cc] == NO) continue;
                            top = Math.Max(top, E[cc]);
                            s += blur[cc];
                            cnt++;
                        }
                    }
                    if (cnt <= 0) continue;
                    double surface = s / cnt, r = o[k];
                    double tt = smooth01((r - (top - LIP_TOP * depth)) / depth);
                    double target = Math.Min(r, surface - gap) * (1.0 - tt) + surface * tt;
                    target = Math.Max(target, Math.Min(floors[k], r));
                    o[k] = r + (target - r) * on * rollW[k];
                }
                return o;
            }

            static double PyMod(double x, double m) { double r = x - m * Math.Floor(x / m); return r >= m ? 0.0 : r; }

            static void pull_upper(In d, D3[] V0, D3[] delta, double pull)
            {
                if (pull <= 0.0 || d.wU == null) return;
                var up = d.e - d.sh;
                double LU = up.Norm;
                up = up / LU;
                for (int i = 0; i < V0.Length; i++)
                {
                    double wu = d.wU[i];
                    double su = (V0[i] - d.sh).Dot(up) / LU;
                    if (!(wu > PULL_W_MIN && su > 0.0)) continue;
                    double tv = (V0[i] - d.e).Dot(d.ax) / d.L;
                    double g = tv >= 0.0 ? PULL_ELBOW + (1.0 - PULL_ELBOW) * smooth01(tv / PULL_RAMP) : PULL_ELBOW * clip01(su);
                    delta[i] = delta[i] - (tv >= 0.0 ? d.ax : up) * (pull * d.L * g * wu);
                }
            }

            static double logaddexp0(double y) => y > 0.0 ? y + Math.Log(1.0 + Math.Exp(-y)) : Math.Log(1.0 + Math.Exp(y));

            static double monotone_radius(In d, double Rr, double flo, double fold, double prog, double radii, double W, double loose)
            {
                double sf = SOFT * d.L;
                double x = Rr - flo;
                double g = sf * logaddexp0(x / sf);
                double b = EPS_FLOOR;
                double Hm = flo + g * d.snug * loose + b * Math.Exp(Math.Min(x, 0.0) * EPS / b) + fold * loose * loose;
                double Hw = Rr + (Hm - Rr) * prog;
                return radii * (1.0 - W) + Hw * W;
            }

            sealed class RollResult { public D3[] rolled; public double[] rollW; public int movedVerts; public double skinMedian; public double pull; }

            static RollResult roll_deltas(In d, D3[] Vn, double[] W, double endPull)
            {
                var V0 = d.v0;
                double L = d.L;
                double G = 0.0;
                foreach (double x in W) G = Math.Max(G, x);   // 옮긴 정도
                double t0 = d.t0 + (d.t0w - d.t0) * G;
                int nv = V0.Length;
                var member = new bool[nv];
                int n = 0;
                for (int i = 0; i < nv; i++) if (d.wL[i] > MEMBER_W) { member[i] = true; n++; }
                if (n == 0) return null;
                var idx = new int[n];
                for (int i = 0, k = 0; i < nv; i++) if (member[i]) idx[k++] = i;
                var T = new double[n]; var Rr = new double[n]; var A = new double[n]; var F = new double[n];
                double end = -1e9; bool anyEnd = false;
                for (int k = 0; k < n; k++)
                {
                    cyl(d, Vn[idx[k]], out T[k], out Rr[k], out A[k]);
                    F[k] = smooth01((d.wL[idx[k]] - d.wLow) / (d.wHigh - d.wLow));
                    if (F[k] > F_END && (!anyEnd || T[k] > end)) { end = T[k]; anyEnd = true; }
                }
                if (end <= d.tT + SHORT_MARGIN) return null;
                // 묶음은 원래 자리로(정규화가 같은 위치를 갈라도 같은 정점)
                var key = new int[nv];
                for (int i = 0; i < nv; i++) key[i] = -1;
                var inv = weld_union(V0, idx, out int count);
                for (int k = 0; k < n; k++) key[idx[k]] = inv[k];
                var tris = d.tris;
                var layer = layers(d, Vn, idx, T, A, key, member);
                double eW = d.ease + (Math.Min(d.ease, d.tT - t0) - d.ease) * G;   // 완만 구간이 걷는 구간보다 길면 끝이 되접힌다
                var m = roll_map(d, end, endPull, t0, eW);
                // 소맷단 재료는 팔을 두른 비율만큼 덜 누른다(wrap 1이면 항등)
                double cw = 1.0 - Math.Min((end - t0) / (m.endPull - t0) - 1.0, 1.0);
                double amp = d.foldA + (Math.Min(d.foldA, SLOPE_MAX * d.foldL / Math.PI) - d.foldA) * G;   // 주름 물결 기울기를 SLOPE_MAX 아래로
                var radii = (double[])Rr.Clone();
                var t2s = new double[n]; var weight = new double[n]; var floors = new double[n]; var t2a = new double[n];
                var touched = new bool[n]; var easeA = new double[n]; var tFullA = new double[n]; var foldA = new double[n];
                var skinList = new List<double>();
                var rollW = new double[nv];
                int movedVerts = 0;
                for (int k = 0; k < n; k++)
                {
                    double ease = smooth01((T[k] - t0) / eW);
                    double w = ease * F[k];
                    double tFull = map_full(m, T[k]);
                    double t2 = T[k] + (tFull - T[k]) * (m.even ? F[k] : w);
                    if (G > 0.0)   // 고른 사상으로 넘어간다: 재료가 많을수록 안 고른 사상은 t2가 되돌아 뒤집힌다
                    {
                        double xW = clip01((T[k] - t0) / (end - t0));
                        double tE = T[k] <= t0 ? T[k] : even_roll(m, t0 + (end - t0) * xW * (cw + (1.0 - cw) * xW));
                        t2 = t2 + (T[k] + (tE - T[k]) * F[k] - t2) * G;
                        tFull = tFull + (tE - tFull) * G;
                    }
                    double wr = (ease * (1.0 - m.taper) + smooth01((t2 - m.radialStart) / (m.tT - m.radialStart)) * m.taper) * F[k];
                    bool tch = w > 0.0 || wr > 0.0;
                    double rskin = skin_at(d.Rs, tFull - m.pull, A[k]);
                    double phase = Math.Max(tFull - t0, 0.0) * L / d.foldL;
                    double fold = amp * (0.5 + 0.5 * Math.Sin(2.0 * Math.PI * phase)) * ease;
                    double rFull = rskin + d.margin + layer[k] + Math.Max(Rr[k] - rskin - d.margin - layer[k], 0.0) * d.snug + fold;
                    double r2 = Rr[k] + (rFull - Rr[k]) * wr;
                    t2a[k] = t2; easeA[k] = ease; tFullA[k] = tFull; foldA[k] = fold; touched[k] = tch;
                    if (!tch) continue;
                    radii[k] = r2;
                    t2s[k] = t2; weight[k] = w; floors[k] = Math.Min(r2, rskin + d.margin);
                    rollW[idx[k]] = w;
                    if (w <= 0.0) continue;
                    movedVerts++;
                    if (F[k] > F_END) skinList.Add(rskin);
                }
                radii = smooth_radii(tris, idx, key, count, radii, weight);
                radii = smooth_lips(tris, nv, idx, A, radii, t2s, weight, floors, L, map_ease_end(m) + LIP_LEAD);
                // 반지름 변화는 나온 자리 t2 기준으로 걷는 구간 전체에 편다. 팔꿈치 쪽은 Start At 0이어도 반지름을 그대로 둔다
                double rs = Math.Max(t0, Math.Min(ELBOW_T, m.tT - d.cone));
                double progSpan = Math.Max(Math.Min(d.cone, m.tT - rs), SPAN_GUARD);
                double loose = Math.Pow((m.endPull - t0) / (end - t0), WRAP_POW);
                for (int k = 0; k < n; k++)
                {
                    if (!touched[k]) continue;
                    double prog = smooth01((t2a[k] - rs) / progSpan) * F[k];
                    double flo = skin_at(d.Rd, tFullA[k] - m.pull, A[k]) + Math.Max(d.margin, MIN_CLEAR);
                    radii[k] = monotone_radius(d, Rr[k], flo, foldA[k], prog, radii[k], G, loose);
                }
                // 걷기는 편 자리(Vn)에서 시작한다. 안 걷힌 정점도 편 자리에 남는다
                var delta = new D3[nv];
                for (int i = 0; i < nv; i++) delta[i] = Vn[i] - V0[i];
                for (int k = 0; k < n; k++)
                {
                    if (!touched[k]) continue;
                    var P = d.e + d.ax * (t2a[k] * L) + (d.u * Math.Cos(A[k]) + d.v * Math.Sin(A[k])) * radii[k];
                    delta[idx[k]] = P - V0[idx[k]];
                }
                pull_upper(d, V0, delta, m.pull);
                var rolled = new D3[nv];
                for (int i = 0; i < nv; i++) rolled[i] = V0[i] + delta[i];
                double med = 0.0;
                if (skinList.Count > 0) { skinList.Sort(); med = skinList[skinList.Count / 2]; }
                return new RollResult { rolled = rolled, rollW = rollW, movedVerts = movedVerts, skinMedian = med, pull = m.pull };
            }

            // ================ 이동 가중 복원 ================
            static double[] move_weight(D3[] off, double radius)
            {
                var W = new double[off.Length];
                for (int i = 0; i < off.Length; i++) W[i] = smooth01(off[i].Norm / (radius * W_SCALE));
                return W;
            }

            static D3[] stretch_repair(In d, D3[] rolled, double[] W, Info info)
            {
                var V0 = d.v0;
                double L = d.L;
                bool anyW = false;
                foreach (double x in W) if (x > 0.0) { anyW = true; break; }
                if (!anyW) return rolled;
                var key = weld_union(V0, null, out int nk);
                var X = new D3[nk]; var Rest = new D3[nk]; var Wk = new double[nk];
                for (int i = 0; i < V0.Length; i++) { X[key[i]] = rolled[i]; Rest[key[i]] = V0[i]; Wk[key[i]] = Math.Max(Wk[key[i]], W[i]); }
                var X0 = (D3[])X.Clone();
                var trList = new List<int>();
                for (int i = 0; i + 2 < d.tris.Length; i += 3)
                {
                    int a = key[d.tris[i]], b = key[d.tris[i + 1]], c = key[d.tris[i + 2]];
                    if (a == b || b == c || a == c) continue;
                    if (Wk[a] > 0.0 || Wk[b] > 0.0 || Wk[c] > 0.0) { trList.Add(a); trList.Add(b); trList.Add(c); }
                }
                unique_edges(trList.ToArray(), nk, out var ea, out var eb, out _);
                int ne = ea.Length;
                var lim = new double[ne];
                for (int i = 0; i < ne; i++) lim[i] = (Rest[ea[i]] - Rest[eb[i]]).Norm * (1.0 + STRETCH_TOL);
                var idx = new List<int>();
                for (int i = 0; i < nk; i++) if (Wk[i] > 0.0) idx.Add(i);
                double floorClear = Math.Max(d.margin, MIN_CLEAR);
                var D = new D3[nk]; var C = new double[nk];
                int it = -1;
                for (it = 0; it < PBD_ITERS; it++)
                {
                    Array.Clear(D, 0, nk); Array.Clear(C, 0, nk);
                    bool over = false;
                    for (int i = 0; i < ne; i++)
                    {
                        int a = ea[i], b = eb[i];
                        var v = X[b] - X[a];
                        double ln = v.Norm;
                        if (!(ln > lim[i])) continue;
                        over = true;
                        double ws = Wk[a] + Wk[b];
                        if (!(ws > 0.0)) continue;
                        double ex = (ln - lim[i]) / Math.Max(ws, PBD_WS_MIN);
                        var dirn = v / ln;
                        D[a] = D[a] + dirn * (Wk[a] * ex);
                        D[b] = D[b] - dirn * (Wk[b] * ex);
                        C[a] += 1.0; C[b] += 1.0;
                    }
                    if (!over) break;
                    for (int i = 0; i < nk; i++)
                    {
                        if (!(C[i] > 0.0)) continue;
                        var step = D[i] * PBD_RELAX / C[i];
                        double sn = step.Norm;
                        X[i] = X[i] + step * Math.Min(1.0, PBD_STEP * L / Math.Max(sn, 1e-12));
                    }
                    foreach (int i in idx)
                    {
                        if (!((X[i] - X0[i]).Norm > 0.0)) continue;
                        cyl(d, X[i], out double t1, out double r1, out double a1);
                        double fl = skin_at(d.Rd, t1, a1) + floorClear;
                        if (!(r1 < fl)) continue;
                        var q = X[i] - d.e;
                        var rad = q - d.ax * q.Dot(d.ax);
                        X[i] = X[i] + rad / Math.Max(r1, 1e-9) * (Wk[i] * (fl - r1));
                    }
                }
                var o = new D3[V0.Length];
                double mv = 0.0;
                for (int i = 0; i < nk; i++) mv = Math.Max(mv, (X[i] - X0[i]).Norm);
                for (int i = 0; i < V0.Length; i++) o[i] = rolled[i] + (X[key[i]] - X0[key[i]]);
                info.repairIters = Math.Min(it + 1, PBD_ITERS); info.repairMm = mv * 1000.0;
                return o;
            }

            // ================ 입구 ================
            /// <summary>solve의 moved 뒤쪽 — off가 있을 때만 부른다. 소매가 다 줄어 걷을 게 없으면 null</summary>
            internal static Out solve_moved(In d, D3[] off, Info info)
            {
                var W = move_weight(off, info.radius);
                var Vn = new D3[d.v0.Length];
                for (int i = 0; i < Vn.Length; i++) Vn[i] = d.v0[i] - off[i];
                double end = sleeve_end(d, Vn);
                var r = roll_deltas(d, Vn, W, end - info.sagLen);
                if (r == null) return null;
                info.pull = r.pull;
                var rolled = stretch_repair(d, r.rolled, W, info);
                return new Out { rolled = rolled, rollW = r.rollW, movedVerts = r.movedVerts, skinMedian = r.skinMedian };
            }

            internal static void SetSkin(In d, double[,] Rs) { d.Rs = Rs; d.Rd = dilate_skin(Rs); }

            /// <summary>빌드 경로. true = 정규화가 움직였다(delta를 그대로 쓴다, 소매가 다 줄어 못 걷으면 null). false = 이동 0 — 기존 RollDeltas로</summary>
            internal static bool TryRoll(Cache.Entry ce, in Forearm f, float[,] R, in Settings s, ref SideReport rep, out Vector3[] delta)
            {
                delta = null;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                double k = f.mPerUnit;
                var verts = ce.Vertices;
                int nv = verts.Length;
                var d = new In { v0 = new D3[nv], tris = ce.Triangles, wL = new double[nv], L = (double)f.L * k };
                var wc = ce.Weights(f.lower, null, true);
                for (int i = 0; i < nv; i++) { d.v0[i] = new D3(verts[i]) * k; d.wL[i] = wc[i]; }
                d.e = new D3(f.e) * k; d.ax = new D3(f.ax); d.u = new D3(f.u); d.v = new D3(f.v);
                d.tT = s.tTarget; d.t0 = s.t0; d.ease = s.easeLength; d.margin = s.margin; d.snug = s.snug;
                d.foldL = s.foldLength; d.foldA = s.foldAmplitude; d.wLow = s.weightLow; d.wHigh = s.weightHigh;
                var Rs = new double[NT, NA];
                for (int i = 0; i < NT; i++) for (int j = 0; j < NA; j++) Rs[i, j] = R[i, j] * k;
                SetSkin(d, Rs);   // norm_core의 시작 원뿔이 Rs를 읽는다
                var info = new Info();
                var off = norm_core(d, info);
                if (off == null) { TotalNormMs += sw.Elapsed.TotalMilliseconds; return false; }
                // 여기부터 정규화가 움직인 소매 — 정규화 경로만 탄다
                d.hasUpper = f.upper != null;
                if (d.hasUpper)
                {
                    d.sh = new D3(f.shoulder) * k;
                    var wu = ce.Weights(f.upper, null, true);
                    d.wU = new double[nv];
                    for (int i = 0; i < nv; i++) d.wU[i] = wu[i];
                }
                var o = solve_moved(d, off, info);
                TotalNormMs += sw.Elapsed.TotalMilliseconds;
                rep.note = (rep.note != null ? rep.note + "\n    " : "") + $"normalized {info} {sw.Elapsed.TotalMilliseconds:F0} ms";
                if (o == null) { rep.message = "the sleeve is already shorter than Roll To after normalizing"; return true; }
                delta = new Vector3[nv];
                rep.roll = new float[nv];
                for (int i = 0; i < nv; i++)
                {
                    var q = (o.rolled[i] - d.v0[i]) / k;
                    delta[i] = new Vector3((float)q.x, (float)q.y, (float)q.z);
                    rep.roll[i] = (float)o.rollW[i];
                    if (float.IsNaN(delta[i].x) || float.IsNaN(delta[i].y) || float.IsNaN(delta[i].z) || float.IsInfinity(delta[i].x) || float.IsInfinity(delta[i].y) || float.IsInfinity(delta[i].z))
                    {
                        rep.message = "normalizing produced an invalid position — skipped";
                        delta = null;
                        return true;
                    }
                }
                rep.movedVertices = o.movedVerts;
                rep.skinRadiusMm = (float)(o.skinMedian * 1000.0);
                return true;
            }
        }

        /// <summary>개발 대조용 — Python과 같은 입력으로 핵심만 돌린다(유니티 메시 안 거침, 세계 좌표·위쪽 축 (0,1,0)). 이동 0이면 null</summary>
        public static double[] NormTrialRaw(float[] v0, int[] tris, float[] wL, float[] wU, double[] e, double[] h, double[] sh, double[] rsFlat,
            double tT, double t0, double margin, double snug, double foldL, double foldA, out string infoText)
        {
            int nv = v0.Length / 3;
            var d = new Norm6.In { v0 = new D3[nv], tris = tris, wL = new double[nv], wU = new double[nv] };
            for (int i = 0; i < nv; i++)
            {
                d.v0[i] = new D3(v0[i * 3], v0[i * 3 + 1], v0[i * 3 + 2]);
                d.wL[i] = wL[i]; d.wU[i] = wU[i];
            }
            d.e = new D3(e[0], e[1], e[2]);
            var hd = new D3(h[0], h[1], h[2]);
            d.sh = new D3(sh[0], sh[1], sh[2]);
            d.L = (hd - d.e).Norm;
            d.ax = (hd - d.e) / d.L;
            var up = new D3(0.0, 1.0, 0.0);
            up = up - d.ax * up.Dot(d.ax);
            d.u = up / up.Norm;
            d.v = d.ax.Cross(d.u);
            d.hasUpper = true;
            d.tT = tT; d.t0 = t0; d.ease = 0.30; d.margin = margin; d.snug = snug; d.foldL = foldL; d.foldA = foldA; d.wLow = 0.2; d.wHigh = 0.8;
            var Rs = new double[NT, NA];
            for (int i = 0; i < NT; i++) for (int j = 0; j < NA; j++) Rs[i, j] = rsFlat[i * NA + j];
            Norm6.SetSkin(d, Rs);
            var info = new Norm6.Info();
            var off = Norm6.norm_core(d, info);
            infoText = info.ToString();
            if (off == null) return null;
            var o = Norm6.solve_moved(d, off, info);
            infoText = info.ToString();
            if (o == null) return null;
            var f = new double[nv * 3];
            for (int i = 0; i < nv; i++) { f[i * 3] = o.rolled[i].x; f[i * 3 + 1] = o.rolled[i].y; f[i * 3 + 2] = o.rolled[i].z; }
            return f;
        }

        /// <summary>개발 대조용 — 지금까지 정규화 계산에 쓴 시간(ms)</summary>
        public static double NormTrialTotalMs { get => Norm6.TotalNormMs; set => Norm6.TotalNormMs = value; }
    }
}
