using System;
using System.Collections.Generic;
using System.Linq;

namespace TactiRogue
{
    public sealed partial class TactiRogueEngine
    {
        private readonly TactiRogueContentDatabase _catalog;

        public TactiRogueEngine(TactiRogueContentDatabase catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public TactiRogueContentDatabase Catalog => _catalog;

        public BattleState CreateBattle(ScenarioDefinition scenario)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            var state = new BattleState
            {
                ScenarioId = scenario.Id,
                ScenarioDisplayName = scenario.DisplayName,
                CurrentMana = scenario.StartingMana,
                MaxMana = scenario.MaxMana,
                CardsPerTurn = scenario.CardsPerTurn,
                RandomSeed = scenario.RandomSeed,
                Grid = CreateGridState(scenario),
            };

            foreach (var spawn in scenario.Spawns ?? Array.Empty<ScenarioEntitySpawn>())
            {
                var template = _catalog.GetEntity(spawn.TemplateId);
                if (template == null)
                {
                    continue;
                }

                var team = TryParseTeam(spawn.Team, out var parsedTeam) ? parsedTeam : template.DefaultTeam;
                var entity = CreateEntityFromTemplate(state, template, team, new GridPosition(spawn.X, spawn.Y), -1, team == TeamId.Player);
                AddEntityToState(state, entity);
            }

            state.DrawPile = BuildCardInstances(state, scenario.StartingDeck ?? Array.Empty<string>());
            Shuffle(state.DrawPile, state);
            state.Phase = BattlePhase.PlayerDrawPhase;
            ResetPlayerActions(state);
            DrawCards(state, state.CardsPerTurn, new List<BattleEvent>());
            RegenerateEnemyIntents(state, new List<BattleEvent>());
            CheckForWinOrLoss(state, new List<BattleEvent>());
            if (!state.PlayerWon && !state.PlayerLost)
            {
                state.Phase = BattlePhase.PlayerAction;
            }

            return state;
        }

        public List<GridPosition> GetValidActionTargetCells(BattleState state, int actorEntityId)
        {
            if (!state.Entities.TryGetValue(actorEntityId, out var actor) || !actor.IsAlive)
            {
                return new List<GridPosition>();
            }

            var action = _catalog.GetAction(actor.ActionId);
            return action == null ? new List<GridPosition>() : GetValidTargetCells(state, actor, action);
        }

        public List<GridPosition> GetValidCardTargetCells(BattleState state, int cardInstanceId)
        {
            if (!state.Hand.Contains(cardInstanceId) || !state.TryGetCardInstance(cardInstanceId, out var card))
            {
                return new List<GridPosition>();
            }

            var template = _catalog.GetCard(card.TemplateId);
            return template == null ? new List<GridPosition>() : GetValidTargetCellsForCard(state, template);
        }

        public List<CardPileViewEntry> GetPileViewEntries(BattleState state, CardZone zone)
        {
            IEnumerable<CardInstance> cards = zone switch
            {
                CardZone.DrawPile => state.GetDrawPileCards(),
                CardZone.Hand => state.GetHandCards(),
                CardZone.DiscardPile => state.GetDiscardPileCards(),
                CardZone.InBattleUnit => state.InBattleUnitCards.Values
                    .Select(state.GetCardInstance)
                    .Where(card => card != null),
                _ => Enumerable.Empty<CardInstance>(),
            };

            return cards
                .Select(card =>
                {
                    var template = _catalog.GetCard(card.TemplateId);
                    return new CardPileViewEntry
                    {
                        CardInstanceId = card.CardInstanceId,
                        TemplateId = card.TemplateId,
                        DisplayName = template?.DisplayName ?? card.TemplateId,
                        Cost = template?.Cost ?? 0,
                        CardKind = (template?.CardKind ?? CardKind.Spell).ToString(),
                        Zone = card.CurrentZone,
                        BoundEntityId = card.BoundEntityId,
                    };
                })
                .OrderBy(entry => entry.DisplayName)
                .ThenBy(entry => entry.CardInstanceId)
                .ToList();
        }

