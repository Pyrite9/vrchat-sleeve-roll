using System.IO;
using System.Linq;
using System.Text;
using nadena.dev.ndmf;
using UnityEngine;

[assembly: ExportsPlugin(typeof(Pyrite9.Sleeve.SleeveRollPlugin))]

namespace Pyrite9.Sleeve
{
    /// <summary>
    /// 빌드 시 SleeveRoll마다: 대상 옷 메시 사본에 SleeveUp_L/R을 계산해 넣고(SleeveRollShape), 걷히는 정점의 손 웨이트를
    /// 손목 대리 본 SleeveRoll_Wrist_L/R(아래팔 자식, 손 회전을 따르는 VRC Rotation Constraint)으로 옮긴다(D6).
    /// MA 뒤(의상 아마추어가 합쳐진 다음 — 옷 본이 아바타 본이어야 한다), AAO 앞(블렌드셰이프·본 정리 전).
    /// 구동(FX·메뉴·대리 본 오브젝트)은 MA 앞 Generating에서 만들어 MA가 병합·경로 추적·메뉴 설치를 맡는다 — 셰이프는 그 뒤에 생기지만 이름이 정해져 있다.
    /// 빌드 로그는 Logs/Pyrite9_SleeveRoll_build.txt, 인스펙터 '마지막 빌드'는 SrBuildRecord.
    /// 파일: 이 파일(패스·로그·기록) · SleeveRollDriver(FX·메뉴·대리 본) · SleeveRollBake(셰이프·웨이트·되살리기) · SleeveRollShape(계산)
    /// </summary>
    public class SleeveRollPlugin : Plugin<SleeveRollPlugin>
    {
        public override string QualifiedName => "pyrite9.sleeve-roll";
        public override string DisplayName => "Pyrite9 Sleeve Roll";

        public const string ProxyLeft = "SleeveRoll_Wrist_L", ProxyRight = "SleeveRoll_Wrist_R";
        /// <summary>메뉴 '걷어 두기' — 동기화 Bool. 이름을 바꾸지 않아(MA internal 아님) 컴포넌트가 여럿이어도 하나</summary>
        public const string HoldParameter = "Pyrite9/SleeveRoll/Hold";

        protected override void Configure()
        {
            // MA가 Shape Changer를 처리하면 컴포넌트가 사라지므로 Delete 모드는 MA 앞에서 미리 본다
            InPhase(BuildPhase.Generating)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Scan shape changers", ctx => SleeveRollDriver.ScanDelete(ctx, ctx.GetState<BuildState>()))
                .Then.Run("Generate sleeve roll animator", ctx => SleeveRollDriver.Generate(ctx, ctx.GetState<BuildState>()));
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .BeforePlugin("com.anatawa12.avatar-optimizer")
                .Run("Build sleeve roll shapes", Process)
                .PreviewingWith(new SleeveRollPreview());
        }

        static void Process(BuildContext ctx)
        {
            var comps = ctx.AvatarRootObject.GetComponentsInChildren<SleeveRoll>(true);
            if (comps.Length == 0) return;
            var state = ctx.GetState<BuildState>();
            var log = new StringBuilder();
            log.AppendLine($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] Pyrite9 Sleeve Roll v{SleeveRollConst.Version} — {ctx.AvatarRootObject.name}");
            log.Append(state.log);
            var rec = new SrBuildRecord.Entry
            {
                avatar = ctx.AvatarRootObject.name,
                time = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                version = SleeveRollConst.Version,
                menu = state.menu,
                syncedBits = state.syncedBits,
                fxLayers = state.fxLayers,
            };
            try
            {
                var animator = ctx.AvatarRootObject.GetComponent<Animator>();
                if (animator == null || !animator.isHuman) log.AppendLine("WARN Not a humanoid avatar — Sleeve Roll not built");
                else
                {
                    rec.built = true;
                    var pb = new SleeveRollBake(ctx.AvatarRootObject, animator, log, rec, state.deleteTargets).Run(comps);
                    if (pb != null && pb.Chains.Count > 0) SleeveRollPbFx.Add(ctx, pb.Chains, log);
                }
            }
            catch (System.Exception e)
            {
                log.AppendLine("EXCEPTION " + e);
                Debug.LogException(e);
            }
            // 설정 컴포넌트는 빌드 사본에서 뺀다 — 남으면 AAO가 "알 수 없는 컴포넌트"로 경고
            foreach (var c in comps)
                if (c != null) Object.DestroyImmediate(c);
            Report(log.ToString(), rec);
        }

        // 로그 글의 WARN 줄 → 빌드 기록, 로그 파일, 콘솔
        static void Report(string text, SrBuildRecord.Entry rec)
        {
            var warns = text.Split('\n').Where(l => l.StartsWith("WARN ")).Select(l => l.Substring(5).TrimEnd('\r')).ToList();
            rec.warnings.AddRange(warns);
            SrBuildRecord.Put(rec);
            WriteLog(text);
            Debug.Log("[Pyrite9 Sleeve Roll] " + text.TrimEnd());
            foreach (var w in warns) Debug.LogWarning("[Pyrite9 Sleeve Roll] " + w);
        }

        static void WriteLog(string text)
        {
            try
            {
                Directory.CreateDirectory("Logs");
                var fi = new FileInfo(SleeveRollConst.LogPath);
                if (fi.Exists && fi.Length > SleeveRollConst.LogLimit)
                {
                    File.Copy(fi.FullName, fi.FullName + ".old", true);
                    File.WriteAllText(fi.FullName, text);
                }
                else File.AppendAllText(fi.FullName, text);
            }
            // 읽기 전용 Logs 폴더면 UnauthorizedAccessException — 로그를 못 써도 빌드는 그대로
            catch (System.Exception e) when (e is IOException || e is System.UnauthorizedAccessException) { Debug.LogWarning("[Pyrite9 Sleeve Roll] build log not written: " + e.Message); }
        }
    }

    /// <summary>빌드 한 번 동안 Generating(MA 앞)에서 Transforming(MA 뒤)으로 넘기는 것 — NDMF가 빌드마다 새로 만든다</summary>
    sealed class BuildState
    {
        public readonly StringBuilder log = new StringBuilder();
        public string menu = "";     // 메뉴 토글 이름, 없으면 빈 값
        public int fxLayers;
        public int syncedBits;       // MA Parameters를 넣었으면 1 — 양쪽 다 꺼도 넣는다
        public readonly System.Collections.Generic.HashSet<string> deleteTargets = new System.Collections.Generic.HashSet<string>();   // MA Shape Changer Delete 모드 대상 렌더러 이름
    }
}
