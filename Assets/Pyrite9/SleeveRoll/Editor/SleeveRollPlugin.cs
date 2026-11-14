using System.IO;
using System.Linq;
using System.Text;
using nadena.dev.ndmf;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

[assembly: ExportsPlugin(typeof(Pyrite9.Sleeve.SleeveRollPlugin))]

namespace Pyrite9.Sleeve
{
    /// <summary>
    /// 빌드 시 SleeveRoll마다: 대상 옷 메시 사본에 SleeveUp_L/R을 계산해 넣고(SleeveRollShape), 걷히는 정점의 손 웨이트를
    /// 손목 대리 본 SleeveRoll_Wrist_L/R(아래팔 자식, 손 회전을 따르는 VRC Rotation Constraint)으로 옮긴다(D6).
    /// MA 뒤(의상 아마추어가 합쳐진 다음 — MA가 합치지 않고 남긴 의상 본은 Snapshot의 짝으로 아바타 본처럼 계산한다), AAO 앞(블렌드셰이프·본 정리 전).
    /// 구동(FX·메뉴·대리 본 오브젝트)은 MA 앞 Generating에서 만들어 MA가 병합·경로 추적·메뉴 설치를 맡는다 — 셰이프는 그 뒤에 생기지만 이름이 정해져 있다.
    /// 빌드 로그는 Logs/Pyrite9_SleeveRoll_build.txt, 인스펙터 '마지막 빌드'는 SrBuildRecord.
    /// 파일: 이 파일(패스·로그·기록) · SleeveRollDriver(FX·메뉴·대리 본) · SleeveRollBake(셰이프·웨이트·되살리기) · SleeveRollShape(계산) ·
    /// SleeveRollPb(PhysBone 소매 판정·그림자 본) · SleeveRollPbFx(그림자 본 FX 곡선) · SleeveRollUnhideFx(옷 토글을 따르는 되돌리기) · SleeveRollPreview(미리 보기)
    /// </summary>
    public class SleeveRollPlugin : Plugin<SleeveRollPlugin>
    {
        public override string QualifiedName => "pyrite9.sleeve-roll";
        public override string DisplayName => "Pyrite9 Sleeve Roll";

        public const string ProxyLeft = "SleeveRoll_Wrist_L", ProxyRight = "SleeveRoll_Wrist_R";
        /// <summary>메뉴 토글('소매 걷기')이 모는 파라미터 — 동기화 Bool. 이름을 바꾸지 않아(MA internal 아님) 컴포넌트가 여럿이어도 하나</summary>
        public const string HoldParameter = "Pyrite9/SleeveRoll/Hold";

