using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Warcraft III style main menu: painted backdrop, golden title, a vertical
    //  button column on the right and Options / Credits sub-panels.  The live
    //  map orbits slowly behind it.
    // -----------------------------------------------------------------------
    public class MainMenuController : MonoBehaviour
    {
        Canvas canvas;
        Font font;

        GameObject root;
        GameObject optionsPanel;
        GameObject creditsPanel;

        Text speedLabel, fogLabel, edgeLabel;

        static readonly Color PanelBlue = new Color(0.05f, 0.10f, 0.28f, 0.94f);
        static readonly Color ButtonBlue = new Color(0.10f, 0.18f, 0.42f, 1f);
        static readonly Color ButtonHover = new Color(0.18f, 0.30f, 0.62f, 1f);
        static readonly Color Gold = new Color(1.00f, 0.82f, 0.35f);
        static readonly Color Border = new Color(0.55f, 0.62f, 0.80f, 0.9f);

        public void Init()
        {
            font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject go = new GameObject("Main Menu Canvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            // Buttons are uGUI: without an EventSystem no click ever lands.
            // (HUDController creates one too, but the menu exists before the HUD.)
            if (EventSystem.current == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            BuildMenu();
        }

        // ===================================================================

        RectTransform Panel(string name, RectTransform parent, Vector2 aMin, Vector2 aMax,
                            Vector2 oMin, Vector2 oMax)
        {
            GameObject g = new GameObject(name, typeof(RectTransform));
            RectTransform rt = g.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = oMin; rt.offsetMax = oMax;
            return rt;
        }

        Image Img(RectTransform parent, Color c, bool raycast)
        {
            GameObject g = new GameObject("Img", typeof(RectTransform), typeof(Image));
            Image i = g.GetComponent<Image>();
            i.color = c;
            i.raycastTarget = raycast;
            RectTransform rt = g.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return i;
        }

        Text Txt(RectTransform parent, string value, int size, Color color, TextAnchor anchor, bool bold)
        {
            GameObject g = new GameObject("Txt", typeof(RectTransform), typeof(Text));
            Text t = g.GetComponent<Text>();
            t.font = font;
            t.text = value;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rt = g.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return t;
        }

        Button MakeButton(RectTransform parent, string label, int fontSize, System.Action onClick)
        {
            RectTransform rt = Panel("Btn_" + label, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image bg = Img(rt, ButtonBlue, true);
            Outline border = bg.gameObject.AddComponent<Outline>();
            border.effectColor = Border;
            border.effectDistance = new Vector2(2f, 2f);

            Text t = Txt(rt, label, fontSize, Gold, TextAnchor.MiddleCenter, true);
            Shadow sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
            sh.effectDistance = new Vector2(2f, -2f);

            Button b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;

            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.85f, 0.9f, 1f);
            cb.pressedColor = new Color(0.6f, 0.7f, 1f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;

            b.onClick.AddListener(delegate
            {
                if (onClick != null) onClick();
            });
            return b;
        }

        // ===================================================================

        void BuildMenu()
        {
            RectTransform rootRt = canvas.transform as RectTransform;

            root = new GameObject("MenuRoot", typeof(RectTransform));
            RectTransform rr = root.GetComponent<RectTransform>();
            rr.SetParent(rootRt, false);
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
            rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;

            // ---- painted backdrop ----
            GameObject bgGo = new GameObject("Backdrop", typeof(RectTransform), typeof(RawImage));
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.SetParent(rr, false);
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            RawImage raw = bgGo.GetComponent<RawImage>();
            raw.texture = Resources.Load<Texture2D>("menu_background");
            raw.raycastTarget = true;
            if (raw.texture == null) raw.color = new Color(0.05f, 0.09f, 0.20f);

            // dark vignette so the UI pops
            Image dim = Img(rr, new Color(0.02f, 0.04f, 0.10f, 0.45f), false);

            // ---- title ----
            RectTransform titleBox = Panel("Title", rr, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                           new Vector2(60f, -140f), new Vector2(900f, -40f));
            Text logo = Txt(titleBox, "WARCRAFT  RTS", 74, Gold, TextAnchor.MiddleLeft, true);
            Outline lo = logo.gameObject.AddComponent<Outline>();
            lo.effectColor = new Color(0.05f, 0.08f, 0.2f, 1f);
            lo.effectDistance = new Vector2(3f, -3f);

            Text sub = Txt(titleBox, "A FAN-MADE REAL TIME STRATEGY  ·  ALLIANCE VS HORDE", 20,
                           new Color(0.75f, 0.85f, 1f), TextAnchor.MiddleLeft, false);
            sub.rectTransform.anchorMin = new Vector2(0f, 0f);
            sub.rectTransform.anchorMax = new Vector2(1f, 0f);
            sub.rectTransform.pivot = new Vector2(0f, 0f);
            sub.rectTransform.sizeDelta = new Vector2(0f, 30f);
            sub.rectTransform.anchoredPosition = new Vector2(6f, 2f);

            // ---- right-hand button column, like the original menu ----
            RectTransform column = Panel("Buttons", rr, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                                         new Vector2(-430f, -210f), new Vector2(-60f, 260f));
            Image colBg = Img(column, PanelBlue, false);
            Outline colBorder = colBg.gameObject.AddComponent<Outline>();
            colBorder.effectColor = Border;
            colBorder.effectDistance = new Vector2(2f, 2f);

            string[] labels = { "Single Player", "Options", "Credits", "Quit" };
            for (int i = 0; i < labels.Length; i++)
            {
                RectTransform slot = Panel("Slot" + i, column, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                           Vector2.zero, Vector2.zero);
                slot.pivot = new Vector2(0.5f, 1f);
                slot.sizeDelta = new Vector2(0f, 96f);
                slot.anchoredPosition = new Vector2(0f, -24f - i * 112f);

                string which = labels[i];
                RectTransform inner = Panel("Inner", slot, new Vector2(0f, 0f), new Vector2(1f, 1f),
                                            new Vector2(22f, 8f), new Vector2(-22f, -8f));
                MakeButton(inner, which, 30, delegate { OnMenu(which); });
            }

            // ---- version ----
            Text ver = Txt(rr, "v1.1   ·   fan project, not affiliated with Blizzard", 15,
                           new Color(0.7f, 0.78f, 0.9f, 0.8f), TextAnchor.LowerRight, false);
            ver.rectTransform.anchorMin = new Vector2(1f, 0f);
            ver.rectTransform.anchorMax = new Vector2(1f, 0f);
            ver.rectTransform.pivot = new Vector2(1f, 0f);
            ver.rectTransform.sizeDelta = new Vector2(700f, 30f);
            ver.rectTransform.anchoredPosition = new Vector2(-14f, 10f);

            BuildOptions(rr);
            BuildCredits(rr);
        }

        void BuildOptions(RectTransform rr)
        {
            optionsPanel = new GameObject("Options", typeof(RectTransform));
            RectTransform o = optionsPanel.GetComponent<RectTransform>();
            o.SetParent(rr, false);
            o.anchorMin = new Vector2(0.5f, 0.5f);
            o.anchorMax = new Vector2(0.5f, 0.5f);
            o.pivot = new Vector2(0.5f, 0.5f);
            o.sizeDelta = new Vector2(640f, 460f);

            Image pbg = Img(o, PanelBlue, true);
            Outline ob = pbg.gameObject.AddComponent<Outline>();
            ob.effectColor = Border;
            ob.effectDistance = new Vector2(2f, 2f);

            Txt(o, "OPTIONS", 40, Gold, TextAnchor.UpperCenter, true)
                .rectTransform.anchoredPosition = new Vector2(0f, -10f);
            Text title = o.GetComponentInChildren<Text>();
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(0f, 60f);
            title.rectTransform.anchoredPosition = Vector2.zero;

            string[] rows = { "Game Speed", "Fog of War", "Edge Scrolling" };
            for (int i = 0; i < rows.Length; i++)
            {
                RectTransform row = Panel("Row" + i, o, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                          Vector2.zero, Vector2.zero);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(0f, 80f);
                row.anchoredPosition = new Vector2(0f, -100f - i * 96f);

                Text label = Txt(row, rows[i], 26, Color.white, TextAnchor.MiddleLeft, true);
                label.rectTransform.anchorMin = new Vector2(0f, 0f);
                label.rectTransform.anchorMax = new Vector2(0f, 1f);
                label.rectTransform.pivot = new Vector2(0f, 0.5f);
                label.rectTransform.sizeDelta = new Vector2(300f, 0f);
                label.rectTransform.anchoredPosition = new Vector2(50f, 0f);

                RectTransform btnBox = Panel("Value", row, new Vector2(1f, 0f), new Vector2(1f, 1f),
                                             new Vector2(-260f, 10f), new Vector2(-50f, -10f));
                int rowIdx = i;
                MakeButton(btnBox, "", 24, delegate { ToggleOption(rowIdx); });
            }

            speedLabel = optionsPanel.transform.Find("Row0/Value/Btn_/Img/Txt") != null
                ? optionsPanel.transform.Find("Row0/Value/Btn_/Img/Txt").GetComponent<Text>() : null;
            fogLabel = optionsPanel.transform.Find("Row1/Value/Btn_/Img/Txt") != null
                ? optionsPanel.transform.Find("Row1/Value/Btn_/Img/Txt").GetComponent<Text>() : null;
            edgeLabel = optionsPanel.transform.Find("Row2/Value/Btn_/Img/Txt") != null
                ? optionsPanel.transform.Find("Row2/Value/Btn_/Img/Txt").GetComponent<Text>() : null;

            RectTransform backBox = Panel("Back", o, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                          new Vector2(-140f, 24f), new Vector2(140f, 84f));
            MakeButton(backBox, "Back", 26, delegate
            {
                optionsPanel.SetActive(false);
            });

            RefreshOptionLabels();
            optionsPanel.SetActive(false);
        }

        void BuildCredits(RectTransform rr)
        {
            creditsPanel = new GameObject("Credits", typeof(RectTransform));
            RectTransform o = creditsPanel.GetComponent<RectTransform>();
            o.SetParent(rr, false);
            o.anchorMin = new Vector2(0.5f, 0.5f);
            o.anchorMax = new Vector2(0.5f, 0.5f);
            o.pivot = new Vector2(0.5f, 0.5f);
            o.sizeDelta = new Vector2(720f, 480f);

            Image pbg = Img(o, PanelBlue, true);
            Outline ob = pbg.gameObject.AddComponent<Outline>();
            ob.effectColor = Border;
            ob.effectDistance = new Vector2(2f, 2f);

            Text head = Txt(o, "CREDITS", 40, Gold, TextAnchor.UpperCenter, true);
            head.rectTransform.anchorMin = new Vector2(0f, 1f);
            head.rectTransform.anchorMax = new Vector2(1f, 1f);
            head.rectTransform.pivot = new Vector2(0.5f, 1f);
            head.rectTransform.sizeDelta = new Vector2(0f, 60f);

            Text body = Txt(o,
                "A Warcraft III style RTS built entirely from code.\n\n" +
                "Design, code & procedural art   —   made with Arena.ai\n" +
                "Menu backdrop   —   AI painted in the style of the classics\n" +
                "Engine   —   Unity 2022 LTS, built-in render pipeline\n\n" +
                "Two races · heroes · fog of war · A* pathfinding · an AI that attacks.\n" +
                "For the Horde... or for the Alliance. Your call, commander.",
                22, new Color(0.85f, 0.9f, 1f), TextAnchor.UpperCenter, false);
            body.rectTransform.anchorMin = new Vector2(0f, 0f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.offsetMin = new Vector2(50f, 90f);
            body.rectTransform.offsetMax = new Vector2(-50f, -70f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform backBox = Panel("Back", o, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                          new Vector2(-140f, 24f), new Vector2(140f, 84f));
            MakeButton(backBox, "Back", 26, delegate { creditsPanel.SetActive(false); });

            creditsPanel.SetActive(false);
        }

        // ===================================================================

        void OnMenu(string which)
        {
            switch (which)
            {
                case "Single Player":
                    Destroy(canvas.gameObject);
                    GameManager.I.StartMatch();
                    break;
                case "Options":
                    optionsPanel.SetActive(true);
                    break;
                case "Credits":
                    creditsPanel.SetActive(true);
                    break;
                case "Quit":
                    Application.Quit();
                    break;
            }
        }

        void ToggleOption(int row)
        {
            GameManager gm = GameManager.I;
            switch (row)
            {
                case 0:
                    gm.gameSpeed = Mathf.Approximately(gm.gameSpeed, 2f) ? 1f : 2f;
                    break;
                case 1:
                    if (gm.Fog != null) gm.Fog.SetEnabled(!gm.Fog.Enabled);
                    else gm.fogEnabled = !gm.fogEnabled;
                    break;
                case 2:
                    if (gm.CameraRig != null) gm.CameraRig.edgeScrollEnabled = !gm.CameraRig.edgeScrollEnabled;
                    break;
            }
            RefreshOptionLabels();
        }

        void RefreshOptionLabels()
        {
            GameManager gm = GameManager.I;
            if (speedLabel != null) speedLabel.text = Mathf.Approximately(gm.gameSpeed, 2f) ? "x2" : "x1";
            bool fogOn = gm.Fog != null ? gm.Fog.Enabled : gm.fogEnabled;
            if (fogLabel != null) fogLabel.text = fogOn ? "On" : "Off";
            if (edgeLabel != null) edgeLabel.text = gm.CameraRig != null && gm.CameraRig.edgeScrollEnabled ? "On" : "Off";
        }

        public void Close()
        {
            if (root != null) Destroy(root);
        }
    }
}