        public List<IntentViewData> BuildIntentViewData(BattleState state)
        {
            return state.EnemyIntents
                .Where(intent => state.Entities.TryGetValue(intent.ActorEntityId, out var actor) && actor.IsAlive)
                .Select(intent => BuildIntentViewData(state, intent))
                .ToList();
        }

        public ActionResult ResolveAction(BattleState state, ActionRequest request)
        {
            var result = CreateBaseResult(state);
            if (state.Phase != BattlePhase.PlayerAction)
            {
                return Fail(result, "Player actions are only available during the player phase.");
            }

            if (!state.Entities.TryGetValue(request.ActorEntityId, out var actor) || !actor.IsAlive || actor.Team != TeamId.Player)
            {
                return Fail(result, "Invalid acting unit.");
            }

            if (!actor.CanAct || actor.RemainingActions <= 0)
            {
                return Fail(result, "That unit has no actions remaining.");
            }

            var action = _catalog.GetAction(actor.ActionId);
            if (action == null)
            {
                return Fail(result, "The selected unit has no action definition.");
            }

            if (!ValidateActionTarget(state, actor, action, request.TargetCell, request.TargetEntityId, request.HasTargetCell, request.HasTargetEntity))
            {
                return Fail(result, "Action target is not valid.");
            }

            ExecuteAction(state, actor.EntityId, action, request.TargetCell, request.TargetEntityId, request.HasTargetCell, request.HasTargetEntity, result.Events, true);
            RefreshEnemyIntents(state, result.Events);
            CheckForWinOrLoss(state, result.Events);
            return Finish(result, state);
        }

        public ActionResult ResolveCard(BattleState state, PlayCardRequest request)
        {
            var result = CreateBaseResult(state);
            if (state.Phase != BattlePhase.PlayerAction)
            {
                return Fail(result, "Cards can only be played during the player phase.");
            }

            if (!state.Hand.Contains(request.CardInstanceId) || !state.TryGetCardInstance(request.CardInstanceId, out var cardInstance))
            {
                return Fail(result, "Card is not in hand.");
            }

            var cardTemplate = _catalog.GetCard(cardInstance.TemplateId);
            if (cardTemplate == null)
            {
                return Fail(result, "Card template is missing.");
            }

            if (state.CurrentMana < cardTemplate.Cost)
            {
                return Fail(result, "Not enough mana.");
            }

            if (cardTemplate.CardKind == CardKind.Unit)
            {
                if (!request.HasTargetCell || !CanSummonCardAt(state, cardTemplate, request.TargetCell))
                {
                    return Fail(result, "Summon cell is not valid.");
                }

                var entityTemplate = _catalog.GetEntity(cardTemplate.SummonEntityId);
                if (entityTemplate == null)
                {
                    return Fail(result, "Summon template is missing.");
                }

                state.CurrentMana -= cardTemplate.Cost;
                var summoned = CreateEntityFromTemplate(state, entityTemplate, entityTemplate.DefaultTeam, request.TargetCell, state.CommanderEntityId, true);
                AddEntityToState(state, summoned);
                BindUnitCardToEntity(state, cardInstance, summoned.EntityId);
                result.Events.Add(CreateEvent(BattleEventType.CardPlayed, $"Played card: {cardTemplate.DisplayName}", -1, -1, request.TargetCell, cardTemplate.Cost));
                result.Events.Add(CreateEvent(BattleEventType.PlayUnitToBattle, $"[Card] Unit card {cardTemplate.DisplayName} entered battle as Entity#{summoned.EntityId}.", summoned.EntityId, -1, request.TargetCell, cardTemplate.Cost));
                result.Events.Add(CreateEvent(BattleEventType.EntitySummoned, $"Summoned {entityTemplate.DisplayName}.", summoned.EntityId, -1, summoned.Position, 0));
            }
            else
            {
                if (!state.Entities.TryGetValue(state.CommanderEntityId, out var commander) || !commander.IsAlive)
                {
                    return Fail(result, "Commander is missing.");
                }

                var action = _catalog.GetAction(cardTemplate.ActionId);
                if (action == null)
                {
                    return Fail(result, "Card action definition is missing.");
                }

                if (!ValidateActionTarget(state, commander, action, request.TargetCell, request.TargetEntityId, request.HasTargetCell, request.HasTargetEntity))
                {
                    return Fail(result, "Card target is not valid.");
                }

                state.CurrentMana -= cardTemplate.Cost;
                MoveCardToZone(state, cardInstance, CardZone.DiscardPile);
                result.Events.Add(CreateEvent(BattleEventType.CardPlayed, $"Played card: {cardTemplate.DisplayName}", state.CommanderEntityId, -1, request.TargetCell, cardTemplate.Cost));
                ExecuteAction(state, commander.EntityId, action, request.TargetCell, request.TargetEntityId, request.HasTargetCell, request.HasTargetEntity, result.Events, false);
                result.Events.Add(CreateEvent(BattleEventType.PlaySpellToDiscard, $"[Card] Spell {cardTemplate.DisplayName} resolved and moved to DiscardPile.", state.CommanderEntityId, -1, request.TargetCell, cardTemplate.Cost));
            }

            RefreshEnemyIntents(state, result.Events);
            CheckForWinOrLoss(state, result.Events);
            return Finish(result, state);
        }

