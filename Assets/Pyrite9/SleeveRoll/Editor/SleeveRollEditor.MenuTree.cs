// Pyrite9 Sleeve Roll — inspector: the avatar's Expression Menu as a tree under Install to (02 menu). Click a menu to install the
// toggle there, the chevron folds it. The tree is what Modular Avatar builds from assets: the descriptor's menu and its submenus,
// and under each menu what MA installers append to it (their menu's controls go into the target, so its submenus show there).
// An appended menu is an alias of the menu it goes into: installing into it lands there (MA 1.18, checked by build 2026-11-08),
// so it selects that row. MA Menu Item submenus have no menu asset and can't be picked.
// The count is the menu's items before the build (8 = full: MA moves the 8th item on into a 'More' submenu) — about, since
// what other tools add at build isn't known here. Last build shows where the toggle really went.
// The tree is cached: the poll only adds up Unity's dirty counts of the descriptor, the menus and the installers it was built from
// (an edited menu, a moved installer target) — rebuilt when that, the hierarchy (an installer added) or the language changes
using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Pyrite9.Sleeve
{
    public partial class SleeveRollEditor
    {
        sealed class MenuNode
        {
            public VRCExpressionsMenu menu;   // null only for an avatar without a root menu (MA makes one)
            public string label;
            public int count;
            public int depth;
            public MenuNode parent;
            public readonly List<VRCExpressionsMenu> alias = new List<VRCExpressionsMenu>();   // menus MA installers append into this one
            public readonly List<MenuNode> kids = new List<MenuNode>();
        }

        MenuNode menuTree;
        int menuTreeKey = int.MinValue;
        readonly List<Object> menuTreeObjs = new List<Object>();   // what the tree was built from — their dirty counts are its key
        readonly HashSet<VRCExpressionsMenu> menuOpen = new HashSet<VRCExpressionsMenu>();
        VRCExpressionsMenu menuOpenFor;   // the selection the open path was last widened for

        int MenuTreeKey()
        {
            var d = Desc;
            int k = unchecked(((hierVersion * 31 + lang) * 31) + (d != null ? d.GetInstanceID() : 0));
            foreach (var o in menuTreeObjs) k = unchecked(k * 31 + (o != null ? UnityEditor.EditorUtility.GetDirtyCount(o) : -1));
            return k;
        }

        // the tree, rebuilt only when something it was built from changed
        MenuNode MenuTreeNow()
        {
            if (MenuTreeKey() == menuTreeKey) return menuTree;
            menuTreeObjs.Clear();
            menuTree = BuildMenuTree();
            menuTreeKey = MenuTreeKey();
            return menuTree;
        }

        MenuNode BuildMenuTree()
        {
            var d = Desc;
            if (d == null) return null;
            menuTreeObjs.Add(d);
            var appendTo = new Dictionary<VRCExpressionsMenu, List<ModularAvatarMenuInstaller>>();
            var toRoot = new List<ModularAvatarMenuInstaller>();
            foreach (var i in d.GetComponentsInChildren<ModularAvatarMenuInstaller>(true))
            {
                if (i == null) continue;
                menuTreeObjs.Add(i);
                var t = i.installTargetMenu;
                if (t == null || t == d.expressionsMenu) toRoot.Add(i);
                else
                {
                    if (!appendTo.TryGetValue(t, out var l)) appendTo[t] = l = new List<ModularAvatarMenuInstaller>();
                    l.Add(i);
                }
            }
            var root = new MenuNode { menu = d.expressionsMenu, label = T(lang, "Root", "ルート", "루트") };
            var seen = new HashSet<VRCExpressionsMenu>();
            if (root.menu != null) seen.Add(root.menu);
            // breadth first, so a menu used twice shows where the radial menu reaches it first
            var queue = new Queue<MenuNode>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                var sources = new List<VRCExpressionsMenu>();
                if (n.menu != null) sources.Add(n.menu);
                // installers into this menu, and into the menus they append here (those land here too)
                for (int s = 0; s <= sources.Count; s++)
                {
                    var key = s == 0 ? null : sources[s - 1];
                    if (s == 0 && n != root) continue;
                    var inst = s == 0 ? toRoot : appendTo.TryGetValue(key, out var l) ? l : null;
                    if (inst == null) continue;
                    foreach (var i in inst)
                    {
                        if (i.menuToAppend == null) n.count += MenuItemCount(i);
                        else if (!sources.Contains(i.menuToAppend))
                        {
                            sources.Add(i.menuToAppend);
                            n.alias.Add(i.menuToAppend);
                        }
                    }
                }
                foreach (var s in sources)
                {
                    menuTreeObjs.Add(s);
                    if (s.controls == null) continue;
                    foreach (var c in s.controls)
                    {
                        if (c == null) continue;
                        n.count++;
                        if (c.type != VRCExpressionsMenu.Control.ControlType.SubMenu || c.subMenu == null || !seen.Add(c.subMenu)) continue;
                        var k = new MenuNode { menu = c.subMenu, label = string.IsNullOrWhiteSpace(c.name) ? c.subMenu.name : c.name, depth = n.depth + 1, parent = n };
                        n.kids.Add(k);
                        queue.Enqueue(k);
                    }
                }
            }
            return root;
        }

        // an installer without a menu asset installs its MA Menu Item objects: the item itself, or a group's items
        static int MenuItemCount(ModularAvatarMenuInstaller i)
        {
            if (i.GetComponent<ModularAvatarMenuItem>() != null) return 1;
            if (i.GetComponent<ModularAvatarMenuGroup>() == null) return 0;
            int n = 0;
            foreach (Transform t in i.transform) if (t.GetComponent<ModularAvatarMenuItem>() != null) n++;
            return n;
        }

        static MenuNode FindNode(MenuNode n, VRCExpressionsMenu m)
        {
            if (n == null || m == null) return null;
            if (n.menu == m || n.alias.Contains(m)) return n;
            foreach (var k in n.kids) { var f = FindNode(k, m); if (f != null) return f; }
            return null;
        }

        // is the menu one MA can install into (before the build — Last build checks the real result)
        bool MenuReachable(VRCExpressionsMenu target)
        {
            if (target == null) return true;
            var t = MenuTreeNow();
            return t != null && FindNode(t, target) != null;
        }

        // the tree under Install to — rebuilt when the avatar's menus, the selection or the folds change
        VisualElement MenuTreeBox()
        {
            var box = Box("sr-mtree");
            System.Action draw = null;
            draw = () =>
            {
                box.Clear();
                var t = menuTree;
                if (t == null)
                {
                    box.Add(Label(T(lang, "Put this under an avatar to see its menu.", "アバターの中に置くとメニューが表示されます。", "아바타 안에 두면 메뉴가 보입니다."), "sr-note"));
                    return;
                }
                var sel = Cfg.installTo;
                bool rootSel = sel == null || sel == t.menu;
                var selNode = rootSel ? t : FindNode(t, sel);
                // open the path to the selection once per new selection — a fold the user closed afterwards stays closed
                if (sel != menuOpenFor)
                {
                    menuOpenFor = sel;
                    for (var p = selNode != null ? selNode.parent : null; p != null; p = p.parent) if (p.menu != null) menuOpen.Add(p.menu);
                }
                void Add(MenuNode n)
                {
                    bool open = n == t || (n.menu != null && menuOpen.Contains(n.menu));
                    box.Add(NodeRow(n, n == selNode, open, n == t, draw));
                    if (open) foreach (var k in n.kids) Add(k);
                }
                Add(t);
            };
            Live(() =>
            {
                MenuTreeNow();
                return unchecked(menuTreeKey * 31 + (Cfg.installTo != null ? Cfg.installTo.GetInstanceID() : 0));
            }, draw);
            return box;
        }

        VisualElement NodeRow(MenuNode n, bool on, bool open, bool isRoot, System.Action redraw)
        {
            var row = Box("sr-mtree__row");
            row.EnableInClassList("sr-mtree__row--on", on);
            row.style.paddingLeft = 4 + n.depth * 16;
            var chev = Label(n.kids.Count == 0 || isRoot ? "" : open ? "▾" : "▸", "sr-mtree__chev");
            if (n.kids.Count > 0 && !isRoot)
                chev.RegisterCallback<ClickEvent>(e =>
                {
                    if (!menuOpen.Remove(n.menu)) menuOpen.Add(n.menu);
                    e.StopPropagation();
                    redraw();
                });
            row.Add(chev);
            row.Add(Label((on ? "✓ " : "") + n.label, "sr-mtree__name"));
            if (isRoot && n.menu != null) row.Add(Label(n.menu.name, "sr-mtree__asset"));
            bool full = n.count >= VRCExpressionsMenu.MAX_CONTROLS;
            var cnt = Label(full ? $"{n.count}/{VRCExpressionsMenu.MAX_CONTROLS} → More" : $"{n.count}/{VRCExpressionsMenu.MAX_CONTROLS}", "sr-mtree__count");
            cnt.EnableInClassList("sr-mtree__count--full", full);
            row.Add(cnt);
            row.tooltip = (n.menu != null ? UnityEditor.AssetDatabase.GetAssetPath(n.menu) + "\n" : "")
                + T(lang, $"{n.count} items before the build (about — tools that add items at build aren't counted).",
                    $"ビルド前の項目数 {n.count}（目安 — ビルド時に他のツールが足す項目は含みません）。",
                    $"빌드 전 항목 {n.count}개(대략 — 빌드 때 다른 도구가 넣는 항목은 안 셉니다).")
                + (full ? T(lang, "\nFull: Modular Avatar moves the overflow into a 'More' submenu at the end — the toggle ends up there.",
                    "\n満杯：Modular Avatar があふれた分を末尾の「More」サブメニューに移します — トグルはそこに入ります。",
                    "\n가득 참: Modular Avatar가 넘친 항목을 끝의 'More' 하위 메뉴로 옮깁니다 — 토글은 거기 들어갑니다.") : "")
                + (on ? "" : T(lang, "\nClick to install the toggle here.", "\nクリックでここにトグルを入れます。", "\n누르면 여기에 토글을 넣습니다."));
            row.RegisterCallback<ClickEvent>(_ =>
            {
                var p = serializedObject.FindProperty("installTo");
                p.objectReferenceValue = isRoot ? null : n.menu;
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                redraw();
            });
            return row;
        }
    }
}
