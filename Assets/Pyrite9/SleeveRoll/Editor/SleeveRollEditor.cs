// Pyrite9 Sleeve Roll — SleeveRoll inspector (UI Toolkit, design 'Assay' — _common/inspector-assay.md, after Orbit Ring's OrbitRingEditor)
//   about + features (fold) · 01 basics (menu language, the garment and its candidates, sleeves) · 02 menu (toggle, its name,
//   synced bit) · 03 roll (stop at the hidden arm, bring it back while rolled, roll to) · 04 fit (gap, looseness, folds) ·
//   05 advanced (fold — start, wrist proxy, skin meshes) · 06 last build (SrBuildRecord) · 07 changelog (fold — this version's
//   line, read from the README). Fold state in EditorPrefs (Pyrite9SR.). Text follows the menu language. Tooltips start with
//   the English name the README uses.
//   Style: SleeveRollInspector.uss next to this script — without it everything still works (Unity's default look).
//   Refreshing: button groups, gauges, checks, the garment picker and the rows that show or hide with a setting refresh in place
//   every 300 ms (rebuilding would drop a gauge drag or a text field's focus), and only do work when what they show changed (Live).
//   Only the menu language, the avatar (its Sleeve Rolls, which one owns the menu, its skinned meshes) and a new build record
//   rebuild; the update check only redraws the version box. Avatar lookups are cached until the hierarchy changes (.Scene). The poll
//   reads no files and builds no text (only the avatar's name is read). Fit values are stored in metres and shown in mm.
//   Scene view: a ring on each forearm where the cuff ends up, and a faint one where the sleeve starts to move. 03 roll also has
//   the Preview switch: the rolled sleeves in the Scene view without a build (SleeveRollPreview).
//   Files: this one (lifecycle, poll, paths) · .Header · .Settings (the setting sections) · .Scene (the avatar, candidates,
//   scene view) · .Record (last build, changelog) · .Controls (shared pieces)
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pyrite9.Sleeve
{
    [CustomEditor(typeof(SleeveRoll))]
    public partial class SleeveRollEditor : UnityEditor.Editor
    {
        static readonly Color Gold = new Color32(0xE2, 0xCE, 0x74, 0xFF);

        VisualElement root;
        int lang = -1;
        (int lang, int record, int avatar, int self, int owner, int meshes) key;
        int secNo;
        readonly List<System.Action> live = new List<System.Action>();
        string Num() => (++secNo).ToString("00");

        // ---------------------------------------------------------------- text (English / Japanese / Korean = SleeveMenuLanguage order)
        static string T(int l, string en, string jp, string ko) => l == 1 ? jp : l == 2 ? ko : en;

        SleeveRoll Cfg => (SleeveRoll)target;

        public override VisualElement CreateInspectorGUI()
        {
            root = Box("sr-root");
            var dir = EditorDir();
            var uss = dir != null ? AssetDatabase.LoadAssetAtPath<StyleSheet>(dir + "/SleeveRollInspector.uss") : null;
            if (uss != null) root.styleSheets.Add(uss);
            Build();
            root.schedule.Execute(Poll).Every(300);
            root.schedule.Execute(RepaintRingsIfChanged).Every(50);   // the rings follow a gauge drag at once, not on the 300 ms poll
            System.Action onChange = () => { if (root.panel != null) FillVersionBox(); };
            SrUpdateCheck.Changed += onChange;
            root.RegisterCallback<DetachFromPanelEvent>(_ => SrUpdateCheck.Changed -= onChange);
            SrUpdateCheck.CheckIfDue();
            return root;
        }

        // rebuild only for what changes the layout; everything else (the garment block too) refreshes in place.
        // Which avatar (and its name — the build record goes by it), where this one is among its Sleeve Rolls, which one owns the menu, how many skinned meshes (a garment added or
        // removed changes the candidates). Picking a garment does not rebuild — the garment block refreshes itself
        (int, int, int, int, int, int) Key()
        {
            var d = Desc;
            if (d == null) return ((int)Cfg.menuLanguage, SrBuildRecord.Revision, 0, -1, -1, -1);
            return ((int)Cfg.menuLanguage, SrBuildRecord.Revision, unchecked(d.GetInstanceID() * 31 + d.gameObject.name.GetHashCode()), System.Array.IndexOf(cSibs, Cfg), System.Array.IndexOf(cSibs, MenuOwner()), cSmrs.Length);
        }

        void Poll()
        {
            if (target == null) return;
            serializedObject.UpdateIfRequiredOrScript();
            if (Key() != key) { Build(); return; }
            foreach (var a in live) a();
        }

        // a refresh that does its work only when 'state' (a cheap hash of what it shows) changes — the text and lists in it aren't made on every poll
        void Live(System.Func<int> state, System.Action refresh)
        {
            int shown = 0;
            bool first = true;
            live.Add(() =>
            {
                int k = state();
                if (!first && k == shown) return;
                first = false;
                shown = k;
                refresh();
            });
        }

        void Build()
        {
            serializedObject.Update();
            lang = (int)Cfg.menuLanguage;
            key = Key();
            ReadRecord();
            secNo = 0;
            live.Clear();
            root.Clear();
            root.Add(Header());
            var body = Box("sr-body");
            root.Add(body);
            body.Add(AboutSection());
            foreach (var s in new System.Func<VisualElement>[]
                     { BasicSection, MenuSection, RollSection, FitSection, AdvancedSection, BuildSection, ChangelogSection })
            {
                body.Add(Box("sr-divider"));
                body.Add(s());
            }
            root.Bind(serializedObject);
            foreach (var a in live) a();
        }

        // ---------------------------------------------------------------- paths
        string editorDir;

        string EditorDir()
        {
            if (editorDir != null) return editorDir;
            var script = MonoScript.FromScriptableObject(this);
            return editorDir = script != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(script)) : null;
        }

        string ReadmePath()
        {
            var d = EditorDir();
            var pkg = d != null ? Path.GetDirectoryName(d) : null;
            if (pkg == null) return null;
            string name = lang == 1 ? "README_jp.md" : lang == 2 ? "README_ko.md" : "README_en.md";
            var p = Path.GetFullPath(Path.Combine(pkg, name));
            return File.Exists(p) ? p : null;
        }
    }
}
