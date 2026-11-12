// Pyrite9 Sleeve Roll — inspector: last build (SrBuildRecord) and changelog (read from the README)
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pyrite9.Sleeve
{
    public partial class SleeveRollEditor
    {
        // ---------------------------------------------------------------- 06 last build
        VisualElement BuildSection()
        {
            var desc = Desc;
            var rec = desc != null ? SrBuildRecord.Get(desc.gameObject.name) : null;
            var sec = Section(Num(), T(lang, "LAST BUILD", "最後のビルド", "마지막 빌드"), rec != null ? $"{rec.avatar} · {ShortTime(rec.time)}" : null);

            if (desc == null)
                sec.Add(Label(T(lang, "Put this inside an avatar (under the object with the VRC Avatar Descriptor).",
                    "アバターの中（VRC Avatar Descriptor のオブジェクトの下）に入れてください。",
                    "아바타 안(VRC Avatar Descriptor가 있는 오브젝트 밑)에 넣어 주세요."), "sr-note"));
            else if (rec == null)
                sec.Add(Label(T(lang, "No build yet — Play or Build & Test fills this in.",
                    "まだビルドの記録がありません。Play または Build & Test で表示されます。",
                    "아직 빌드 기록이 없습니다. Play 또는 Build & Test 때 채워집니다."), "sr-note"));
            else
            {
                var grid = Box("sr-facts");
                sec.Add(grid);
                if (rec.built) AddBuiltFacts(grid, rec);
                else grid.Add(Fact("Sleeve Roll", T(lang, "not built — needs a humanoid avatar", "作られていません — ヒューマノイドが必要", "안 만들어짐 — 휴머노이드 필요"), "sr-fact__value--gold"));
                grid.Add(Fact(T(lang, "Warnings", "警告", "경고"), rec.warnings.Count == 0 ? T(lang, "none", "なし", "없음") : rec.warnings.Count.ToString(),
                    rec.warnings.Count == 0 ? null : "sr-fact__value--warn"));
                if (rec.version != SleeveRollConst.Version)
                    grid.Add(Fact(T(lang, "Built with", "ビルド時の版", "빌드 버전"), "v" + rec.version + T(lang, " — rebuild", " — 再ビルドを", " — 다시 빌드 필요"), "sr-fact__value--gold"));
                // English, as in the build log, + a short hint in the menu language for the known ones (README 'Troubleshooting').
                // OK folds one per avatar and warning kind — the numbers in it (mm, t=…) don't count, so a re-measure stays folded
                for (int i = 0; i < Mathf.Min(6, rec.warnings.Count); i++)
                    sec.Add(Warn(rec.warnings[i], rec.avatar + "/" + WarnKind(rec.warnings[i]), Hint(rec.warnings[i])));
                if (rec.warnings.Count > 6) sec.Add(Label($"… +{rec.warnings.Count - 6}", "sr-note"));
            }
            sec.Add(BuildButtons());
            return sec;
        }

        // in build order (inside → outside); this component's garments in gold (refreshed in place — picking doesn't rebuild)
        void AddBuiltFacts(VisualElement grid, SrBuildRecord.Entry rec)
        {
            var heads = new List<(Label l, string name)>();
            foreach (var g in rec.garments)
            {
                var head = Fact(g.name, string.IsNullOrEmpty(g.over) ? "" : T(lang, $"over {g.over}", $"{g.over} の上", $"{g.over} 위에"), null);
                heads.Add((head.Q<Label>(className: "sr-label"), g.name));
                grid.Add(head);
                grid.Add(Child(Fact(T(lang, "Left", "左", "왼쪽"), SideText(g.left), SideCls(g.left))));
                grid.Add(Child(Fact(T(lang, "Right", "右", "오른쪽"), SideText(g.right), SideCls(g.right))));
            }
            if (rec.garments.Count == 0)
                grid.Add(Fact(T(lang, "Garments", "衣装", "옷"), T(lang, "none picked", "選ばれていません", "고른 옷 없음"), "sr-fact__value--gold"));
            grid.Add(Fact(T(lang, "Menu", "メニュー", "메뉴"), string.IsNullOrEmpty(rec.menu)
                ? T(lang, "off — drive Pyrite9/SleeveRoll/Hold yourself", "なし — Pyrite9/SleeveRoll/Hold を自分で", "없음 — Pyrite9/SleeveRoll/Hold 직접")
                : rec.menu, null));
            // read back from the built menu and parameters (0.1.1 —; older records don't have them)
            bool menuInfo = rec.version != "0.1.0";
            if (menuInfo && !string.IsNullOrEmpty(rec.menu))
                grid.Add(Child(Fact(T(lang, "Location", "場所", "위치"), !rec.menuFound
                        ? T(lang, "not in the menu — see the warning", "メニューにありません — 警告を見てください", "메뉴에 없음 — 경고 참고")
                        : T(lang, "Root", "ルート", "루트") + (string.IsNullOrEmpty(rec.menuPath) ? "" : " / " + rec.menuPath.Replace("/", " / ")),
                    rec.menuFound ? null : "sr-fact__value--warn")));
            grid.Add(Fact(T(lang, "Synced", "同期", "동기화"), rec.syncedBits + " bit", null));
            if (menuInfo && rec.syncedBits > 0)
                grid.Add(Child(Fact(T(lang, "Saved", "保存", "저장"), rec.saved ? T(lang, "yes", "する", "함") : T(lang, "no", "しない", "안 함"), null)));
            grid.Add(Fact(T(lang, "FX layers", "FX レイヤー", "FX 레이어"), rec.fxLayers.ToString(), null));
            Live(() => GarmentHash(Cfg), () =>
            {
                var mine = new HashSet<string>(Cfg.Garments.Select(x => x.name));
                foreach (var (l, name) in heads) l.EnableInClassList("sr-fact__value--gold", mine.Contains(name));
            });
        }

        VisualElement BuildButtons()
        {
            var buttons = Box("sr-buttons");
            var logPath = Path.GetFullPath(SleeveRollConst.LogPath);
            var logBtn = new Button(() => EditorUtility.OpenWithDefaultApp(logPath)) { text = T(lang, "Build log", "ビルドログ", "빌드 로그") };
            logBtn.AddToClassList("sr-button");
            logBtn.SetEnabled(File.Exists(logPath));
            buttons.Add(logBtn);
            var readme = ReadmePath();
            var rmBtn = new Button(() => EditorUtility.OpenWithDefaultApp(readme)) { text = "README" };
            rmBtn.AddToClassList("sr-button");
            rmBtn.SetEnabled(readme != null);
            buttons.Add(rmBtn);
            return buttons;
        }

        // one sleeve's result: 'off' · 'to 0.45 · PhysBone 1 kept (still while rolled) · stopped at the shrink edge · shrink released on 312 vertices' · 'failed — message' (the message stays English)
        string SideText(SrBuildRecord.Side s)
        {
            if (!s.on) return T(lang, "off", "なし", "안 걷음");
            if (!s.ok) return T(lang, "failed", "失敗", "실패") + (string.IsNullOrEmpty(s.message) ? "" : " — " + s.message);
            float up = 1f - s.rolledTo;   // same axis as the Cuff position gauge: 0 wrist ~ 1 elbow
            var t = T(lang, $"to {up:0.00}", $"{up:0.00} まで", $"{up:0.00}까지");
            if (s.physBones > 0) t += T(lang, $" · PhysBone {s.physBones} kept (still while rolled)", $" · PhysBone {s.physBones} 個を維持(まくっている間は動かない)", $" · PhysBone {s.physBones}개 유지(걷힌 동안 안 움직임)");
            if (s.stopped) t += T(lang, " · stopped at the shrink edge", " · shrink の境目で停止", " · shrink 경계에서 멈춤");
            if (s.unhide > 0) t += T(lang, $" · shrink released on {s.unhide} vertices", $" · shrink 解除 {s.unhide} 頂点", $" · {s.unhide}개 정점 shrink 해제");
            return t;
        }

        static string SideCls(SrBuildRecord.Side s) => s.on && !s.ok ? "sr-fact__value--warn" : null;

        static string WarnKind(string w) => System.Text.RegularExpressions.Regex.Replace(w, @"[0-9]+([.,][0-9]+)?", "#");

        // what to do, for the build's known warnings (matched on the English text — SleeveRollPlugin / SleeveRollBake / SleeveRollShape / SleeveRollPb).
        // The README's 'Troubleshooting' table covers most of them, not all. Unknown ones get no hint
        static readonly (string[] match, string en, string jp, string ko)[] Hints =
        {
            (new[] { "Not a humanoid avatar" },
                "Set the avatar's Rig to Humanoid — only humanoids are supported.",
                "アバターの Rig を Humanoid にしてください（ヒューマノイドのみ対応）。",
                "아바타 Rig을 Humanoid로 해 주세요(휴머노이드만 지원)."),
            (new[] { "no humanoid forearm or hand bone" },
                "Check the avatar's Humanoid mapping — the lower arm and hand bones are needed.",
                "アバターの Humanoid 設定で前腕（Lower Arm）と手（Hand）のボーンが割り当てられているか確認してください。",
                "아바타 Humanoid 설정에 아래팔(Lower Arm)·손(Hand) 본이 들어 있는지 확인해 주세요."),
            (new[] { "no garment picked" },
                "Pick the sleeved garment in Basics > Garments, or remove this Sleeve Roll.",
                "「基本 > 衣装」で袖のある衣装を選ぶか、この Sleeve Roll を削除してください。",
                "'기본 > 옷'에서 소매 달린 옷을 고르거나 이 Sleeve Roll을 지워 주세요."),
            (new[] { "picked in two Sleeve Rolls" },
                "Drop it from one of them — each garment belongs to one Sleeve Roll.",
                "どちらか一方から外してください（衣装は 1 つの Sleeve Roll にだけ）。",
                "둘 중 하나에서 빼 주세요(옷 하나는 Sleeve Roll 하나에만)."),
            (new[] { "is thinner than the skin" },
                "That is the body mesh — pick the garment instead.",
                "体のメッシュを選んでいます — 衣装のメッシュを選んでください。",
                "몸 메시를 골랐습니다 — 옷 메시를 골라 주세요."),
            (new[] { "not weighted to the forearm bone" },
                "This mesh isn't bound to the forearm (short sleeves, or not a garment?). For an outfit added with MA Merge Armature, check that its forearm bone merges into the avatar's (bone names match).",
                "このメッシュは前腕のボーンに付いていません（半袖か、衣装ではない？）。MA Merge Armature で着せた衣装なら、衣装の前腕ボーンが素体の前腕に統合されるか（ボーン名が合っているか）確認してください。",
                "이 메시는 아래팔 본에 묶여 있지 않습니다(반소매거나 옷이 아님?). MA Merge Armature로 입힌 옷이면 옷의 아래팔 본이 소체 아래팔에 합쳐지는지(본 이름이 맞는지) 확인해 주세요."),
            (new[] { "no sleeve vertices on the forearm" },
                "This mesh has no sleeve on the forearm (short sleeves, or not a garment?) — it can't be rolled. Drop it.",
                "前腕に袖がないメッシュです（半袖か、衣装ではない？）— まくれないので外してください。",
                "팔뚝에 소매가 없는 메시입니다(반소매거나 옷이 아님?) — 걷을 수 없으니 빼 주세요."),
            (new[] { "already shorter than Roll To" },
                "The sleeve is already shorter than Cuff position — raise Cuff position (toward the elbow), or drop this garment.",
                "袖がすでに「袖口の位置」より短いです — 「袖口の位置」を上げる（肘側へ）か、この衣装を外してください。",
                "소매가 이미 '소맷단 위치'보다 짧습니다 — '소맷단 위치'를 높이거나(팔꿈치 쪽) 이 옷을 빼 주세요."),
            (new[] { "has forearm skin only from", "forearm skin starts at" },
                "The body model has the forearm cut away under the outfit — use a body mesh with the whole forearm (Advanced > Body meshes).",
                "体のモデルが衣装の下の前腕を切り取っています — 前腕のある体のメッシュを使ってください（詳細 > 体のメッシュ）。",
                "몸 모델이 옷 밑 팔뚝을 잘라 냈습니다 — 팔뚝이 있는 몸 메시를 쓰세요(고급 > 몸 메시)."),
            (new[] { "no skin (body) mesh found" },
                "Put the body mesh in Advanced > Body meshes.",
                "「詳細 > 体のメッシュ」に体のメッシュを入れてください。",
                "'고급 > 몸 메시'에 몸 메시를 넣어 주세요."),
            (new[] { "MA Shape Changer in Delete mode" },
                "If a hole shows where the sleeve rolls, set that Shape Changer to Set mode.",
                "まくった所に穴が見えたら、その Shape Changer を Set モードにしてください。",
                "걷힌 자리에 구멍이 보이면 그 Shape Changer를 Set 모드로 바꿔 주세요."),
            (new[] { "menu toggle is not in the built Expression Menu" },
                "Install to is not a menu of this avatar — empty it (the root) or pick a submenu of the avatar's Expression Menu.",
                "インストール先がこのアバターのメニューではありません — 空にする（ルート）か、アバターの Expression Menu のサブメニューを選んでください。",
                "설치 위치가 이 아바타의 메뉴가 아닙니다 — 비우거나(루트) 아바타 Expression Menu의 하위 메뉴를 골라 주세요."),
            (new[] { "controls in the Expression Menu drive" },
                "Two switches for the same sleeves — delete the toggle you made yourself, or turn Menu off and keep only yours.",
                "同じ袖のスイッチが 2 つあります — 自分で作ったトグルを消すか、メニュー をオフにして自分のものだけ残してください。",
                "같은 소매 스위치가 두 개입니다 — 직접 만든 토글을 지우거나, 메뉴를 끄고 직접 만든 것만 남기세요."),
            (new[] { "PhysBone stops moving" },
                "This sleeve's PhysBone can't be kept safely (reason in the brackets), so the sleeve follows the hand with no physics. It still rolls. To keep the physics, remove the cause in the brackets.",
                "この袖の PhysBone を安全に維持できないため（理由は括弧内）、袖は物理なしで手に付いていきます。まくることはできます。物理を残すには括弧内の原因を取り除いてください。",
                "이 소매의 PhysBone을 안전하게 유지할 수 없어서(이유는 괄호 안) 소매가 물리 없이 손을 따라갑니다. 걷히기는 합니다. 물리를 살리려면 괄호 안 원인을 없애세요."),
            (new[] { "cannot be held still while rolled" },
                "This sleeve does not roll, because its physics can't be held still. Remove the cause in the second brackets.",
                "この袖はまくれません。物理を止めておけないためです。2 つ目の括弧内の原因を取り除いてください。",
                "이 쪽 소매는 걷히지 않습니다. 물리를 멈춰 둘 수 없어서입니다. 두 번째 괄호 안 원인을 없애세요."),
            (new[] { "it cannot be rolled" },
                "A PhysBone on the upper arm holds the sleeve down to the cuff. This outfit can't be rolled.",
                "上腕の PhysBone が袖口まで支えているため、まくれない衣装です。",
                "위팔의 PhysBone이 소맷단까지 잡고 있어 걷을 수 없는 옷입니다."),
            (new[] { "the two fight over it" },
                "Two of the outfit's PhysBones move the same bone — the outfit's own setting, it sways the same without Sleeve Roll and the sleeve still rolls. To fix it, remove one, or put the inner PhysBone's first bone in the outer one's Ignore Transforms.",
                "衣装の PhysBone 2 つが同じボーンを動かしています — 衣装自体の設定で、Sleeve Roll がなくても同じ揺れです。そのままでもまくれます。直すなら片方を消すか、外側の PhysBone の Ignore Transforms に内側の開始ボーンを入れてください。",
                "옷의 PhysBone 두 개가 같은 본을 움직입니다 — 옷 자체 설정이라 Sleeve Roll이 없어도 같은 흔들림이고, 그대로 둬도 걷힙니다. 고치려면 하나를 지우거나 바깥쪽 PhysBone의 Ignore Transforms에 안쪽 시작 본을 넣으세요."),
            (new[] { "the rolled shape" },
                "Sleeve Roll could not work out a sound rolled shape for this sleeve, so it is left as it is. Check the avatar's arm bones (lower arm and hand apart?) — if they look fine, please send the build log to the author.",
                "この袖はまくった形を正しく計算できなかったため、そのままにしています。アバターの腕のボーン（前腕と手が離れているか）を確認し、問題なければビルドログを作者に送ってください。",
                "이 소매는 걷은 모양을 제대로 계산하지 못해 그대로 뒀습니다. 아바타 팔 본(아래팔과 손이 떨어져 있는지)을 확인하고, 문제없으면 빌드 로그를 제작자에게 보내 주세요."),
            (new[] { "stopped on an internal error" },
                "Sleeve Roll hit a bug on this avatar — the sleeves may roll wrong. Please send Logs/Pyrite9_SleeveRoll_build.txt (the EXCEPTION line) to the author.",
                "このアバターで Sleeve Roll の不具合が起きました — 袖が正しくまくれない可能性があります。Logs/Pyrite9_SleeveRoll_build.txt（EXCEPTION の行）を作者に送ってください。",
                "이 아바타에서 Sleeve Roll 버그가 났습니다 — 소매가 제대로 안 걷힐 수 있습니다. Logs/Pyrite9_SleeveRoll_build.txt(EXCEPTION 줄)를 제작자에게 보내 주세요."),
            (new[] { "not inside this avatar" },
                "That garment belongs to another avatar (a copied component?) — pick the garment of this avatar.",
                "その衣装は別のアバターのものです（コンポーネントをコピーした？）— このアバターの衣装を選んでください。",
                "그 옷은 다른 아바타의 것입니다(컴포넌트를 복사함?) — 이 아바타의 옷을 골라 주세요."),
            (new[] { "already has a shape key with this name" },
                "The garment already has SleeveUp_L/R (another tool or a baked copy?) — use the original garment mesh.",
                "衣装にすでに SleeveUp_L/R があります（別のツールか焼き込み済みのコピー？）— 元の衣装のメッシュを使ってください。",
                "옷에 이미 SleeveUp_L/R가 있습니다(다른 도구나 구운 사본?) — 원래 옷 메시를 쓰세요."),
        };

        string Hint(string w)
        {
            foreach (var h in Hints)
                foreach (var m in h.match)
                    if (w.Contains(m)) return T(lang, h.en, h.jp, h.ko);
            return null;
        }

        // ---------------------------------------------------------------- 07 changelog
        VisualElement ChangelogSection()
        {
            var readme = ReadmePath();
            var entries = readme != null ? ReadChangelog(readme) : new List<(string ver, string date, string text)>();
            var cur = entries.FirstOrDefault(e => e.ver == SleeveRollConst.Version);
            if (cur.ver == null && entries.Count > 0) cur = entries[0];
            var sec = Section(Num(), T(lang, "CHANGELOG", "更新履歴", "변경 기록"), cur.ver != null ? "v" + cur.ver + " · " + cur.date : null);
            var content = new VisualElement();
            if (cur.ver == null) content.Add(Label(T(lang, "See the README.", "README を見てください。", "README를 보세요."), "sr-note"));
            else content.Add(Label(cur.text, "sr-changelog"));
            if (readme != null)
            {
                var all = Label(T(lang, "Full changelog — README", "すべての更新履歴 — README", "전체 변경 기록 — README"), "sr-link");
                all.RegisterCallback<ClickEvent>(_ => EditorUtility.OpenWithDefaultApp(readme));
                content.Add(all);
            }
            sec.Add(Fold("Pyrite9SR.ShowChangelog", true, T(lang, "THIS VERSION", "この版", "이번 버전"), content));
            return sec;
        }

        // README '## 변경 기록 / Changelog / 更新履歴' lines "- **0.1.0** (2026-11-04): text" (indented next lines are joined)
        static List<(string ver, string date, string text)> ReadChangelog(string path)
        {
            var list = new List<(string ver, string date, string text)>();
            try
            {
                bool inSec = false;
                var rx = new System.Text.RegularExpressions.Regex(@"^-\s*\*\*(?<v>[0-9][0-9A-Za-z.-]*)\*\*\s*[(（](?<d>[^)）]*)[)）]\s*[:：]\s*(?<t>.*)$");
                foreach (var line in File.ReadAllLines(path))
                {
                    if (line.StartsWith("## ")) { inSec = line.Contains("Changelog") || line.Contains("변경 기록") || line.Contains("更新履歴"); continue; }
                    if (!inSec) continue;
                    var m = rx.Match(line.Trim());
                    if (m.Success) list.Add((m.Groups["v"].Value, m.Groups["d"].Value.Trim(), Plain(m.Groups["t"].Value)));
                    else if (list.Count > 0 && line.StartsWith("  ") && line.Trim().Length > 0)
                    {
                        var last = list[list.Count - 1];
                        list[list.Count - 1] = (last.ver, last.date, last.text + " " + Plain(line.Trim()));
                    }
                }
            }
            catch { }
            return list;
        }

        static string Plain(string md) => md.Replace("**", "").Replace("`", "");

        static string ShortTime(string t)
        {
            if (!System.DateTime.TryParse(t, out var d)) return t;
            return d.Date == System.DateTime.Today ? d.ToString("HH:mm") : d.ToString("MM-dd HH:mm");
        }
    }
}
