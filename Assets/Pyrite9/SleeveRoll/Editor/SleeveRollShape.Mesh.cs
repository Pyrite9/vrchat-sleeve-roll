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
            /// <summary>셰이프 계산의 아래팔·위팔 웨이트(Cache.Entry.RollWeights, Side가 채움). null이면 읽는 쪽이 Weights(…, true)로</summary>
            public float[] wc, wu;
            /// <summary>셰이프로 접혀 숨은 소매 정점(Side의 Folded, 0.1.3-beta6) — 겹 두께가 그 면을 겹으로 읽지 않게. null = 없음</summary>
            public bool[] hid;
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
            var wr = hi >= 0 && hi < bp.Length ? bp[hi].inverse.MultiplyPoint3x4(Vector3.zero)
                             : f.laToMesh.MultiplyPoint3x4(f.lower.InverseTransformPoint(f.hand.position));
            var ax = wr - f.e;
            f.L = ax.magnitude;
            if (f.L < 1e-6f) return "the forearm has zero length";
            ax /= f.L;
            f.ax = ax;
            f.mPerUnit = Vector3.Distance(f.lower.position, f.hand.position) / f.L;
            f.upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            int ua = f.upper != null ? Array.IndexOf(bones, f.upper) : -1;
            if (ua >= 0 && ua < bp.Length) f.shoulder = bp[ua].inverse.MultiplyPoint3x4(Vector3.zero); else f.upper = null;   // 본 목록이 바인드 포즈보다 긴 렌더러(메시만 바꿔 끼움)도 있다
            // 둘레 각도 기준은 아무 수직 벡터면 된다 — 옷·피부에 같은 틀만 쓰면 됨
            var cand = Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.forward;
            f.u = (cand - ax * Vector3.Dot(cand, ax)).normalized;
            f.v = Vector3.Cross(ax, f.u);
            return null;
        }

        /// <summary>
        /// 빌드 한 번 동안 렌더러마다 메시 읽기(본·바인드 포즈·정점·웨이트·몸 숨김)를 나눠 쓴다 — 옷마다·쪽마다 다른 메시 전부를 다시 읽지 않게.
        /// 렌더러의 sharedMesh가 바뀌면(옷·몸 사본) 새로 읽는다. Rebind는 메시 객체를 그대로 두고 대리 본을 본 목록 맨 뒤에 붙이므로
        /// 그 뒤의 Get은 Rebind 전 항목을 돌려준다 — 기존 본 인덱스는 그대로라 체인 본 바인드 포즈 읽기(SleeveRollPb.Reparent)엔 맞지만, 대리 본·옮긴 웨이트는 안 보인다
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
                public Dictionary<Transform, Transform> boneMap;   // 의상 본 → 아바타 본 짝(Cache.boneMap) — 미리 보기는 그때 읽은 것, 빌드는 Generating에서 찍어 둔 것
                Vector3[] vertices, normals, baseVertices;
                List<(int index, string name, float weight)> worn;
                readonly Dictionary<(Transform, Transform, int), float[]> weights = new Dictionary<(Transform, Transform, int), float[]>();

                int[] triangles;

                /// <summary>
                /// 입은 모양(0.1.3-beta6) — 기본 정점 + 렌더러에 지금 걸린 셰이프 값 × 그 값의 프레임 보간 델타(Sleeve Roll 자신의 셰이프는 빼고).
                /// 걷기 계산의 입력. 벤더 '겉옷용' 축소 셰이프처럼 켜 둔 셰이프가 소매를 옮긴 옷도 보이는 모양으로 걷는다(10-92). 셰이프가 다 0이면 기본 정점 그대로(같은 배열). 읽기만
                /// </summary>
                public Vector3[] Vertices { get { if (vertices == null) Wear(); return vertices; } }
                /// <summary>입은 모양의 노멀(기본 + 셰이프 노멀 델타, 정규화 전 — 렌더러가 더하는 그대로). 노멀 없는 메시는 빈 배열. 읽기만</summary>
                public Vector3[] Normals { get { if (vertices == null) Wear(); return normals; } }
                /// <summary>메시 기본 정점(셰이프 0) — 셰이프 이동을 따로 재는 몸 숨김(Hide)·겹 순서용 반지름. 읽기만</summary>
                public Vector3[] BaseVertices => baseVertices ?? (baseVertices = mesh.vertices);
                /// <summary>입은 모양에 더한 셰이프(메시 인덱스·이름·값) — 정점이나 노멀을 움직인 것만</summary>
                public List<(int index, string name, float weight)> Worn { get { if (vertices == null) Wear(); return worn; } }

                void Wear()
                {
                    var v = BaseVertices;
                    var nrm = mesh.normals;
                    worn = new List<(int, string, float)>();
                    int nv = v.Length, count = mesh.blendShapeCount;
                    if (shapes != null && count > 0)
                    {
                        bool hasN = nrm.Length == nv;
                        Vector3[] dv = null, fa = null, fb = null, dn = null, na = null, nb = null;
                        for (int i = 0; i < count; i++)
                        {
                            var name = mesh.GetBlendShapeName(i);
                            if (OwnShape(name)) continue;
                            float w = shapes.GetBlendShapeWeight(i);
                            if (Mathf.Abs(w) < 0.01f) continue;   // Hide와 같은 문턱
                            if (dv == null)
                            {
                                dv = new Vector3[nv]; fa = new Vector3[nv]; fb = new Vector3[nv];
                                if (hasN) { dn = new Vector3[nv]; na = new Vector3[nv]; nb = new Vector3[nv]; }
                            }
                            if (!FrameDelta(mesh, i, w, dv, fa, fb, dn, na, nb)) continue;
                            bool any = false;
                            for (int k = 0; k < nv && !any; k++) any = dv[k].x != 0f || dv[k].y != 0f || dv[k].z != 0f
                                                                     || (hasN && (dn[k].x != 0f || dn[k].y != 0f || dn[k].z != 0f));
                            if (!any) continue;
                            if (ReferenceEquals(v, baseVertices)) { v = (Vector3[])v.Clone(); }
                            for (int k = 0; k < nv; k++) v[k] += dv[k];
                            if (hasN) for (int k = 0; k < nv; k++) nrm[k] += dn[k];
                            worn.Add((i, name, w));
                        }
                    }
                    vertices = v; normals = nrm;
                }

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
                public float[] Weights(Transform lower, Transform hand, bool descendants) => Weights(lower, hand, descendants ? 1 : 0);

                /// <summary>
                /// 피부 표본(아래팔이 본 목록에 있는 몸, 0.1.3-beta4 문제 A) — 아래팔 + 손 + 아래팔 자손 중 손·손 밑이 아닌 본(트위스트 등).
                /// 팔뚝 피부 웨이트가 트위스트 본에 더 많이 실린 몸(Milltina `Lower_arm_Twist_L`)도 팔꿈치 쪽부터 잡는다
                /// </summary>
                public float[] ArmWeights(Transform lower, Transform hand) => Weights(lower, hand, 2);

                // mode 0: 아래팔(+손) / 1: 아래팔과 그 자손 전부 / 2: 아래팔 + 손 + 아래팔 자손 중 손 밑이 아닌 본
                float[] Weights(Transform lower, Transform hand, int mode)
                {
                    var key = (lower, hand, mode);
                    if (weights.TryGetValue(key, out var w)) return w;
                    Func<Transform, bool> pick;
                    if (mode == 1) pick = b => { var x = Resolve(b); return x == lower || x.IsChildOf(lower); };
                    else if (mode == 2)
                        pick = b =>
                        {
                            if (b == lower || b == hand) return true;
                            var x = Resolve(b);
                            return (x == lower || x.IsChildOf(lower)) && !(hand != null && x.IsChildOf(hand));
                        };
                    else if (hand != null) pick = b => b == lower || b == hand;
                    else pick = b => b == lower;
                    w = SleeveRollShape.Weights(bones, mesh, pick);
                    weights[key] = w;
                    return w;
                }

                readonly Dictionary<(Transform, Transform), (float[] wc, float[] wu)> rollWeights = new Dictionary<(Transform, Transform), (float[], float[])>();

                /// <summary>
                /// 셰이프 계산의 아래팔·위팔 웨이트(0.1.3-beta5). 걷힌 뒤 아래팔에 고정될 본(따라가기 그림자 대상 — moving 중 그쪽 아래팔 서브트리 밖)의
                /// 웨이트를 아래팔 웨이트로 본다: wc(아래팔 서브트리)에 더하고, 그중 위팔 서브트리 본 몫은 wu에서 뺀다.
                /// 아래팔 서브트리 웨이트가 0.01 넘는 정점(소매 멤버)만 — 아래팔 웨이트 없는 PB 자락은 그대로. 바뀌는 정점이 없으면 원래 배열 그대로(같은 객체)
                /// </summary>
                public void RollWeights(Transform lower, Transform upper, HashSet<Transform> moving, out float[] wc, out float[] wu)
                {
                    wc = Weights(lower, null, true);
                    wu = upper != null ? Weights(upper, null, true) : null;
                    if (moving == null || moving.Count == 0) return;
                    if (rollWeights.TryGetValue((lower, upper), out var got)) { wc = got.wc; wu = got.wu; return; }
                    var follow = new bool[bones.Length];
                    var underUpper = new bool[bones.Length];
                    bool any = false;
                    for (int i = 0; i < bones.Length && i < rawBones.Length; i++)
                    {
                        if (bones[i] == null || rawBones[i] == null || !moving.Contains(rawBones[i])) continue;
                        var x = Resolve(bones[i]);
                        if (x.IsChildOf(lower)) continue;   // 아래팔 서브트리(자기 포함) — 유지 체인·대리 본·손가락 그림자 몫
                        follow[i] = any = true;
                        underUpper[i] = upper != null && x.IsChildOf(upper);
                    }
                    float[] nc = null, nu = null;
                    if (any)
                    {
                        var per = mesh.GetBonesPerVertex();
                        var bw = mesh.GetAllBoneWeights();
                        int c = 0;
                        for (int v = 0; v < per.Length; v++)
                            for (int j = 0; j < per[v]; j++, c++)
                            {
                                var b = bw[c];
                                if (b.weight <= 0f || b.boneIndex >= follow.Length || !follow[b.boneIndex] || wc[v] <= 0.01f) continue;
                                if (nc == null) nc = (float[])wc.Clone();
                                nc[v] += b.weight;
                                if (!underUpper[b.boneIndex] || wu == null) continue;
                                if (nu == null) nu = (float[])wu.Clone();
                                nu[v] = Mathf.Max(0f, nu[v] - b.weight);
                            }
                    }
                    if (nc != null) wc = nc;
                    if (nu != null) wu = nu;
                    rollWeights[(lower, upper)] = (wc, wu);
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

            HashSet<Transform> moving;
            /// <summary>
            /// 아래팔에 대해 움직이는 본(SleeveRollPb.MovingBones, 0.1.3-beta5) — 이 캐시에서 처음 물을 때 한 번 구해 셰이프 계산(RollWeights)과
            /// 따라가기 그림자(SleeveRollBake.Follow)가 같은 집합을 쓴다. 빌드는 셰이프 전(그림자·체인 이동 전)에 구한다. 대응이 휴머노이드인 의상 본도 몸으로 본다(미리 보기 = 빌드)
            /// </summary>
            public HashSet<Transform> Moving(Animator animator) => moving ?? (moving = SleeveRollPb.MovingBones(animator.gameObject, animator, Map));

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

        /// <summary>Sleeve Roll이 넣는 셰이프(걷기·되돌리기, 숨김 셰이프별로 나눈 되돌리기 SleeveRoll_Unhide_L__&lt;셰이프&gt; 포함) — 입은 모양에 넣지 않는다(이미 셰이프가 있는 옷, 미리 보기 프록시가 켠 값)</summary>
        static bool OwnShape(string name) =>
            name == ShapeLeft || name == ShapeRight || name == UnhideLeft || name == UnhideRight
            || name.StartsWith(UnhideLeft + "__") || name.StartsWith(UnhideRight + "__");

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

        /// <summary>b가 x의 자손인가 — 미리 보기(MA 앞)에선 의상에만 있는 본이 아직 의상 본 밑이라, 처음 만나는 짝 있는 조상을 아바타 본으로 본다.
        /// 빌드(MA 뒤)에선 MA가 남긴 의상 본(짝 있음)이 아바타 본 밑에 있어 같은 답이 나온다. 짝은 한 단계만 따라간다(의상을 의상에 합친 경우는 MA 앞에서 못 찾는다)</summary>
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
        /// MA 앞(Generating의 되돌리기 FX 곡선, 미리 보기 그룹)에서 본 "이 메시의 아래팔 슬롯" — 빌드 뒷단이 팔뚝 틀을 잡는 규칙(LowerBind)과 같게:
        /// 짝을 끝까지 따라가 아래팔인 본. 그런 본이 하나도 없으면 아래팔의 자손 본(손 밑 제외 — 트위스트, 소체가 제약으로 따라가게 둔 사본 등).
        /// 뒷단은 그런 몸에도 되돌리기 셰이프를 넣는다 — 여기가 아래팔 자체만 보면 곡선 없는 셰이프가 생겨 걷힌 동안 팔이 줄어든 채 남는다(감사 0.1.3 C1)
        /// </summary>
        internal static bool[] ForearmSlots(Transform[] bones, Transform lower, Transform hand, Dictionary<Transform, Transform> merge, out bool any)
        {
            var on = new bool[bones.Length];
            any = false;
            if (lower == null) return on;
            var pairs = new Cache { boneMap = merge };
            for (int k = 0; k < bones.Length; k++) any |= on[k] = bones[k] != null && pairs.Map(bones[k]) == lower;
            if (any) return on;
            for (int k = 0; k < bones.Length; k++)
            {
                var b = bones[k];
                if (b == null || !IsUnder(b, lower, merge)) continue;
                if (hand != null && (pairs.Map(b) == hand || IsUnder(b, hand, merge))) continue;
                any |= on[k] = true;
            }
            return on;
        }

        /// <summary>
        /// 실패 진단(빌드 로그용, 영어) — 그쪽 위팔 밑에 웨이트가 실린 메시 본 전부: 실제 본 경로, 계산에서 본 본, Merge 짝·MA 앞에 있던 본인지,
        /// 웨이트 합·정점 수 — 웨이트 큰 순 16개까지. 위팔 밑에 하나도 없으면 메시 전체에서 16개. 문의 10-75 — 사용자 옷 구조를 로그 한 번으로 알기 위해
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
