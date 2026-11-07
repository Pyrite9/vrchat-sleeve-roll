// Pyrite9 Sleeve Roll — inspector: the setting sections (basics, menu, roll, fit, advanced)
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pyrite9.Sleeve
{
    public partial class SleeveRollEditor
    {
        // ---------------------------------------------------------------- 01 basics
        VisualElement BasicSection()
        {
            var sec = Section(Num(), T(lang, "BASICS", "基本", "기본"), null);
            var grid = Box("sr-rows");
            sec.Add(grid);
            grid.Add(Row(T(lang, "Menu language", "メニュー言語", "메뉴 언어"),
                "Menu Language — " + T(lang, "The Expression Menu toggle's name (when Menu name is empty) and this inspector. Applied at build.",
                    "Expression Menu のトグル名（メニュー名が空のとき）とこのインスペクター。ビルド時に反映。",
                    "Expression Menu 토글 이름(메뉴 이름이 비었을 때)과 이 인스펙터. 빌드 때 적용."),
                Seg("menuLanguage", new[] { 0, 1, 2 }, new[] { "EN", "日本語", "한국어" })));

            // checks that don't need a build — each right under what it is about: not in an avatar (top), nothing picked, a garment
            // outside the avatar, two Sleeve Rolls on one garment (under the garments)
            var noAvatar = Warn(T(lang, "Put this inside an avatar (under the object with the VRC Avatar Descriptor) — outside it nothing happens.",
                "アバターの中（VRC Avatar Descriptor のオブジェクトの下）に入れてください — 外では何も起きません。",
                "아바타 안(VRC Avatar Descriptor가 있는 오브젝트 밑)에 넣어 주세요 — 밖에서는 아무 일도 일어나지 않습니다."));
            grid.Add(noAvatar);
            grid.Add(GarmentBlock());
            var noTarget = Warn(T(lang, "Pick the garment to roll above — until then this Sleeve Roll does nothing.",
                "上でまくる衣装を選んでください — 選ぶまでこの Sleeve Roll は何もしません。",
                "위에서 걷을 옷을 골라 주세요 — 고르기 전까지 이 Sleeve Roll은 아무것도 하지 않습니다."));
            var outside = Warn(T(lang, "A picked garment is not inside this avatar — pick ones from this avatar.",
                "選んだ衣装にこのアバターの外のものがあります — このアバターの衣装を選んでください。",
                "고른 옷 중에 이 아바타 밖의 것이 있습니다 — 이 아바타의 옷을 골라 주세요."));
            var twice = Warn(T(lang, "Another Sleeve Roll in this avatar uses the same garment — the first one's settings are used for it.",
                "このアバターの別の Sleeve Roll が同じ衣装を使っています — その衣装には最初の Sleeve Roll の設定が使われます。",
                "이 아바타의 다른 Sleeve Roll이 같은 옷을 씁니다 — 그 옷엔 첫 Sleeve Roll의 설정을 씁니다."));
            foreach (var w in new[] { noTarget, outside, twice }) { w.AddToClassList("sr-warn--inline"); grid.Add(w); }
            noAvatar.AddToClassList("sr-warn--inline");

            grid.Add(Row(T(lang, "Sleeves", "袖", "소매"),
                "Sleeves — " + T(lang, "Which sleeves roll up. Both go up together with the one toggle.",
                    "まくる袖。両方とも 1 つのトグルで一緒に上がります。",
                    "걷을 소매. 둘 다 토글 하나로 같이 올라갑니다."),
                SideSeg()));

            Live(() => unchecked(PickState() * 31 + hierVersion), () =>
            {
                var c = Cfg;
                var d = Desc;
                Display(noAvatar, d == null);
                bool has = HasGarment(c), outs = false, dup = false;
                if (d != null && has)
                {
                    var gs = c.Garments;
                    foreach (var g in gs) if (!g.transform.IsChildOf(d.transform)) { outs = true; break; }
                    foreach (var s in Siblings())
                    {
                        if (s == null || s == c || !HasGarment(s)) continue;
                        if (s.Garments.Any(gs.Contains)) { dup = true; break; }
                    }
                }
                Display(noTarget, d != null && !has);
                Display(outside, outs);
                Display(twice, dup);
            });
            return sec;
        }

        // the garments: one toggle button per sleeved garment found in the avatar (picked = gold with ✓, click again to drop, several for
        // layered outfits), then the picked ones that are not buttons, each with a remove button, and an empty field to drop one in.
        // Without candidates (outside an avatar, nothing found) the field list is the whole picker. The block refreshes itself when any
        // Sleeve Roll's picks change — no whole-inspector rebuild, so nothing flickers and the scroll stays
        VisualElement GarmentBlock()
        {
            var box = Box("sr-rows");
            int shown = 0;
            bool pending = false;

            void Fill()
            {
                shown = PickState();
                box.Clear();
                FillGarments(box);
            }

            // a pick writes the component, then the block refreshes on the next tick (not inside the clicked button's own callback)
            pickRefresh = () =>
            {
                if (pending) return;
                pending = true;
                box.schedule.Execute(() => { pending = false; Fill(); });
            };
            Fill();
            live.Add(() => { if (!pending && PickState() != shown) Fill(); });
            return box;
        }

        void FillGarments(VisualElement box)
        {
            string tip = "Garments — " + T(lang, "The sleeved outfits' Skinned Mesh Renderers. Pick several for layered outfits (a shirt under a jacket): at build they are rolled from the inside out, and each outer sleeve sits over the inner one's rolled cuff.",
                "袖のある衣装の Skinned Mesh Renderer。重ね着（ジャケットの下のシャツなど）は複数選びます：ビルド時に内側から順にまくり、外側の袖は内側のまくった袖口の上に重なります。",
                "소매 달린 옷의 Skinned Mesh Renderer. 겹쳐 입은 옷(재킷 밑 셔츠 등)은 여러 개 고릅니다: 빌드 때 안쪽부터 걷고, 바깥 소매는 안쪽의 걷힌 소맷단 위에 얹힙니다.");
            var c = Cfg;
            var d = Desc;
            var picked = c.Garments;
            var cands = d != null ? Candidates() : new List<SkinnedMeshRenderer>();
            var taken = new HashSet<SkinnedMeshRenderer>();
            foreach (var s in Siblings()) if (s != null && s != c) foreach (var g in s.Garments) taken.Add(g);

            if (cands.Count > 0)
            {
                var row = Box("sr-candidates");
                foreach (var r in cands) row.Add(CandidateButton(r, picked.Contains(r), taken.Contains(r), d.transform));
                box.Add(Stack(Row(T(lang, "Garments", "衣装", "옷"), tip, row)));
                box.Add(Label(T(lang, "Garments found automatically are listed here. If yours isn't, add it below. Several can be picked for layered outfits.",
                    "自動で推定した衣装を表示します。なければ下で追加できます。重ね着は複数選択できます。",
                    "자동으로 추정한 대상을 표시합니다. 없으면 아래에서 추가할 수 있습니다. 여러 벌 껴입은 옷은 다중 선택할 수 있습니다."), "sr-note sr-note--gap"));
            }

            // the picked ones that aren't buttons (all of them when there are none), each removable, then the empty field to add one
            var listBox = Box("sr-garments");
            foreach (var r in picked) if (!cands.Contains(r)) listBox.Add(GarmentLine(r));
            listBox.Add(AddGarmentField());
            var listRow = Row(cands.Count > 0 ? T(lang, "Not listed", "ボタンにない衣装", "목록에 없는 옷") : T(lang, "Garments", "衣装", "옷"), tip, listBox);
            listRow.AddToClassList("sr-row--top");
            box.Add(listRow);
            if (picked.Count > 1)
                box.Add(Label(T(lang, $"{picked.Count} layers — rolled from the inside out at build, each outer sleeve over the inner one's rolled cuff. The order is in Last build.",
                    $"{picked.Count} 枚重ね — ビルド時に内側から順にまくり、外側の袖は内側のまくった袖口の上に。順番は「最後のビルド」に。",
                    $"{picked.Count}겹 — 빌드 때 안쪽부터 걷고, 바깥 소매는 안쪽의 걷힌 소맷단 위에 얹힙니다. 순서는 '마지막 빌드'에."), "sr-note sr-note--gap"));
        }

        Button CandidateButton(SkinnedMeshRenderer r, bool on, bool other, Transform avatar)
        {
            var b = new Button(() =>
            {
                var list = Cfg.Garments;
                if (list.Contains(r)) list.Remove(r); else list.Add(r);
                SetGarments(list);
            }) { text = (on ? "✓ " : "") + r.name + (other ? " · " + T(lang, "in use", "使用中", "사용 중") : "") };
            b.tooltip = AnimationUtility.CalculateTransformPath(r.transform, avatar)
                + (other ? T(lang, "\nAnother Sleeve Roll in this avatar already rolls this one.", "\nこのアバターの別の Sleeve Roll がすでにこれをまくっています。", "\n이 아바타의 다른 Sleeve Roll이 이미 이 옷을 걷습니다.") : "")
                + (on ? T(lang, "\nClick to drop it.", "\nクリックで外す。", "\n누르면 빼기.") : T(lang, "\nClick to pick it.", "\nクリックで選ぶ。", "\n누르면 고르기."));
            b.AddToClassList("sr-button");
            b.EnableInClassList("sr-candidate--on", on);
            b.EnableInClassList("sr-candidate--taken", other && !on);
            return b;
        }

        VisualElement GarmentLine(SkinnedMeshRenderer r)
        {
            var line = Box("sr-garment");
            var f = new ObjectField { objectType = typeof(SkinnedMeshRenderer), allowSceneObjects = true, value = r };
            f.AddToClassList("sr-field");
            f.RegisterValueChangedCallback(e =>
            {
                var list = Cfg.Garments;
                int i = list.IndexOf(r);
                var nv = e.newValue as SkinnedMeshRenderer;
                if (i < 0) return;
                if (nv == null || list.Contains(nv)) list.RemoveAt(i); else list[i] = nv;
                SetGarments(list);
            });
            line.Add(f);
            var x = new Button(() => { var list = Cfg.Garments; list.Remove(r); SetGarments(list); }) { text = "×" };
            x.tooltip = T(lang, "Remove", "外す", "빼기");
            x.AddToClassList("sr-button");
            x.AddToClassList("sr-garment__remove");
            line.Add(x);
            return line;
        }

        VisualElement AddGarmentField()
        {
            var add = new ObjectField { objectType = typeof(SkinnedMeshRenderer), allowSceneObjects = true };
            add.AddToClassList("sr-field");
            add.tooltip = T(lang, "Drop the garment's Skinned Mesh Renderer here to add it.", "衣装の Skinned Mesh Renderer をここにドロップして追加。", "옷의 Skinned Mesh Renderer를 여기에 끌어다 놓으면 추가.");
            add.RegisterValueChangedCallback(e =>
            {
                var nv = e.newValue as SkinnedMeshRenderer;
                if (nv == null) return;
                var list = Cfg.Garments;
                if (!list.Contains(nv)) list.Add(nv);
                SetGarments(list);
            });
            return add;
        }

        System.Action pickRefresh;

        // writes the picked list to 'targets' and clears the old single 'target' (0.1.0 dev builds) — one undo step
        void SetGarments(List<SkinnedMeshRenderer> list)
        {
            serializedObject.Update();
            var prop = serializedObject.FindProperty("targets");
            prop.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            serializedObject.FindProperty("target").objectReferenceValue = null;
            serializedObject.ApplyModifiedProperties();
            pickRefresh?.Invoke();
        }

        // Both / Left / Right over the two bools 'left' and 'right'; the 'on' item refreshes in place (Poll)
        VisualElement SideSeg()
        {
            var seg = Box("sr-seg");
            var opts = new[] { (true, true), (true, false), (false, true) };
            var names = new[] { T(lang, "Both", "両方", "양쪽"), T(lang, "Left", "左", "왼쪽"), T(lang, "Right", "右", "오른쪽") };
            var items = new List<Button>();
            void Refresh()
            {
                var c = Cfg;
                for (int i = 0; i < opts.Length; i++) items[i].EnableInClassList("sr-seg__item--on", c.left == opts[i].Item1 && c.right == opts[i].Item2);
            }
            for (int i = 0; i < opts.Length; i++)
            {
                var o = opts[i];
                var b = new Button(() =>
                {
                    serializedObject.FindProperty("left").boolValue = o.Item1;
                    serializedObject.FindProperty("right").boolValue = o.Item2;
                    serializedObject.ApplyModifiedProperties();
                    Refresh();
                }) { text = names[i] };
                b.AddToClassList("sr-seg__item");
                if (i == 0) b.AddToClassList("sr-seg__item--first");
                seg.Add(b);
                items.Add(b);
            }
            Refresh();
            live.Add(Refresh);
            return seg;
        }

        // ---------------------------------------------------------------- 02 menu
        VisualElement MenuSection()
        {
            var owner = MenuOwner();
            bool first = owner == Cfg;
            var sec = Section(Num(), T(lang, "MENU", "メニュー", "메뉴"), null);
            var grid = Box("sr-rows");
            sec.Add(grid);
            var menu = Box("sr-group");
            grid.Add(menu);
            menu.Add(Row(T(lang, "Menu", "メニュー", "메뉴"),
                "Install Menu — " + T(lang, "Adds a toggle to the Expression Menu root: on = sleeves rolled (not saved, others see it). Off: no menu — the synced Bool Pyrite9/SleeveRoll/Hold is still made, for your own menu or contact.",
                    "Expression Menu のルートにトグルを追加：オン = 袖をまくる（保存しない、他の人にも見える）。オフ：メニューなし — 同期 Bool Pyrite9/SleeveRoll/Hold は作るので、自分のメニューやコンタクトから動かせます。",
                    "Expression Menu 루트에 토글 추가: 켬 = 소매 걷음(저장 안 함, 남에게도 보임). 끄면 메뉴 없음 — 동기화 Bool Pyrite9/SleeveRoll/Hold는 만들어 두니 직접 만든 메뉴나 Contact로 몰 수 있습니다."),
                Check("installMenu", T(lang, "Toggle in the Expression Menu", "Expression Menu にトグル", "Expression Menu에 토글"))));
            var nameRow = Child(Row(T(lang, "Menu name", "メニュー名", "메뉴 이름"),
                "Menu Name — " + T(lang, "The toggle's name. Empty: Sleeve Roll / 袖まくり / 소매 걷기, by the menu language.",
                    "トグルの名前。空なら メニュー言語で Sleeve Roll / 袖まくり / 소매 걷기。",
                    "토글 이름. 비우면 메뉴 언어에 따라 Sleeve Roll / 袖まくり / 소매 걷기."),
                Text("menuName")));
            menu.Add(nameRow);
            // the empty name field doesn't say what the menu will show — the note does (only then; a typed name speaks for itself)
            var nameNote = Label("", "sr-note");
            sec.Add(nameNote);
            sec.Add(Label(T(lang, "Synced: 1 bit (Pyrite9/SleeveRoll/Hold) for every Sleeve Roll in the avatar.",
                "同期：1 bit（Pyrite9/SleeveRoll/Hold）— アバターの Sleeve Roll 全部で共通。",
                "동기화: 1 bit(Pyrite9/SleeveRoll/Hold) — 아바타의 Sleeve Roll 전부 공통."), "sr-note"));
            if (!first)
            {
                sec.Add(Label(T(lang, $"The menu comes from the first Sleeve Roll in this avatar with a garment ({owner.name}) — these two settings are not used.",
                    $"メニューは衣装を選んだ最初の Sleeve Roll（{owner.name}）のもの — ここの 2 つの設定は使われません。",
                    $"메뉴는 옷을 고른 첫 Sleeve Roll({owner.name}) 것을 씁니다 — 여기 두 설정은 쓰지 않습니다."), "sr-note sr-note--gold"));
                menu.SetEnabled(false);
            }
            Live(() => unchecked((Cfg.installMenu ? 1 : 2) + 31 * (Cfg.menuName ?? "").GetHashCode()), () =>
            {
                var c = Cfg;
                nameRow.SetEnabled(c.installMenu);
                bool show = c.installMenu && first && string.IsNullOrWhiteSpace(c.menuName);
                if (show) nameNote.text = T(lang, $"Empty — the menu shows: {c.MenuLabel}", $"空欄のときのメニュー名：{c.MenuLabel}", $"비워 두면 메뉴 이름: {c.MenuLabel}");
                Display(nameNote, show);
            });
            return sec;
        }

        // ---------------------------------------------------------------- 03 roll
        // where 'Cuff position' starts to look odd, stored values (shown 0.45 / 0.60): from renders of Salt, Milfy and Kipfel, 2026-11-06. A guide, not a limit
        const float RollWarn = 0.55f, RollDanger = 0.40f;

        VisualElement RollSection()
        {
            var sec = Section(Num(), T(lang, "HOW FAR IT ROLLS", "まくる位置", "걷는 위치"), null);
            sec.Add(Label(T(lang, "Where the cuff stops when the menu toggle rolls the sleeves up.",
                "メニューのトグルで袖をまくったとき、袖口がどこまで上がるかを決めます。",
                "메뉴 토글로 소매를 걷을 때 소맷단이 어디까지 올라갈지 정합니다."), "sr-note sr-note--gap"));
            var grid = Box("sr-rows");
            sec.Add(grid);
            Put(grid, PreviewRow(), T(lang, "See the rolled sleeves in the Scene view without building. Changing a value updates it right away.",
                "ビルドせずに、まくった袖を Scene ビューで確認します。値を変えるとすぐ反映されます。",
                "빌드 없이 걷은 소매를 Scene 뷰에서 봅니다. 수치를 바꾸면 바로 반영됩니다."));
            AddPreviewNotes(grid);
            Put(grid, GaugeRow("rollTo", T(lang, "Cuff position", "袖口の位置", "소맷단 위치"),
                "Roll To — " + T(lang, "How far up the cuff goes: 0 = wrist, 1 = elbow. Gold ring in the Scene view.",
                    "袖口をどこまで上げるか：0 = 手首、1 = 肘。Scene ビューの金色の輪。",
                    "소맷단을 얼마나 올릴지: 0 = 손목, 1 = 팔꿈치. Scene 뷰의 금색 고리."),
                0f, 1f, 0.70f, 0.01f, "0.00",
                lo: T(lang, "Wrist side", "手首側", "손목 쪽"), hi: T(lang, "Elbow side", "肘側", "팔꿈치 쪽"), flip: true, warnAt: RollWarn, dangerAt: RollDanger),
                T(lang, "How far up the cuff goes. Bigger rolls further. 0 = wrist, 1 = elbow.",
                    "袖口をどこまで上げるか。大きいほど上までまくります。0 = 手首、1 = 肘。",
                    "소맷단을 얼마나 올릴지 정합니다. 값이 클수록 많이 걷힙니다. 0 = 손목, 1 = 팔꿈치."));
            // how far is too far differs per outfit: the gauge's yellow / orange stretches and these lines send you to Preview, they don't forbid anything
            var rollWarn = Label(T(lang, "From here, depending on the outfit, the cuff can bend or the fold gets thin. Check it with Preview.",
                "ここからは衣装によって袖口が折れたり、折り返しが薄くなることがあります。プレビューで確認してください。",
                "이 구간부터는 옷에 따라 소맷단이 꺾이거나 접힌 천이 얇아질 수 있습니다. 미리 보기로 확인하세요."), "sr-note sr-note--gold sr-note--gap");
            grid.Add(rollWarn);
            var rollDanger = Warn(T(lang, "In this range the sleeve can look cut off or otherwise odd. Always check it with Preview.",
                "この範囲では袖が切れたように見えるなど、不自然になることがあります。必ずプレビューで確認してください。",
                "이 구간에서는 소매가 잘려 보이는 등 어색할 수 있습니다. 꼭 미리 보기로 확인하세요."));
            rollDanger.AddToClassList("sr-warn--inline");
            grid.Add(rollDanger);
            Live(() => Cfg.rollTo <= RollDanger + 1e-4f ? 2 : Cfg.rollTo <= RollWarn + 1e-4f ? 1 : 0, () =>
            {
                float v = Cfg.rollTo;
                Display(rollDanger, v <= RollDanger + 1e-4f);
                Display(rollWarn, v > RollDanger + 1e-4f && v <= RollWarn + 1e-4f);
            });
            // the last build stopped at the shrink edge: the slider can't go past it — say so, or dragging it looks broken
            var stoppedNote = Label("", "sr-note sr-note--gold sr-note--gap");
            grid.Add(stoppedNote);
            Put(grid, Row(T(lang, "Arm shrink", "腕の shrink", "팔 shrink"),
                "Unhide When Rolled — " + T(lang, "Only matters if the outfit shrinks the arm under the sleeve. On: releases the shrink on the forearm while rolled, from where the sleeve starts to move to the wrist, and rolls to Cuff position — no thin arm shows inside the rolled cuff. Shrunk again when the sleeve comes down. Off: keeps the shrink and stops before the shrunk arm shows. If you turn it off, be sure to check inside the sleeve in-game.",
                    "衣装が袖の下の腕を shrink している場合にだけ働きます。オン：まくっている間、袖が動き始める所から手首まで前腕の shrink を解除し、「袖口の位置」までまくります — 袖口の中に細い腕が見えません。袖を下ろすと再び shrink されます。オフ：shrink を保ち、shrink された腕が見える手前で止まります。オフにした場合は、インゲームで袖の中を必ず確認してください。",
                    "옷이 소매 밑 팔을 shrink 해 둔 경우에만 작동합니다. 켬: 걷는 동안 소매가 움직이기 시작하는 자리부터 손목까지 팔뚝 shrink를 해제하고 '소맷단 위치'까지 걷습니다 — 소맷단 안에 가는 팔이 안 보입니다. 소매를 내리면 다시 shrink됩니다. 끔: shrink를 유지하고, shrink된 팔이 보이기 전에 멈춥니다. 끈 경우 인게임에서 소매 안을 꼭 확인하세요."),
                Check("unhideWhenRolled", T(lang, "Release the shrink while rolled", "まくっている間 shrink を解除", "걷는 동안 shrink 해제"))),
                T(lang, "Only matters if the outfit shrinks the arm under the sleeve. On: releases the shrink while rolled and rolls to Cuff position. Off: keeps the shrink and stops before the shrunk arm shows. If you turn it off, be sure to check inside the sleeve in-game.",
                    "衣装が袖の下の腕を shrink している場合にだけ働きます。オン：まくっている間 shrink を解除し、「袖口の位置」までまくります。オフ：shrink を保ち、shrink された腕が見える手前で止まります。オフにした場合は、インゲームで袖の中を必ず確認してください。",
                    "옷이 소매 밑 팔을 shrink 해 둔 경우에만 작동합니다. 켜면 걷는 동안 shrink를 해제하고 '소맷단 위치'까지 걷습니다. 끄면 shrink를 유지하고, shrink된 팔이 보이기 전에 멈춥니다. 끈 경우 인게임에서 소매 안을 꼭 확인하세요."));
            sec.Add(Label(T(lang, "Scene view: gold ring = where the cuff ends up; faint ring = where the sleeve starts to move.",
                "Scene ビュー：金の輪 = 袖口の位置、薄い輪 = 袖が動き始める所。",
                "Scene 뷰: 금색 고리 = 소맷단이 올 자리, 흐린 고리 = 소매가 움직이기 시작하는 자리."), "sr-note"));
            // the record only changes with a rebuild
            Live(() => unchecked((Cfg.unhideWhenRolled ? 1 : 2) + 31 * GarmentHash(Cfg)), () =>
            {
                var c = Cfg;
                float at = -1f;
                var rec = ringRec;
                if (!c.unhideWhenRolled && rec != null && rec.garments.Count > 0)
                {
                    var mine = c.Garments;
                    foreach (var g in rec.garments)
                    {
                        if (!mine.Any(x => x.name == g.name)) continue;
                        foreach (bool left in SrSide.Both)
                        {
                            var s = left ? g.left : g.right;
                            if (s.on && s.ok && s.stopped) at = Mathf.Max(at, s.rolledTo);
                        }
                    }
                }
                Display(stoppedNote, at >= 0f);
                if (at >= 0f)
                    stoppedNote.text = T(lang, $"Last build stopped at the shrink edge ({1f - at:0.00}). To roll further, turn on Release the shrink while rolled.",
                        $"最後のビルドは shrink の境目で止まりました（{1f - at:0.00}）。もっとまくるには「まくっている間 shrink を解除」をオンに。",
                        $"마지막 빌드는 shrink 경계에서 멈췄습니다({1f - at:0.00}). 더 걷으려면 '걷는 동안 shrink 해제'를 켜세요.");
            });
            return sec;
        }

        // the Scene-view preview (SleeveRollPreview): sleeves down / rolled, for every avatar in the scene. Settings apply as they change
        VisualElement PreviewRow()
        {
            var seg = Box("sr-seg");
            var down = new Button(() => SleeveRollPreview.Toggle.IsEnabled.Value = false) { text = "OFF" };
            var up = new Button(() => SleeveRollPreview.Toggle.IsEnabled.Value = true) { text = "ON" };
            down.AddToClassList("sr-seg__item"); down.AddToClassList("sr-seg__item--first");
            up.AddToClassList("sr-seg__item");
            seg.Add(down); seg.Add(up);
            Live(() => SleeveRollPreview.Toggle.IsEnabled.Value ? 1 : 2, () =>
            {
                bool on = SleeveRollPreview.Toggle.IsEnabled.Value;
                down.EnableInClassList("sr-seg__item--on", !on);
                up.EnableInClassList("sr-seg__item--on", on);
            });
            return Row(T(lang, "Preview", "プレビュー", "미리 보기"),
                "Preview — " + T(lang, "Shows the rolled sleeves in the Scene view without building, with the same calculation as the build. The scene, the meshes and your uploads don't change.",
                    "ビルドせずにまくった袖を Scene ビューで表示します。計算はビルドと同じです。シーン・メッシュ・アップロードは変わりません。",
                    "빌드 없이 걷은 소매를 Scene 뷰에 보여 줍니다. 계산은 빌드와 같습니다. 씬·메시·업로드는 바뀌지 않습니다."), seg);
        }

        void AddPreviewNotes(VisualElement sec)
        {
            var result = Label("", "sr-note sr-note--gap");
            var warn = Label("", "sr-note sr-note--gold sr-note--gap");
            sec.Add(result);
            sec.Add(warn);
            Live(() => unchecked((SleeveRollPreview.Toggle.IsEnabled.Value ? 1 : 2) + 31 * SleeveRollPreview.Revision + 961 * (nadena.dev.ndmf.preview.NDMFPreviewPrefs.instance.EnablePreview ? 1 : 2)), () =>
            {
                bool on = SleeveRollPreview.Toggle.IsEnabled.Value;
                var d = Desc;
                var rec = on && d != null ? SleeveRollPreview.Result(d.gameObject) : null;
                string text = "", warning = "";
                if (on && !nadena.dev.ndmf.preview.NDMFPreviewPrefs.instance.EnablePreview)
                    warning = T(lang, "NDMF previews are off — turn on Tools > NDM Framework > Enable Previews.",
                        "NDMF のプレビューがオフです — Tools > NDM Framework > Enable Previews をオンに。",
                        "NDMF 미리 보기가 꺼져 있습니다 — Tools > NDM Framework > Enable Previews를 켜세요.");
                else if (rec != null)
                {
                    var mine = Cfg.Garments;
                    float l = -1f, r = -1f;
                    foreach (var g in rec.garments)
                    {
                        if (!mine.Any(x => x != null && x.name == g.name)) continue;
                        if (g.left.on && g.left.ok) l = Mathf.Max(l, g.left.rolledTo);
                        if (g.right.on && g.right.ok) r = Mathf.Max(r, g.right.rolledTo);
                    }
                    string At(float v) => v >= 0f ? (1f - v).ToString("0.00") : "—";
                    if (l >= 0f || r >= 0f)
                        text = T(lang, $"Previewing: cuff at L {At(l)} / R {At(r)}. Scene view only — the wrist bone and the menu come with the build.",
                            $"プレビュー中：袖口 左 {At(l)} / 右 {At(r)}。Scene ビューのみ — 手首ボーンとメニューはビルドで入ります。",
                            $"미리 보는 중: 소맷단 왼쪽 {At(l)} / 오른쪽 {At(r)}. Scene 뷰에만 보입니다 — 손목 본과 메뉴는 빌드 때 들어갑니다.");
                    if (rec.warnings.Count > 0) warning = string.Join("\n", rec.warnings);
                }
                result.text = text; warn.text = warning;
                Display(result, text.Length > 0);
                Display(warn, warning.Length > 0);
            });
        }

        // ---------------------------------------------------------------- 04 fit
        // stored in metres (the build's unit), shown in mm — a 0.0040 is hard to read, 4.0 mm isn't
        VisualElement FitSection()
        {
            var sec = Section(Num(), T(lang, "ROLLED SHAPE", "まくった袖の形", "걷힌 소매 모양"), null);
            sec.Add(Label(T(lang, "What the rolled-up cloth looks like on the arm.",
                "まくり上げた布が腕の上でどんな形になるかを決めます。",
                "걷혀 올라간 천이 팔 위에서 어떤 모양이 될지 정합니다."), "sr-note sr-note--gap"));
            var grid = Box("sr-rows");
            sec.Add(grid);
            Put(grid, GaugeRow("margin", T(lang, "Gap to skin", "肌とのすき間", "피부와 간격"),
                "Margin — " + T(lang, "How far the rolled cloth stays off the skin (mm). Raise it if skin pokes through the rolled part.",
                    "まくった布を肌からどれだけ離すか（mm）。まくった所から肌が突き抜けるなら上げます。",
                    "걷힌 천을 피부에서 얼마나 띄울지(mm). 걷힌 데로 피부가 뚫고 나오면 올립니다."),
                0f, 0.02f, 0.004f, 0.0005f, "0.0", 1000f,
                lo: T(lang, "Close", "近い", "붙임"), hi: T(lang, "Far", "遠い", "띄움"), unit: "mm"),
                T(lang, "How far the rolled cloth stays off the skin. Raise it if skin pokes through.",
                    "まくった布を肌から離す距離。肌が突き抜けるなら上げます。",
                    "걷힌 천을 피부에서 띄우는 거리입니다. 천 사이로 피부가 뚫고 나오면 올리세요."));
            Put(grid, GaugeRow("looseness", T(lang, "Sleeve width", "袖の太さ", "소매 굵기"),
                "Looseness — " + T(lang, "How much of the sleeve's slack is left after rolling — 0 hugs the arm, 1 keeps the sleeve's own width.",
                    "まくった後に残す袖のゆとり — 0 で腕にぴったり、1 で袖の元の太さ。",
                    "걷은 뒤 남길 소매 여유 — 0이면 팔에 딱 붙고, 1이면 소매 원래 굵기."),
                0f, 1f, 0.35f, 0.05f, "0.00",
                lo: T(lang, "Snug", "ぴったり", "딱 붙음"), hi: T(lang, "Original", "元の太さ", "원래 굵기")),
                T(lang, "How wide the sleeve stays after rolling. Adjust it if it looks too bulky or too thin.",
                    "まくった後の袖の太さ。太すぎる・細すぎるときに調整します。",
                    "걷은 뒤 남는 소매 굵기입니다. 너무 두껍거나 얇아 보이면 조절하세요."));
            Put(grid, GaugeRow("foldAmplitude", T(lang, "Fold depth", "しわの深さ", "주름 깊이"),
                "Fold Amplitude — " + T(lang, "How deep the bunched folds are (mm). 0 = smooth.",
                    "たまったしわの深さ（mm）。0 でなめらか。",
                    "뭉친 주름의 깊이(mm). 0이면 매끈."),
                0f, 0.015f, 0.004f, 0.0005f, "0.0", 1000f,
                lo: T(lang, "Smooth", "なめらか", "매끈"), hi: T(lang, "Deep", "深い", "깊게"), unit: "mm"),
                T(lang, "How deep the folds in the bunched cloth are.",
                    "たまった布のしわの深さ。",
                    "뭉친 천에 생기는 주름의 깊이입니다."));
            Put(grid, GaugeRow("foldLength", T(lang, "Fold spacing", "しわの間隔", "주름 간격"),
                "Fold Length — " + T(lang, "The spacing of the folds on the rolled sleeve (mm).",
                    "まくった袖のしわの間隔（mm）。",
                    "걷힌 소매의 주름 간격(mm)."),
                0.01f, 0.1f, 0.045f, 0.005f, "0", 1000f,
                lo: T(lang, "Fine", "細かい", "촘촘"), hi: T(lang, "Wide", "広い", "넓게"), unit: "mm"),
                T(lang, "The distance between folds. Smaller means more, finer folds.",
                    "しわの間隔。小さいほど細かく多くなります。",
                    "주름 사이 거리입니다. 작을수록 잘고 많아집니다."));
            Put(grid, GaugeRow("startAt", T(lang, "Starts at", "動き始め", "움직임 시작"),
                "Start At — " + T(lang, "On the elbow side of this point (1 = elbow) the sleeve does not move. The faint ring in the Scene view.",
                    "これより肘側（1 = 肘）の袖は動きません。Scene ビューの薄い輪。",
                    "이보다 팔꿈치 쪽(1 = 팔꿈치) 소매는 움직이지 않습니다. Scene 뷰의 흐린 고리."),
                0f, 1f, 0.25f, 0.01f, "0.00",
                lo: T(lang, "Wrist side", "手首側", "손목 쪽"), hi: T(lang, "Elbow side", "肘側", "팔꿈치 쪽"), flip: true),
                T(lang, "For fine-tuning. The sleeve above this point doesn't move (faint ring in the Scene view). It changes the rolled shape, so give it a try with Preview on. Too close to the elbow, the sleeve looks shrunk rather than rolled, and on some outfits skin can poke through when the elbow bends, so be sure to check in-game.",
                    "細かい調整用です。これより肘側の袖は動きません（Scene ビューの薄い輪）。まくった形が変わるので、プレビューをオンにして一度動かしてみてください。肘に近づけすぎると、まくったというより袖が縮んだように見え、衣装によっては肘を曲げたときに肌が突き抜けることがあるので、インゲームで必ず確認してください。",
                    "세부 조절용입니다. 이보다 팔꿈치 쪽 소매는 움직이지 않습니다(Scene 뷰의 흐린 고리). 걷힌 모양이 달라지니 미리 보기를 켜고 한번 움직여 보세요. 팔꿈치에 너무 가까우면 걷었다기보다 소매가 줄어든 것처럼 보이고, 옷에 따라 팔꿈치를 굽힐 때 피부가 뚫릴 수 있으니 인게임에서 꼭 확인하세요."));
            // past the cuff it can't apply: the bake starts just above the cuff instead (t0 = min(startAt, rollTo - 0.05))
            var startPast = Label(T(lang, "This is at or past Cuff position, so the sleeve starts moving just above the cuff instead.",
                "「袖口の位置」と同じか、それより手首側です。袖は袖口のすぐ上から動き始めます。",
                "'소맷단 위치'와 같거나 더 손목 쪽입니다. 소매는 소맷단 바로 위에서 움직이기 시작합니다."), "sr-note sr-note--gold sr-note--gap");
            grid.Add(startPast);
            Live(() => Cfg.startAt > Cfg.rollTo - 0.05f ? 1 : 0, () => Display(startPast, Cfg.startAt > Cfg.rollTo - 0.05f));
            sec.Add(Label(T(lang, "Double-click a gauge to reset it.",
                "ゲージをダブルクリックで初期値に。",
                "게이지를 두 번 누르면 기본값으로 돌아갑니다."), "sr-note"));
            return sec;
        }

        // ---------------------------------------------------------------- 05 advanced (fold)
        VisualElement AdvancedSection()
        {
            var sec = Section(Num(), T(lang, "ADVANCED", "詳細", "고급"), null);
            sec.Add(Label(T(lang, "Usually fine as is. Open it to change how the cuff follows the wrist, or if the build can't find the skin.",
                "通常はこのままで大丈夫です。手首を曲げたときの袖口の動きを変えたいとき、ビルドが肌を見つけられないときに開きます。",
                "보통은 그대로 두면 됩니다. 손목을 꺾을 때 소맷단 움직임을 바꾸고 싶을 때, 빌드가 피부를 못 찾을 때만 펼치세요."), "sr-note sr-note--gap"));
            var content = Box("sr-rows");
            Put(content, Row(T(lang, "Wrist", "手首", "손목"),
                "Wrist Proxy — " + T(lang, "Moves the rolled cloth's hand weights to a helper bone that follows the hand only while the sleeve is down — the rolled cuff stays put when the wrist bends. Off: the cuff bends with the wrist.",
                    "まくった布の手のウェイトを、袖を下ろしている間だけ手に従う補助ボーンへ移します — 手首を曲げてもまくった袖口はそのまま。オフ：袖口が手首と一緒に曲がります。",
                    "걷힌 천의 손 웨이트를 소매를 내렸을 때만 손을 따르는 보조 본으로 옮깁니다 — 손목을 꺾어도 걷힌 소맷단은 제자리. 끄면 소맷단이 손목을 따라 꺾입니다."),
                Check("wristProxy", T(lang, "Keep the rolled cuff still", "まくった袖口を固定", "걷힌 소맷단 고정"))),
                T(lang, "On: the rolled cuff stays put when the wrist bends. Off: it bends with the hand.",
                    "オン：手首を曲げてもまくった袖口はそのまま。オフ：手と一緒に曲がります。",
                    "켜면 손목을 꺾어도 걷힌 소맷단이 제자리에 있습니다. 끄면 손을 따라 꺾입니다."));
            var skins = new PropertyField(serializedObject.FindProperty("bodyMeshes"),
                T(lang, "Body meshes (empty = auto)", "体のメッシュ（空 = 自動）", "몸 메시(비우면 자동)"));
            skins.AddToClassList("sr-raw");
            skins.tooltip = "Body Meshes — " + T(lang, "The meshes the skin is measured on. Empty: every other skinned mesh with forearm weights. Set it only when the build says no skin was found.",
                "肌を測るメッシュ。空なら前腕のウェイトがあるほかのスキンメッシュすべて。ビルドが肌を見つけられないときだけ入れます。",
                "피부를 잴 메시. 비우면 아래팔 웨이트가 있는 다른 스킨드 메시 전부. 빌드가 피부를 못 찾았다고 할 때만 넣습니다.");
            Put(content, skins, T(lang, "The mesh used to measure the skin, so the rolled cloth doesn't sink into the arm. Empty finds it automatically — add your body mesh only if the build says no skin was found.",
                "布が腕に埋まらないよう、肌を測るメッシュ。空なら自動で探します。ビルドが肌を見つけられないと言うときだけ体のメッシュを入れます。",
                "걷힌 천이 팔에 박히지 않게 피부를 잴 메시입니다. 비우면 자동으로 찾습니다. 빌드가 피부를 못 찾았다고 할 때만 몸 메시를 넣으세요."));
            sec.Add(Fold("Pyrite9SR.ShowAdvanced", false, T(lang, "SHOW", "表示", "펼치기"), content));
            return sec;
        }
    }
}
