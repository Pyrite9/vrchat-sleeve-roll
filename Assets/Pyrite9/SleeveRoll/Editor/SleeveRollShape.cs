using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pyrite9.Sleeve
{
    /// <summary>
    /// 소매 걷기 셰이프키 계산(D3) — 받는 사람의 옷 메시로 빌드 때 만든다. 배포물엔 메시 데이터가 없다.
    /// 팔뚝 원통 좌표(t: 0 팔꿈치 ~ 1 손목, r: 축 거리, θ)에서 소매를 [t0, 소매 끝] → [t0, tTarget]로 압축하고,
    /// 반지름은 새 위치의 피부 + 여유 + 원래 겹 두께 + 남은 헐렁함 × snug + 주름 물결로 둔다(sleeve-roll.md 2절).
    /// 파일: 이 파일(입구·걷기) · .Skin(피부 반지름) · .Layers(겹 두께) · .Smooth(걷힌 반지름 다듬기) · .Normals(노멀 변화) · .Hide(옷이 줄인 몸) · .Bind(손목 대리 본 웨이트) · .Mesh(팔뚝 좌표계·읽기 캐시)
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
            /// <summary>정규화가 움직인 소매만 채운다(빌드 로그 한 줄)</summary>
            public string note;
            /// <summary>정점마다 걷히는 정도(0~1) — 손목 대리 본으로 웨이트를 옮길 정점 고르기</summary>
            public float[] roll;
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
                        mesh.AddBlendShapeFrame(name, 100f, delta, NormalDeltas(nb, delta), null);
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
            // 소매가 피부보다 가늘면 대상을 잘못 고른 것(몸 메시를 옷으로) — 걷으면 몸이 부풀어 오른다
            if (rep.clothRadiusMm < rep.skinRadiusMm)
            {
                rep.message = $"the sleeve ({rep.clothRadiusMm:F1} mm) is thinner than the skin ({rep.skinRadiusMm:F1} mm) — the picked mesh looks like a body, not a garment";
                return null;
            }
            if (rep.skinFrom + SkinOverlap > 0.3f && rep.rolledTo > 0f)
                rep.message = $"forearm skin starts at t={rep.skinFrom:F2} — rolled only to there (t={rep.rolledTo:F2})";
            rep.ok = true;
            return delta;
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
            var wc = ce.Weights(f.lower, null, true);
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
                even = 1f - (1f - kComp) * EaseSlopePeak < MinEaseSlope && kEven > 0f;
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
            var cuffNormals = cuff != null ? ce.mesh.normals : null;
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
            var wu = ce.Weights(f.upper, null, true);
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
                var verts = ce.Vertices;
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
