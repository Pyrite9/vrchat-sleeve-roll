// Pyrite9 Sleeve Roll — 계산: 팔뚝 좌표계, 빌드 한 번 동안의 메시 읽기 캐시, 칸·웨이트 보조
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pyrite9.Sleeve
{
    public static partial class SleeveRollShape
    {
        /// <summary>팔뚝 좌표계 — 메시(바인드 포즈) 공간의 팔꿈치 e·축 ax(단위)·길이 L·둘레 기준 u, v, 메시 단위 → m 환산</summary>
        struct Forearm
        {
            public Transform upper; public Vector3 shoulder;   // 위팔 본이 메시에 없으면 upper = null — 위팔 당김만 건너뛴다
            public Transform lower, hand;
            public Matrix4x4 laToMesh;   // 아래팔 본 공간 → 메시 공간(바인드 포즈 역)
            public Vector3 e, ax, u, v;
            public float L, mPerUnit;

            /// <summary>메시 공간 점 → 팔뚝 원통 좌표(t: 축 위치 ÷ L, r: 축 거리, a: 둘레 각도)</summary>
            public readonly void Cyl(Vector3 p, out float t, out float r, out float a)
            {
                var d = p - e;
                t = Vector3.Dot(d, ax) / L;
                float x = Vector3.Dot(d, u), y = Vector3.Dot(d, v);
                r = Mathf.Sqrt(x * x + y * y);
                a = Mathf.Atan2(y, x);
            }
        }

        // 쪽 하나의 팔뚝 좌표계. 못 만들면 이유(영어, 보고용), 되면 null
        static string GetForearm(Animator animator, bool left, Transform[] bones, Matrix4x4[] bp, out Forearm f, Dictionary<Transform, Transform> map = null)
        {
            f = default;
            f.lower = animator.GetBoneTransform(SrSide.Lower(left));
            f.hand = animator.GetBoneTransform(SrSide.Hand(left));
            if (f.lower == null || f.hand == null) return "no humanoid forearm or hand bone";
            int hi = Array.IndexOf(bones, f.hand);
            if (!LowerBind(bones, bp, f.lower, f.hand, out var bpLower, map)) return "the garment is not weighted to the forearm bone";
            f.laToMesh = bpLower.inverse;
            f.e = f.laToMesh.MultiplyPoint3x4(Vector3.zero);
            // 손 본이 메시에 없으면 바인드 포즈를 아래팔 기준 상대 위치로 구한다
            var wr = hi >= 0 ? bp[hi].inverse.MultiplyPoint3x4(Vector3.zero)
                             : f.laToMesh.MultiplyPoint3x4(f.lower.InverseTransformPoint(f.hand.position));
            var ax = wr - f.e;
            f.L = ax.magnitude;
            if (f.L < 1e-6f) return "the forearm has zero length";
            ax /= f.L;
            f.ax = ax;
            f.mPerUnit = Vector3.Distance(f.lower.position, f.hand.position) / f.L;
            f.upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            int ua = f.upper != null ? Array.IndexOf(bones, f.upper) : -1;
            if (ua >= 0) f.shoulder = bp[ua].inverse.MultiplyPoint3x4(Vector3.zero); else f.upper = null;
            // 둘레 각도 기준은 아무 수직 벡터면 된다 — 옷·피부에 같은 틀만 쓰면 됨
            var cand = Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.forward;
            f.u = (cand - ax * Vector3.Dot(cand, ax)).normalized;
            f.v = Vector3.Cross(ax, f.u);
            return null;
        }

        /// <summary>
        /// 빌드 한 번 동안 렌더러마다 메시 읽기(본·바인드 포즈·정점·웨이트·몸 숨김)를 나눠 쓴다 — 옷마다·쪽마다 다른 메시 전부를 다시 읽지 않게.
        /// 렌더러의 sharedMesh가 바뀌면(옷·몸 사본) 새로 읽는다. 본 목록이 바뀐 렌더러(Rebind 뒤의 옷)엔 쓰지 않는다
        /// </summary>
        internal sealed class Cache
        {
            internal sealed class Entry
            {
                public Mesh mesh;
                /// <summary>계산용 — 의상 쪽 본(합치기 전 의상 본, MA가 남긴 의상 본, MA ScaleProxy)을 대응하는 아바타 본으로 바꾸고,
                /// 바인드 포즈는 MA 재타깃과 같은 식으로 그 아바타 본 기준으로 고친 것(가상 재타깃 — 렌더러·메시는 안 바꾼다)</summary>
                public Transform[] bones;
                public Matrix4x4[] bindposes;
                /// <summary>렌더러의 실제 본·메시 바인드 포즈 — PhysBone 체인처럼 실제 Transform을 비교할 때. 대응이 없으면 bones·bindposes와 같은 배열</summary>
                public Transform[] rawBones;
                public Matrix4x4[] rawBindposes;
                public HideInfo[] hide;          // ActiveHide 좌·우, null = 아직 안 구함
                public SkinnedMeshRenderer shapes;   // 셰이프 값을 읽을 렌더러 — 빌드는 그 렌더러, 미리 보기는 프록시
                public Dictionary<Transform, Transform> boneMap;   // 미리 보기만 — 아직 안 합쳐진 의상 본 → 아바타 본
                Vector3[] vertices;
                readonly Dictionary<(Transform, Transform, bool), float[]> weights = new Dictionary<(Transform, Transform, bool), float[]>();

                int[] triangles;

                /// <summary>읽기만 — 고칠 땐 복사본을</summary>
                public Vector3[] Vertices => vertices ?? (vertices = mesh.vertices);

                /// <summary>삼각형 서브메시 전부를 이어 붙인 것(읽기만)</summary>
                public int[] Triangles
                {
                    get
                    {
                        if (triangles != null) return triangles;
                        var all = new List<int>();
                        for (int sm = 0; sm < mesh.subMeshCount; sm++)
                            if (mesh.GetTopology(sm) == MeshTopology.Triangles) all.AddRange(mesh.GetTriangles(sm));
                        return triangles = all.ToArray();
                    }
                }

                /// <summary>
                /// 정점마다 고른 본 웨이트 합. descendants: 아래팔과 그 자손 전부 / 아니면 아래팔(+ hand가 있으면 손)만
                /// </summary>
                public float[] Weights(Transform lower, Transform hand, bool descendants)
                {
                    var key = (lower, hand, descendants);
                    if (weights.TryGetValue(key, out var w)) return w;
                    Func<Transform, bool> pick;
                    if (descendants) pick = b => { var x = Resolve(b); return x == lower || x.IsChildOf(lower); };
                    else if (hand != null) pick = b => b == lower || b == hand;
                    else pick = b => b == lower;
                    w = SleeveRollShape.Weights(bones, mesh, pick);
                    weights[key] = w;
                    return w;
                }

                // 의상에만 있는 본(소맷단 흔들림 본 등)은 합쳐지면 아바타 본 밑에 들어간다 — 가장 가까운 대응 본으로 본다
                Transform Resolve(Transform b)
                {
                    if (boneMap == null) return b;
                    for (var p = b; p != null; p = p.parent)
                        if (boneMap.TryGetValue(p, out var to)) return to;
                    return b;
                }
            }

            readonly Dictionary<SkinnedMeshRenderer, Entry> map = new Dictionary<SkinnedMeshRenderer, Entry>();

            /// <summary>미리 보기만(빌드는 null): 원본 → 프록시(앞 단계가 바꾼 메시·셰이프 값)</summary>
            public Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer> proxies;
            /// <summary>
            /// 의상 본 → 아바타 본(MA Merge Armature 매핑). 미리 보기는 그때 읽은 것, 빌드는 Generating(MA 앞)에서 찍어 둔 것 —
            /// MA가 합치지 않고 남긴 의상 본도 Transform이 그대로라 찾힌다(문의 10-73)
            /// </summary>
            public Dictionary<Transform, Transform> boneMap;
            /// <summary>빌드만: 찍어 둘 때 있던 Transform 전부. 그 뒤에 생긴 본(MA ScaleProxy·중간 오브젝트)은 가장 가까운 조상의 대응으로 본다. null = 매핑만</summary>
            public HashSet<Transform> known;

            /// <summary>계산에서 이 본을 어느 본으로 볼까 — 대응이 없으면 그대로</summary>
            public Transform Map(Transform b)
            {
                if (b == null || boneMap == null) return b;
                if (boneMap.TryGetValue(b, out var to))
                {
                    // 의상을 다른 의상에 합치는 경우 — 짝을 끝까지 따라간다
                    for (int hop = 0; hop < 8 && boneMap.TryGetValue(to, out var next) && next != null; hop++) to = next;
                    return to;
                }
                if (known == null || known.Contains(b)) return b;
                for (var p = b.parent; p != null; p = p.parent)
                {
                    if (boneMap.ContainsKey(p)) return Map(p);
                    if (known.Contains(p)) return p;
                }
                return b;
            }

            /// <summary>렌더러 메시를 정점·웨이트가 같은 사본으로 바꾼 뒤(셰이프만 더함) — 읽은 것을 그대로 쓴다. 안 그러면 다음 Get이 전부 다시 읽는다</summary>
            public void Adopt(SkinnedMeshRenderer r, Mesh copy)
            {
                if (map.TryGetValue(r, out var en)) en.mesh = copy;
            }

            public Entry Get(SkinnedMeshRenderer r)
            {
                SkinnedMeshRenderer proxy = null;
                if (proxies != null) proxies.TryGetValue(r, out proxy);
                var m = proxy != null ? proxy.sharedMesh : r.sharedMesh;
                if (map.TryGetValue(r, out var en) && en.mesh == m) return en;
                var raw = r.bones;
                var rawBp = m.bindposes;
                Transform[] bones = raw;
                Matrix4x4[] bp = rawBp;
                for (int i = 0; i < raw.Length && i < rawBp.Length; i++)
                {
                    var to = Map(raw[i]);
                    if (to == null || to == raw[i]) continue;
                    if (bones == raw) { bones = (Transform[])raw.Clone(); bp = (Matrix4x4[])rawBp.Clone(); }
                    bones[i] = to;
                    // MA RetargetMeshes와 같은 식: 그 본에 실려 있던 정점이 아바타 본 기준으로 같은 자리에 오게
                    bp[i] = to.worldToLocalMatrix * raw[i].localToWorldMatrix * rawBp[i];
                }
                en = new Entry { mesh = m, bones = bones, bindposes = bp, rawBones = raw, rawBindposes = rawBp, shapes = proxy != null ? proxy : r, boneMap = boneMap };
                map[r] = en;
                return en;
            }
        }

        /// <summary>MA Merge Armature 의상 본 → 아바타 본(MA가 합칠 짝). MA 앞(미리 보기·Generating)에서만 의미가 있다</summary>
        internal static Dictionary<Transform, Transform> MergeMap(IEnumerable<nadena.dev.modular_avatar.core.ModularAvatarMergeArmature> merges)
        {
            var map = new Dictionary<Transform, Transform>();
            foreach (var m in merges)
            {
                var target = m.mergeTargetObject;
                if (target == null) continue;
                map[m.transform] = target.transform;
                var pairs = m.GetBonesMapping();
                if (pairs == null) continue;
                foreach (var (avatarBone, outfitBone) in pairs)
                    if (avatarBone != null && outfitBone != null) map[outfitBone] = avatarBone;
            }
            return map;
        }

        // 같은 위치 정점(UV 이음새)을 한 번호로 묶는다. only: 이 정점들만(나머지는 −1), null이면 전부. count: 묶음 수
        static int[] WeldKeys(Vector3[] verts, bool[] only, out int count)
        {
            var key = new int[verts.Length];
            var map = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < verts.Length; i++)
            {
                if (only != null && !only[i]) { key[i] = -1; continue; }
                var q = Vector3Int.RoundToInt(verts[i] * 100000f);
                if (!map.TryGetValue(q, out key[i])) { key[i] = map.Count; map[q] = key[i]; }
            }
            count = map.Count;
            return key;
        }

        // 칸 값을 t·θ로 이어서 읽는다 — 칸 그대로면 걷은 띠에 몰린 정점이 칸 경계에서 계단을 탄다
        static float SkinAt(float[,] R, float t, float a)
        {
            float ft = (t - TMin) / (TMax - TMin) * NT - 0.5f;
            int t0 = Mathf.Clamp(Mathf.FloorToInt(ft), 0, NT - 1), t1 = Mathf.Min(t0 + 1, NT - 1);
            float ut = Mathf.Clamp01(ft - t0);
            float fa = (a + Mathf.PI) / (2f * Mathf.PI) * NA - 0.5f;
            int a0 = Mathf.FloorToInt(fa);
            float ua = fa - a0;
            int a1 = ((a0 + 1) % NA + NA) % NA;
            a0 = (a0 % NA + NA) % NA;
            return Mathf.Lerp(Mathf.Lerp(R[t0, a0], R[t0, a1], ua), Mathf.Lerp(R[t1, a0], R[t1, a1], ua), ut);
        }

        static int TBin(float t) => Mathf.Clamp((int)((t - TMin) / (TMax - TMin) * NT), 0, NT - 1);
        static int ABin(float a) => ((int)((a + Mathf.PI) / (2f * Mathf.PI) * NA) % NA + NA) % NA;
        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>
        /// 메시 공간에서 본 아래팔 바인드 포즈. 본 목록에 아래팔이 있으면 그것, 없으면 아래팔의 자손 본(손 밑은 제외 — 트위스트·변환 도구가 남긴
        /// 원래 본 등, 문의 10-75)에서 유도한다(가상 재타깃과 같은 식, 지금 자세 = 바인드 자세 가정은 MA와 같음). 손 밑만 있으면 소매가 다른 데
        /// (위팔 밑 소매 본 등) 묶여 있다는 뜻이라 유도하지 않는다 — 소매 일부만 걷혀 찢어진 결과를 성공으로 내지 않게. 못 구하면 false
        /// </summary>
        internal static bool LowerBind(Transform[] bones, Matrix4x4[] bp, Transform lower, Transform hand, out Matrix4x4 bpLower, Dictionary<Transform, Transform> map = null)
        {
            bpLower = default;
            if (lower == null) return false;
            int la = Array.IndexOf(bones, lower);
            if (la >= 0 && la < bp.Length) { bpLower = bp[la]; return true; }
            for (int i = 0; i < bones.Length && i < bp.Length; i++)
            {
                var b = bones[i];
                if (b == null || b == lower || !IsUnder(b, lower, map)) continue;
                if (hand != null && (b == hand || IsUnder(b, hand, map))) continue;
                bpLower = lower.worldToLocalMatrix * b.localToWorldMatrix * bp[i];
                return true;
            }
            return false;
        }

        /// <summary>b가 x의 자손인가 — 미리 보기(MA 앞)에선 의상에만 있는 본이 아직 의상 본 밑이라, 처음 만나는 짝 있는 조상을 아바타 본으로 본다</summary>
        static bool IsUnder(Transform b, Transform x, Dictionary<Transform, Transform> map)
        {
            for (var p = b.parent; p != null; p = p.parent)
            {
                if (p == x) return true;
                if (map != null && map.TryGetValue(p, out var q) && q != null) return q == x || q.IsChildOf(x);
            }
            return false;
        }

        /// <summary>
        /// 실패 진단(빌드 로그용, 영어) — 그쪽 위팔 밑에 웨이트가 실린 메시 본 전부: 실제 본 경로, 계산에서 본 본, Merge 짝·MA 앞에 있던 본인지,
        /// 웨이트 합·정점 수. 위팔 밑에 하나도 없으면 웨이트가 큰 본 8개. 문의 10-75 — 사용자 옷 구조를 로그 한 번으로 알기 위해
        /// </summary>
        internal static string Diagnose(Animator animator, bool left, Cache cache, Cache.Entry ce)
        {
            var sb = new System.Text.StringBuilder();
            var root = animator.transform;
            var upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var lower = animator.GetBoneTransform(SrSide.Lower(left));
            var hand = animator.GetBoneTransform(SrSide.Hand(left));
            string P(Transform t) => t == null ? "null" : t == root ? "<root>" : Path(root, t);
            sb.Append($"diag: humanoid upper={P(upper)} lower={P(lower)} hand={P(hand)}; mesh bones={ce.rawBones.Length}, " +
                      $"lower in list: raw={Array.IndexOf(ce.rawBones, lower) >= 0} mapped={Array.IndexOf(ce.bones, lower) >= 0}; " +
                      $"merge pairs={(cache.boneMap != null ? cache.boneMap.Count : -1)}, known={(cache.known != null ? cache.known.Count : -1)}");
            var sum = new float[ce.rawBones.Length];
            var cnt = new int[ce.rawBones.Length];
            var per = ce.mesh.GetBonesPerVertex();
            var bw = ce.mesh.GetAllBoneWeights();
            int c = 0;
            for (int v = 0; v < per.Length; v++)
                for (int j = 0; j < per[v]; j++, c++)
                    if (bw[c].boneIndex < sum.Length && bw[c].weight > 0f) { sum[bw[c].boneIndex] += bw[c].weight; cnt[bw[c].boneIndex]++; }
            var arm = new List<int>();
            for (int i = 0; i < sum.Length; i++)
            {
                if (cnt[i] == 0) continue;
                var raw = ce.rawBones[i]; var map = ce.bones[i];
                if (upper != null && ((raw != null && raw.IsChildOf(upper)) || (map != null && map.IsChildOf(upper)))) arm.Add(i);
            }
            if (arm.Count == 0)
            {
                for (int i = 0; i < sum.Length; i++) if (cnt[i] > 0) arm.Add(i);
                sb.Append("\n    diag: no weighted bone under the upper arm — top weighted bones:");
            }
            arm.Sort((a, b) => sum[b].CompareTo(sum[a]));
            if (arm.Count > 16) arm.RemoveRange(16, arm.Count - 16);
            foreach (int i in arm)
            {
                var raw = ce.rawBones[i]; var map = ce.bones[i];
                bool pair = raw != null && cache.boneMap != null && cache.boneMap.ContainsKey(raw);
                bool known = raw != null && cache.known != null && cache.known.Contains(raw);
                sb.Append($"\n    diag: [{i}] {P(raw)}{(map != raw ? " -> " + P(map) : "")} w={sum[i]:F1} v={cnt[i]} pair={pair} known={known}");
            }
            return sb.ToString();
        }

        static string Path(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var p = t; p != null && p != root; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            // 앞쪽(Armature/Hips/Spine…)은 줄인다 — 마지막 5단만
            if (parts.Count > 5) { parts.RemoveRange(0, parts.Count - 5); parts.Insert(0, "…"); }
            return string.Join("/", parts);
        }

        static float[] Weights(Transform[] bones, Mesh m, Func<Transform, bool> pick)
        {
            var use = new bool[bones.Length];
            bool any = false;
            for (int i = 0; i < bones.Length; i++) any |= use[i] = bones[i] != null && pick(bones[i]);
            var w = new float[m.vertexCount];
            if (!any) return w;
            var per = m.GetBonesPerVertex();
            var bw = m.GetAllBoneWeights();
            int c = 0;
            for (int i = 0; i < per.Length; i++)
                for (int j = 0; j < per[i]; j++, c++)
                    if (bw[c].boneIndex < use.Length && use[bw[c].boneIndex]) w[i] += bw[c].weight;
            return w;
        }
    }
}
