using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pyrite9.Sleeve
{
    /// <summary>
    /// 소매 걷기 셰이프키 계산(D3) — 받는 사람의 옷 메시로 빌드 때 만든다. 배포물엔 메시 데이터가 없다.
    /// 팔뚝 원통 좌표(t: 0 팔꿈치 ~ 1 손목, r: 축 거리, θ)에서 소매를 [t0, 소매 끝] → [t0, tTarget]로 압축하고,
    /// 반지름은 새 위치의 피부 + 여유 + 원래 겹 두께 + 남은 헐렁함 × snug + 주름 물결로 둔다(sleeve-roll.md 2절).
    /// 파일: 이 파일(입구·걷기) · .Norm(처진 소매 정규화 — 움직인 소매는 이 경로만) · .Skin(피부 반지름) · .Layers(겹 두께) · .Smooth(걷힌 반지름 다듬기) · .Normals(노멀 변화) · .Hide(옷이 줄인 몸) · .Bind(손목 대리 본 웨이트) · .Mesh(팔뚝 좌표계·읽기 캐시)
    /// </summary>
    public static partial class SleeveRollShape
    {
        public const string ShapeLeft = "SleeveUp_L", ShapeRight = "SleeveUp_R";

        [Serializable]
        public struct Settings
        {
            public float tTarget, t0, easeLength;        // 팔뚝 길이 비율. easeLength = 안 움직이는 데서 다 걷히는 데까지(짧으면 헐렁한 소매가 걷힌 띠로 뚝 꺾인다 — Milfy, 10-16)
            public float margin, thickMax, foldLength, foldAmplitude; // m
            public float snug;                           // 남길 헐렁함 비율
            public float weightLow, weightHigh;          // 소매로 볼 아래팔 웨이트 합(부드럽게 섞음)

            // 빌드(SleeveRollBake.Shape)가 tTarget·t0·margin·snug·주름을 컴포넌트 값(rollTo·startAt 등)으로 덮어쓴다. 여기 값은 그 기본값과 같게 둔다
            public static Settings Default => new Settings
            {
                tTarget = 0.70f, t0 = 0.25f, easeLength = 0.30f,
                margin = 0.004f, thickMax = 0.006f, foldLength = 0.045f, foldAmplitude = 0.004f,
                snug = 0.35f, weightLow = 0.2f, weightHigh = 0.8f,
            };
        }

        public struct SideReport
        {
            public bool left, ok;
            public string message;
            public int clothVertices, skinVertices, movedVertices;
            public float forearmMeters, sleeveEnd, clothRadiusMm, skinRadiusMm;
            /// <summary>손목에서 팔꿈치 쪽으로 피부 메시가 이어지는 데까지(t) — 이보다 팔꿈치 쪽은 몸이 잘려 없다. 실제로 걷은 자리 rolledTo는 이 너머로 밀린다</summary>
            public float skinFrom, rolledTo;
            /// <summary>피부가 늦게 시작해 거기까지만 걷었다(ok인 채) — 빌드·미리 보기에 WARN도 낸다(0.1.3-beta4)</summary>
            public bool skinShort;
            /// <summary>정규화가 움직인 소매만 채운다(빌드 로그 한 줄)</summary>
            public string note;
            /// <summary>정점마다 걷히는 정도(0~1) — 손목 대리 본으로 웨이트를 옮길 정점 고르기</summary>
            public float[] roll;
            /// <summary>입은 모양에 넣은 셰이프 중 이쪽 소매를 움직인 것('이름=값', 빌드 로그용) — 없으면 null(0.1.3-beta6)</summary>
            public string worn;
            /// <summary>셰이프로 접혀 피부 안에 숨은 소매 정점 수 — 걷기 계산에서 빼고 이웃을 따라 옮겼다(0.1.3-beta6)</summary>
            public int folded;
            /// <summary>그 접혀 숨은 정점(정점 수 길이, 없으면 null) — 바깥 옷이 이 옷을 피부로 잴 때 뺀다(SleeveRollBake.RolledVertices)</summary>
            public bool[] hid;
        }

        const int NT = 66, NA = 24;      // 팔뚝 방향 칸(t −0.2 ~ 2.0) × 둘레 칸
        const float TMin = -0.2f, TMax = 2.0f;
        /// <summary>피부 표본 사이 빈 틈이 팔뚝 길이의 이 비율보다 길면 몸이 잘린 것으로 본다(고리 간격이 넓은 로우폴리도 넘지 않게 넉넉히)</summary>
        const float SkinGap = 0.45f;
        /// <summary>잘린 피부 경계 너머로 소맷단을 더 내려 덮을 양(팔뚝 길이 비율)</summary>
        const float SkinOverlap = 0.03f;
        /// <summary>이 길이(팔뚝 길이 비율)까지 걷는 건 기본 걷기, 그 2배 이상이면 긴 전환을 다 쓴다(10-21)</summary>
        const float RollLengthBase = 0.30f;
        /// <summary>긴 전환이 t0보다 앞서 시작하는 양(팔뚝 길이 비율)</summary>
        const float TaperLead = 0.15f;
        // 전환 구간에서 천이 뒤로 접히는 조건(10-39). 변위를 Smooth로 섞으면 기울기 최소 = 1 - (1 - kComp) × 1.6875 — 0 밑이면 메시가 접혀 윤곽선이 선으로 보인다
        const float EaseSlopePeak = 1.6875f, MinEaseSlope = 0.05f;

        /// <summary>긴 소매 위팔 당김(10-37, 전부 팔뚝 길이 비율) — 소매 끝이 From 이하면 0, From + Span 이상이면 Max.
        /// 아래팔에만 몰아넣으면 천이 통처럼 뭉치므로 소매 전체를 어깨 쪽으로 끌어 띠를 팔꿈치 쪽으로 넓힌다.
        /// 팔꿈치 자리는 Elbow 비율만 당기고 손목 쪽으로 Ramp에 걸쳐 전부 당긴다 — 아래팔 웨이트 정점이 팔꿈치를 많이 넘어가면 굽힐 때 안쪽이 찢어진다</summary>
        const float PullFrom = 1.5f, PullSpan = 0.7f, PullMax = 0.30f, PullElbow = 0.5f, PullRamp = 0.45f;

        /// <summary>처진 소맷단(10-44, Foxy) — 소맷단이 축에서 벗어나 고리가 축을 두르지 못하면 각도를 단면 중심 기준으로 다시 편다.
        /// 칸 Bin, 정점 MinVerts 미만 칸은 건너뜀. 손목(From) 너머 단면에서 천이 축에 닿을 만큼(축과 천의 최소 거리 ÷ 단면 반지름 &lt; Touch) 벗어난 옷만 켠다 — 측정상 일반 옷은 0.37 이상, Foxy 오른쪽 0.07.
        /// 그 칸부터 어긋남 q = 단면 중심까지 거리 ÷ 단면 반지름이 Stop 밑인 칸까지 안쪽으로, RampBins 칸에 걸쳐 서서히 편다.
        /// 기울어진 소맷단의 끝 칸은 고리 일부만 잘린 조각이라 제 중심이 고리 중심이 아니다 — 반지름이 안쪽 칸의 Partial 배 밑이면 안쪽 칸 중심을 그대로 쓴다</summary>
        const float CuffBin = 0.1f, CuffFrom = 1f, CuffTouch = 0.15f, CuffStop = 0.2f, CuffPartial = 0.7f;
        const int CuffMinVerts = 6;
        const int CuffRampBins = 2;
        /// <summary>기울어진 소맷단 끝(온전한 마지막 단면 너머)을 t 방향으로 줄이는 비율 — 그대로면 걷힌 고리가 기운 채 남는다(10-46, 10-47)</summary>
        const float CuffFlat = 0.5f;
        /// <summary>긴 소매를 이 끝까지 줄여 놓고 걷는다(몸통만 줄이고 소맷단 폭은 유지) — 그대로 걷으면 소맷단이 납작한 와셔가 된다. 원래 짧은 소매의 압축 비율에 맞춘 값이고, 위팔 당김도 안 생긴다(10-47)</summary>
        const float CuffEnd = 1.45f;
        const float CuffBodyMin = 0.25f;
        /// <summary>소맷단 안쪽·바깥 벽을 가르는 노멀 띠(반지름 방향과의 내적)</summary>
        const float CuffWallBand = 0.3f;

        public const string UnhideLeft = "SleeveRoll_Unhide_L", UnhideRight = "SleeveRoll_Unhide_R";

        /// <summary>cloth 메시 사본에 SleeveUp_L·SleeveUp_R을 넣어 돌려준다. 실패한 쪽은 셰이프를 안 넣고 보고에 남긴다.</summary>
        public static Mesh Build(SkinnedMeshRenderer cloth, IList<SkinnedMeshRenderer> skins, Animator animator, Settings s, List<SideReport> reports)
            => Build(cloth, skins, null, animator, s, s, true, true, new Cache(), reports);

        /// <summary>
        /// 플러그인용. 좌우 설정이 다를 수 있다(숨김 경계까지만 걷기). wantLeft/Right가 꺼진 쪽은 계산도 셰이프도 안 넣는다(보고는 left만 채워 남긴다).
        /// rolled: 겹쳐 입은 안쪽 옷의 걷힌 정점 위치(그 옷 메시 공간, 정점 순서 그대로) — skins에 든 그 옷은 원래 정점 대신 이걸로 잰다.
        /// 바깥 옷이 안쪽 옷의 '걷힌' 소매 위에 얹힌다. 웨이트는 렌더러의 지금 것(손목 대리 본으로 옮기기 전). cache는 빌드 한 번 동안 메시 읽기를 나눠 쓴다
        /// </summary>
        internal static Mesh Build(SkinnedMeshRenderer cloth, IList<SkinnedMeshRenderer> skins, IDictionary<SkinnedMeshRenderer, Vector3[]> rolled,
            Animator animator, Settings sLeft, Settings sRight, bool wantLeft, bool wantRight, Cache cache, List<SideReport> reports)
        {
            var ce = cache.Get(cloth);
            var mesh = UnityEngine.Object.Instantiate(ce.mesh);
            mesh.name = ce.mesh.name + "_SleeveRoll";
            NormalBase nb = null;   // 좌우 셰이프가 같이 쓰는 원래 모양의 면 노멀
            foreach (bool left in SrSide.Both)
            {
                var rep = new SideReport { left = left };
                var delta = (left ? wantLeft : wantRight) ? Side(cloth, ce, skins, rolled, animator, cache, left ? sLeft : sRight, left, ref rep) : null;
                if (delta != null)
                {
                    var name = SrSide.ShapeName(left);
                    if (mesh.GetBlendShapeIndex(name) < 0)
                    {
                        if (nb == null) nb = PrepareNormals(ce);
                        var dn = NormalDeltas(nb, delta);
                        if (Finite(dn)) mesh.AddBlendShapeFrame(name, 100f, delta, dn, null);
                        else { rep.ok = false; rep.message = "the rolled shape has invalid normals (NaN) — skipped"; }
                    }
                    else { rep.ok = false; rep.message = "the garment already has a shape key with this name — skipped"; }   // not a success: no WARN otherwise, and the weights would move to the wrist proxy for a shape we did not make
                }
                reports?.Add(rep);
            }
            return mesh;
        }

        // 쪽 하나의 셰이프 정점 이동. 못 만들면 null + rep.message
        static Vector3[] Side(SkinnedMeshRenderer cloth, Cache.Entry ce, IList<SkinnedMeshRenderer> skins, IDictionary<SkinnedMeshRenderer, Vector3[]> rolled,
            Animator animator, Cache cache, Settings s, bool left, ref SideReport rep)
        {
            var err = GetForearm(animator, left, ce.bones, ce.bindposes, out var f, ce.boneMap);
            if (err != null) { rep.message = err; rep.note = Diagnose(animator, left, cache, ce); return null; }
            rep.forearmMeters = f.L * f.mPerUnit;
            // 걷힌 뒤 아래팔에 고정될 본(따라가기 그림자 대상)의 웨이트는 아래팔 웨이트로 — 그 정점도 다른 정점만큼 걷힌다(0.1.3-beta5)
            ce.RollWeights(f.lower, f.upper, cache.Moving(animator), out f.wc, out f.wu);
            // 아래팔 본이 메시에 없어 자손 본에서 틀을 유도한 옷 — 로그에 남긴다(정규화 줄이 생기면 그 앞에 붙는다)
            // 소매 일부가 다른 본(위팔 밑 소매 본 등)에 묶여 일부만 걷혀도 성공으로 나올 수 있다 — 진단을 늘 같이 남긴다
            if (Array.IndexOf(ce.bones, f.lower) < 0)
                rep.note = "forearm bone not in the mesh — frame derived from its child bone\n    " + Diagnose(animator, left, cache, ce);

            var R = SampleSkin(cloth, skins, rolled, cache, f, out var occupied, ref rep);
            if (rep.skinVertices == 0) { rep.message = "no skin (body) mesh found on the forearm"; return null; }
            rep.skinFrom = SkinStart(occupied);
            if (rep.skinFrom + SkinOverlap > s.tTarget)
            {
                s.tTarget = Mathf.Min(1f, rep.skinFrom + SkinOverlap);
                s.t0 = Mathf.Min(s.t0, s.tTarget - 0.05f);
            }
            rep.rolledTo = s.tTarget;
            SmoothSkin(R);

            // 입은 모양(0.1.3-beta6): 켜진 셰이프가 피부 안으로 접어 넣은 소매 정점은 걷기 계산에서 뺀다(소매 웨이트 0) — 걷은 뒤 이웃을 따라 숨은 채로 옮긴다
            rep.worn = WornNote(ce, f);
            var folded = Folded(ce, f, R, out rep.folded);
            if (folded != null)
            {
                var wc = (float[])f.wc.Clone();
                for (int i = 0; i < wc.Length; i++) if (folded[i]) wc[i] = 0f;
                f.wc = wc;
                f.hid = folded;
                rep.hid = folded;
            }

            var sleeve = ReadSleeve(ce, f, s, ref rep);
            if (sleeve == null) { rep.message = "no sleeve vertices on the forearm"; rep.note = Diagnose(animator, left, cache, ce); return null; }
            if (sleeve.end <= s.tTarget + 0.02f)
            {
                rep.message = rep.skinFrom > 0.3f
                    ? $"the body mesh has forearm skin only from t={rep.skinFrom:F2} (cut away under the outfit) — nothing to roll onto; use a body mesh with the whole forearm"
                    : "the sleeve is already shorter than Roll To";
                return null;
            }

            // 정규화: 이동이 정확히 0이면 기존 경로 그대로. 움직인 소매는 정규화 경로만 탄다(SleeveRollShape.Norm.cs)
            var delta = Norm6.TryRoll(ce, f, R, s, ref rep, out var normDelta) ? normDelta : RollDeltas(ce, f, R, sleeve, s, ref rep);
            if (delta == null) return null;
            if (folded != null) FollowFolded(ce, f, R, folded, delta, rep.roll);
            // 마지막 안전망(읽기만 — 계산은 안 바꾼다): 깨진 이동이 셰이프로 들어가면 인게임에서 메시가 터진다. 그쪽만 실패로
            var broken = Broken(ce.Vertices, delta, f);
            if (broken != null) { rep.message = broken; return null; }
            // 소매가 피부보다 가늘면 대상을 잘못 고른 것(몸 메시를 옷으로) — 걷으면 몸이 부풀어 오른다
            if (rep.clothRadiusMm < rep.skinRadiusMm)
            {
                rep.message = $"the sleeve ({rep.clothRadiusMm:F1} mm) is thinner than the skin ({rep.skinRadiusMm:F1} mm) — the picked mesh looks like a body, not a garment";
                return null;
            }
            if (rep.skinFrom + SkinOverlap > 0.3f && rep.rolledTo > 0f)
            {
                rep.message = $"forearm skin starts at t={rep.skinFrom:F2} — rolled only to there (t={rep.rolledTo:F2})";
                rep.skinShort = true;
            }
            rep.ok = true;
            return delta;
        }

        /// <summary>
        /// 입은 모양에 넣은 셰이프 중 이쪽 소매(아래팔 웨이트 정점)를 움직인 것 — '이름=값'을 쉼표로(빌드 로그). 없으면 null(0.1.3-beta6)
        /// </summary>
        static string WornNote(Cache.Entry ce, in Forearm f)
        {
            var worn = ce.Worn;
            if (worn.Count == 0) return null;
            var mesh = ce.mesh;
            int n = mesh.vertexCount;
            var dv = new Vector3[n]; var fa = new Vector3[n]; var fb = new Vector3[n];
            float eps = f.L * FoldedMove;
            var names = new List<string>();
            foreach (var (index, name, weight) in worn)
            {
                if (!FrameDelta(mesh, index, weight, dv, fa, fb)) continue;
                for (int i = 0; i < n; i++)
                    if (f.wc[i] > 0.01f && dv[i].magnitude >= eps) { names.Add(name + "=" + weight.ToString("0")); break; }
            }
            return names.Count > 0 ? string.Join(", ", names) : null;
        }

        /// <summary>
        /// pick이 고른 셰이프 중 소매(어느 쪽이든 아래팔 서브트리 웨이트 정점)를 움직이는 것의 (이름, 지금 값) — 값이 0이어도 마지막 프레임으로 잰다.
        /// 애니메이션이 바꾸는 셰이프 경고용(0.1.3-beta6). Sleeve Roll 자신의 셰이프는 뺀다
        /// </summary>
        internal static List<(string name, float weight)> SleeveShapes(Cache.Entry ce, Animator animator, Func<string, bool> pick)
        {
            var names = new List<(string name, float weight)>();
            var mesh = ce.mesh;
            int n = mesh.vertexCount;
            Vector3[] dv = null, fa = null, fb = null;
            float[][] w = new float[2][]; float[] eps = new float[2];
            foreach (bool left in SrSide.Both)
                if (GetForearm(animator, left, ce.bones, ce.bindposes, out var f, ce.boneMap) == null)
                { w[left ? 0 : 1] = ce.Weights(f.lower, null, true); eps[left ? 0 : 1] = f.L * FoldedMove; }
            if (w[0] == null && w[1] == null) return names;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                var name = mesh.GetBlendShapeName(i);
                if (OwnShape(name) || !pick(name)) continue;
                int fc = mesh.GetBlendShapeFrameCount(i);
                if (fc == 0) continue;
                if (dv == null) { dv = new Vector3[n]; fa = new Vector3[n]; fb = new Vector3[n]; }
                if (!FrameDelta(mesh, i, mesh.GetBlendShapeFrameWeight(i, fc - 1), dv, fa, fb)) continue;
                bool moves = false;
                for (int s = 0; s < 2 && !moves; s++)
                {
                    if (w[s] == null) continue;
                    for (int v = 0; v < n && !moves; v++) moves = w[s][v] > 0.01f && dv[v].magnitude >= eps[s];
                }
                if (moves) names.Add((name, ce.shapes.GetBlendShapeWeight(i)));
            }
            return names;
        }

        /// <summary>셰이프가 이만큼(팔뚝 길이 비율) 넘게 옮긴 정점만 셰이프가 움직인 것으로 본다 — 몸 숨김(Hide)과 같은 문턱</summary>
        const float FoldedMove = 0.002f;

        /// <summary>피부 반지름의 이 비율보다 축에 가까이 넣은 정점은 원래 자리와 상관없이 접혀 숨은 것으로 본다(몸에 붙는 소매를 접어 넣은 경우)</summary>
        const float FoldedDeep = 0.5f;

        /// <summary>
        /// 셰이프로 접혀 숨은 소매 정점(0.1.3-beta6, 10-92): 소매 멤버 중 켜진 셰이프가 옮겼고(FoldedMove 넘게) 입은 자리가 피부 안인 것 —
        /// 기본 모양에선 피부 밖이었거나(접어 넣음), 피부 반지름의 절반(FoldedDeep)보다 깊은 곳에 있거나. 벤더 '겉옷용' 축소 셰이프가 소매 원통을 한 점·가는 관으로 모아 숨긴 경우 —
        /// 그대로 걷으면 피부 밖으로 밀려 판·띠·지느러미가 된다. 피부는 다듬은 격자(R). 피부 밖에 남은 정점(두께 셰이프)이나 원래 피부 안이던 정점을 조금 조인 셰이프는 전과 같이 걷는다. 없으면 null
        /// </summary>
        static bool[] Folded(Cache.Entry ce, in Forearm f, float[,] R, out int count)
        {
            count = 0;
            var v = ce.Vertices; var b = ce.BaseVertices;
            if (ReferenceEquals(v, b)) return null;
            float eps = f.L * FoldedMove;
            bool[] hid = null;
            for (int i = 0; i < v.Length; i++)
            {
                if (f.wc[i] <= 0.01f || (v[i] - b[i]).magnitude < eps) continue;
                f.Cyl(v[i], out var t, out var r, out var a);
                float s = SkinAt(R, t, a);
                if (r >= s) continue;
                f.Cyl(b[i], out var tb, out var rb, out var ab);
                if (r >= s * FoldedDeep && rb < SkinAt(R, tb, ab)) continue;
                if (hid == null) hid = new bool[v.Length];
                hid[i] = true;
                count++;
            }
            return hid;
        }

        /// <summary>
        /// 접혀 숨은 정점을 숨은 채로 옮긴다: 메시 연결(기본 정점으로 묶은 UV 이음새)을 따라, 이동이 정해진 이웃에게서 한 겹씩 받는다 —
        /// 축 방향 이동(평균)과 축 거리 비율(이웃 걷은 반지름 합 ÷ 입은 반지름 합)만. 이동 벡터를 그대로 받으면 축 가까이 모인 정점이 이웃의 바깥 이동만큼 피부 밖으로 나온다(maid 커프 지느러미).
        /// 그리고 입은 자리의 피부 대비 깊이(r ÷ 피부)를 넘지 않게 — 숨은 정점은 걷은 뒤에도 피부 안. 걷힘 정도도 받아 손목 대리 본으로 같이 옮겨진다
        /// </summary>
        static void FollowFolded(Cache.Entry ce, in Forearm f, float[,] R, bool[] hid, Vector3[] delta, float[] roll)
        {
            var key = WeldKeys(ce.BaseVertices, null, out int nk);
            var worn = ce.Vertices;
            var hk = new bool[nk];
            for (int i = 0; i < hid.Length; i++) if (hid[i]) hk[key[i]] = true;
            var kd = new Vector3[nk]; var kp = new Vector3[nk]; var kr = new float[nk]; var cnt = new int[nk];
            for (int i = 0; i < key.Length; i++)
            {
                int k = key[i];
                if (cnt[k] == 0 && hk[k]) kp[k] = worn[i];
                if (hk[k]) { cnt[k] = -1; continue; }
                kd[k] += delta[i]; kp[k] += worn[i]; kr[k] += roll != null && i < roll.Length ? roll[i] : 0f; cnt[k]++;
            }
            // 키마다 축 방향 이동(ks), 입은·걷은 축 거리(kw, kn) — 숨은 키는 받은 비율로 kn = kw × 비율
            var ks = new float[nk]; var kw = new float[nk]; var kn = new float[nk];
            var known = new bool[nk];
            for (int k = 0; k < nk; k++)
            {
                if (cnt[k] <= 0) continue;
                known[k] = true; kd[k] /= cnt[k]; kp[k] /= cnt[k]; kr[k] /= cnt[k];
                ks[k] = Vector3.Dot(kd[k], f.ax);
                f.Cyl(kp[k], out _, out kw[k], out _);
                f.Cyl(kp[k] + kd[k], out _, out kn[k], out _);
            }
            var nb = new List<int>[nk];
            var tris = ce.Triangles;
            for (int i = 0; i + 2 < tris.Length; i += 3)
                for (int e = 0; e < 3; e++)
                {
                    int p = key[tris[i + e]], q = key[tris[i + (e + 1) % 3]];
                    if (p == q || !(hk[p] || hk[q])) continue;
                    if (hk[p]) { if (nb[p] == null) nb[p] = new List<int>(6); if (!nb[p].Contains(q)) nb[p].Add(q); }
                    if (hk[q]) { if (nb[q] == null) nb[q] = new List<int>(6); if (!nb[q].Contains(p)) nb[q].Add(p); }
                }
            var layer = new List<int>();
            var ls = new List<float>(); var lq = new List<float>(); var lr = new List<float>();
            while (true)
            {
                layer.Clear(); ls.Clear(); lq.Clear(); lr.Clear();
                for (int k = 0; k < nk; k++)
                {
                    if (!hk[k] || known[k] || nb[k] == null) continue;
                    float ss = 0f, sw = 0f, sn = 0f, sr = 0f; int c = 0;
                    foreach (int q in nb[k]) if (known[q]) { ss += ks[q]; sw += kw[q]; sn += kn[q]; sr += kr[q]; c++; }
                    if (c == 0) continue;
                    layer.Add(k); ls.Add(ss / c); lq.Add(sw > 1e-6f ? sn / sw : 1f); lr.Add(sr / c);
                }
                if (layer.Count == 0) break;
                for (int j = 0; j < layer.Count; j++)
                {
                    int k = layer[j]; known[k] = true; ks[k] = ls[j]; kr[k] = lr[j];
                    f.Cyl(kp[k], out _, out kw[k], out _);
                    kn[k] = kw[k] * lq[j];
                }
            }
            for (int i = 0; i < key.Length; i++)
            {
                int k = key[i];
                if (!hk[k] || !known[k]) continue;
                // 입은 자리 p = 축 위 점 + 축에서 떨어진 o. 걷은 자리 = 축 방향으로 ks, o는 비율만큼 — 피부 대비 깊이 이하로
                var p = worn[i];
                var d = p - f.e;
                var o = d - f.ax * Vector3.Dot(d, f.ax);
                float r = o.magnitude;
                float scale = kw[k] > 1e-6f ? kn[k] / kw[k] : 1f;
                var np = p + f.ax * ks[k] + o * (scale - 1f);
                if (r > 1e-6f)
                {
                    f.Cyl(p, out var t0, out _, out var a0);
                    f.Cyl(np, out var t1, out var r1, out var a1);
                    float s0 = SkinAt(R, t0, a0), s1 = SkinAt(R, t1, a1);
                    if (s0 > 1e-6f)
                    {
                        float cap = r / s0 * s1;
                        if (r1 > cap) np -= o / r * (r1 - cap);
                    }
                }
                delta[i] = np - p;
                if (roll != null && i < roll.Length) roll[i] = kr[k];
            }
        }

        /// <summary>
        /// 걷은 모양이 깨졌나 — NaN·Inf, 또는 정점이 팔꿈치에서 원래보다 팔뚝 길이 넘게 멀어진다. 걷기는 천을 팔 쪽으로 모으는 이동이라
        /// (가장 큰 반대 방향 이동인 위팔 당김도 팔뚝 길이의 0.3) 정상 옷은 근처에도 안 온다. 기준은 옷 치수가 아니라 팔뚝 길이. 깨졌으면 이유(영어), 아니면 null
        /// </summary>
        static string Broken(Vector3[] verts, Vector3[] delta, in Forearm f)
        {
            for (int i = 0; i < delta.Length && i < verts.Length; i++)
            {
                var d = delta[i];
                if (d.x == 0f && d.y == 0f && d.z == 0f) continue;
                if (float.IsNaN(d.x) || float.IsNaN(d.y) || float.IsNaN(d.z) || float.IsInfinity(d.x) || float.IsInfinity(d.y) || float.IsInfinity(d.z))
                    return "the rolled shape has invalid numbers (NaN) — skipped";
                if ((verts[i] + d - f.e).magnitude > (verts[i] - f.e).magnitude + f.L)
                    return "the rolled shape throws vertices off the arm — skipped";
            }
            return null;
        }

        static bool Finite(Vector3[] a)
        {
            if (a == null) return true;
            foreach (var d in a)
                if (float.IsNaN(d.x) || float.IsNaN(d.y) || float.IsNaN(d.z) || float.IsInfinity(d.x) || float.IsInfinity(d.y) || float.IsInfinity(d.z)) return false;
            return true;
        }

        /// <summary>소매 정점들의 원통 좌표와 섞는 정도</summary>
        sealed class Sleeve
        {
            public int[] idx;                // 메시 정점 번호
            public float[] T, R, A, F;       // t, 축 거리, 각도, 웨이트로 부드럽게 섞는 정도(k번째 = idx[k])
            public float end;                // 소매 끝 t
            public bool[] member;            // 메시 정점마다 소매인가
            public int[] key;                // 메시 정점마다 같은 위치 묶음 번호(소매 밖은 −1) — 겹 두께·다듬기가 같이 쓴다
            public int welded;               // 묶음 수
        }

        // 아래팔(과 그 자손) 웨이트가 있는 정점. 없으면 null. 소매 끝·반지름 중앙값은 rep에
        static Sleeve ReadSleeve(Cache.Entry ce, in Forearm f, in Settings s, ref SideReport rep)
        {
            var verts = ce.Vertices;
            var wc = f.wc ?? ce.Weights(f.lower, null, true);
            var member = new bool[verts.Length];
            int n = 0;
            for (int i = 0; i < verts.Length; i++) if (wc[i] > 0.01f) { member[i] = true; n++; }
            rep.clothVertices = n;
            if (n == 0) return null;
            var idx = new int[n];
            for (int i = 0, k = 0; i < verts.Length; i++) if (member[i]) idx[k++] = i;
            var sl = new Sleeve { idx = idx, member = member, T = new float[n], R = new float[n], A = new float[n], F = new float[n], end = float.MinValue };
            var rList = new List<float>();
            for (int k = 0; k < n; k++)
            {
                f.Cyl(verts[idx[k]], out sl.T[k], out sl.R[k], out sl.A[k]);
                sl.F[k] = Smooth((wc[idx[k]] - s.weightLow) / (s.weightHigh - s.weightLow));
                if (sl.F[k] > 0.5f) { sl.end = Mathf.Max(sl.end, sl.T[k]); rList.Add(sl.R[k]); }
            }
            rep.sleeveEnd = sl.end;
            if (rList.Count > 0) { rList.Sort(); rep.clothRadiusMm = rList[rList.Count / 2] * f.mPerUnit * 1000f; }
            return sl;
        }

        /// <summary>소매 끝이 정하는 걷기 사상 — 쪽 하나에 한 번 구한다</summary>
        readonly struct RollMap
        {
            readonly float t0, ease, kComp, kEven;
            readonly bool even;
            /// <summary>위팔 당김(10-37). 소맷단은 [t0, tT]로 압축한 뒤 당겨서 tTarget에 온다</summary>
            public readonly float pull, tT;
            /// <summary>많이 걷는 옷의 반지름 전환(10-21) — 0 = 기본 걷기, 전환은 radialStart에서 시작</summary>
            public readonly float taper, radialStart;
            /// <summary>전환 구간이 뒤로 접히지 않게 기울기를 섞는가(10-39)</summary>
            public bool Even => even;

            public RollMap(float sleeveEnd, bool hasUpper, in Settings s)
            {
                t0 = s.t0; ease = s.easeLength;
                pull = hasUpper ? PullMax * Mathf.Clamp01((sleeveEnd - PullFrom) / PullSpan) : 0f;
                tT = s.tTarget + pull;
                kComp = (tT - t0) / (sleeveEnd - t0);
                // 많이 걷는 옷(Salt 0.86)은 좁아진 띠가 길어 뒷부분과 턱이 진다 — 반지름을 걷은 뒤 자리 기준으로, 팔꿈치 쪽 조금 앞에서 소맷단까지 줄인다(10-21).
                // 원래 자리 기준이면 압축으로 전환 구간이 도로 짧아진다. 기본 걷기(걷는 길이 0.30 이하)는 taper 0 — easeLength 그대로
                taper = Mathf.Clamp01((tT - t0) / RollLengthBase - 1f);
                radialStart = t0 - TaperLead * taper;
                // 많이 압축하는 옷은 기울기를 Smooth로 섞어 접히지 않게 — 전환 구간이 길어지는 만큼 나머지를 조금 더 압축해 소맷단은 같은 자리에 온다
                kEven = (tT - t0 - ease * 0.5f) / (sleeveEnd - t0 - ease * 0.5f);
                // 분자·분모가 둘 다 음수여도 kEven > 0이다(움직임 시작이 소맷단 자리에 붙고 소매 끝이 가까울 때) — 그 사상은 소매를 손 쪽으로 늘인다.
                // 분모가 양수일 때만 쓴다(그때는 kEven > 0 ⇔ 분자 > 0, 전과 같다). 아니면 선형 압축(kComp)
                even = 1f - (1f - kComp) * EaseSlopePeak < MinEaseSlope && kEven > 0f && sleeveEnd - t0 - ease * 0.5f > 0f;
            }

            /// <summary>웨이트를 빼고 다 걷혔을 때의 자리</summary>
            public float Full(float t) => t <= t0 ? t : even ? EvenRoll(t, t0, ease, kEven) : t0 + (t - t0) * kComp;

            /// <summary>전환 구간이 끝나는 자리(걷은 뒤)</summary>
            public float EaseEnd => even ? EvenRoll(t0 + ease, t0, ease, kEven) : t0 + ease * kComp;
        }

        /// <summary>정점마다 걷은 결과(k번째 = sleeve.idx[k]) — 다듬기가 고친 뒤 메시 공간으로 옮긴다</summary>
        sealed class Roll
        {
            public float[] radii;    // 안 움직이는 정점은 원래 반지름 — 다듬을 때 이웃으로 쓴다
            public float[] t2s;      // 걷은 뒤 자리
            public float[] weight;   // 걷히는 정도
            public float[] floors;   // 다듬기가 넘지 않을 바닥(피부 + 여유)
            public bool[] touched;
        }

        // 소매를 [t0, 소매 끝] → [t0, tTarget]로 압축하고 반지름을 피부 + 여유 + 겹 두께 + 남은 헐렁함 + 주름으로. rep.roll·movedVertices·skinRadiusMm을 채운다
        static Vector3[] RollDeltas(Cache.Entry ce, in Forearm f, float[,] R, Sleeve sl, in Settings s, ref SideReport rep)
        {
            var verts = ce.Vertices;
            var idx = sl.idx;
            int n = idx.Length;
            float L = f.L, mPerUnit = f.mPerUnit;
            float margin = s.margin / mPerUnit, thickMax = s.thickMax / mPerUnit;
            sl.key = WeldKeys(verts, sl.member, out sl.welded);
            // 겹(안감·소맷단 접힘) 두께 — 정점에서 축 쪽으로 곧게 들어가 thickMax 안에서 만나는 옷 면 중 가장 깊은 데까지.
            // 칸(t, θ) 안 최소 반지름으로 재던 옛 방식은 주름 마루·골을 겹으로 읽어, 압축된 소맷단에 주름 높이가 그대로 남아 삐죽 튀어나왔다(10-15)
            var layer = Layers(ce, sl, f, thickMax, 0.0002f / mPerUnit);
            // 겹 두께는 실제 자리에서 축으로 재므로 그 뒤에 — 이 뒤로 sl.R·sl.A는 편 좌표, 옮긴 만큼은 맨 끝에서 (1 - wr)로 돌려준다
            var cuff = RecenterCuff(sl, s.t0, out var tight, out var flat);
            var radial = cuff != null ? new float[n] : null;
            var map = new RollMap(sl.end, f.upper != null, s);
            // 편 구간은 정점마다 헐렁함이 달라(납작한 단면) 그대로 줄이면 모서리 진 고리가 된다 — 고리 하나의 평균으로 둥글게
            float ringLoose = 0f;
            if (cuff != null)
            {
                float sum = 0f;
                int count = 0;
                for (int k = 0; k < n; k++)
                {
                    if (tight[k] < 0.5f || sl.F[k] <= 0.5f) continue;
                    sum += Mathf.Max(sl.R[k] - SkinAt(R, map.Full(sl.T[k]) - map.pull, sl.A[k]) - margin - layer[k], 0f);
                    count++;
                }
                if (count > 0) ringLoose = sum / count;
            }
            var cuffNormals = cuff != null ? ce.Normals : null;   // 입은 모양의 노멀(0.1.3-beta6)
            float foldL = s.foldLength / mPerUnit, foldA = s.foldAmplitude / mPerUnit;
            var delta = new Vector3[verts.Length];
            rep.roll = new float[verts.Length];
            var skinList = new List<float>();
            var roll = new Roll { radii = (float[])sl.R.Clone(), t2s = new float[n], weight = new float[n], floors = new float[n], touched = new bool[n] };
            for (int k = 0; k < n; k++)
            {
                float t = sl.T[k], r = sl.R[k], a = sl.A[k], F = sl.F[k];
                float ease = Smooth((t - s.t0) / s.easeLength);
                float w = ease * F;
                float tFull = map.Full(t);
                float t2 = t + (tFull - t) * (map.Even ? F : w);
                float wr = Mathf.Lerp(ease, Smooth((t2 - map.radialStart) / (map.tT - map.radialStart)), map.taper) * F;
                if (w <= 0f && wr <= 0f) continue;
                if (radial != null) radial[k] = wr;
                float rskin = SkinAt(R, tFull - map.pull, a);
                float thick = layer[k];
                // 주름 물결은 걷은 뒤 자리(tFull) 기준 — 원래 자리 기준이면 압축(×kComp)으로 간격이 3~4배 좁아져 깊이는 그대로인 톱니가 됐다(10-15)
                float phase = Mathf.Max(tFull - s.t0, 0f) * L / foldL;
                float fold = foldA * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * phase)) * ease;
                float rFull;
                if (cuff != null)
                {
                    // 소맷단은 겹으로 접혀 있다 — 안쪽 벽(노멀이 축을 향함)은 피부 바로 위, 바깥 벽만 두껍게. 순서가 뒤집히면 뒷면 컬링으로 틈이 뚫려 보인다
                    float wall = 1f;
                    if (tight[k] > 0f && cuffNormals != null && cuffNormals.Length == verts.Length)
                        wall = Mathf.Lerp(1f, Mathf.InverseLerp(-CuffWallBand, CuffWallBand, Vector3.Dot(cuffNormals[idx[k]], f.u * Mathf.Cos(a) + f.v * Mathf.Sin(a))), tight[k]);
                    rFull = rskin + margin + (thick + Mathf.Lerp(Mathf.Max(r - rskin - margin - thick, 0f), ringLoose, tight[k]) * s.snug) * wall + fold;
                }
                else rFull = rskin + margin + thick + Mathf.Max(r - rskin - margin - thick, 0f) * s.snug + fold;
                float r2 = r + (rFull - r) * wr;
                roll.radii[k] = r2; roll.t2s[k] = t2; roll.weight[k] = w; roll.touched[k] = true;
                roll.floors[k] = Mathf.Min(r2, rskin + margin);
                rep.roll[idx[k]] = w;
                if (w <= 0f) continue;   // 반지름만 줄어드는 앞쪽은 걷힘 수·피부 측정에 안 넣는다
                rep.movedVertices++;
                if (F > 0.5f) skinList.Add(rskin);
            }
            if (skinList.Count > 0) { skinList.Sort(); rep.skinRadiusMm = skinList[skinList.Count / 2] * mPerUnit * 1000f; }
            SmoothRadii(ce, sl, roll);
            SmoothLips(ce, sl, roll, L, mPerUnit, map.EaseEnd + LipStartLead);
            for (int k = 0; k < n; k++)
            {
                if (!roll.touched[k]) continue;
                float a = sl.A[k];
                var p = f.e + f.ax * (roll.t2s[k] * L) + (f.u * Mathf.Cos(a) + f.v * Mathf.Sin(a)) * roll.radii[k];
                if (cuff != null) p += (f.u * cuff[k].x + f.v * cuff[k].y + f.ax * (flat[k] * L)) * (1f - radial[k]);   // 안 걷힌 만큼 원래 자리로 — wr 0이면 움직임 0
                delta[idx[k]] = p - verts[idx[k]];
            }
            if (map.pull > 0f) PullUpperArm(ce, f, delta, map.pull);
            return delta;
        }

        // 처진 소맷단의 R·A를 단면 중심 기준으로 고쳐 쓰고 정점마다 옮긴 양(u, v 성분)을 돌려준다. 해당 없으면 아무것도 안 고치고 null
        static Vector2[] RecenterCuff(Sleeve sl, float t0, out float[] tight, out float[] flat)
        {
            tight = null; flat = null;
            if (sl.end <= CuffFrom) return null;
            int first = Mathf.RoundToInt(CuffFrom / CuffBin), bins = Mathf.FloorToInt(sl.end / CuffBin) + 1;
            var lo = new Vector2[bins]; var hi = new Vector2[bins]; var count = new int[bins]; var nearest = new float[bins];
            int n = sl.idx.Length;
            for (int k = 0; k < n; k++)
            {
                if (sl.F[k] <= 0.5f || sl.T[k] < 0f) continue;
                int b = Mathf.Min(Mathf.FloorToInt(sl.T[k] / CuffBin), bins - 1);
                var p = new Vector2(sl.R[k] * Mathf.Cos(sl.A[k]), sl.R[k] * Mathf.Sin(sl.A[k]));
                lo[b] = count[b] == 0 ? p : Vector2.Min(lo[b], p);
                hi[b] = count[b] == 0 ? p : Vector2.Max(hi[b], p);
                nearest[b] = count[b] == 0 ? sl.R[k] : Mathf.Min(nearest[b], sl.R[k]);
                count[b]++;
            }
            float Radius(int b) => Mathf.Max((hi[b].x - lo[b].x + hi[b].y - lo[b].y) * 0.25f, 1e-6f);
            float Q(int b) => ((lo[b] + hi[b]) * 0.5f).magnitude / Radius(b);
            int stop = bins - 1;   // 천이 축에 닿는 칸 → 안쪽으로 어긋남이 작아지는 칸(여기까지는 안 옮긴다)
            while (stop >= first && (count[stop] < CuffMinVerts || nearest[stop] / Radius(stop) >= CuffTouch)) stop--;
            if (stop < first) return null;
            while (stop >= first && (count[stop] < CuffMinVerts || Q(stop) >= CuffStop)) stop--;

            var off = new Vector2[bins];
            var ramp = new float[bins];
            Vector2 held = Vector2.zero;
            float heldRadius = 0f, flatFrom = sl.end;
            for (int b = Mathf.Max(stop + 1, first); b < bins; b++)
            {
                if (count[b] >= CuffMinVerts && Radius(b) >= heldRadius * CuffPartial) { held = (lo[b] + hi[b]) * 0.5f; heldRadius = Radius(b); flatFrom = b * CuffBin; }
                ramp[b] = Smooth((b - stop) / (float)CuffRampBins);
                off[b] = held * ramp[b];
            }

            var moved = new Vector2[n];
            tight = new float[n];
            flat = new float[n];
            for (int k = 0; k < n; k++)
            {
                float x = sl.T[k] / CuffBin - 0.5f;
                int b0 = Mathf.FloorToInt(x);
                if (b0 < first - 1) continue;
                var o = Vector2.Lerp(off[Mathf.Min(b0, bins - 1)], off[Mathf.Min(b0 + 1, bins - 1)], x - b0);
                tight[k] = Mathf.Lerp(ramp[Mathf.Min(b0, bins - 1)], ramp[Mathf.Min(b0 + 1, bins - 1)], x - b0);
                if (o == Vector2.zero) continue;
                float px = sl.R[k] * Mathf.Cos(sl.A[k]) - o.x, py = sl.R[k] * Mathf.Sin(sl.A[k]) - o.y;
                sl.R[k] = Mathf.Sqrt(px * px + py * py);
                sl.A[k] = Mathf.Atan2(py, px);
                moved[k] = o;
            }
            float flatK = CuffFlat, endOld = float.MinValue;
            for (int k = 0; k < n; k++)
            {
                float t = sl.T[k];
                if (t > flatFrom) t = flatFrom + (t - flatFrom) * flatK;
                if (sl.F[k] > 0.5f) endOld = Mathf.Max(endOld, t);
            }
            float body = Mathf.Clamp((CuffEnd - t0 - (endOld - flatFrom)) / Mathf.Max(flatFrom - t0, 1e-4f), CuffBodyMin, 1f);
            float end = float.MinValue;
            for (int k = 0; k < n; k++)
            {
                float t = sl.T[k];
                if (t > flatFrom) t = flatFrom + (t - flatFrom) * flatK;
                if (t > t0) t = t <= flatFrom ? t0 + (t - t0) * body : t0 + (flatFrom - t0) * body + (t - flatFrom);
                flat[k] = sl.T[k] - t;
                sl.T[k] = t;
                if (sl.F[k] > 0.5f) end = Mathf.Max(end, t);
            }
            sl.end = end;
            return moved;
        }

        // 기울기가 1에서 k로 Smooth하게 바뀌는 사상 — 기울기를 적분한 꼴이라 뒤로 접히지 않는다
        static float EvenRoll(float t, float t0, float ease, float k)
        {
            float x = (t - t0) / ease;
            return x < 1f ? t0 + ease * (x + (k - 1f) * (x * x * x - 0.5f * x * x * x * x)) : t0 + ease * (1f + k) * 0.5f + (t - t0 - ease) * k;
        }

        // 소매 전체를 어깨 쪽으로 — 어깨 0 → 팔꿈치 PullElbow → 아래팔 PullRamp까지 전부. 위팔(과 자손) 웨이트만큼만 움직여 몸통은 그대로
        static void PullUpperArm(Cache.Entry ce, in Forearm f, Vector3[] delta, float pull)
        {
            var verts = ce.Vertices;
            var wu = f.wu ?? ce.Weights(f.upper, null, true);
            var up = f.e - f.shoulder;
            float LU = up.magnitude;
            if (LU < 1e-6f) return;
            up /= LU;
            for (int i = 0; i < verts.Length; i++)
            {
                if (wu[i] <= 0.01f) continue;
                float su = Vector3.Dot(verts[i] - f.shoulder, up) / LU;
                if (su <= 0f) continue;
                float tv = Vector3.Dot(verts[i] - f.e, f.ax) / f.L;
                float g = tv >= 0f ? PullElbow + (1f - PullElbow) * Smooth(tv / PullRamp) : PullElbow * Mathf.Clamp01(su);
                delta[i] -= (tv >= 0f ? f.ax : up) * (pull * f.L * g * wu[i]);
            }
        }

        /// <summary>
        /// 겹쳐 입은 옷의 안팎 순서용 — 양 팔뚝(t 0.3~0.9)에서 아래팔 웨이트가 큰 정점의 축 거리 중앙값(m). 소매가 없으면 NaN
        /// </summary>
        internal static float SleeveRadius(SkinnedMeshRenderer cloth, Animator animator, Cache cache)
        {
            if (cloth.sharedMesh == null) return float.NaN;
            var ce = cache.Get(cloth);
            var rs = new List<float>();
            foreach (bool left in SrSide.Both)
            {
                if (GetForearm(animator, left, ce.bones, ce.bindposes, out var f, ce.boneMap) != null) continue;
                var verts = ce.BaseVertices;   // 겹 순서는 옷 자체 모양으로 — 셰이프로 접어 숨긴 소매가 재기에서 빠지지 않게(0.1.3-beta6)
                // 아래팔 본 없이 자손(트위스트 등)에만 묶인 옷은 자손째로(10-75) — 아래팔이 있으면 전과 같다
                var w = ce.Weights(f.lower, null, Array.IndexOf(ce.bones, f.lower) < 0);
                for (int i = 0; i < verts.Length; i++)
                {
                    if (w[i] < 0.5f) continue;
                    f.Cyl(verts[i], out var t, out var r, out _);
                    if (t >= 0.3f && t <= 0.9f) rs.Add(r * f.mPerUnit);
                }
            }
            if (rs.Count == 0) return float.NaN;
            rs.Sort();
            return rs[rs.Count / 2];
        }
    }
}
