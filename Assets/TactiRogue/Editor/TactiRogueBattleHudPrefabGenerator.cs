using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TactiRogue
{
    public static class TactiRogueBattleHudPrefabGenerator
    {
        private const string RootFolder = "Assets/Resources/TactiRogue/UI/BattleHUD";
        private const string CommonFolder = RootFolder + "/Common";
        private const string DynamicFolder = RootFolder + "/Dynamic";
        private const string PopupFolder = RootFolder + "/Popups";
        private const string SidebarFolder = RootFolder + "/Sidebar";
        private const string RootPrefabFolder = RootFolder + "/Root";

        private static readonly Color TopBarColor = new Color(0.08f, 0.09f, 0.12f, 0.96f);
        private static readonly Color SidebarColor = new Color(0.09f, 0.1f, 0.14f, 0.96f);
        private static readonly Color PanelColor = new Color(0.13f, 0.14f, 0.19f, 1f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.3f, 1f);

        [MenuItem("Tools/TactiRogue/UI/Generate Battle HUD Prefabs")]
        public static void GenerateBattleHudPrefabs()
        {
            EnsureFolders();

            SaveCommonPrefabs();
            var sidebarPrefabs = SaveSidebarPrefabs();
            var pileViewerPrefab = SavePileViewerPrefab();
            var scenarioButtonPrefab = SaveScenarioButtonPrefab();
            var handCardPrefab = SaveHandCardPrefab();
            SaveRootPrefab(scenarioButtonPrefab, handCardPrefab, sidebarPrefabs, pileViewerPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated TactiRogue battle HUD prefabs.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/TactiRogue");
            EnsureFolder("Assets/Resources/TactiRogue/UI");
            EnsureFolder(RootFolder);
            EnsureFolder(CommonFolder);
            EnsureFolder(DynamicFolder);
            EnsureFolder(PopupFolder);
            EnsureFolder(SidebarFolder);
            EnsureFolder(RootPrefabFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            Directory.CreateDirectory(path);
            AssetDatabase.ImportAsset(path);
        }

        private static void SaveCommonPrefabs()
        {
            var panel = CreatePanelObject("Panel", PanelColor);
            SavePrefab(panel, CommonFolder + "/Panel.prefab");

            var textRoot = CreateUiObject("Text");
            var text = textRoot.AddComponent<Text>();
            ConfigureText(text, 14, TextAnchor.UpperLeft, Color.white);
            SavePrefab(textRoot, CommonFolder + "/Text.prefab");

            var button = CreateButtonObject("Button", "Button", new Vector2(120f, 40f));
            SavePrefab(button.gameObject, CommonFolder + "/Button.prefab");
        }

        private static SidebarPrefabSet SaveSidebarPrefabs()
        {
            var detailPanel = CreatePanelObject("DetailPanel", PanelColor);
            var detailRect = detailPanel.GetComponent<RectTransform>();
            var detailTitle = CreateText("DetailTitle", detailRect, 22, TextAnchor.UpperLeft);
            var detailBody = CreateText("DetailBody", detailRect, 14, TextAnchor.UpperLeft);
            var actionButton = CreateButton("ActionButton", detailRect, "Use Action", new Vector2(140f, 38f));
            PlaceDetailPanel(detailRect, detailTitle, detailBody, actionButton);

            return new SidebarPrefabSet
            {
                DetailPanel = SavePrefab(detailPanel, SidebarFolder + "/DetailPanel.prefab"),
                IntentPanel = SavePrefab(CreateSidebarTextPanelPrefab("IntentPanel", "IntentText", 14), SidebarFolder + "/IntentPanel.prefab"),
                PreviewPanel = SavePrefab(CreateSidebarTextPanelPrefab("PreviewPanel", "PreviewText", 14), SidebarFolder + "/PreviewPanel.prefab"),
                LogPanel = SavePrefab(CreateSidebarTextPanelPrefab("LogPanel", "LogText", 13), SidebarFolder + "/LogPanel.prefab"),
                SnapshotPanel = SavePrefab(CreateSidebarTextPanelPrefab("SnapshotPanel", "SnapshotText", 11), SidebarFolder + "/SnapshotPanel.prefab"),
            };
        }

        private static GameObject SavePileViewerPrefab()
        {
            var panel = CreatePanelObject("PileViewerPanel", TopBarColor);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.17f, 0.24f);
            rect.anchorMax = new Vector2(0.52f, 0.72f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var title = CreateText("PileViewerTitle", rect, 20, TextAnchor.UpperLeft);
            var body = CreateText("PileViewerBody", rect, 13, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            var closeButton = CreateButton("PileViewerCloseButton", rect, "Close", new Vector2(90f, 34f));
            PlacePileViewer(rect, title, body, closeButton);
            panel.SetActive(false);
            return SavePrefab(panel, PopupFolder + "/PileViewer.prefab");
        }

        private static GameObject SaveScenarioButtonPrefab()
        {
            var button = CreateButtonObject("ScenarioButton", "Scenario", new Vector2(128f, 38f));
            return SavePrefab(button.gameObject, DynamicFolder + "/ScenarioButton.prefab");
        }

        private static GameObject SaveHandCardPrefab()
        {
            var card = CreateUiObject("HandCard");
            var rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(170f, 144f);
            var background = card.AddComponent<Image>();
            background.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            var button = card.AddComponent<Button>();
            button.targetGraphic = background;

            var title = CreateText("Title", rect, 16, TextAnchor.UpperCenter);
            Stretch(title.rectTransform, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.95f));
            var body = CreateText("Body", rect, 12, TextAnchor.UpperLeft);
            body.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(body.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.58f));

            var view = card.AddComponent<CardButtonView>();
            view.BindPrefabReferences(button, background, title, body);
            return SavePrefab(card, DynamicFolder + "/HandCard.prefab");
        }

        private static void SaveRootPrefab(
            GameObject scenarioButtonPrefab,
            GameObject handCardPrefab,
            SidebarPrefabSet sidebarPrefabs,
            GameObject pileViewerPrefab)
        {
            var canvasGo = new GameObject("TactiRogueBattleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(BattleHudPrefabRoot));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var rootRect = canvasGo.GetComponent<RectTransform>();
            Stretch(rootRect, Vector2.zero, Vector2.one);

            var topBar = CreatePanel("TopBar", rootRect, TopBarColor, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -64f), Vector2.zero);
            var topLayout = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            topLayout.spacing = 10f;
            topLayout.padding = new RectOffset(10, 10, 10, 10);
            topLayout.childAlignment = TextAnchor.MiddleLeft;

            var scenarioPanel = CreatePanel("ScenarioButtons", topBar, Color.clear, new Vector2(0f, 0f), new Vector2(0.42f, 1f), Vector2.zero, Vector2.zero);
            var scenarioLayout = scenarioPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            scenarioLayout.spacing = 8f;
            scenarioLayout.childAlignment = TextAnchor.MiddleLeft;
            scenarioLayout.childControlWidth = false;
            scenarioLayout.childForceExpandWidth = false;

            var infoPanel = CreatePanel("BattleInfo", topBar, Color.clear, new Vector2(0.42f, 0f), new Vector2(0.72f, 1f), Vector2.zero, Vector2.zero);
            var turnText = CreateText("TurnText", infoPanel, 18, TextAnchor.MiddleLeft);
            var manaText = CreateText("ManaText", infoPanel, 18, TextAnchor.MiddleCenter);
            var stateText = CreateText("StateText", infoPanel, 16, TextAnchor.MiddleRight);
            PlaceRowTexts(infoPanel, turnText, manaText, stateText);

            var buttonsPanel = CreatePanel("TopButtons", topBar, Color.clear, new Vector2(0.72f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var buttonLayout = buttonsPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 10f;
            buttonLayout.childAlignment = TextAnchor.MiddleRight;
            buttonLayout.childControlWidth = false;
            buttonLayout.childForceExpandWidth = false;
            var drawPileButton = CreateButton("DrawPileButton", buttonsPanel, "DrawPile", new Vector2(120f, 40f));
            var discardPileButton = CreateButton("DiscardPileButton", buttonsPanel, "DiscardPile", new Vector2(130f, 40f));
            var endTurnButton = CreateButton("EndTurnButton", buttonsPanel, "End Turn", new Vector2(110f, 40f));
            var resetButton = CreateButton("ResetButton", buttonsPanel, "Reset", new Vector2(90f, 40f));
            var cancelButton = CreateButton("CancelButton", buttonsPanel, "Cancel", new Vector2(90f, 40f));

            var handBar = CreatePanel("HandBar", rootRect, TopBarColor, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 178f));
            var handViewport = CreatePanel("HandViewport", handBar, Color.clear, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 12f), new Vector2(-12f, -12f));
            var handLayout = handViewport.gameObject.AddComponent<HorizontalLayoutGroup>();
            handLayout.spacing = 12f;
            handLayout.childAlignment = TextAnchor.MiddleCenter;
            handLayout.childControlWidth = false;
            handLayout.childForceExpandWidth = false;

            var sidebar = CreatePanel("Sidebar", rootRect, SidebarColor, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-380f, 188f), new Vector2(0f, -74f));
            var detailPanel = InstantiatePanelPrefab(sidebarPrefabs.DetailPanel, sidebar, "DetailPanel", new Vector2(0f, 0.68f), new Vector2(1f, 1f), new Vector2(10f, -10f), new Vector2(-10f, -10f));
            var detailTitle = detailPanel.Find("DetailTitle").GetComponent<Text>();
            var detailBody = detailPanel.Find("DetailBody").GetComponent<Text>();
            var actionButton = detailPanel.Find("ActionButton").GetComponent<Button>();
            var intentText = InstantiateSidebarTextPanel(sidebarPrefabs.IntentPanel, sidebar, "IntentPanel", "IntentText", new Vector2(0f, 0.47f), new Vector2(1f, 0.68f));
            var previewText = InstantiateSidebarTextPanel(sidebarPrefabs.PreviewPanel, sidebar, "PreviewPanel", "PreviewText", new Vector2(0f, 0.3f), new Vector2(1f, 0.47f));
            var logText = InstantiateSidebarTextPanel(sidebarPrefabs.LogPanel, sidebar, "LogPanel", "LogText", new Vector2(0f, 0.11f), new Vector2(1f, 0.3f));
            var snapshotText = InstantiateSidebarTextPanel(sidebarPrefabs.SnapshotPanel, sidebar, "SnapshotPanel", "SnapshotText", new Vector2(0f, 0f), new Vector2(1f, 0.11f));

            var boardPanel = CreatePanel("BoardPanel", rootRect, Color.clear, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 188f), new Vector2(-392f, -74f));
            boardPanel.GetComponent<Image>().raycastTarget = false;

            var pileViewer = InstantiatePanelPrefab(pileViewerPrefab, rootRect, "PileViewerPanel", new Vector2(0.17f, 0.24f), new Vector2(0.52f, 0.72f), Vector2.zero, Vector2.zero);
            var pileTitle = pileViewer.Find("PileViewerTitle").GetComponent<Text>();
            var pileBody = pileViewer.Find("PileViewerBody").GetComponent<Text>();
            var pileClose = pileViewer.Find("PileViewerCloseButton").GetComponent<Button>();
            pileViewer.gameObject.SetActive(false);

            var binding = canvasGo.GetComponent<BattleHudPrefabRoot>();
            binding.Canvas = canvas;
            binding.CanvasScaler = scaler;
            binding.GraphicRaycaster = canvasGo.GetComponent<GraphicRaycaster>();
            binding.ScenarioButtonsContainer = scenarioPanel;
            binding.HandContent = handViewport;
            binding.BoardViewport = boardPanel;
            binding.TurnText = turnText;
            binding.ManaText = manaText;
            binding.StateText = stateText;
            binding.DrawPileButton = drawPileButton;
            binding.DrawPileButtonText = drawPileButton.GetComponentInChildren<Text>();
            binding.DiscardPileButton = discardPileButton;
            binding.DiscardPileButtonText = discardPileButton.GetComponentInChildren<Text>();
            binding.EndTurnButton = endTurnButton;
            binding.ResetButton = resetButton;
            binding.CancelButton = cancelButton;
            binding.DetailTitleText = detailTitle;
            binding.DetailBodyText = detailBody;
            binding.ActionButton = actionButton;
            binding.ActionButtonText = actionButton.GetComponentInChildren<Text>();
            binding.IntentText = intentText;
            binding.PreviewText = previewText;
            binding.LogText = logText;
            binding.SnapshotText = snapshotText;
            binding.PileViewerPanel = pileViewer;
            binding.PileViewerTitleText = pileTitle;
            binding.PileViewerBodyText = pileBody;
            binding.PileViewerCloseButton = pileClose;
            binding.ScenarioButtonPrefab = scenarioButtonPrefab.GetComponent<Button>();
            binding.HandCardPrefab = handCardPrefab.GetComponent<CardButtonView>();

            SavePrefab(canvasGo, RootPrefabFolder + "/TactiRogueBattleCanvas.prefab");
        }

        private static GameObject CreateSidebarTextPanelPrefab(string panelName, string textName, int fontSize)
        {
            var panel = CreatePanelObject(panelName, PanelColor);
            var panelRect = panel.GetComponent<RectTransform>();
            var text = CreateText(textName, panelRect, fontSize, TextAnchor.UpperLeft);
            Stretch(text.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
            return panel;
        }

        private static Text InstantiateSidebarTextPanel(GameObject prefab, RectTransform sidebar, string panelName, string textName, Vector2 anchorMin, Vector2 anchorMax)
        {
            var panel = InstantiatePanelPrefab(prefab, sidebar, panelName, anchorMin, anchorMax, new Vector2(10f, -10f), new Vector2(-10f, -10f));
            return panel.Find(textName).GetComponent<Text>();
        }

        private static RectTransform InstantiatePanelPrefab(GameObject prefab, RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private sealed class SidebarPrefabSet
        {
            public GameObject DetailPanel;
            public GameObject IntentPanel;
            public GameObject PreviewPanel;
            public GameObject LogPanel;
            public GameObject SnapshotPanel;
        }

        private static Text CreateSidebarTextPanel(RectTransform sidebar, string panelName, string textName, Vector2 anchorMin, Vector2 anchorMax, int fontSize)
        {
            var panel = CreatePanel(panelName, sidebar, PanelColor, anchorMin, anchorMax, new Vector2(10f, -10f), new Vector2(-10f, -10f));
            var text = CreateText(textName, panel, fontSize, TextAnchor.UpperLeft);
            Stretch(text.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
            return text;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return asset;
        }

        private static GameObject CreateUiObject(string name)
        {
            return new GameObject(name, typeof(RectTransform));
        }

        private static GameObject CreatePanelObject(string name, Color color)
        {
            var panel = CreateUiObject(name);
            var image = panel.AddComponent<Image>();
            image.color = color;
            return panel;
        }

        private static RectTransform CreatePanel(string name, RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var panel = CreatePanelObject(name, color);
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static Button CreateButtonObject(string name, string label, Vector2 size)
        {
            var root = CreateUiObject(name);
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            var image = root.AddComponent<Image>();
            image.color = ButtonColor;
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText("Label", rect, 14, TextAnchor.MiddleCenter);
            text.text = label;
            Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            return button;
        }

        private static Button CreateButton(string name, RectTransform parent, string label, Vector2 size)
        {
            var button = CreateButtonObject(name, label, size);
            button.transform.SetParent(parent, false);
            return button;
        }

        private static Text CreateText(string name, RectTransform parent, int fontSize, TextAnchor alignment)
        {
            var textRoot = CreateUiObject(name);
            textRoot.transform.SetParent(parent, false);
            var text = textRoot.AddComponent<Text>();
            ConfigureText(text, fontSize, alignment, Color.white);
            return text;
        }

        private static void ConfigureText(Text text, int fontSize, TextAnchor alignment, Color color)
        {
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void PlaceRowTexts(RectTransform parent, Text left, Text center, Text right)
        {
            Stretch(left.rectTransform, new Vector2(0f, 0f), new Vector2(0.36f, 1f));
            Stretch(center.rectTransform, new Vector2(0.36f, 0f), new Vector2(0.66f, 1f));
            Stretch(right.rectTransform, new Vector2(0.66f, 0f), new Vector2(1f, 1f));
        }

        private static void PlacePileViewer(RectTransform panel, Text title, Text body, Button closeButton)
        {
            Stretch(title.rectTransform, new Vector2(0.05f, 0.82f), new Vector2(0.72f, 0.95f));
            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.77f, 0.84f);
            closeRect.anchorMax = new Vector2(0.95f, 0.95f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            Stretch(body.rectTransform, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.78f));
        }

        private static void PlaceDetailPanel(RectTransform parent, Text title, Text body, Button actionButton)
        {
            Stretch(title.rectTransform, new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.94f));
            Stretch(body.rectTransform, new Vector2(0.04f, 0.24f), new Vector2(0.96f, 0.72f));
            var rect = actionButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.04f, 0.05f);
            rect.anchorMax = new Vector2(0.5f, 0.2f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
