using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TactiRogue
{
    public enum BattleInputState
    {
        Idle,
        MoveTargeting,
        BehaviorTargeting,
        CardSelected,
        CardTargeting,
    }

    public sealed class BattleSelectionController
    {
        public int SelectedEntityId { get; private set; } = -1;
        public int SelectedCardId { get; private set; } = -1;
        public GridPosition CommittedMoveCell { get; private set; }
        public bool HasCommittedMoveCell { get; private set; }

        public void SelectEntity(int entityId)
        {
            SelectedEntityId = entityId;
            SelectedCardId = -1;
            HasCommittedMoveCell = false;
        }

        public void SelectCard(int cardId)
        {
            SelectedCardId = cardId;
            SelectedEntityId = -1;
            HasCommittedMoveCell = false;
        }

        public void SetCommittedMoveCell(GridPosition moveCell)
        {
            CommittedMoveCell = moveCell;
            HasCommittedMoveCell = true;
        }

        public void ClearCommittedMoveCell()
        {
            HasCommittedMoveCell = false;
        }

        public void Clear()
        {
            SelectedEntityId = -1;
            SelectedCardId = -1;
            HasCommittedMoveCell = false;
        }
    }

    public sealed class BattleInputController
    {
        public BattleInputState CurrentState { get; private set; } = BattleInputState.Idle;

        public void Clear()
        {
            CurrentState = BattleInputState.Idle;
        }

        public void BeginMoveTargeting()
        {
            CurrentState = BattleInputState.MoveTargeting;
        }

        public void BeginBehaviorTargeting()
        {
            CurrentState = BattleInputState.BehaviorTargeting;
        }

        public void SelectCard()
        {
            CurrentState = BattleInputState.CardSelected;
        }

        public void BeginCardTargeting()
        {
            CurrentState = BattleInputState.CardTargeting;
        }
    }

    public sealed class PreviewPresentationController
    {
        public PreviewResult ActivePreview { get; private set; }

        public void SetPreview(PreviewResult preview)
        {
            ActivePreview = preview;
        }

        public void Clear()
        {
            ActivePreview = null;
        }
    }

    public sealed class EntityPresentationSnapshot
    {
        public GridPosition Position;
        public bool IsAlive;
        public bool OccupiesCell;
    }

    public sealed class BattleSandboxBootstrap : MonoBehaviour
    {
        private readonly List<string> _logLines = new List<string>();
        private readonly List<ScenarioDefinition> _scenarios = new List<ScenarioDefinition>();
        private BattleHUDController _hudController;

        public TactiRogueEngine Engine { get; private set; }
        public BattleState State { get; private set; }
        public IReadOnlyList<ScenarioDefinition> Scenarios => _scenarios;
        public int CurrentScenarioIndex { get; private set; }
        public bool IsInitialized { get; private set; }
        public IReadOnlyList<string> LogLines => _logLines;
        public BattleSelectionController SelectionController { get; private set; }
        public BattleInputController InputController { get; private set; }
        public PreviewPresentationController PreviewController { get; private set; }
        public int BoardCellCount => _hudController?.BoardCellCount ?? 0;
        public int UnitCardCount => _hudController?.UnitCardCount ?? 0;
        public CardZone ViewedPileZone { get; private set; }

        private void Awake()
        {
            InitializeIfNeeded();
        }

        public void InitializeIfNeeded()
        {
            if (IsInitialized)
            {
                return;
            }

            EnsureEventSystem();
            Engine = new TactiRogueEngine(TactiRogueContentProvider.LoadOrCreateDatabase());
            SelectionController = new BattleSelectionController();
            InputController = new BattleInputController();
            PreviewController = new PreviewPresentationController();
            _scenarios.AddRange(TactiRogueScenarioRepository.LoadAll());

            _hudController = new BattleHUDController();
            _hudController.Build(this);

            if (_scenarios.Count > 0)
            {
                LoadScenarioByIndex(0);
            }

            IsInitialized = true;
        }

        public void LoadScenarioByIndex(int index)
        {
            if (index < 0 || index >= _scenarios.Count)
            {
                return;
            }

            CurrentScenarioIndex = index;
            State = Engine.CreateBattle(_scenarios[index]);
            SelectionController.Clear();
            InputController.Clear();
            PreviewController.Clear();
            ViewedPileZone = CardZone.None;
            _logLines.Clear();
            AppendLog($"Loaded scenario: {State.ScenarioDisplayName}");
            AppendLog("Click a friendly unit to start acting, or click a card in hand.");
            AppendLog("Red cells show the enemy's current danger area.");
            RefreshHud();
        }

        public bool TryGetBoardCellBackground(GridPosition position, out Color color)
        {
            color = default;
            return _hudController != null && _hudController.TryGetBoardCellBackground(position, out color);
        }

        public bool TryGetUnitCardWorldPosition(int entityId, out Vector3 position)
        {
            position = default;
            return _hudController != null && _hudController.TryGetUnitCardWorldPosition(entityId, out position);
        }

        public bool TryGetUnitCardIdleTiltAngle(int entityId, out float idleTiltAngle)
        {
            idleTiltAngle = 0f;
            return _hudController != null && _hudController.TryGetUnitCardIdleTiltAngle(entityId, out idleTiltAngle);
        }

        public bool TryGetUnitPresentationHasStandardHierarchy(int entityId)
        {
            return _hudController != null && _hudController.TryGetUnitPresentationHasStandardHierarchy(entityId);
        }

        public IReadOnlyList<CardPileViewEntry> GetViewedPileEntries()
        {
            if (State == null || ViewedPileZone == CardZone.None)
            {
                return Array.Empty<CardPileViewEntry>();
            }

            return Engine.GetPileViewEntries(State, ViewedPileZone);
        }

        public void HandleBoardCellClicked(GridPosition position)
        {
            if (State == null)
            {
                return;
            }

            var hasEntity = State.Grid.Occupancy.TryGetValue(position, out var entityId);

            switch (InputController.CurrentState)
            {
                case BattleInputState.MoveTargeting:
                    if (SelectionController.SelectedEntityId >= 0 && Engine.GetValidMoveTargetCells(State, SelectionController.SelectedEntityId).Contains(position))
                    {
                        SelectionController.SetCommittedMoveCell(position);
                        InputController.BeginBehaviorTargeting();
                        RefreshSelectionPreview();
                        RefreshHud();
                        return;
                    }

                    break;
                case BattleInputState.BehaviorTargeting:
                    ExecuteUnitTurn(position, hasEntity ? entityId : -1, hasEntity);
                    return;
                case BattleInputState.CardSelected:
                case BattleInputState.CardTargeting:
                    ExecuteCard(position, hasEntity ? entityId : -1, hasEntity);
                    return;
            }

            if (hasEntity && State.Entities.TryGetValue(entityId, out var entity))
            {
                SelectionController.SelectEntity(entityId);
                if (entity.Team == TeamId.Player && entity.CanAct && entity.RemainingActions > 0 && !string.IsNullOrWhiteSpace(entity.ActionId))
                {
                    if (Engine.UsesSeparateMovePhase(State, entityId))
                    {
                        InputController.BeginMoveTargeting();
                    }
                    else
                    {
                        SelectionController.SetCommittedMoveCell(entity.Position);
                        InputController.BeginBehaviorTargeting();
                    }
                }
                else
                {
                    InputController.Clear();
                }
            }
            else
            {
                SelectionController.Clear();
                InputController.Clear();
            }

            PreviewController.Clear();
            RefreshHud();
        }

        public void HandleBoardCellHovered(GridPosition position)
        {
            if (State == null)
            {
                return;
            }

            var hasEntity = State.Grid.Occupancy.TryGetValue(position, out var entityId);

            switch (InputController.CurrentState)
            {
                case BattleInputState.MoveTargeting when SelectionController.SelectedEntityId >= 0:
                    PreviewController.SetPreview(Engine.Preview(State, new PreviewRequest
                    {
                        SourceKind = PreviewSourceKind.UnitAction,
                        Stage = UnitTurnStage.MoveTargeting,
                        ActorEntityId = SelectionController.SelectedEntityId,
                        TargetCell = position,
                        TargetEntityId = hasEntity ? entityId : -1,
                        HasTargetCell = true,
                        HasTargetEntity = hasEntity,
                    }));
                    RefreshHud();
                    break;
                case BattleInputState.BehaviorTargeting when SelectionController.SelectedEntityId >= 0:
                    PreviewController.SetPreview(Engine.Preview(State, new PreviewRequest
                    {
                        SourceKind = PreviewSourceKind.UnitAction,
                        Stage = UnitTurnStage.BehaviorTargeting,
                        ActorEntityId = SelectionController.SelectedEntityId,
                        CommittedMoveCell = SelectionController.CommittedMoveCell,
                        HasCommittedMoveCell = SelectionController.HasCommittedMoveCell,
                        TargetCell = position,
                        TargetEntityId = hasEntity ? entityId : -1,
                        HasTargetCell = true,
                        HasTargetEntity = hasEntity,
                    }));
                    RefreshHud();
                    break;
                case BattleInputState.CardSelected:
                case BattleInputState.CardTargeting:
                    if (SelectionController.SelectedCardId >= 0)
                    {
                        InputController.BeginCardTargeting();
                        PreviewController.SetPreview(Engine.Preview(State, new PreviewRequest
                        {
                            SourceKind = PreviewSourceKind.Card,
                            CardInstanceId = SelectionController.SelectedCardId,
                            TargetCell = position,
                            TargetEntityId = hasEntity ? entityId : -1,
                            HasTargetCell = true,
                            HasTargetEntity = hasEntity,
                        }));
                        RefreshHud();
                    }

                    break;
            }
        }

        public void HandleBoardCellHoverExited(GridPosition _)
        {
            if (State == null)
            {
                return;
            }

            RefreshSelectionPreview();
            RefreshHud();
        }

        public void HandleCardClicked(int cardInstanceId)
        {
            if (State == null)
            {
                return;
            }

            if (SelectionController.SelectedCardId == cardInstanceId)
            {
                SelectionController.Clear();
                InputController.Clear();
                PreviewController.Clear();
            }
            else
            {
                SelectionController.SelectCard(cardInstanceId);
                InputController.SelectCard();
                RefreshSelectionPreview();
            }

            RefreshHud();
        }

        public void HandleDrawPileClicked()
        {
            TogglePileViewer(CardZone.DrawPile, BattleEventType.DrawPileClicked, $"[Card] Viewed DrawPile ({State?.DrawPile.Count ?? 0}).");
        }

        public void HandleDiscardPileClicked()
        {
            TogglePileViewer(CardZone.DiscardPile, BattleEventType.DiscardPileClicked, $"[Card] Viewed DiscardPile ({State?.DiscardPile.Count ?? 0}).");
        }

        public void HandleClosePileViewerClicked()
        {
            ViewedPileZone = CardZone.None;
            RefreshHud();
        }

        public void HandleActionButtonClicked()
        {
            if (State == null || SelectionController.SelectedEntityId < 0)
            {
                return;
            }

            if (!State.Entities.TryGetValue(SelectionController.SelectedEntityId, out var entity) || !entity.IsAlive)
            {
                return;
            }

            if (!entity.CanAct || entity.RemainingActions <= 0 || string.IsNullOrWhiteSpace(entity.ActionId))
            {
                AppendLog("Selected unit cannot act.");
                RefreshHud();
                return;
            }

            if (Engine.UsesSeparateMovePhase(State, entity.EntityId))
            {
                SelectionController.ClearCommittedMoveCell();
                InputController.BeginMoveTargeting();
            }
            else
            {
                SelectionController.SetCommittedMoveCell(entity.Position);
                InputController.BeginBehaviorTargeting();
            }

            RefreshSelectionPreview();
            RefreshHud();
        }

        public void HandleEndTurnClicked()
        {
            if (State == null)
            {
                return;
            }

            var beforeSnapshots = CapturePresentationSnapshots();
            ApplyResult(Engine.EndTurn(State), beforeSnapshots);
        }

        public void HandleResetClicked()
        {
            LoadScenarioByIndex(CurrentScenarioIndex);
        }

        public void HandleCancelClicked()
        {
            if (State != null
                && InputController.CurrentState == BattleInputState.BehaviorTargeting
                && SelectionController.SelectedEntityId >= 0
                && Engine.UsesSeparateMovePhase(State, SelectionController.SelectedEntityId))
            {
                SelectionController.ClearCommittedMoveCell();
                InputController.BeginMoveTargeting();
                RefreshSelectionPreview();
                RefreshHud();
                return;
            }

            SelectionController.Clear();
            InputController.Clear();
            PreviewController.Clear();
            RefreshHud();
        }

        private void ExecuteUnitTurn(GridPosition position, int entityId, bool hasEntity)
        {
            var beforeSnapshots = CapturePresentationSnapshots();
            var result = Engine.ResolveUnitTurn(State, new UnitTurnRequest
            {
                ActorEntityId = SelectionController.SelectedEntityId,
                MoveTargetCell = SelectionController.HasCommittedMoveCell ? SelectionController.CommittedMoveCell : default,
                HasMoveTargetCell = SelectionController.HasCommittedMoveCell,
                BehaviorTargetCell = position,
                BehaviorTargetEntityId = entityId,
                HasBehaviorTargetCell = true,
                HasBehaviorTargetEntity = hasEntity,
            });

            ApplyResult(result, beforeSnapshots);
        }

        private void ExecuteCard(GridPosition position, int entityId, bool hasEntity)
        {
            var beforeSnapshots = CapturePresentationSnapshots();
            var result = Engine.ResolveCard(State, new PlayCardRequest
            {
                CardInstanceId = SelectionController.SelectedCardId,
                TargetCell = position,
                TargetEntityId = entityId,
                HasTargetCell = true,
                HasTargetEntity = hasEntity,
            });

            ApplyResult(result, beforeSnapshots);
        }

        private void ApplyResult(ActionResult result, IReadOnlyDictionary<int, EntityPresentationSnapshot> beforeSnapshots)
        {
            if (!result.Success)
            {
                AppendLog($"Action failed: {result.FailureReason}");
                RefreshSelectionPreview();
                RefreshHud();
                return;
            }

            if (result.Events.Count == 0)
            {
                AppendLog("No battle events were produced.");
            }
            else
            {
                foreach (var battleEvent in result.Events)
                {
                    AppendLog(battleEvent.Message);
                }
            }

            SelectionController.Clear();
            InputController.Clear();
            PreviewController.Clear();
            RefreshHud(result.Events, beforeSnapshots);
        }

        private void TogglePileViewer(CardZone zone, BattleEventType eventType, string message)
        {
            if (State == null)
            {
                return;
            }

            ViewedPileZone = ViewedPileZone == zone ? CardZone.None : zone;
            AppendLog(CreateUiBattleEvent(eventType, message).Message);
            RefreshHud();
        }

        private void RefreshSelectionPreview()
        {
            if (State == null)
            {
                PreviewController.Clear();
                return;
            }

            if (InputController.CurrentState == BattleInputState.MoveTargeting && SelectionController.SelectedEntityId >= 0)
            {
                PreviewController.SetPreview(Engine.Preview(State, new PreviewRequest
                {
                    SourceKind = PreviewSourceKind.UnitAction,
                    Stage = UnitTurnStage.MoveTargeting,
                    ActorEntityId = SelectionController.SelectedEntityId,
                }));
                return;
            }

            if (InputController.CurrentState == BattleInputState.BehaviorTargeting && SelectionController.SelectedEntityId >= 0)
            {
                PreviewController.SetPreview(Engine.Preview(State, new PreviewRequest
                {
                    SourceKind = PreviewSourceKind.UnitAction,
                    Stage = UnitTurnStage.BehaviorTargeting,
                    ActorEntityId = SelectionController.SelectedEntityId,
                    CommittedMoveCell = SelectionController.CommittedMoveCell,
                    HasCommittedMoveCell = SelectionController.HasCommittedMoveCell,
                }));
                return;
            }

            if ((InputController.CurrentState == BattleInputState.CardSelected || InputController.CurrentState == BattleInputState.CardTargeting) && SelectionController.SelectedCardId >= 0)
            {
                PreviewController.SetPreview(Engine.Preview(State, new PreviewRequest
                {
                    SourceKind = PreviewSourceKind.Card,
                    CardInstanceId = SelectionController.SelectedCardId,
                }));
                return;
            }

            PreviewController.Clear();
        }

        private IReadOnlyDictionary<int, EntityPresentationSnapshot> CapturePresentationSnapshots()
        {
            if (State == null)
            {
                return new Dictionary<int, EntityPresentationSnapshot>();
            }

            return State.Entities.ToDictionary(
                pair => pair.Key,
                pair => new EntityPresentationSnapshot
                {
                    Position = pair.Value.Position,
                    IsAlive = pair.Value.IsAlive,
                    OccupiesCell = pair.Value.OccupiesCell,
                });
        }

        private void RefreshHud(
            IReadOnlyList<BattleEvent> motionEvents = null,
            IReadOnlyDictionary<int, EntityPresentationSnapshot> beforeSnapshots = null)
        {
            _hudController?.Refresh(this, motionEvents, beforeSnapshots);
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _logLines.Add(message);
            if (_logLines.Count > 80)
            {
                _logLines.RemoveAt(0);
            }
        }

        private static BattleEvent CreateUiBattleEvent(BattleEventType eventType, string message)
        {
            return new BattleEvent
            {
                EventType = eventType,
                Message = message,
            };
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystem);
        }
    }

    public static class BattleSandboxAutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (UnityEngine.Object.FindObjectOfType<BattleSandboxBootstrap>() != null)
            {
                return;
            }

            var bootstrap = new GameObject("BattleSandboxBootstrap");
            bootstrap.AddComponent<BattleSandboxBootstrap>();
        }
    }
}
