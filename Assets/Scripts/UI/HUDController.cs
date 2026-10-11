using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  The whole interface: resources, command card, selection portraits,
    //  message log, health bars, floating text, drag box and game over.
    //  Built entirely from code so the project needs no imported assets.
    // -----------------------------------------------------------------------
    public class HUDController : MonoBehaviour
    {
        public bool FloatingTextEnabled = true;

        /// <summary>Root RectTransform of the HUD canvas (minimap docks here).</summary>
        public RectTransform UiRoot { get { return canvas != null ? canvas.transform as RectTransform : null; } }

        Canvas canvas;
        Font font;

        // top bar
        Text goldText, lumberText, supplyText, timeText, speedText;

        // bottom panels
        Text cardTitle, cardSubtitle;
        Image portraitImage;
        RectTransform hpFill, manaFill, manaRow;
        readonly List<Button> cardButtons = new List<Button>();
        readonly List<Image> cardIcons = new List<Image>();
        readonly List<Text> cardLabels = new List<Text>();
        readonly List<Image> cardCooldowns = new List<Image>();

        readonly List<Image> stripIcons = new List<Image>();
        readonly List<Text> stripLabels = new List<Text>();

        readonly List<Text> messages = new List<Text>();
        readonly List<float> messageExpiry = new List<float>();

        RectTransform dragBox;
        GameObject gameOverRoot;
        Text gameOverText;

        readonly List<HealthBarElement> barPool = new List<HealthBarElement>();
        readonly List<FloatingEntry> floatPool = new List<FloatingEntry>();
        RectTransform barLayer;
        RectTransform floatLayer;

        int firstAbilitySlot = -1;
        string lastSubtitle = "";

        class FloatingEntry
        {
            public Text text;
            public RectTransform rect;
            public float born;
            public Vector3 start;
            public Color baseColor;
            public bool inUse;
        }

        struct CardAction
        {
            public string label;
            public Color color;
            public System.Action onClick;
            public float cooldownRatio;
        }

        const int CardSlots = 12;
        const int StripSlots = 12;

        // ===================================================================
        //  UI hit testing (used by camera + selection)
        // ===================================================================

        static GraphicRaycaster raycaster;
        static PointerEventData pointerData;
        static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        public static bool IsPointerOverUI(Vector2 screenPos)
        {
            if (raycaster == null) return false;
            if (EventSystem.current == null) return false;
            if (pointerData == null) pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = screenPos;
            raycastResults.Clear();
            raycaster.Raycast(pointerData, raycastResults);
            return raycastResults.Count > 0;
        }

        // ===================================================================

        public void Init()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            BuildCanvas();
            BuildTopBar();
            BuildBottomPanels();
            BuildMessageLog();
            BuildDragBox();
            BuildPools();
            BuildGameOver();

            GameManager.I.Events.OnResourcesChanged += RefreshTopBar;
            GameManager.I.Events.OnSupplyChanged += RefreshTopBar;
            GameManager.I.Events.OnSelectionChanged += RebuildCommandCard;
            GameManager.I.Events.OnMessage += PushMessage;

            RefreshTopBar();
            RebuildCommandCard();
        }

        // ===================================================================
        //  Construction helpers
        // ===================================================================

        void BuildCanvas()
        {
            GameObject go = new GameObject("HUD Canvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;

            raycaster = go.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        /// <summary>Anchored panel. Offsets are in reference-resolution pixels.</summary>
        RectTransform NewPanel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
                               Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        Image NewImage(string name, RectTransform parent, Color color, bool raycast)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return img;
        }

        Text NewText(string name, RectTransform parent, string value, int size, Color color,
                     TextAnchor anchor, bool bold)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            Text t = go.GetComponent<Text>();
            t.font = font;
            t.text = value;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return t;
        }

        // ===================================================================

        void BuildTopBar()
        {
            RectTransform root = canvas.transform as RectTransform;
            RectTransform bar = NewPanel("TopBar", root, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                         new Vector2(0f, -44f), new Vector2(0f, 0f));
            NewImage("BG", bar, new Color(0.06f, 0.06f, 0.10f, 0.85f), false);

            RectTransform left = NewPanel("Left", bar, new Vector2(0f, 0f), new Vector2(0f, 1f),
                                          new Vector2(16f, 0f), new Vector2(760f, 0f));

            goldText = NewText("Gold", left, "Gold 0", 20, new Color(1f, 0.85f, 0.25f), TextAnchor.MiddleLeft, true);
            SetAnchored(goldText.rectTransform, 0f, 150f);

            lumberText = NewText("Lumber", left, "Lumber 0", 20, new Color(0.55f, 1f, 0.55f), TextAnchor.MiddleLeft, true);
            SetAnchored(lumberText.rectTransform, 160f, 160f);

            supplyText = NewText("Supply", left, "0/10", 20, Color.white, TextAnchor.MiddleLeft, true);
            SetAnchored(supplyText.rectTransform, 330f, 120f);

            timeText = NewText("Time", left, "00:00", 18, new Color(0.8f, 0.85f, 1f), TextAnchor.MiddleLeft, false);
            SetAnchored(timeText.rectTransform, 460f, 90f);

            speedText = NewText("Speed", left, "x1", 18, new Color(0.6f, 1f, 1f), TextAnchor.MiddleLeft, false);
            SetAnchored(speedText.rectTransform, 560f, 90f);
        }

        void SetAnchored(RectTransform rt, float x, float width)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(width, 0f);
            rt.anchoredPosition = new Vector2(x, 0f);
        }

        // ===================================================================

        void BuildBottomPanels()
        {
            RectTransform root = canvas.transform as RectTransform;

            // ---------- command card ----------
            RectTransform card = NewPanel("CommandCard", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                          new Vector2(-268f, 8f), new Vector2(268f, 168f));
            NewImage("BG", card, new Color(0.07f, 0.07f, 0.11f, 0.9f), false);

            // portrait + labels
            RectTransform info = NewPanel("Info", card, new Vector2(0f, 0f), new Vector2(0f, 1f),
                                          new Vector2(8f, 8f), new Vector2(152f, -8f));
            NewImage("PortraitBG", info, new Color(0.14f, 0.15f, 0.22f, 1f), false);

            portraitImage = NewImage("Portrait", info, Color.white, false);
            portraitImage.rectTransform.offsetMin = new Vector2(8f, 30f);
            portraitImage.rectTransform.offsetMax = new Vector2(-8f, -30f);

            cardTitle = NewText("Title", info, "Warcraft RTS", 13, Color.white, TextAnchor.UpperCenter, true);
            cardTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            cardTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            cardTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            cardTitle.rectTransform.sizeDelta = new Vector2(0f, 24f);
            cardTitle.rectTransform.anchoredPosition = new Vector2(0f, -4f);

            cardSubtitle = NewText("Subtitle", info, "", 11, new Color(0.75f, 0.8f, 0.9f), TextAnchor.LowerCenter, false);
            cardSubtitle.rectTransform.anchorMin = new Vector2(0f, 0f);
            cardSubtitle.rectTransform.anchorMax = new Vector2(1f, 0f);
            cardSubtitle.rectTransform.pivot = new Vector2(0.5f, 0f);
            cardSubtitle.rectTransform.sizeDelta = new Vector2(0f, 26f);
            cardSubtitle.rectTransform.anchoredPosition = new Vector2(0f, 3f);

            // ---------- health / mana ----------
            RectTransform bars = NewPanel("Bars", card, new Vector2(0f, 0f), new Vector2(0f, 1f),
                                          new Vector2(164f, 96f), new Vector2(336f, 160f));

            RectTransform hpRow = NewPanel("HPRow", bars, new Vector2(0f, 1f), new Vector2(1f, 1f),
                                           Vector2.zero, Vector2.zero);
            hpRow.pivot = new Vector2(0f, 1f);
            hpRow.sizeDelta = new Vector2(0f, 24f);
            hpRow.anchoredPosition = Vector2.zero;
            NewImage("HPBG", hpRow, new Color(0.16f, 0.05f, 0.05f, 1f), false);
            hpFill = NewFill(hpRow, new Color(0.2f, 0.85f, 0.25f));

            manaRow = NewPanel("ManaRow", bars, new Vector2(0f, 1f), new Vector2(1f, 1f),
                               Vector2.zero, Vector2.zero);
            manaRow.pivot = new Vector2(0f, 1f);
            manaRow.sizeDelta = new Vector2(0f, 22f);
            manaRow.anchoredPosition = new Vector2(0f, -26f);
            NewImage("ManaBG", manaRow, new Color(0.05f, 0.08f, 0.2f, 1f), false);
            manaFill = NewFill(manaRow, new Color(0.25f, 0.45f, 1f));
            manaRow.gameObject.SetActive(false);

            // ---------- button grid ----------
            RectTransform grid = NewPanel("Grid", card, new Vector2(0f, 0f), new Vector2(0f, 1f),
                                          new Vector2(346f, 8f), new Vector2(528f, 160f));

            for (int i = 0; i < CardSlots; i++)
            {
                int col = i % 4, row = i / 4;
                RectTransform cell = NewPanel("Cell" + i, grid, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                              Vector2.zero, Vector2.zero);
                cell.pivot = new Vector2(0f, 1f);
                cell.sizeDelta = new Vector2(42f, 42f);
                cell.anchoredPosition = new Vector2(col * 45f, -row * 45f);

                Image img = NewImage("Icon", cell, new Color(0.22f, 0.24f, 0.32f, 1f), true);
                img.rectTransform.offsetMin = new Vector2(2f, 2f);
                img.rectTransform.offsetMax = new Vector2(-2f, -2f);

                Button btn = cell.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;

                Text label = NewText("Label", cell, "", 14, Color.white, TextAnchor.MiddleCenter, true);

                Text key = NewText("Key", cell, "", 10, new Color(1f, 1f, 0.6f), TextAnchor.UpperLeft, true);
                key.rectTransform.anchorMin = new Vector2(0f, 1f);
                key.rectTransform.anchorMax = new Vector2(0f, 1f);
                key.rectTransform.pivot = new Vector2(0f, 1f);
                key.rectTransform.sizeDelta = new Vector2(18f, 14f);
                key.rectTransform.anchoredPosition = new Vector2(3f, -2f);

                Image cd = NewImage("CD", cell, new Color(0f, 0f, 0f, 0.55f), false);
                cd.type = Image.Type.Filled;
                cd.fillMethod = Image.FillMethod.Vertical;
                cd.fillOrigin = 1;
                cd.fillAmount = 0f;
                cd.gameObject.SetActive(false);

                cardButtons.Add(btn);
                cardIcons.Add(img);
                cardLabels.Add(label);
                cardCooldowns.Add(cd);
            }

            // ---------- selection strip ----------
            RectTransform strip = NewPanel("SelectionStrip", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                           new Vector2(-268f, 180f), new Vector2(268f, 236f));
            NewImage("BG", strip, new Color(0.07f, 0.07f, 0.11f, 0.78f), false);

            for (int i = 0; i < StripSlots; i++)
            {
                RectTransform cell = NewPanel("Slot" + i, strip, new Vector2(0f, 0f), new Vector2(0f, 1f),
                                              Vector2.zero, Vector2.zero);
                cell.pivot = new Vector2(0f, 0.5f);
                cell.sizeDelta = new Vector2(40f, -8f);
                cell.anchoredPosition = new Vector2(6f + i * 44f, 0f);

                Image icon = NewImage("Icon", cell, new Color(0.2f, 0.22f, 0.3f, 1f), false);
                icon.rectTransform.offsetMin = new Vector2(2f, 2f);
                icon.rectTransform.offsetMax = new Vector2(-2f, -2f);

                Text label = NewText("Label", cell, "", 13, Color.white, TextAnchor.MiddleCenter, true);

                stripIcons.Add(icon);
                stripLabels.Add(label);
                cell.gameObject.SetActive(false);
            }
        }

        /// <summary>A left-anchored fill inside a bar row.</summary>
        RectTransform NewFill(RectTransform row, Color color)
        {
            RectTransform fill = NewPanel("Fill", row, new Vector2(0f, 0f), new Vector2(0f, 1f),
                                          new Vector2(1f, 1f), new Vector2(1f, -1f));
            fill.pivot = new Vector2(0f, 0.5f);
            Image img = fill.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return fill;
        }

        void BuildMessageLog()
        {
            RectTransform root = canvas.transform as RectTransform;
            RectTransform log = NewPanel("Messages", root, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                         new Vector2(16f, -330f), new Vector2(560f, -52f));
            for (int i = 0; i < 6; i++)
            {
                Text t = NewText("Msg" + i, log, "", 14, Color.white, TextAnchor.UpperLeft, false);
                RectTransform rt = t.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(0f, 20f);
                rt.anchoredPosition = new Vector2(0f, -i * 20f);
                t.gameObject.SetActive(false);
                messages.Add(t);
                messageExpiry.Add(0f);
            }
        }

        void BuildDragBox()
        {
            RectTransform root = canvas.transform as RectTransform;
            RectTransform box = NewPanel("DragBox", root, new Vector2(0f, 0f), new Vector2(0f, 0f),
                                         Vector2.zero, Vector2.zero);
            box.pivot = new Vector2(0f, 0f);
            box.sizeDelta = Vector2.zero;

            Image fill = box.gameObject.AddComponent<Image>();
            fill.color = new Color(0.35f, 1f, 0.35f, 0.14f);
            fill.raycastTarget = false;

            Outline o = box.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.35f, 1f, 0.35f, 0.9f);
            o.effectDistance = new Vector2(1.5f, 1.5f);

            dragBox = box;
            box.gameObject.SetActive(false);
        }

        void BuildPools()
        {
            RectTransform root = canvas.transform as RectTransform;

            GameObject bl = new GameObject("HealthBars", typeof(RectTransform));
            barLayer = bl.GetComponent<RectTransform>();
            barLayer.SetParent(root, false);
            barLayer.anchorMin = Vector2.zero;
            barLayer.anchorMax = Vector2.one;
            barLayer.offsetMin = Vector2.zero;
            barLayer.offsetMax = Vector2.zero;

            GameObject fl = new GameObject("FloatingText", typeof(RectTransform));
            floatLayer = fl.GetComponent<RectTransform>();
            floatLayer.SetParent(root, false);
            floatLayer.anchorMin = Vector2.zero;
            floatLayer.anchorMax = Vector2.one;
            floatLayer.offsetMin = Vector2.zero;
            floatLayer.offsetMax = Vector2.zero;

            for (int i = 0; i < 30; i++) barPool.Add(CreateBar());
            for (int i = 0; i < 40; i++) floatPool.Add(CreateFloating());
        }

        HealthBarElement CreateBar()
        {
            GameObject go = new GameObject("Bar", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(barLayer, false);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(34f, 5f);

            RectTransform bg = NewPanel("BG", rt, Vector2.zero, Vector2.one,
                                        new Vector2(-1f, -1f), new Vector2(1f, 1f));
            Image bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.75f);
            bgImg.raycastTarget = false;

            RectTransform fill = NewPanel("Fill", rt, new Vector2(0f, 0f), new Vector2(0f, 1f),
                                          new Vector2(1f, 1f), new Vector2(1f, -1f));
            fill.pivot = new Vector2(0f, 0.5f);
            Image fImg = fill.gameObject.AddComponent<Image>();
            fImg.color = new Color(0.2f, 0.9f, 0.25f);
            fImg.raycastTarget = false;

            RectTransform mana = NewPanel("Mana", rt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                          new Vector2(1f, 5f), new Vector2(1f, 8f));
            mana.pivot = new Vector2(0f, 0f);
            Image mImg = mana.gameObject.AddComponent<Image>();
            mImg.color = new Color(0.25f, 0.45f, 1f);
            mImg.raycastTarget = false;
            mana.gameObject.SetActive(false);

            go.SetActive(false);

            HealthBarElement e = new HealthBarElement();
            e.root = go;
            e.rect = rt;
            e.hpFill = fill;
            e.manaFill = mana;
            e.background = bg;
            e.hpFullWidth = 32f;
            e.manaFullWidth = 32f;
            return e;
        }

        FloatingEntry CreateFloating()
        {
            GameObject go = new GameObject("Float", typeof(RectTransform), typeof(Text));
            Text t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = 15;
            t.alignment = TextAnchor.MiddleCenter;
            t.fontStyle = FontStyle.Bold;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(floatLayer, false);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100f, 20f);
            go.SetActive(false);

            FloatingEntry e = new FloatingEntry();
            e.text = t;
            e.rect = rt;
            return e;
        }

        void BuildGameOver()
        {
            RectTransform root = canvas.transform as RectTransform;
            gameOverRoot = new GameObject("GameOver", typeof(RectTransform));
            RectTransform rt = gameOverRoot.GetComponent<RectTransform>();
            rt.SetParent(root, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            NewImage("Dim", rt, new Color(0f, 0f, 0f, 0.62f), true);
            gameOverText = NewText("Result", rt, "", 56, Color.white, TextAnchor.MiddleCenter, true);
            gameOverRoot.SetActive(false);
        }

        // ===================================================================
        //  Content
        // ===================================================================

        public void RefreshTopBar()
        {
            if (goldText == null || GameManager.I == null) return;
            RTSPlayer p = GameManager.I.Human;
            if (p == null) return;
            goldText.text = "Gold " + p.gold;
            lumberText.text = "Lumber " + p.lumber;
            supplyText.text = "Food " + p.supplyUsed + "/" + p.supplyCap;
            supplyText.color = p.SupplyLeft <= 0 ? new Color(1f, 0.4f, 0.3f) : Color.white;
        }

        public void PushMessage(string message, Color color)
        {
            if (messages.Count == 0) return;

            int oldest = 0;
            float oldestTime = float.MaxValue;
            for (int i = 0; i < messages.Count; i++)
                if (messageExpiry[i] < oldestTime) { oldestTime = messageExpiry[i]; oldest = i; }

            Text t = messages[oldest];
            t.text = message;
            t.color = color;
            t.gameObject.SetActive(true);
            messageExpiry[oldest] = Time.time + 6f;
        }

        public void SpawnFloatingText(Vector3 worldPosition, string text, Color color)
        {
            for (int i = 0; i < floatPool.Count; i++)
            {
                FloatingEntry e = floatPool[i];
                if (e.inUse) continue;
                e.inUse = true;
                e.born = Time.time;
                e.start = worldPosition;
                e.baseColor = color;
                e.text.text = text;
                e.text.color = color;
                e.rect.gameObject.SetActive(true);
                return;
            }
        }

        // ===================================================================
        //  Command card
        // ===================================================================

        readonly List<CardAction> actions = new List<CardAction>();

        void RebuildCommandCard()
        {
            actions.Clear();
            firstAbilitySlot = -1;

            GameManager gm = GameManager.I;
            if (gm == null || gm.Selection == null) return;

            SelectionManager sel = gm.Selection;
            RTSPlayer me = gm.Human;
            BuildingController b = sel.SelectedBuilding;

            if (b != null && b.Owner == me)
            {
                SetTitle(b.def.displayName,
                         b.IsComplete ? "Completed" : "Building " + Mathf.RoundToInt(b.buildProgress * 100f) + "%");
                portraitImage.color = Color.Lerp(me.color, Color.white, 0.3f);

                if (b.defId == BuildingId.Blacksmith && b.IsComplete)
                {
                    BuildingController smith = b;
                    UpgradeId[] ups = { UpgradeId.Weapons, UpgradeId.Armor };
                    string[] names = { "Weapons", "Armor" };
                    for (int ui = 0; ui < ups.Length; ui++)
                    {
                        UpgradeId up = ups[ui];
                        int lvl = b.CurrentLevel(up);
                        int next = lvl + 1;
                        bool maxed = lvl >= GameData.MaxUpgradeLevel;
                        string cost = maxed ? "MAX" :
                            GameData.UpgradeGold(up, next) + "g " + GameData.UpgradeLumber(up, next) + "l";
                        string nm = names[ui];
                        actions.Add(new CardAction
                        {
                            label = (up == UpgradeId.Weapons ? "W" : "A") + Mathf.Min(next, 3),
                            color = maxed ? new Color(0.35f, 0.35f, 0.4f) : new Color(0.55f, 0.35f, 0.15f),
                            onClick = delegate { smith.StartResearch(up); }
                        });
                        // tooltip-ish info goes to the subtitle while researching
                        if (b.researchQueue.Count > 0)
                            SetTitle(b.def.displayName, "Researching " +
                                     (b.researchQueue[0] == UpgradeId.Weapons ? "weapons" : "armor") + " " +
                                     Mathf.RoundToInt(b.TrainingResearch * 100f) + "%");
                    }
                }

                if (b.IsComplete && b.def.trains != null)
                {
                    for (int i = 0; i < b.def.trains.Length && actions.Count < CardSlots - 2; i++)
                    {
                        UnitId uid = b.def.trains[i];
                        UnitDef ud = GameData.GetUnit(uid);
                        BuildingController trainer = b;
                        actions.Add(new CardAction
                        {
                            label = ShortName(ud.displayName),
                            color = Color.Lerp(me.color, Color.white, 0.45f),
                            onClick = delegate { trainer.Train(uid); }
                        });
                    }
                    if (b.trainQueue.Count > 0)
                    {
                        BuildingController trainer = b;
                        actions.Add(new CardAction
                        {
                            label = "X",
                            color = new Color(0.75f, 0.28f, 0.24f),
                            onClick = delegate { trainer.CancelTraining(0); }
                        });
                    }
                }
            }
            else if (sel.SelectedUnits.Count > 0)
            {
                UnitController first = sel.SelectedUnits[0];
                bool own = first.Owner == me;

                string title = sel.SelectedUnits.Count > 1
                    ? sel.SelectedUnits.Count + " units"
                    : first.def.displayName;
                string sub = own
                    ? Mathf.CeilToInt(first.hp) + "/" + Mathf.CeilToInt(first.MaxHp) + " HP" +
                      (first.def.isHero ? "   Level " + first.level : "")
                    : "Enemy " + first.Owner.name;
                SetTitle(title, sub);
                portraitImage.color = Color.Lerp(first.Owner.color, Color.white, 0.35f);

                if (own)
                {
                    bool anyWorker = false;
                    for (int i = 0; i < sel.SelectedUnits.Count; i++)
                        if (sel.SelectedUnits[i].def.unitClass == UnitClass.Worker) anyWorker = true;

                    if (anyWorker)
                    {
                        BuildingId[] list = GameData.BuildableFor(me.faction);
                        for (int i = 0; i < list.Length && actions.Count < CardSlots - 4; i++)
                        {
                            BuildingDef bd = GameData.GetBuilding(list[i]);
                            BuildingId bid = list[i];
                            actions.Add(new CardAction
                            {
                                label = ShortName(bd.displayName),
                                color = new Color(0.28f, 0.40f, 0.60f),
                                onClick = delegate { gm.Placement.Begin(bid); }
                            });
                        }
                    }

                    if (first.def.isHero)
                    {
                        AbilityId[] abs = GameData.AbilitiesOf(first.defId);
                        firstAbilitySlot = actions.Count;
                        for (int i = 0; i < abs.Length; i++)
                        {
                            AbilityDef a = GameData.GetAbility(abs[i]);
                            AbilityId aid = abs[i];
                            UnitController caster = first;
                            actions.Add(new CardAction
                            {
                                label = a.hotkey,
                                color = a.iconColor,
                                cooldownRatio = a.cooldown > 0f
                                    ? gm.Abilities.CooldownRemaining(caster, aid) / a.cooldown
                                    : 0f,
                                onClick = delegate { gm.Placement.BeginAbility(caster, aid); }
                            });
                        }
                    }

                    actions.Add(new CardAction
                    {
                        label = "STP",
                        color = new Color(0.5f, 0.5f, 0.56f),
                        onClick = delegate { gm.Orders.StopGroup(sel.SelectedUnits); }
                    });
                    actions.Add(new CardAction
                    {
                        label = "HLD",
                        color = new Color(0.5f, 0.5f, 0.56f),
                        onClick = delegate { gm.Orders.HoldGroup(sel.SelectedUnits); }
                    });
                }
            }
            else if (sel.SelectedNode != null)
            {
                SetTitle(sel.SelectedNode.kind == ResourceKind.Gold ? "Gold Mine" : "Trees",
                         sel.SelectedNode.amount + " remaining");
                portraitImage.color = sel.SelectedNode.kind == ResourceKind.Gold
                    ? new Color(1f, 0.85f, 0.3f) : new Color(0.3f, 0.7f, 0.3f);
            }
            else
            {
                SetTitle("Warcraft RTS", "Drag to select  ·  Right-click to command");
                portraitImage.color = new Color(0.18f, 0.20f, 0.28f);
            }

            for (int i = 0; i < CardSlots; i++)
            {
                bool has = i < actions.Count;
                cardButtons[i].gameObject.SetActive(has);
                if (!has) continue;

                CardAction a = actions[i];
                cardIcons[i].color = a.color;
                cardLabels[i].text = a.label;
                cardCooldowns[i].fillAmount = Mathf.Clamp01(a.cooldownRatio);
                cardCooldowns[i].gameObject.SetActive(a.cooldownRatio > 0.001f);

                System.Action cb = a.onClick;
                cardButtons[i].onClick.RemoveAllListeners();
                cardButtons[i].onClick.AddListener(delegate { if (cb != null) cb(); });
            }

            RebuildStrip();
        }

        void SetTitle(string title, string subtitle)
        {
            cardTitle.text = title;
            cardSubtitle.text = subtitle;
            lastSubtitle = subtitle;
        }

        void RebuildStrip()
        {
            SelectionManager sel = GameManager.I.Selection;
            for (int i = 0; i < StripSlots; i++)
            {
                bool has = i < sel.SelectedUnits.Count;
                stripIcons[i].transform.parent.gameObject.SetActive(has);
                if (!has) continue;

                UnitController u = sel.SelectedUnits[i];
                stripIcons[i].color = Color.Lerp(u.Owner.color, Color.white, u.def.isHero ? 0.5f : 0.25f);
                stripLabels[i].text = ShortName(u.def.displayName);
                stripLabels[i].color = u.CurrentHp < u.MaxHpValue * 0.4f
                    ? new Color(1f, 0.5f, 0.4f) : Color.white;
            }
        }

        static string ShortName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string[] words = s.Split(' ');
            if (words.Length == 1) return s.Length <= 3 ? s.ToUpper() : s.Substring(0, 3).ToUpper();
            return (words[0][0].ToString() + words[1][0]).ToUpper();
        }

        // ===================================================================
        //  Per-frame
        // ===================================================================

        public void LateTick()
        {
            if (GameManager.I == null || GameManager.I.Map == null) return;

            RefreshClock();
            UpdateDragBox();
            UpdateHealthBars();
            UpdateFloating();
            CombatFx.Tick();
            GroundEffect.Tick();
            RefreshCooldownOverlays();
            FadeMessages();
            RefreshPlacementSubtitle();
        }

        void RefreshClock()
        {
            float t = GameManager.I.MatchTime;
            timeText.text = ((int)(t / 60f)).ToString("00") + ":" + ((int)(t % 60f)).ToString("00");
            speedText.text = GameManager.I.gameSpeed < 0.01f ? "PAUSED" : "x" + GameManager.I.gameSpeed.ToString("0.#");
        }

        void FadeMessages()
        {
            for (int i = 0; i < messages.Count; i++)
            {
                if (!messages[i].gameObject.activeSelf) continue;
                if (Time.time > messageExpiry[i]) messages[i].gameObject.SetActive(false);
            }
        }

        void UpdateDragBox()
        {
            SelectionManager sel = GameManager.I.Selection;
            if (!sel.IsDragging)
            {
                if (dragBox.gameObject.activeSelf) dragBox.gameObject.SetActive(false);
                return;
            }

            Vector2 min = Vector2.Min(sel.DragStart, sel.DragCurrent);
            Vector2 max = Vector2.Max(sel.DragStart, sel.DragCurrent);
            float scale = Mathf.Max(0.0001f, canvas.scaleFactor);

            dragBox.gameObject.SetActive(true);
            dragBox.anchoredPosition = min / scale;
            dragBox.sizeDelta = (max - min) / scale;
        }

        void UpdateHealthBars()
        {
            GameManager gm = GameManager.I;
            SelectionManager sel = gm.Selection;
            int used = 0;

            for (int i = 0; i < sel.SelectedUnits.Count && used < barPool.Count; i++)
                if (AssignBar(used, sel.SelectedUnits[i], true)) used++;

            if (sel.SelectedBuilding != null && used < barPool.Count)
                if (AssignBar(used, sel.SelectedBuilding, true)) used++;

            for (int i = 0; i < gm.AllUnits.Count && used < barPool.Count; i++)
            {
                UnitController u = gm.AllUnits[i];
                if (u == null || !u.IsAlive || u.Owner != gm.Human) continue;
                if (sel.SelectedUnits.Contains(u)) continue;
                if (u.CurrentHp >= u.MaxHpValue - 0.5f && !u.def.isHero) continue;
                if (AssignBar(used, u, false)) used++;
            }

            for (int i = 0; i < gm.AllBuildings.Count && used < barPool.Count; i++)
            {
                BuildingController b = gm.AllBuildings[i];
                if (b == null || !b.IsAlive || b.Owner != gm.Human) continue;
                if (b == sel.SelectedBuilding) continue;
                if (b.IsComplete && b.CurrentHp >= b.MaxHpValue - 0.5f) continue;
                if (AssignBar(used, b, false)) used++;
            }

            for (int i = used; i < barPool.Count; i++)
                if (barPool[i].root.activeSelf) barPool[i].root.SetActive(false);
        }

        bool AssignBar(int index, IDamageable target, bool alwaysShow)
        {
            HealthBarElement e = barPool[index];
            Camera cam = Camera.main;
            if (cam == null) return false;

            float height = target.IsBuilding ? 4.4f : 2.5f;
            Vector3 sp = cam.WorldToScreenPoint(target.Position + Vector3.up * height);
            if (sp.z <= 0f) { e.root.SetActive(false); return false; }

            float ratio = Mathf.Clamp01(target.CurrentHp / Mathf.Max(0.001f, target.MaxHpValue));
            if (!alwaysShow && ratio > 0.995f) { e.root.SetActive(false); return false; }

            float scale = Mathf.Max(0.0001f, canvas.scaleFactor);
            e.root.SetActive(true);
            e.rect.anchoredPosition = new Vector2(sp.x / scale, sp.y / scale);

            float width = target.IsBuilding ? 48f : 34f;
            e.rect.sizeDelta = new Vector2(width, 5f);

            e.hpFill.sizeDelta = new Vector2((width - 2f) * ratio, 3f);
            Image hpImg = e.hpFill.GetComponent<Image>();
            hpImg.color = target.Owner == GameManager.I.Human
                ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.95f, 0.25f, 0.2f);

            UnitController u = target as UnitController;
            bool showMana = u != null && u.MaxMana > 0f;
            e.manaFill.gameObject.SetActive(showMana);
            if (showMana)
                e.manaFill.sizeDelta = new Vector2((width - 2f) * Mathf.Clamp01(u.mana / u.MaxMana), 2f);

            return true;
        }

        void UpdateFloating()
        {
            Camera cam = Camera.main;
            float scale = Mathf.Max(0.0001f, canvas.scaleFactor);
            for (int i = 0; i < floatPool.Count; i++)
            {
                FloatingEntry e = floatPool[i];
                if (!e.inUse) continue;

                float age = Time.time - e.born;
                if (age > 0.9f || cam == null)
                {
                    e.inUse = false;
                    e.rect.gameObject.SetActive(false);
                    continue;
                }

                Vector3 sp = cam.WorldToScreenPoint(e.start + Vector3.up * (age * 1.7f));
                if (sp.z <= 0f)
                {
                    e.inUse = false;
                    e.rect.gameObject.SetActive(false);
                    continue;
                }

                e.rect.anchoredPosition = new Vector2(sp.x / scale, sp.y / scale);
                Color c = e.baseColor;
                c.a = Mathf.Clamp01(1f - age / 0.9f);
                e.text.color = c;
            }
        }

        void RefreshCooldownOverlays()
        {
            if (firstAbilitySlot < 0) return;
            GameManager gm = GameManager.I;
            SelectionManager sel = gm.Selection;
            if (sel.SelectedUnits.Count == 0) return;

            UnitController first = sel.SelectedUnits[0];
            if (!first.def.isHero) return;

            AbilityId[] abs = GameData.AbilitiesOf(first.defId);
            for (int i = 0; i < abs.Length; i++)
            {
                int slot = firstAbilitySlot + i;
                if (slot < 0 || slot >= cardCooldowns.Count) continue;
                AbilityDef a = GameData.GetAbility(abs[i]);
                float r = a.cooldown > 0f ? gm.Abilities.CooldownRemaining(first, abs[i]) / a.cooldown : 0f;
                cardCooldowns[slot].fillAmount = Mathf.Clamp01(r);
                cardCooldowns[slot].gameObject.SetActive(r > 0.001f);
            }
        }

        void RefreshPlacementSubtitle()
        {
            PlacementController p = GameManager.I.Placement;
            if (p == null || !p.IsActive) return;

            string text;
            if (p.ActiveAbility != AbilityId.None)
            {
                AbilityDef a = GameData.GetAbility(p.ActiveAbility);
                text = "Targeting " + a.displayName + " — right-click to cancel";
            }
            else
            {
                BuildingDef d = GameData.GetBuilding(p.ActiveBuilding);
                text = "Placing " + d.displayName + (p.CanPlaceHere ? " — click to build" : " — blocked");
            }

            if (text != lastSubtitle)
            {
                cardSubtitle.text = text;
                lastSubtitle = text;
            }
        }

        // ===================================================================

        public void ShowGameOver(bool victory)
        {
            gameOverRoot.SetActive(true);
            gameOverText.text = victory ? "VICTORY" : "DEFEAT";
            gameOverText.color = victory ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.4f, 0.35f);
        }
    }
}