        public ActionResult EndTurn(BattleState state)
        {
            var result = CreateBaseResult(state);
            if (state.Phase != BattlePhase.PlayerAction)
            {
                return Fail(result, "Turn cannot be ended right now.");
            }

            result.Events.Add(CreateEvent(BattleEventType.TurnEnded, "Player turn ended.", -1, -1, default, state.TurnNumber));
            TickStatuses(state, TeamId.Player, StatusTickPhase.OwnerTurnEnd, result.Events);
            DiscardHand(state);
            ExecuteEnemyPhase(state, result.Events);

            if (!state.PlayerWon && !state.PlayerLost)
            {
                state.TurnNumber += 1;
                state.ActiveTeam = TeamId.Player;
                state.Phase = BattlePhase.PlayerDrawPhase;
                state.CurrentMana = state.MaxMana;
                TickStatuses(state, TeamId.Player, StatusTickPhase.OwnerTurnStart, result.Events);
                ResetPlayerActions(state);
                result.Events.Add(CreateEvent(BattleEventType.DrawPhaseStarted, $"Player draw phase started for turn {state.TurnNumber}.", -1, -1, default, state.TurnNumber));
                DrawCards(state, state.CardsPerTurn, result.Events);
                RegenerateEnemyIntents(state, result.Events);
                state.Phase = BattlePhase.PlayerAction;
                result.Events.Add(CreateEvent(BattleEventType.TurnStarted, $"Player turn {state.TurnNumber} started.", -1, -1, default, state.TurnNumber));
            }

            CheckForWinOrLoss(state, result.Events);
            return Finish(result, state);
        }

