// Pyrite9 Sleeve Roll — inspector: shared Assay pieces (_common/inspector-assay.md 4절), copied from Orbit Ring
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pyrite9.Sleeve
{
    public partial class SleeveRollEditor
    {
        // fold: click the head (small title + chevron), state in EditorPrefs
        static VisualElement Fold(string prefKey, bool defaultOpen, string title, VisualElement content)
        {
            var box = Box("sr-fold");
            var head = Box("sr-fold__head");
            head.Add(Label(title, "sr-fold__title"));
            var chev = Box("sr-fold__chevron");
            bool open = EditorPrefs.GetBool(prefKey, defaultOpen);
            chev.generateVisualContent += mgc =>
            {
                var p = mgc.painter2D;
                p.strokeColor = new Color32(0x7D, 0x83, 0x91, 0xFF); p.lineWidth = 1.5f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
                p.BeginPath();
                if (open) { p.MoveTo(new Vector2(2f, 4f)); p.LineTo(new Vector2(5f, 7f)); p.LineTo(new Vector2(8f, 4f)); }
                else { p.MoveTo(new Vector2(4f, 2f)); p.LineTo(new Vector2(7f, 5f)); p.LineTo(new Vector2(4f, 8f)); }
                p.Stroke();
            };
            head.Add(chev);
            box.Add(head);
            box.Add(content);
            Display(content, open);
            head.RegisterCallback<ClickEvent>(_ =>
            {
                open = !open;
                EditorPrefs.SetBool(prefKey, open);
                Display(content, open);
                chev.MarkDirtyRepaint();
            });
            return box;
        }

        // ---------------------------------------------------------------- shared pieces
        // check + text. The checkmark is drawn by hand (Unity's default image covers the gold fill)
        static VisualElement Check(string path, string text)
        {
            var box = Box("sr-check");
            var tg = new Toggle { bindingPath = path };
            tg.AddToClassList("sr-toggle");
            var mark = tg.Q(className: "unity-toggle__checkmark");
            if (mark != null)
            {
                mark.generateVisualContent += mgc =>
                {
                    if (!tg.value) return;
                    var p = mgc.painter2D;
                    p.strokeColor = new Color32(0x15, 0x17, 0x1C, 0xFF);
                    p.lineWidth = 2f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
                    p.BeginPath();
                    p.MoveTo(new Vector2(3.5f, 7.5f)); p.LineTo(new Vector2(6.5f, 10.5f)); p.LineTo(new Vector2(11.5f, 4.5f));
                    p.Stroke();
                };
                tg.RegisterValueChangedCallback(_ => mark.MarkDirtyRepaint());
            }
            box.Add(tg);
            box.Add(Label(text, "sr-check__text"));
            return box;
        }

        // gauge + value: fills from the left. Drag the gauge to change (snapped to step), double-click resets to def; gold when not at def.
        // shown ≠ 1: the value field shows value × shown (metres as mm) and writes back ÷ shown — min, max, def and step stay in the
        // stored unit. That field is not bound: it follows the property in Show() (not while it is being typed in).
        // A typed number may go past the gauge's ends (the gauge only covers the usual range); the component's OnValidate keeps it sane
        VisualElement GaugeRow(string path, string label, string tip, float min, float max, float def, float step, string format, float shown = 1f, string lo = null, string hi = null, string unit = null, bool flip = false, float warnAt = float.NaN, float dangerAt = float.NaN)
        {
            var prop = serializedObject.FindProperty(path);
            // two lines: name + value on top, the bar below with what each end means written beside it
            var row = Box("sr-offset");
            row.tooltip = tip;
            var head = Box("sr-offset__head");
            row.Add(head);
            head.Add(Label(label, "sr-label"));
            var bar = Box("sr-offset__bar");
            row.Add(bar);
            if (lo != null) bar.Add(Label(lo, "sr-offset__end"));

            var gauge = Box("sr-gauge");
            gauge.Add(Box("sr-gauge__track"));
            var fill = Box("sr-gauge__fill"); gauge.Add(fill);
            // warnAt / dangerAt (stored values): the yellow and orange stretches of the bar, from there to the far end
            bool zones = !float.IsNaN(warnAt);
            float Frac(float v) { float x = (Mathf.Clamp(v, min, max) - min) / (max - min); return flip ? 1f - x : x; }
            if (zones)
            {
                void Zone(float from, float to, string cls)
                {
                    var z = Box("sr-gauge__zone");
                    z.AddToClassList(cls);
                    z.pickingMode = PickingMode.Ignore;
                    z.style.left = Length.Percent(from * 100f);
                    z.style.width = Length.Percent((to - from) * 100f);
                    gauge.Add(z);
                }
                Zone(Frac(warnAt), Frac(dangerAt), "sr-gauge__zone--warn");
                Zone(Frac(dangerAt), 1f, "sr-gauge__zone--danger");
            }
            var knob = Box("sr-gauge__knob"); gauge.Add(knob);
            fill.style.left = 0f;
            bar.Add(gauge);
            if (hi != null) bar.Add(Label(hi, "sr-offset__end"));

            // flip: stored along the forearm (0 elbow ~ 1 wrist), shown as how far it rolls (0 wrist ~ 1 elbow) — bigger and further right = more rolled
            bool scaled = flip || !Mathf.Approximately(shown, 1f);
            float Disp(float v) => flip ? 1f - v : v * shown;
            float Stored(float x) => flip ? 1f - x : x / shown;
            var field = scaled ? new FloatField { formatString = format } : new FloatField { bindingPath = path, formatString = format };
            field.AddToClassList("sr-value");
            head.Add(field);
            if (unit != null) head.Add(Label(unit, "sr-offset__unit"));

            float drawn = float.NaN;   // only redraw when the value changed (the poll asks every 300 ms)
            bool typing = false;
            void Show()
            {
                float v = prop.floatValue;
                if (v == drawn) return;
                drawn = v;
                float t = (Mathf.Clamp(v, min, max) - min) / (max - min);
                if (flip) t = 1f - t;
                fill.style.width = Length.Percent(t * 100f);
                knob.style.left = Length.Percent(t * 100f);
                row.EnableInClassList("sr-offset--on", Mathf.Abs(v - def) > step * 0.5f);
                if (zones) row.EnableInClassList("sr-offset--danger", Frac(v) >= Frac(dangerAt) - 1e-4f);
                if (scaled && !typing) field.SetValueWithoutNotify(Disp(v));
            }
            Show();
            live.Add(Show);
            if (scaled)
            {
                field.RegisterCallback<FocusInEvent>(_ => typing = true);
                // what was typed may have been clamped back to the value it had — draw it again
                field.RegisterCallback<FocusOutEvent>(_ => { typing = false; drawn = float.NaN; Show(); });
                field.RegisterValueChangedCallback(e =>
                {
                    float v = Stored(e.newValue);
                    if (Mathf.Approximately(prop.floatValue, v)) return;
                    prop.floatValue = v;
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();   // OnValidate may have clamped it
                });
            }
            field.RegisterCallback<ChangeEvent<float>>(_ => Show());

            void Set(float v)
            {
                v = Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
                if (Mathf.Approximately(prop.floatValue, v)) return;
                prop.floatValue = v;
                serializedObject.ApplyModifiedProperties();
                Show();
            }
            bool drag = false;
            void SetFrom(Vector2 local)
            {
                float w = gauge.contentRect.width;
                if (w < 1f) return;
                float x = Mathf.Clamp01(local.x / w);
                Set(Mathf.Lerp(min, max, flip ? 1f - x : x));
            }
            gauge.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                if (e.clickCount == 2) { Set(def); return; }
                drag = true; gauge.CapturePointer(e.pointerId); SetFrom(e.localPosition);
            });
            gauge.RegisterCallback<PointerMoveEvent>(e => { if (drag) SetFrom(e.localPosition); });
            gauge.RegisterCallback<PointerUpEvent>(e => { drag = false; gauge.ReleasePointer(e.pointerId); });
            return row;
        }

        // button group bound to an int/enum property; the 'on' item refreshes in place (Poll)
        VisualElement Seg(string path, int[] values, string[] names)
        {
            var prop = serializedObject.FindProperty(path);
            var seg = Box("sr-seg");
            var items = new List<(Button b, int v)>();
            void Refresh()
            {
                foreach (var (b, v) in items) b.EnableInClassList("sr-seg__item--on", v == prop.intValue);
            }
            for (int i = 0; i < values.Length; i++)
            {
                int v = values[i];
                var b = new Button(() =>
                {
                    prop.intValue = v;
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();
                    Refresh();
                }) { text = names[i] };
                b.AddToClassList("sr-seg__item");
                if (i == 0) b.AddToClassList("sr-seg__item--first");
                seg.Add(b);
                items.Add((b, v));
            }
            Refresh();
            live.Add(Refresh);
            return seg;
        }

        // warning: orange box with a drawn warning sign (triangle + '!') — has to read as a warning at a glance.
        // okKey: an OK button folds the box to one dim line, remembered in EditorPrefs; the folded line's button opens it again. null = no button
        // hint: a short line under the text in the menu language (what to do) — the warning itself stays English, as in the build log
        VisualElement Warn(string text, string okKey = null, string hint = null)
        {
            var box = Box("sr-warn");
            var icon = Box("sr-warn__icon");
            icon.generateVisualContent += mgc =>
            {
                var p = mgc.painter2D;
                p.fillColor = new Color32(0xFF, 0x8A, 0x4C, 0xFF);
                p.BeginPath(); p.MoveTo(new Vector2(9f, 1f)); p.LineTo(new Vector2(17.5f, 16.5f)); p.LineTo(new Vector2(0.5f, 16.5f)); p.ClosePath(); p.Fill();
                p.strokeColor = new Color32(0x3A, 0x1F, 0x14, 0xFF); p.lineWidth = 2f; p.lineCap = LineCap.Round;
                p.BeginPath(); p.MoveTo(new Vector2(9f, 6.5f)); p.LineTo(new Vector2(9f, 11f)); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(9f, 13.8f)); p.LineTo(new Vector2(9f, 14f)); p.Stroke();
            };
            box.Add(icon);
            var label = Label(text, "sr-warn__text");
            if (hint == null) box.Add(label);
            else
            {
                var col = Box("sr-warn__body");
                col.Add(label);
                col.Add(Label(hint, "sr-warn__hint"));
                box.Add(col);
            }
            if (okKey == null) return box;

            string pref = "Pyrite9SR.WarnOk." + okKey;
            var btn = new Button(); btn.AddToClassList("sr-warn__ok");
            void Show(bool ok)
            {
                box.EnableInClassList("sr-warn--ok", ok);
                label.tooltip = ok ? text + (hint != null ? "\n" + hint : "") : null;   // folded to one line: the whole text on hover
                btn.text = ok ? T(lang, "Show", "開く", "펼치기") : T(lang, "OK", "確認", "확인");
                btn.tooltip = ok ? null : T(lang, "Seen it — fold this warning for this avatar.", "確認済み — このアバターのこの警告をたたみます。", "확인함 — 이 아바타의 이 경고를 접습니다.");
            }
            btn.clicked += () =>
            {
                bool ok = !EditorPrefs.GetBool(pref, false);
                if (ok) EditorPrefs.SetBool(pref, true); else EditorPrefs.DeleteKey(pref);
                Show(ok);
            };
            Show(EditorPrefs.GetBool(pref, false));
            box.Add(btn);
            return box;
        }

        static VisualElement Box(string cls)
        {
            var e = new VisualElement();
            e.AddToClassList(cls);
            return e;
        }

        static void Display(VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        static Label Label(string text, string cls)
        {
            var l = new Label(text);
            if (cls != null) foreach (var k in cls.Split(' ')) l.AddToClassList(k);
            return l;
        }

        static VisualElement Section(string num, string title, string meta)
        {
            var sec = Box("sr-section");
            var head = Box("sr-section__head");
            head.Add(Label(num, "sr-section__num"));
            head.Add(Label(title, "sr-section__title"));
            if (meta != null) head.Add(Label(meta, "sr-section__meta"));
            sec.Add(head);
            return sec;
        }

        // a setting with its one-line explanation always visible under it — the tooltip keeps the long version
        static void Put(VisualElement parent, VisualElement row, string desc)
        {
            parent.Add(row);
            parent.Add(Label(desc, "sr-desc"));
        }

        static VisualElement Row(string label, string tip, VisualElement control)
        {
            var row = Box("sr-row");
            row.tooltip = tip;
            row.Add(Label(label, "sr-label"));
            control.AddToClassList("sr-row__control");
            row.Add(control);
            return row;
        }

        // child row: a setting that only works with the row above it. Indented with a drawn connector from the parent; the caller
        // dims it (SetEnabled) while the parent is off. Wrap the parent row and its children in an 'sr-group' so they sit together
        static VisualElement Child(VisualElement row)
        {
            row.AddToClassList("sr-row--child");
            var link = Box("sr-row__link");
            link.pickingMode = PickingMode.Ignore;
            link.generateVisualContent += mgc =>
            {
                var r = link.contentRect;
                var p = mgc.painter2D;
                p.strokeColor = new Color32(0x5A, 0x60, 0x6C, 0xFF); p.lineWidth = 1.2f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
                p.BeginPath(); p.MoveTo(new Vector2(1.5f, 0f)); p.LineTo(new Vector2(1.5f, r.height - 1f)); p.LineTo(new Vector2(r.width, r.height - 1f)); p.Stroke();
            };
            row.Insert(0, link);
            return row;
        }

        // stacked row: the label above, the control full width below — for a button group too wide beside the label
        static VisualElement Stack(VisualElement row) { row.AddToClassList("sr-row--stack"); return row; }

        // text field bound to a string property (menu name), in the Assay field look
        static VisualElement Text(string path)
        {
            var f = new TextField { bindingPath = path };
            f.AddToClassList("sr-text");
            return f;
        }

        static VisualElement Fact(string label, string value, string valueCls)
        {
            var row = Box("sr-fact");
            row.Add(Label(label, "sr-label"));
            var v = Label(value, "sr-fact__value");
            if (valueCls != null) v.AddToClassList(valueCls);
            row.Add(v);
            return row;
        }
    }
}