        protected override void Configure()
        {
            // MA가 Shape Changer를 처리하면 컴포넌트가 사라지므로 Delete 모드는 MA 앞에서 미리 본다
            InPhase(BuildPhase.Generating)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Scan shape changers", ctx => SleeveRollDriver.ScanDelete(ctx, ctx.GetState<BuildState>()))
                .Then.Run("Generate sleeve roll animator", ctx => SleeveRollDriver.Generate(ctx, ctx.GetState<BuildState>()))
                .Then.Run("Snapshot outfit bones", ctx => Snapshot(ctx, ctx.GetState<BuildState>()));
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
                    var bake = new SleeveRollBake(ctx.AvatarRootObject, animator, log, rec, state.deleteTargets, state.boneMap, state.known);
                    bake.live = SleeveRollUnhideFx.Scan(ctx.AvatarRootObject, log);
                    var pb = bake.Run(comps);
                    // FX 컨텍스트는 넣을 게 있을 때만 — 그림자도 대리 본 스케일도 손가락·따라가기 그림자도 없는 아바타는 컨트롤러가 그대로 남는다
                    if (pb != null && (pb.Chains.Count > 0 || bake.proxyScales.Count > 0 || bake.fingerShadows.Count > 0 || bake.followShadows.Count > 0))
                        SleeveRollPbFx.Add(ctx, pb.Chains, bake.proxyScales, bake.fingerShadows, bake.followShadows, log);
                    // 옷 토글이 숨김 셰이프를 움직이는 아바타만 — 되돌리기 몫이 그 셰이프의 지금 값을 따라가게(SleeveRollUnhideFx)
                    if (bake.liveUnhides.Count > 0 && SleeveRollUnhideFx.Add(ctx, bake.liveUnhides, log))
                        rec.fxLayers++;
                }
            }
            catch (System.Exception e)
            {
                log.AppendLine("EXCEPTION " + e);
                // 빌드 기록·인스펙터엔 WARN 줄만 간다 — 없으면 '경고: 없음'으로 보인다. 앞서 만든 셰이프는 남고 그 뒤 단계(대리 본·되돌리기·PhysBone 그림자)는 빠진 상태다
                log.AppendLine("WARN Sleeve Roll stopped on an internal error — the sleeves may be left half set up (see the EXCEPTION line in the Console)");
                Debug.LogException(e);
            }
            if (rec.built) CheckMenu(ctx, state, rec, log);
            // 설정 컴포넌트는 빌드 사본에서 뺀다 — 남으면 AAO가 "알 수 없는 컴포넌트"로 경고
            foreach (var c in comps)
                if (c != null) Object.DestroyImmediate(c);
            Report(log.ToString(), rec);
        }

        /// <summary>
        /// MA 앞에서 의상 본 → 아바타 본 짝과 지금 있는 Transform을 찍어 둔다. MA는 PhysBone·Contact·Constraint가 가리키는 본, Bone Proxy 대상,
        /// 옷 토글이 있는 옷의 컴포넌트 붙은 본을 합치지 않고 남기고, Scale Adjuster는 ScaleProxy를 끼운다 — 그 옷 메시는 MA 뒤에도 아바타 본이 아닌 본에 묶여 있다.
        /// 계산은 이 짝으로 그 본을 아바타 본으로 본다(미리 보기와 같은 정의, 문의 10-73)
        /// </summary>
        static void Snapshot(BuildContext ctx, BuildState state)
        {
            var root = ctx.AvatarRootObject;
            if (root.GetComponentsInChildren<SleeveRoll>(true).Length == 0) return;
            state.boneMap = SleeveRollShape.MergeMap(root.GetComponentsInChildren<nadena.dev.modular_avatar.core.ModularAvatarMergeArmature>(true));
            state.known = new System.Collections.Generic.HashSet<Transform>(root.GetComponentsInChildren<Transform>(true));
        }

        /// <summary>
        /// 빌드된 Expression Parameters·Menu에서 실제 결과를 읽는다 — MA가 메뉴를 설치하고 파라미터를 합친 뒤(Transforming, MA 뒤)라 최종 모양이다.
        /// 저장 여부는 다른 선언(사용자가 직접 넣은 Saved 등)까지 합친 값. 설치 위치가 아바타 메뉴 트리 밖이면 MA는 토글을 조용히 빼므로 여기서 경고한다(D17)
        /// </summary>
        static void CheckMenu(BuildContext ctx, BuildState state, SrBuildRecord.Entry rec, StringBuilder log)
        {
            if (state.syncedBits == 0) return;
            var desc = ctx.AvatarRootObject.GetComponent<VRCAvatarDescriptor>();
            if (desc == null) return;
            var p = desc.expressionParameters != null ? desc.expressionParameters.FindParameter(HoldParameter) : null;
            rec.saved = p != null && p.saved;
            // two switches for one parameter: usually a toggle made by hand (0.1.0 README) left in next to ours — with the menu off, one is fine
            int drivers = CountDrivers(desc.expressionsMenu);
            if (drivers > 1)
                log.AppendLine($"WARN {drivers} controls in the Expression Menu drive '{HoldParameter}' — one is enough (a toggle you made yourself as well?)");
            if (string.IsNullOrEmpty(state.menu))
            {
                log.AppendLine($"Parameter '{HoldParameter}': {(rec.saved ? "saved" : "not saved")}");
                return;
            }
            var path = FindToggle(desc.expressionsMenu, state.menu);
            rec.menuFound = path != null;
            rec.menuPath = path ?? "";
            if (path != null) log.AppendLine($"Menu: '{state.menu}' at {(path.Length == 0 ? "the root" : "'" + path + "'")}, parameter {(rec.saved ? "saved" : "not saved")}");
            else log.AppendLine("WARN the menu toggle is not in the built Expression Menu" +
                                (string.IsNullOrEmpty(state.installTo) ? "" : $" — Install to ('{state.installTo}') is not a menu of this avatar"));
        }

        // controls anywhere in the menu tree whose parameter is Hold (a toggle or a button)
        static int CountDrivers(VRCExpressionsMenu root)
        {
            var seen = new System.Collections.Generic.HashSet<VRCExpressionsMenu>();
            var stack = new System.Collections.Generic.Stack<VRCExpressionsMenu>();
            stack.Push(root);
            int n = 0;
            while (stack.Count > 0)
            {
                var m = stack.Pop();
                if (m == null || m.controls == null || !seen.Add(m)) continue;
                foreach (var c in m.controls)
                {
                    if (c == null) continue;
                    if (c.parameter?.name == HoldParameter) n++;
                    if (c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu != null) stack.Push(c.subMenu);
                }
            }
            return n;
        }

        // our toggle (the Hold parameter and the menu name) in the menu tree — the path is the submenu names, "" = the root. Shallowest first
        static string FindToggle(VRCExpressionsMenu root, string label)
        {
            var seen = new System.Collections.Generic.HashSet<VRCExpressionsMenu>();
            var queue = new System.Collections.Generic.Queue<(VRCExpressionsMenu m, string path)>();
            queue.Enqueue((root, ""));
            while (queue.Count > 0)
            {
                var (m, path) = queue.Dequeue();
                if (m == null || m.controls == null || !seen.Add(m)) continue;
                foreach (var c in m.controls)
                    if (c != null && c.type == VRCExpressionsMenu.Control.ControlType.Toggle && c.parameter?.name == HoldParameter && c.name == label) return path;
                foreach (var c in m.controls)
                    if (c != null && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu != null)
                        queue.Enqueue((c.subMenu, path.Length == 0 ? c.name : path + "/" + c.name));
            }
            return null;
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
        public string installTo = "";   // 설치 위치 메뉴 에셋 이름, 빈 값 = 루트(메뉴를 끄면 빈 값)
        public int fxLayers;
        public int syncedBits;       // MA Parameters를 넣었으면 1 — 양쪽 다 꺼도 넣는다
        public readonly System.Collections.Generic.HashSet<string> deleteTargets = new System.Collections.Generic.HashSet<string>();   // MA Shape Changer Delete 모드 대상 렌더러 이름
        public System.Collections.Generic.Dictionary<Transform, Transform> boneMap;   // MA 앞에서 찍은 의상 본 → 아바타 본(Snapshot)
        public System.Collections.Generic.HashSet<Transform> known;                   // 그때 있던 Transform 전부
    }
}
