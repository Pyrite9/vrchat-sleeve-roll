// Pyrite9 Sleeve Roll — inspector: header (glyph, version + update check), about + features
using UnityEngine;
using UnityEngine.UIElements;

namespace Pyrite9.Sleeve
{
    public partial class SleeveRollEditor
    {
        // ---------------------------------------------------------------- header
        VisualElement Header()
        {
            var h = Box("sr-header");
            var left = Box("sr-header__left");
            var glyph = Box("sr-glyph");
            glyph.generateVisualContent += DrawGlyph;
            left.Add(glyph);
            var titles = Box("sr-header__titles");
            titles.Add(Label("PYRITE9", "sr-kicker"));
            titles.Add(Label("Sleeve Roll", "sr-title"));
            left.Add(titles);
            h.Add(left);
            versionBox = Box("sr-versionbox");
            FillVersionBox();
            h.Add(versionBox);
            return h;
        }

        VisualElement versionBox;

        // version + new version check (SrUpdateCheck): click the version to check again, click the new-version line for the release page.
        // Until the public mirror has a Release (SrUpdateCheck.Enabled) only the version shows. A check's result redraws only this box
        void FillVersionBox()
        {
            versionBox.Clear();
            var ver = Label("v" + SleeveRollConst.Version, "sr-version");
            versionBox.Add(ver);
            if (!SrUpdateCheck.Enabled) return;
            ver.tooltip = T(lang, "Click to check for updates", "クリックで更新を確認", "누르면 새 버전 확인");
            ver.RegisterCallback<ClickEvent>(_ => SrUpdateCheck.Check());
            Label st = null;
            switch (SrUpdateCheck.Current)
            {
                case SrUpdateCheck.State.Checking: st = Label(T(lang, "checking…", "確認中…", "확인 중…"), "sr-update"); break;
                case SrUpdateCheck.State.Latest: st = Label(T(lang, "up to date", "最新", "최신"), "sr-update"); break;
                case SrUpdateCheck.State.Failed: st = Label(T(lang, "couldn't check", "確認できません", "확인 못 함"), "sr-update"); break;
                case SrUpdateCheck.State.Newer:
                    st = Label(T(lang, SrUpdateCheck.LatestTag + " available", SrUpdateCheck.LatestTag + " があります", SrUpdateCheck.LatestTag + " 있음"), "sr-update");
                    st.AddToClassList("sr-update--new");
                    st.tooltip = SrUpdateCheck.LatestUrl;
                    st.RegisterCallback<ClickEvent>(_ => Application.OpenURL(SrUpdateCheck.LatestUrl));
                    break;
            }
            if (st != null) versionBox.Add(st);
        }

        // a sleeve rolled up a forearm (34x34): the sleeve tube from the left, two bands of rolled cuff, the bare forearm and the hand
        static void DrawGlyph(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            p.strokeColor = Gold; p.fillColor = Gold;
            p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            void Line(float w, params Vector2[] pts)
            {
                p.lineWidth = w;
                p.BeginPath();
                p.MoveTo(pts[0]);
                for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
                p.Stroke();
            }
            void Ellipse(Vector2 c, float rx, float ry, float w)
            {
                p.lineWidth = w;
                p.BeginPath();
                const int n = 24;
                for (int i = 0; i <= n; i++)
                {
                    float a = i / (float)n * Mathf.PI * 2f;
                    var v = c + new Vector2(rx * Mathf.Cos(a), ry * Mathf.Sin(a));
                    if (i == 0) p.MoveTo(v); else p.LineTo(v);
                }
                p.Stroke();
            }
            Line(1.5f, new Vector2(1.5f, 8.5f), new Vector2(13f, 11f));               // sleeve, top
            Line(1.5f, new Vector2(1.5f, 25.5f), new Vector2(13f, 23f));              // sleeve, bottom
            Ellipse(new Vector2(15f, 17f), 2.6f, 6.4f, 1.5f);                          // rolled cuff, two bands
            Ellipse(new Vector2(19f, 17f), 2.2f, 5.4f, 1.2f);
            Line(1.2f, new Vector2(21.5f, 13.4f), new Vector2(27f, 14.6f));           // forearm
            Line(1.2f, new Vector2(21.5f, 20.6f), new Vector2(27f, 19.4f));
            Line(1.5f, new Vector2(27f, 14.6f), new Vector2(31f, 13f), new Vector2(32.5f, 15.5f),
                       new Vector2(32.5f, 18.5f), new Vector2(31f, 21f), new Vector2(27f, 19.4f));   // hand
        }

        // ---------------------------------------------------------------- about + features
        // short inspector text (the README 'Features' section is longer). Change both together.
        static readonly string[][] Features =
        {
            new[] { "An Expression Menu toggle rolls both sleeves. (0.2 s animation) (global)",
                    "Expression Menu のトグルで両袖をまくります。（0.2 秒のアニメーション）（グローバル）",
                    "Expression Menu 토글로 양 소매를 걷습니다. (0.2초 애니메이션) (글로벌)" },
            new[] { "Worked out at build from the garment you picked. Any garment with sleeves can be picked.",
                    "ビルド時に指定した衣装をもとに計算します。袖のある衣装ならどれでも指定できます。",
                    "빌드 시 지정된 옷을 기반으로 계산합니다. 소매 있는 옷이면 무엇이든 지정 가능합니다." },
            new[] { "Wearing several layers? Pick several garments.",
                    "重ね着でも複数指定できます。",
                    "여러 벌 껴입어도 여러 개 지정 가능합니다." },
        };

        VisualElement AboutSection()
        {
            var sec = Box("sr-section");
            sec.Add(Label(T(lang, "Put it under a humanoid avatar and pick the sleeved garment — sleeve rolling is added at build.",
                "ヒューマノイドの下に入れて袖のある衣装を指定すると、ビルド時に袖まくり機能を追加します。",
                "휴머노이드 하위에 넣고, 소매 달린 옷을 지정하면 빌드 시 소매 걷기 기능을 추가합니다."), "sr-intro"));
            var list = Box("sr-features");
            foreach (var f in Features)
            {
                var row = Box("sr-feature");
                var dot = Box("sr-feature__dot"); row.Add(dot);
                row.Add(Label(f[Mathf.Clamp(lang, 0, 2)], "sr-feature__text"));
                list.Add(row);
            }
            sec.Add(Fold("Pyrite9SR.ShowFeatures", true, T(lang, "FEATURES", "機能", "기능"), list));
            return sec;
        }
    }
}
