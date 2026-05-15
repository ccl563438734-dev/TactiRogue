using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TactiRogue
{
    public sealed class BoardCellView : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private const float LongPressSeconds = 0.45f;
        private bool _isPressing;
        private bool _longPressTriggered;
        private float _pressStartTime;
        private PointerEventData.InputButton _pressedButton;

        public GridPosition Position { get; private set; }
        public Image Background { get; private set; }
        public Text MainText { get; private set; }
        public Text SubText { get; private set; }
        public Button Button { get; private set; }

        public event Action<GridPosition, PointerEventData.InputButton> Clicked;
        public event Action<GridPosition, PointerEventData.InputButton> LongPressed;
        public event Action<GridPosition> Hovered;
        public event Action<GridPosition> HoverExited;

        public void Initialize(GridPosition position, Font font)
        {
            Position = position;
            var rect = gameObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(72f, 72f);

            Background = gameObject.AddComponent<Image>();
            Button = gameObject.AddComponent<Button>();

            var mainGo = new GameObject("MainText", typeof(RectTransform));
            mainGo.transform.SetParent(transform, false);
            MainText = mainGo.AddComponent<Text>();
            MainText.font = font;
            MainText.fontSize = 20;
            MainText.alignment = TextAnchor.MiddleCenter;
            MainText.color = Color.white;
            MainText.horizontalOverflow = HorizontalWrapMode.Wrap;
            MainText.verticalOverflow = VerticalWrapMode.Overflow;
            var mainRect = MainText.rectTransform;
            mainRect.anchorMin = new Vector2(0f, 0.25f);
            mainRect.anchorMax = new Vector2(1f, 0.9f);
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;

            var subGo = new GameObject("SubText", typeof(RectTransform));
            subGo.transform.SetParent(transform, false);
            SubText = subGo.AddComponent<Text>();
            SubText.font = font;
            SubText.fontSize = 12;
            SubText.alignment = TextAnchor.LowerCenter;
            SubText.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            var subRect = SubText.rectTransform;
            subRect.anchorMin = new Vector2(0f, 0f);
            subRect.anchorMax = new Vector2(1f, 0.3f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;
        }

        public void SetVisual(string mainLabel, string subLabel, Color backgroundColor, bool interactable)
        {
            MainText.text = mainLabel;
            SubText.text = subLabel;
            Background.color = backgroundColor;
            Button.interactable = interactable;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_longPressTriggered)
            {
                _longPressTriggered = false;
                return;
            }

            Clicked?.Invoke(Position, eventData.button);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressing = true;
            _longPressTriggered = false;
            _pressStartTime = Time.unscaledTime;
            _pressedButton = eventData.button;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressing = false;
        }

        private void Update()
        {
            if (!_isPressing
                || _longPressTriggered
                || _pressedButton != PointerEventData.InputButton.Right
                || Time.unscaledTime - _pressStartTime < LongPressSeconds)
            {
                return;
            }

            _longPressTriggered = true;
            LongPressed?.Invoke(Position, _pressedButton);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hovered?.Invoke(Position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverExited?.Invoke(Position);
        }
    }

    public sealed class CardButtonView : MonoBehaviour
    {
        public int CardInstanceId { get; private set; }
        public Button Button { get; private set; }
        public Image Background { get; private set; }
        public Text TitleText { get; private set; }
        public Text BodyText { get; private set; }

        public void Initialize(int cardInstanceId, Font font, Action<int> onClicked)
        {
            CardInstanceId = cardInstanceId;
            var rect = gameObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(170f, 144f);

            Background = gameObject.AddComponent<Image>();
            Button = gameObject.AddComponent<Button>();
            Button.onClick.AddListener(() => onClicked?.Invoke(CardInstanceId));

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(transform, false);
            TitleText = titleGo.AddComponent<Text>();
            TitleText.font = font;
            TitleText.fontSize = 16;
            TitleText.alignment = TextAnchor.UpperCenter;
            TitleText.color = Color.white;
            var titleRect = TitleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.05f, 0.58f);
            titleRect.anchorMax = new Vector2(0.95f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(transform, false);
            BodyText = bodyGo.AddComponent<Text>();
            BodyText.font = font;
            BodyText.fontSize = 12;
            BodyText.alignment = TextAnchor.UpperLeft;
            BodyText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            BodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            BodyText.verticalOverflow = VerticalWrapMode.Truncate;
            var bodyRect = BodyText.rectTransform;
            bodyRect.anchorMin = new Vector2(0.06f, 0.08f);
            bodyRect.anchorMax = new Vector2(0.94f, 0.58f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;
        }

        public void SetVisual(string title, string body, Color color, bool selected, bool interactable)
        {
            TitleText.text = title;
            BodyText.text = body;
            Background.color = selected ? Color.Lerp(color, Color.white, 0.25f) : color;
            Button.interactable = interactable;
        }
    }

    public sealed class BattleHUDController
    {
        private readonly Dictionary<GridPosition, BoardCellView> _boardCells = new Dictionary<GridPosition, BoardCellView>();
        private readonly Dictionary<int, CardButtonView> _cardButtons = new Dictionary<int, CardButtonView>();
        private BattleBoard3DController _board3DController;
        private Font _font;
        private GridLayoutGroup _boardGrid;
        private RectTransform _boardGridRect;
        private HorizontalLayoutGroup _scenarioButtonsLayout;
        private HorizontalLayoutGroup _handLayout;
        private RectTransform _handContent;
        private Text _turnText;
        private Text _manaText;
        private Text _stateText;
        private Text _detailTitleText;
        private Text _detailBodyText;
        private Text _intentText;
        private Text _logText;
        private Text _snapshotText;
        private Text _previewText;
        private Button _actionButton;
        private Text _actionButtonText;
        private Button _endTurnButton;
        private Button _resetButton;
        private Button _cancelButton;
        private Button _drawPileButton;
        private Button _discardPileButton;
        private Text _drawPileButtonText;
        private Text _discardPileButtonText;
        private RectTransform _pileViewerPanel;
        private Text _pileViewerTitleText;
        private Text _pileViewerBodyText;
        private Button _pileViewerCloseButton;

        public int BoardCellCount => _board3DController?.BoardCellCount ?? _boardCells.Count;
        public int UnitCardCount => _board3DController?.UnitCardCount ?? 0;

        public bool TryGetBoardCellBackground(GridPosition position, out Color color)
        {
            color = default;
            if (_board3DController != null)
            {
                return _board3DController.TryGetBoardCellBackground(position, out color);
            }

            if (!_boardCells.TryGetValue(position, out var view) || view?.Background == null)
            {
                return false;
            }

            color = view.Background.color;
            return true;
        }

        public bool TryGetUnitCardWorldPosition(int entityId, out Vector3 position)
        {
            position = default;
            return _board3DController != null && _board3DController.TryGetUnitCardWorldPosition(entityId, out position);
        }

        public bool TryGetUnitCardIdleTiltAngle(int entityId, out float idleTiltAngle)
        {
            idleTiltAngle = 0f;
            return _board3DController != null && _board3DController.TryGetUnitCardIdleTiltAngle(entityId, out idleTiltAngle);
        }

        public bool TryGetUnitPresentationHasStandardHierarchy(int entityId)
        {
            return _board3DController != null && _board3DController.TryGetUnitPresentationHasStandardHierarchy(entityId);
        }

        public bool TryGetUnitPresentationUsesModelPortrait(int entityId)
        {
            return _board3DController != null && _board3DController.TryGetUnitPresentationUsesModelPortrait(entityId);
        }

        public void Build(BattleSandboxBootstrap host)
        {
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var canvasGo = new GameObject("TactiRogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var root = canvasGo.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var topBar = CreatePanel("TopBar", root, new Color(0.08f, 0.09f, 0.12f, 0.96f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -64f), Vector2.zero);
            var topLayout = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            topLayout.spacing = 10f;
            topLayout.padding = new RectOffset(10, 10, 10, 10);
            topLayout.childAlignment = TextAnchor.MiddleLeft;

            var scenarioPanel = CreatePanel("ScenarioButtons", topBar, new Color(0f, 0f, 0f, 0f), new Vector2(0f, 0f), new Vector2(0.42f, 1f), Vector2.zero, Vector2.zero);
            _scenarioButtonsLayout = scenarioPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            _scenarioButtonsLayout.spacing = 8f;
            _scenarioButtonsLayout.childAlignment = TextAnchor.MiddleLeft;
            _scenarioButtonsLayout.childControlWidth = false;
            _scenarioButtonsLayout.childForceExpandWidth = false;

            var infoPanel = CreatePanel("BattleInfo", topBar, new Color(0f, 0f, 0f, 0f), new Vector2(0.42f, 0f), new Vector2(0.72f, 1f), Vector2.zero, Vector2.zero);
            _turnText = CreateText("TurnText", infoPanel, 18, TextAnchor.MiddleLeft);
            _manaText = CreateText("ManaText", infoPanel, 18, TextAnchor.MiddleCenter);
            _stateText = CreateText("StateText", infoPanel, 16, TextAnchor.MiddleRight);
            PlaceRowTexts(infoPanel, _turnText, _manaText, _stateText);

            var buttonsPanel = CreatePanel("TopButtons", topBar, new Color(0f, 0f, 0f, 0f), new Vector2(0.72f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _drawPileButton = CreateButton("DrawPileButton", buttonsPanel, "DrawPile", new Vector2(120f, 40f), host.HandleDrawPileClicked);
            _drawPileButtonText = _drawPileButton.GetComponentInChildren<Text>();
            _discardPileButton = CreateButton("DiscardPileButton", buttonsPanel, "DiscardPile", new Vector2(130f, 40f), host.HandleDiscardPileClicked);
            _discardPileButtonText = _discardPileButton.GetComponentInChildren<Text>();
            _endTurnButton = CreateButton("EndTurnButton", buttonsPanel, "End Turn", new Vector2(110f, 40f), host.HandleEndTurnClicked);
            _resetButton = CreateButton("ResetButton", buttonsPanel, "Reset", new Vector2(90f, 40f), host.HandleResetClicked);
            _cancelButton = CreateButton("CancelButton", buttonsPanel, "Cancel", new Vector2(90f, 40f), host.HandleCancelClicked);
            PlaceHorizontalButtons(buttonsPanel, _drawPileButton, _discardPileButton, _endTurnButton, _resetButton, _cancelButton);

            var bottomBar = CreatePanel("HandBar", root, new Color(0.08f, 0.09f, 0.12f, 0.96f), new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 178f));
            var handViewport = CreatePanel("HandViewport", bottomBar, new Color(0f, 0f, 0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 12f), new Vector2(-12f, -12f));
            _handContent = handViewport;
            _handLayout = handViewport.gameObject.AddComponent<HorizontalLayoutGroup>();
            _handLayout.spacing = 12f;
            _handLayout.childAlignment = TextAnchor.MiddleCenter;
            _handLayout.childControlWidth = false;
            _handLayout.childForceExpandWidth = false;

            var sidebar = CreatePanel("Sidebar", root, new Color(0.09f, 0.1f, 0.14f, 0.96f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-380f, 188f), new Vector2(0f, -74f));
            BuildSidebar(sidebar, host);

            var boardPanel = CreatePanel("BoardPanel", root, new Color(0f, 0f, 0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 188f), new Vector2(-392f, -74f));
            boardPanel.GetComponent<Image>().raycastTarget = false;
            _board3DController = new BattleBoard3DController();
            _board3DController.Build(host, boardPanel);

            _pileViewerPanel = CreatePanel("PileViewerPanel", root, new Color(0.08f, 0.09f, 0.12f, 0.98f), new Vector2(0.17f, 0.24f), new Vector2(0.52f, 0.72f), Vector2.zero, Vector2.zero);
            _pileViewerTitleText = CreateText("PileViewerTitle", _pileViewerPanel, 20, TextAnchor.UpperLeft);
            _pileViewerBodyText = CreateText("PileViewerBody", _pileViewerPanel, 13, TextAnchor.UpperLeft);
            _pileViewerBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _pileViewerBodyText.verticalOverflow = VerticalWrapMode.Overflow;
            _pileViewerCloseButton = CreateButton("PileViewerCloseButton", _pileViewerPanel, "Close", new Vector2(90f, 34f), host.HandleClosePileViewerClicked);
            PlacePileViewer(_pileViewerPanel, _pileViewerTitleText, _pileViewerBodyText, _pileViewerCloseButton);
            _pileViewerPanel.gameObject.SetActive(false);
        }

        public void Refresh(
            BattleSandboxBootstrap host,
            IReadOnlyList<BattleEvent> motionEvents = null,
            IReadOnlyDictionary<int, EntityPresentationSnapshot> beforeSnapshots = null)
        {
            if (host.State == null)
            {
                return;
            }

            RebuildScenarioButtons(host);
            _board3DController?.Refresh(host, motionEvents, beforeSnapshots);
            RebuildHand(host);
            RefreshSidebar(host);
            RefreshTopBar(host);
            RefreshPileViewer(host);
        }

        private void BuildSidebar(RectTransform sidebar, BattleSandboxBootstrap host)
        {
            var detailPanel = CreatePanel("DetailPanel", sidebar, new Color(0.13f, 0.14f, 0.19f, 1f), new Vector2(0f, 0.68f), new Vector2(1f, 1f), new Vector2(10f, -10f), new Vector2(-10f, -10f));
            _detailTitleText = CreateText("DetailTitle", detailPanel, 22, TextAnchor.UpperLeft);
            _detailBodyText = CreateText("DetailBody", detailPanel, 14, TextAnchor.UpperLeft);
            _detailBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailBodyText.verticalOverflow = VerticalWrapMode.Overflow;
            _actionButton = CreateButton("ActionButton", detailPanel, "Use Action", new Vector2(140f, 38f), host.HandleActionButtonClicked);
            _actionButtonText = _actionButton.GetComponentInChildren<Text>();
            PlaceDetailPanel(detailPanel, _detailTitleText, _detailBodyText, _actionButton);

            var intentPanel = CreatePanel("IntentPanel", sidebar, new Color(0.13f, 0.14f, 0.19f, 1f), new Vector2(0f, 0.47f), new Vector2(1f, 0.68f), new Vector2(10f, -10f), new Vector2(-10f, -10f));
            _intentText = CreateText("IntentText", intentPanel, 14, TextAnchor.UpperLeft);
            StretchText(intentPanel, _intentText, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));

            var previewPanel = CreatePanel("PreviewPanel", sidebar, new Color(0.13f, 0.14f, 0.19f, 1f), new Vector2(0f, 0.3f), new Vector2(1f, 0.47f), new Vector2(10f, -10f), new Vector2(-10f, -10f));
            _previewText = CreateText("PreviewText", previewPanel, 14, TextAnchor.UpperLeft);
            StretchText(previewPanel, _previewText, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));

            var logPanel = CreatePanel("LogPanel", sidebar, new Color(0.13f, 0.14f, 0.19f, 1f), new Vector2(0f, 0.11f), new Vector2(1f, 0.3f), new Vector2(10f, -10f), new Vector2(-10f, -10f));
            _logText = CreateText("LogText", logPanel, 13, TextAnchor.UpperLeft);
            StretchText(logPanel, _logText, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f));

            var snapshotPanel = CreatePanel("SnapshotPanel", sidebar, new Color(0.13f, 0.14f, 0.19f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0.11f), new Vector2(10f, 10f), new Vector2(-10f, -10f));
            _snapshotText = CreateText("SnapshotText", snapshotPanel, 11, TextAnchor.UpperLeft);
            StretchText(snapshotPanel, _snapshotText, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
        }

        private void RebuildScenarioButtons(BattleSandboxBootstrap host)
        {
            if (_scenarioButtonsLayout.transform.childCount != host.Scenarios.Count)
            {
                foreach (Transform child in _scenarioButtonsLayout.transform)
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }

                for (var index = 0; index < host.Scenarios.Count; index++)
                {
                    var scenarioIndex = index;
                    CreateButton($"ScenarioButton_{scenarioIndex}", _scenarioButtonsLayout.transform as RectTransform, host.Scenarios[index].DisplayName, new Vector2(128f, 38f), () => host.LoadScenarioByIndex(scenarioIndex));
                }
            }

            for (var index = 0; index < _scenarioButtonsLayout.transform.childCount; index++)
            {
                var button = _scenarioButtonsLayout.transform.GetChild(index).GetComponent<Button>();
                var image = button.GetComponent<Image>();
                image.color = index == host.CurrentScenarioIndex ? new Color(0.35f, 0.54f, 0.81f, 1f) : new Color(0.22f, 0.24f, 0.3f, 1f);
            }
        }

        private void RebuildBoard(BattleSandboxBootstrap host)
        {
            var width = host.State.Grid.Width;
            var height = host.State.Grid.Height;
            var desiredCount = width * height;

            if (_boardCells.Count == desiredCount)
            {
                return;
            }

            foreach (Transform child in _boardGridRect)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            _boardCells.Clear();
            _boardGrid.constraintCount = width;

            for (var y = height - 1; y >= 0; y--)
            {
                for (var x = 0; x < width; x++)
                {
                    var position = new GridPosition(x, y);
                    var cellGo = new GameObject($"Cell_{x}_{y}");
                    cellGo.transform.SetParent(_boardGridRect, false);
                    var view = cellGo.AddComponent<BoardCellView>();
                    view.Initialize(position, _font);
                    view.Clicked += host.HandleBoardCellClicked;
                    view.LongPressed += host.HandleBoardCellLongPressed;
                    view.Hovered += host.HandleBoardCellHovered;
                    view.HoverExited += host.HandleBoardCellHoverExited;
                    _boardCells[position] = view;
                }
            }
        }

        private void RebuildHand(BattleSandboxBootstrap host)
        {
            var currentIds = new HashSet<int>(host.State.Hand);
            foreach (Transform child in _handContent)
            {
                var view = child.GetComponent<CardButtonView>();
                if (view != null && !currentIds.Contains(view.CardInstanceId))
                {
                    _cardButtons.Remove(view.CardInstanceId);
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }

            foreach (var cardInstanceId in host.State.Hand)
            {
                if (_cardButtons.ContainsKey(cardInstanceId))
                {
                    continue;
                }

                if (!host.State.TryGetCardInstance(cardInstanceId, out var card))
                {
                    continue;
                }

                var cardGo = new GameObject($"Card_{card.CardInstanceId}");
                cardGo.transform.SetParent(_handContent, false);
                var view = cardGo.AddComponent<CardButtonView>();
                view.Initialize(card.CardInstanceId, _font, host.HandleCardClicked);
                _cardButtons[card.CardInstanceId] = view;
            }

            foreach (var button in _cardButtons.Values.ToList())
            {
                if (!currentIds.Contains(button.CardInstanceId))
                {
                    _cardButtons.Remove(button.CardInstanceId);
                }
            }
        }

        private void RefreshBoardVisuals(BattleSandboxBootstrap host)
        {
            var state = host.State;
            var validTargets = new HashSet<GridPosition>(host.PreviewController.ActivePreview?.ValidTargetCells ?? Enumerable.Empty<GridPosition>());
            var previewKinds = host.PreviewController.ActivePreview?.CellPreviews
                .GroupBy(item => item.Position)
                .ToDictionary(group => group.Key, group => new HashSet<CellPreviewKind>(group.Select(item => item.Kind)))
                ?? new Dictionary<GridPosition, HashSet<CellPreviewKind>>();
            var usePreviewIntentState = host.PreviewController.ActivePreview != null
                                        && host.InputController.CurrentState != BattleInputState.MoveTargeting;
            var displayedDangerCells = new HashSet<GridPosition>(
                ((usePreviewIntentState)
                    ? host.PreviewController.ActivePreview.EnemyIntents
                    : host.Engine.BuildIntentViewData(host.State))
                .SelectMany(intent => intent.DangerCells));
            var selectedEntity = host.SelectionController.SelectedEntityId >= 0 && state.Entities.TryGetValue(host.SelectionController.SelectedEntityId, out var selectedEntityValue)
                ? selectedEntityValue
                : null;
            var selectedCell = host.InputController.CurrentState == BattleInputState.BehaviorTargeting && host.SelectionController.HasCommittedMoveCell
                ? host.SelectionController.CommittedMoveCell
                : selectedEntity?.Position ?? default;
            var hasSelectedCell = selectedEntity != null || (host.InputController.CurrentState == BattleInputState.BehaviorTargeting && host.SelectionController.HasCommittedMoveCell);

            foreach (var pair in _boardCells)
            {
                var cell = pair.Key;
                var view = pair.Value;
                var isValid = state.Grid.IsValid(cell);
                var background = isValid ? new Color(0.19f, 0.21f, 0.27f, 1f) : new Color(0.05f, 0.05f, 0.06f, 1f);
                var mainLabel = string.Empty;
                var subLabel = string.Empty;

                if (displayedDangerCells.Contains(cell))
                {
                    background = Color.Lerp(background, new Color(0.65f, 0.14f, 0.14f, 1f), 0.55f);
                }

                if (previewKinds.TryGetValue(cell, out var kinds))
                {
                    if (kinds.Contains(CellPreviewKind.Impact))
                    {
                        background = Color.Lerp(background, new Color(0.95f, 0.66f, 0.18f, 1f), 0.65f);
                    }

                    if (kinds.Contains(CellPreviewKind.Collision))
                    {
                        background = Color.Lerp(background, new Color(0.84f, 0.38f, 0.84f, 1f), 0.75f);
                    }

                    if (kinds.Contains(CellPreviewKind.MovePath))
                    {
                        background = Color.Lerp(background, new Color(0.36f, 0.78f, 0.86f, 1f), 0.45f);
                    }
                }

                if (validTargets.Contains(cell))
                {
                    background = Color.Lerp(background, new Color(0.26f, 0.67f, 0.34f, 1f), 0.45f);
                }

                if (hasSelectedCell && selectedCell.Equals(cell))
                {
                    background = Color.Lerp(background, new Color(0.29f, 0.64f, 0.91f, 1f), 0.7f);
                }

                if (isValid && state.Grid.Occupancy.TryGetValue(cell, out var entityId) && state.Entities.TryGetValue(entityId, out var entity) && entity.IsAlive)
                {
                    var template = host.Engine.Catalog.GetEntity(entity.TemplateId);
                    mainLabel = template?.ShortLabel ?? entity.TemplateId;
                    subLabel = $"HP {entity.CurrentHp}";
                    if (entity.Team == TeamId.Player && entity.RemainingActions > 0 && entity.CanAct)
                    {
                        subLabel += $" | A {entity.RemainingActions}";
                    }

                    background = Color.Lerp(background, template?.Tint ?? Color.white, 0.45f);
                }
                else if (isValid)
                {
                    mainLabel = ".";
                }

                view.SetVisual(mainLabel, subLabel, background, isValid);
            }
        }

        private void RefreshSidebar(BattleSandboxBootstrap host)
        {
            var state = host.State;
            var selectedCardId = host.SelectionController.SelectedCardId;
            var selectedEntityId = host.SelectionController.SelectedEntityId;

                if (selectedCardId >= 0)
            {
                var cardInstance = state.GetCardInstance(selectedCardId);
                var template = cardInstance == null ? null : host.Engine.Catalog.GetCard(cardInstance.TemplateId);
                _detailTitleText.text = template == null ? "No Card" : $"{template.DisplayName}  |  Cost {template.Cost}";
                _detailBodyText.text = template == null ? string.Empty : template.Description;
                _actionButton.gameObject.SetActive(false);
            }
            else if (selectedEntityId >= 0 && state.Entities.TryGetValue(selectedEntityId, out var entity))
            {
                var template = host.Engine.Catalog.GetEntity(entity.TemplateId);
                var keywords = new List<string>();
                foreach (var statusId in template?.StartingStatusIds ?? Array.Empty<string>())
                {
                    keywords.Add(host.Engine.Catalog.GetStatus(statusId)?.DisplayName ?? statusId);
                }

                foreach (var active in entity.Statuses.ActiveStatuses)
                {
                    if (!keywords.Contains(active.TemplateId))
                    {
                        keywords.Add(host.Engine.Catalog.GetStatus(active.TemplateId)?.DisplayName ?? active.TemplateId);
                    }
                }

                var sourceCardName = "None";
                if (state.InBattleUnitCards.TryGetValue(entity.EntityId, out var boundCardInstanceId)
                    && state.TryGetCardInstance(boundCardInstanceId, out var boundCardInstance))
                {
                    sourceCardName = host.Engine.Catalog.GetCard(boundCardInstance.TemplateId)?.DisplayName ?? boundCardInstance.TemplateId;
                }

                _detailTitleText.text = template == null ? entity.TemplateId : template.DisplayName;
                _detailBodyText.text =
                    $"Team: {entity.Team}\n" +
                    $"HP: {entity.CurrentHp}/{entity.MaxHp}\n" +
                    $"Attack: {entity.GetEffectiveAttack(host.Engine.Catalog)}\n" +
                    $"Actions: {entity.RemainingActions}\n" +
                    $"Action: {(string.IsNullOrWhiteSpace(entity.ActionId) ? "None" : host.Engine.Catalog.GetAction(entity.ActionId)?.DisplayName ?? entity.ActionId)}\n" +
                    $"Move: {(template?.MoveProfile == null ? "None" : $"{template.MoveProfile.MoveType} {template.MoveProfile.MoveRange}")}\n" +
                    $"Source Card: {sourceCardName}\n" +
                    $"Keywords: {(keywords.Count == 0 ? "None" : string.Join(", ", keywords))}\n\n" +
                    $"{template?.Description}";

                _actionButton.gameObject.SetActive(false);
            }
            else
            {
                _detailTitleText.text = host.State.ScenarioDisplayName;
                _detailBodyText.text =
                    "Left click a friendly unit to inspect it.\n" +
                    "Standard units enter move targeting first.\n" +
                    "Click the current cell to stay in place and continue.\n" +
                    "Click a card in hand, then click the board to play it.\n" +
                    "Use DrawPile and DiscardPile buttons to inspect card loops.\n" +
                    "Red cells show enemy intent danger.\n" +
                    "Orange and magenta previews show impacts and collision crits.";
                _actionButton.gameObject.SetActive(false);
            }

            var intentViews = host.PreviewController.ActivePreview != null && host.InputController.CurrentState != BattleInputState.MoveTargeting
                ? host.PreviewController.ActivePreview.EnemyIntents
                : host.Engine.BuildIntentViewData(host.State);
            _intentText.text = intentViews.Count == 0
                ? "Enemy intents:\nNone"
                : "Enemy intents:\n" + string.Join("\n", intentViews.Select(intent =>
                {
                    var details = $"{intent.TargetingMode} / {intent.RevalidationPolicy} / {intent.FallbackMode}";
                    var reason = intent.IsCancelled && !string.IsNullOrWhiteSpace(intent.DebugReason)
                        ? $" [{intent.DebugReason}]"
                        : string.Empty;
                    return $"- {intent.Summary} ({details}){reason}";
                }));

            var preview = host.PreviewController.ActivePreview;
            _previewText.text =
                $"Input: {host.InputController.CurrentState}\n" +
                $"Selected Unit: {(selectedEntityId >= 0 ? selectedEntityId.ToString() : "None")}\n" +
                $"Move Cell: {(host.SelectionController.HasCommittedMoveCell ? host.SelectionController.CommittedMoveCell.ToString() : "None")}\n" +
                $"Selected Card: {(selectedCardId >= 0 ? selectedCardId.ToString() : "None")}\n" +
                $"Preview Valid: {(preview == null ? "No preview" : preview.Valid ? "Yes" : $"No ({preview.FailureReason})")}\n" +
                $"Valid Targets: {(preview == null ? 0 : preview.ValidTargetCells.Count)}";

            var logStart = Math.Max(0, host.LogLines.Count - 18);
            _logText.text = "Battle log:\n" + string.Join("\n", host.LogLines.Skip(logStart));
            _snapshotText.text = UnityEngine.JsonUtility.ToJson(host.State.CaptureSnapshot(host.Engine.Catalog), true);
        }

        private void RefreshTopBar(BattleSandboxBootstrap host)
        {
            _turnText.text = $"{host.State.ScenarioDisplayName} | Turn {host.State.TurnNumber}";
            _manaText.text = $"Mana {host.State.CurrentMana}/{host.State.MaxMana}";
            _stateText.text = $"{host.State.Phase} | {host.InputController.CurrentState}";
            _drawPileButtonText.text = $"DrawPile ({host.State.DrawPile.Count})";
            _discardPileButtonText.text = $"DiscardPile ({host.State.DiscardPile.Count})";
            _endTurnButton.interactable = host.State.Phase == BattlePhase.PlayerAction && !host.State.PlayerWon && !host.State.PlayerLost;

            foreach (var cardInstanceId in host.State.Hand)
            {
                if (!_cardButtons.TryGetValue(cardInstanceId, out var view) || !host.State.TryGetCardInstance(cardInstanceId, out var card))
                {
                    continue;
                }

                var template = host.Engine.Catalog.GetCard(card.TemplateId);
                var selected = host.SelectionController.SelectedCardId == card.CardInstanceId;
                var affordable = host.State.CurrentMana >= (template?.Cost ?? 0);
                var body = template == null
                    ? string.Empty
                    : $"Cost {template.Cost}\n{template.Description}";
                view.SetVisual(template?.DisplayName ?? card.TemplateId, body, template?.Tint ?? new Color(0.3f, 0.3f, 0.3f, 1f), selected, affordable);
            }
        }

        private void RefreshPileViewer(BattleSandboxBootstrap host)
        {
            var zone = host.ViewedPileZone;
            var isVisible = zone == CardZone.DrawPile || zone == CardZone.DiscardPile;
            _pileViewerPanel.gameObject.SetActive(isVisible);
            if (!isVisible)
            {
                return;
            }

            var entries = host.GetViewedPileEntries();
            var zoneName = zone == CardZone.DrawPile ? "DrawPile" : "DiscardPile";
            _pileViewerTitleText.text = $"{zoneName}  |  {entries.Count} cards";
            _pileViewerBodyText.text = entries.Count == 0
                ? "No cards."
                : string.Join("\n", entries.Select(entry => $"- {entry.DisplayName} | Cost {entry.Cost} | {entry.CardKind}"));
        }

        private static RectTransform CreatePanel(string name, RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var panelGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(parent, false);
            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            panelGo.GetComponent<Image>().color = color;
            return rect;
        }

        private Button CreateButton(string name, RectTransform parent, string label, Vector2 size, Action onClick)
        {
            var buttonGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, false);
            var rect = buttonGo.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            var image = buttonGo.GetComponent<Image>();
            image.color = new Color(0.22f, 0.24f, 0.3f, 1f);
            var button = buttonGo.GetComponent<Button>();
            button.onClick.AddListener(() => onClick?.Invoke());

            var text = CreateText("Label", rect, 14, TextAnchor.MiddleCenter);
            text.text = label;
            StretchText(rect, text, Vector2.zero, Vector2.one);
            return button;
        }

        private Text CreateText(string name, RectTransform parent, int fontSize, TextAnchor anchor)
        {
            var textGo = new GameObject(name, typeof(RectTransform));
            textGo.transform.SetParent(parent, false);
            var text = textGo.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void StretchText(RectTransform parent, Text text, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void PlaceRowTexts(RectTransform parent, Text left, Text center, Text right)
        {
            StretchText(parent, left, new Vector2(0f, 0f), new Vector2(0.36f, 1f));
            StretchText(parent, center, new Vector2(0.36f, 0f), new Vector2(0.66f, 1f));
            StretchText(parent, right, new Vector2(0.66f, 0f), new Vector2(1f, 1f));
        }

        private static void PlaceHorizontalButtons(RectTransform parent, params Button[] buttons)
        {
            var layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childForceExpandWidth = false;
        }

        private static void PlacePileViewer(RectTransform panel, Text title, Text body, Button closeButton)
        {
            StretchText(panel, title, new Vector2(0.05f, 0.82f), new Vector2(0.72f, 0.95f));
            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.77f, 0.84f);
            closeRect.anchorMax = new Vector2(0.95f, 0.95f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            StretchText(panel, body, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.78f));
        }

        private static void PlaceDetailPanel(RectTransform parent, Text title, Text body, Button actionButton)
        {
            StretchText(parent, title, new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.94f));
            StretchText(parent, body, new Vector2(0.04f, 0.24f), new Vector2(0.96f, 0.72f));
            var rect = actionButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.04f, 0.05f);
            rect.anchorMax = new Vector2(0.5f, 0.2f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