        public PreviewResult Preview(BattleState state, PreviewRequest request)
        {
            var preview = new PreviewResult
            {
                Valid = true,
                EnemyIntents = BuildIntentViewData(state),
            };

            var effectiveStage = request.Stage == UnitTurnStage.None ? UnitTurnStage.BehaviorTargeting : request.Stage;
            switch (request.SourceKind)
            {
                case PreviewSourceKind.UnitAction:
                    if (effectiveStage == UnitTurnStage.MoveTargeting)
                    {
                        preview.ValidTargetCells = GetValidMoveTargetCells(state, request.ActorEntityId);
                    }
                    else
                    {
                        if (!state.Entities.TryGetValue(request.ActorEntityId, out var previewActor) || !previewActor.IsAlive)
                        {
                            preview.Valid = false;
                            preview.FailureReason = "Invalid acting unit.";
                            return preview;
                        }

                        var previewAction = _catalog.GetAction(previewActor.ActionId);
                        if (previewAction == null)
                        {
                            preview.Valid = false;
                            preview.FailureReason = "The selected unit has no action definition.";
                            return preview;
                        }

                        var committedMoveCell = GetResolvedBehaviorOrigin(previewActor, previewAction, request.CommittedMoveCell, request.HasCommittedMoveCell);
                        var behaviorPreviewState = state.Clone();
                        var behaviorPreviewActor = behaviorPreviewState.Entities[request.ActorEntityId];
                        if (UsesSeparateMovePhase(behaviorPreviewActor, previewAction) && committedMoveCell != behaviorPreviewActor.Position)
                        {
                            MoveEntityTo(behaviorPreviewState, behaviorPreviewActor, committedMoveCell, new List<BattleEvent>());
                        }

                        RefreshEnemyIntents(behaviorPreviewState, new List<BattleEvent>());
                        preview.ValidTargetCells = GetValidBehaviorTargetCells(state, previewActor, previewAction, request.CommittedMoveCell, request.HasCommittedMoveCell);
                        preview.EnemyIntents = BuildIntentViewData(behaviorPreviewState);
                    }

                    break;
                case PreviewSourceKind.Card:
                    preview.ValidTargetCells = GetValidCardTargetCells(state, request.CardInstanceId);
                    break;
            }

            if (!request.HasTargetCell && !request.HasTargetEntity)
            {
                AppendDangerPreviews(preview.CellPreviews, preview.EnemyIntents);
                return preview;
            }

            ActionResult actionResult;
            if (request.SourceKind == PreviewSourceKind.UnitAction && effectiveStage == UnitTurnStage.MoveTargeting)
            {
                actionResult = null;
                if (!state.Entities.TryGetValue(request.ActorEntityId, out var actor) || !actor.IsAlive)
                {
                    preview.Valid = false;
                    preview.FailureReason = "Invalid acting unit.";
                    return preview;
                }

                if (!preview.ValidTargetCells.Contains(request.TargetCell))
                {
                    preview.Valid = false;
                    preview.FailureReason = "Move target is not valid.";
                    AppendDangerPreviews(preview.CellPreviews, preview.EnemyIntents);
                    return preview;
                }

                if (TryBuildMovePath(state, actor, request.TargetCell, out var movePath))
                {
                    foreach (var cell in movePath.Skip(1))
                    {
                        preview.CellPreviews.Add(new CellPreview { Position = cell, Kind = CellPreviewKind.MovePath, Value = 0 });
                    }
                }
            }
            else
            {
                var clonedState = state.Clone();
                if (request.SourceKind == PreviewSourceKind.UnitAction)
                {
                    actionResult = ResolveUnitTurn(clonedState, new UnitTurnRequest
                    {
                        ActorEntityId = request.ActorEntityId,
                        MoveTargetCell = request.CommittedMoveCell,
                        HasMoveTargetCell = request.HasCommittedMoveCell,
                        BehaviorTargetCell = request.TargetCell,
                        BehaviorTargetEntityId = request.TargetEntityId,
                        HasBehaviorTargetCell = request.HasTargetCell,
                        HasBehaviorTargetEntity = request.HasTargetEntity,
                    });
                }
                else
                {
                    actionResult = ResolveCard(clonedState, new PlayCardRequest
                    {
                        CardInstanceId = request.CardInstanceId,
                        TargetCell = request.TargetCell,
                        TargetEntityId = request.TargetEntityId,
                        HasTargetCell = request.HasTargetCell,
                        HasTargetEntity = request.HasTargetEntity,
                    });
                }

                preview.Valid = actionResult.Success;
                preview.FailureReason = actionResult.FailureReason;
                preview.SimulatedResult = actionResult;
                preview.CellPreviews = BuildCellPreviewsFromEvents(actionResult.Events);
                preview.EnemyIntents = actionResult.Success ? BuildIntentViewData(clonedState) : preview.EnemyIntents;
            }

            AppendDangerPreviews(preview.CellPreviews, preview.EnemyIntents);
            return preview;
        }

        private static ActionResult CreateBaseResult(BattleState state)
        {
            return new ActionResult
            {
                Success = true,
                State = state,
            };
        }

        private static ActionResult Fail(ActionResult result, string reason)
        {
            result.Success = false;
            result.FailureReason = reason;
            return result;
        }

        private ActionResult Finish(ActionResult result, BattleState state)
        {
            result.State = state;
            result.Snapshot = state.CaptureSnapshot(_catalog);
            return result;
        }

        private static void AppendDangerPreviews(ICollection<CellPreview> previews, IEnumerable<IntentViewData> intentViews)
        {
            foreach (var intent in intentViews)
            {
                foreach (var cell in intent.DangerCells)
                {
                    previews.Add(new CellPreview { Position = cell, Kind = CellPreviewKind.Danger, Value = 0 });
                }
            }
        }
    }
}
