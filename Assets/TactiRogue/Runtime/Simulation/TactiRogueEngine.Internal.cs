using System;
using System.Collections.Generic;
using System.Linq;

namespace TactiRogue
{
    public sealed partial class TactiRogueEngine
    {
        private GridState CreateGridState(ScenarioDefinition scenario)
        {
            var grid = new GridState
            {
                Width = scenario.Width,
                Height = scenario.Height,
            };

            for (var y = 0; y < scenario.Height; y++)
            {
                for (var x = 0; x < scenario.Width; x++)
                {
                    grid.ValidCells.Add(new GridPosition(x, y));
                }
            }

            foreach (var raw in scenario.VoidCells ?? Array.Empty<string>())
            {
                if (GridPosition.TryParse(raw, out var position))
                {
                    grid.ValidCells.Remove(position);
                }
            }

            return grid;
        }

        private static bool TryParseTeam(string raw, out TeamId team)
        {
            return Enum.TryParse(raw, true, out team);
        }

        private EntityInstance CreateEntityFromTemplate(BattleState state, EntityTemplate template, TeamId team, GridPosition position, int summonerEntityId, bool giveImmediateAction)
        {
            var entity = new EntityInstance
            {
                EntityId = state.NextEntityId++,
                TemplateId = template.Id,
                Team = team,
                EntityKind = template.EntityKind,
                Position = position,
                CurrentHp = template.MaxHp,
                MaxHp = template.MaxHp,
                Attack = template.Attack,
                PushBonus = template.PushBonus,
                OccupiesCell = template.OccupiesCell,
                BlocksMovement = template.BlocksMovement,
                Targetable = template.Targetable,
                CanAct = template.CanAct,
                RemainingActions = template.CanAct && giveImmediateAction && team == TeamId.Player ? 1 : 0,
                ActionId = template.ActionId,
                IntentDefinitionId = template.IntentDefinitionId,
                SummonerEntityId = summonerEntityId,
            };

            foreach (var statusId in template.StartingStatusIds ?? Array.Empty<string>())
            {
                var status = _catalog.GetStatus(statusId);
                if (status != null)
                {
                    entity.Statuses.AddOrRefresh(status.Id, status.TickPhase == StatusTickPhase.None ? status.DefaultDuration : 9999);
                }
            }

            return entity;
        }

        private void AddEntityToState(BattleState state, EntityInstance entity)
        {
            state.Entities[entity.EntityId] = entity;
            if (entity.OccupiesCell)
            {
                state.Grid.Occupancy[entity.Position] = entity.EntityId;
            }

            if (entity.Team == TeamId.Player && entity.EntityKind == EntityKind.Commander)
            {
                state.CommanderEntityId = entity.EntityId;
            }
        }

        private List<int> BuildCardInstances(BattleState state, IEnumerable<string> deckIds)
        {
            var cards = new List<int>();
            foreach (var deckId in deckIds)
            {
                if (_catalog.GetCard(deckId) == null)
                {
                    continue;
                }

                var cardInstance = new CardInstance
                {
                    CardInstanceId = state.NextCardId++,
                    TemplateId = deckId,
                    CurrentZone = CardZone.DrawPile,
                    BoundEntityId = -1,
                };
                state.CardInstances[cardInstance.CardInstanceId] = cardInstance;
                cards.Add(cardInstance.CardInstanceId);
            }

            return cards;
        }

        private static int NextRandom(BattleState state, int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                return 0;
            }

            unchecked
            {
                state.RandomSeed = (state.RandomSeed * 1103515245) + 12345;
            }

            return Math.Abs(state.RandomSeed) % maxExclusive;
        }

        private static void Shuffle<T>(IList<T> values, BattleState state)
        {
            for (var index = values.Count - 1; index > 0; index--)
            {
                var swapIndex = NextRandom(state, index + 1);
                (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
            }
        }

        private void ResetPlayerActions(BattleState state)
        {
            foreach (var entity in state.LivingEntities(TeamId.Player))
            {
                entity.RemainingActions = entity.CanAct ? 1 : 0;
            }
        }

        private void DrawCards(BattleState state, int amount, ICollection<BattleEvent> events)
        {
            for (var drawIndex = 0; drawIndex < amount; drawIndex++)
            {
                if (state.DrawPile.Count == 0)
                {
                    if (state.DiscardPile.Count == 0)
                    {
                        return;
                    }

                    ShuffleDiscardIntoDrawPile(state, events);
                }

                if (!DrawOneCard(state, events))
                {
                    return;
                }
            }
        }

        private bool DrawOneCard(BattleState state, ICollection<BattleEvent> events)
        {
            if (state.DrawPile.Count == 0)
            {
                return false;
            }

            var cardInstanceId = state.DrawPile[0];
            state.DrawPile.RemoveAt(0);
            if (!state.TryGetCardInstance(cardInstanceId, out var cardInstance))
            {
                return false;
            }

            MoveCardToZone(state, cardInstance, CardZone.Hand);
            events.Add(CreateEvent(BattleEventType.CardDrawn, $"[Card] Draw {GetCardName(cardInstance.TemplateId)} from DrawPile.", -1, -1, default, 0));
            return true;
        }

        private void ShuffleDiscardIntoDrawPile(BattleState state, ICollection<BattleEvent> events)
        {
            if (state.DiscardPile.Count == 0)
            {
                return;
            }

            var cardsToShuffle = state.DiscardPile.ToList();
            state.DiscardPile.Clear();
            foreach (var cardInstanceId in cardsToShuffle)
            {
                if (state.TryGetCardInstance(cardInstanceId, out var cardInstance))
                {
                    cardInstance.CurrentZone = CardZone.DrawPile;
                }
            }

            state.DrawPile.AddRange(cardsToShuffle);
            Shuffle(state.DrawPile, state);
            events.Add(CreateEvent(BattleEventType.ShuffleDiscardIntoDrawPile, "[Card] DrawPile empty. Shuffle DiscardPile into DrawPile.", -1, -1, default, cardsToShuffle.Count));
        }

        private void DiscardHand(BattleState state)
        {
            foreach (var cardInstanceId in state.Hand.ToList())
            {
                if (state.TryGetCardInstance(cardInstanceId, out var cardInstance))
                {
                    MoveCardToZone(state, cardInstance, CardZone.DiscardPile);
                }
            }
        }

        private void BindUnitCardToEntity(BattleState state, CardInstance cardInstance, int entityId)
        {
            if (cardInstance == null)
            {
                return;
            }

            MoveCardToZone(state, cardInstance, CardZone.InBattleUnit);
            cardInstance.BoundEntityId = entityId;
            state.InBattleUnitCards[entityId] = cardInstance.CardInstanceId;
        }

        private void ReturnBoundUnitCardToDiscard(BattleState state, int entityId, ICollection<BattleEvent> events)
        {
            if (!state.InBattleUnitCards.TryGetValue(entityId, out var cardInstanceId))
            {
                return;
            }

            state.InBattleUnitCards.Remove(entityId);
            if (!state.TryGetCardInstance(cardInstanceId, out var cardInstance))
            {
                return;
            }

            cardInstance.BoundEntityId = -1;
            MoveCardToZone(state, cardInstance, CardZone.DiscardPile);
            events.Add(CreateEvent(BattleEventType.UnitCardReturnedToDiscard, $"[Card] Entity#{entityId} died. {GetCardName(cardInstance.TemplateId)} returned to DiscardPile.", entityId, -1, default, cardInstanceId));
        }

        private void MoveCardToZone(BattleState state, CardInstance cardInstance, CardZone targetZone)
        {
            if (cardInstance == null)
            {
                return;
            }

            state.DrawPile.Remove(cardInstance.CardInstanceId);
            state.Hand.Remove(cardInstance.CardInstanceId);
            state.DiscardPile.Remove(cardInstance.CardInstanceId);

            if (cardInstance.BoundEntityId >= 0
                && state.InBattleUnitCards.TryGetValue(cardInstance.BoundEntityId, out var boundCardInstanceId)
                && boundCardInstanceId == cardInstance.CardInstanceId)
            {
                state.InBattleUnitCards.Remove(cardInstance.BoundEntityId);
            }

            if (targetZone != CardZone.InBattleUnit)
            {
                cardInstance.BoundEntityId = -1;
            }

            switch (targetZone)
            {
                case CardZone.DrawPile:
                    state.DrawPile.Add(cardInstance.CardInstanceId);
                    break;
                case CardZone.Hand:
                    state.Hand.Add(cardInstance.CardInstanceId);
                    break;
                case CardZone.DiscardPile:
                    state.DiscardPile.Add(cardInstance.CardInstanceId);
                    break;
            }

            cardInstance.CurrentZone = targetZone;
        }

        private string GetCardName(string cardTemplateId)
        {
            return _catalog.GetCard(cardTemplateId)?.DisplayName ?? cardTemplateId;
        }

        private List<GridPosition> GetValidTargetCells(BattleState state, EntityInstance actor, ActionDefinition action)
        {
            if (action.TargetMode == ActionTargetMode.None)
            {
                return new List<GridPosition> { actor.Position };
            }

            var validCells = new List<GridPosition>();
            foreach (var cell in state.Grid.ValidCells)
            {
                var hasEntity = state.Grid.Occupancy.TryGetValue(cell, out var occupantId);
                if (ValidateActionTarget(state, actor, action, cell, occupantId, true, hasEntity))
                {
                    validCells.Add(cell);
                }
            }

            return validCells;
        }

        private List<GridPosition> GetValidTargetCellsForCard(BattleState state, CardTemplate template)
        {
            var validCells = new List<GridPosition>();
            if (template.CardKind == CardKind.Unit)
            {
                foreach (var cell in state.Grid.ValidCells)
                {
                    if (CanSummonCardAt(state, template, cell))
                    {
                        validCells.Add(cell);
                    }
                }

                return validCells;
            }

            if (!state.Entities.TryGetValue(state.CommanderEntityId, out var commander) || !commander.IsAlive)
            {
                return validCells;
            }

            var action = _catalog.GetAction(template.ActionId);
            return action == null ? validCells : GetValidTargetCells(state, commander, action);
        }

        private bool CanSummonCardAt(BattleState state, CardTemplate template, GridPosition targetCell)
        {
            if (!state.Entities.TryGetValue(state.CommanderEntityId, out var commander) || !commander.IsAlive)
            {
                return false;
            }

            if (!state.Grid.IsValid(targetCell) || state.Grid.IsOccupied(targetCell))
            {
                return false;
            }

            var distance = GridMath.Chebyshev(commander.Position, targetCell);
            return distance >= template.SummonMinRange && distance <= template.SummonMaxRange;
        }

        private bool ValidateActionTarget(
            BattleState state,
            EntityInstance actor,
            ActionDefinition action,
            GridPosition targetCell,
            int targetEntityId,
            bool hasTargetCell,
            bool hasTargetEntity,
            GridPosition? originOverride = null)
        {
            var origin = originOverride ?? actor.Position;
            if (action.TargetMode == ActionTargetMode.None)
            {
                return true;
            }

            if (!hasTargetCell && hasTargetEntity && state.Entities.TryGetValue(targetEntityId, out var targetEntityFromId))
            {
                targetCell = targetEntityFromId.Position;
                hasTargetCell = true;
            }

            if (action.TargetMode == ActionTargetMode.Cell)
            {
                if (!hasTargetCell || !state.Grid.IsValid(targetCell))
                {
                    return false;
                }

                if (action.TargetFilter == ActionTargetFilter.EmptyCell && state.Grid.IsOccupied(targetCell))
                {
                    return false;
                }

                return IsWithinRange(origin, targetCell, action.MinRange, action.MaxRange, action.AllowDiagonalTargeting);
            }

            if (!hasTargetEntity && hasTargetCell)
            {
                hasTargetEntity = state.Grid.Occupancy.TryGetValue(targetCell, out targetEntityId);
            }

            if (!hasTargetEntity || !state.Entities.TryGetValue(targetEntityId, out var targetEntity) || !targetEntity.IsAlive || !targetEntity.Targetable)
            {
                return false;
            }

            if (!MatchesTargetFilter(actor, targetEntity, action.TargetFilter))
            {
                return false;
            }

            return IsWithinRange(origin, targetEntity.Position, action.MinRange, action.MaxRange, action.AllowDiagonalTargeting);
        }

        private static bool MatchesTargetFilter(EntityInstance actor, EntityInstance target, ActionTargetFilter targetFilter)
        {
            switch (targetFilter)
            {
                case ActionTargetFilter.None:
                case ActionTargetFilter.AnyUnit:
                    return true;
                case ActionTargetFilter.Self:
                    return actor.EntityId == target.EntityId;
                case ActionTargetFilter.Ally:
                    return actor.Team == target.Team;
                case ActionTargetFilter.Enemy:
                    return actor.Team != target.Team && target.Team != TeamId.Neutral;
                case ActionTargetFilter.EmptyCell:
                    return false;
                default:
                    return false;
            }
        }

        private static bool IsWithinRange(GridPosition from, GridPosition to, int minRange, int maxRange, bool allowDiagonal)
        {
            var distance = allowDiagonal ? GridMath.Chebyshev(from, to) : GridMath.Manhattan(from, to);
            return distance >= minRange && distance <= maxRange;
        }

        private void TickStatuses(BattleState state, TeamId ownerTeam, StatusTickPhase tickPhase, ICollection<BattleEvent> events)
        {
            foreach (var entity in state.LivingEntities(ownerTeam).ToList())
            {
                var before = entity.Statuses.ActiveStatuses.Select(status => status.TemplateId).ToList();
                entity.Statuses.Tick(_catalog, tickPhase);
                var after = entity.Statuses.ActiveStatuses.Select(status => status.TemplateId).ToList();
                foreach (var removed in before.Except(after))
                {
                    events.Add(CreateEvent(BattleEventType.StatusExpired, $"{GetEntityName(entity.TemplateId)} lost {GetStatusName(removed)}.", entity.EntityId, -1, entity.Position, 0));
                }
            }
        }

        private void RegenerateEnemyIntents(BattleState state, ICollection<BattleEvent> events)
        {
            RefreshEnemyIntentState(state, events, true);
        }

        private IntentState GenerateIntentForEnemy(BattleState state, EntityInstance enemy)
        {
            return DecideEnemyIntentCore(state, enemy);
        }

        private IEnumerable<EntityInstance> GetEnemyTargetCandidates(BattleState state, EntityInstance enemy)
        {
            var playerUnits = state.LivingEntities(TeamId.Player).Where(entity => entity.Targetable).ToList();
            var tauntUnits = playerUnits.Where(entity => HasKeyword(entity, KeywordId.Taunt)).ToList();
            if (tauntUnits.Count == 0)
            {
                return playerUnits;
            }

            return playerUnits.Where(candidate =>
            {
                if (tauntUnits.Any(taunt => taunt.EntityId == candidate.EntityId))
                {
                    return true;
                }

                return !tauntUnits.Any(taunt => GridMath.Chebyshev(taunt.Position, candidate.Position) <= 1);
            });
        }

        private IEnumerable<GridPosition> GetAreaCells(BattleState state, GridPosition center, int radius)
        {
            foreach (var cell in state.Grid.ValidCells)
            {
                if (GridMath.Chebyshev(center, cell) <= radius)
                {
                    yield return cell;
                }
            }
        }

        private void ExecuteEnemyPhase(BattleState state, ICollection<BattleEvent> events)
        {
            state.ActiveTeam = TeamId.Enemy;
            state.Phase = BattlePhase.EnemyAction;
            events.Add(CreateEvent(BattleEventType.TurnStarted, "Enemy turn started.", -1, -1, default, state.TurnNumber));

            foreach (var enemy in state.LivingEntities(TeamId.Enemy).OrderBy(entity => entity.EntityId).ToList())
            {
                if (!enemy.IsAlive)
                {
                    continue;
                }

                var currentIntent = state.EnemyIntents.FirstOrDefault(candidate => candidate.ActorEntityId == enemy.EntityId);
                var intent = currentIntent == null ? DecideEnemyIntentCore(state, enemy) : RefreshIntentForActorCore(state, enemy, currentIntent, events);
                if (intent == null)
                {
                    continue;
                }

                ReplaceEnemyIntentCore(state, intent);
                if (intent.IsCancelled)
                {
                    continue;
                }

                var action = _catalog.GetAction(intent.ActionId);
                if (action == null)
                {
                    continue;
                }

                var executionTargetCell = ResolveExecutionTargetCellCore(state, enemy, intent, action);
                var hasTargetCell = action.TargetMode != ActionTargetMode.None;
                var hasTargetEntity = action.TargetMode == ActionTargetMode.Unit && intent.TargetEntityId >= 0;
                var unitTurnRequest = new UnitTurnRequest
                {
                    ActorEntityId = enemy.EntityId,
                    MoveTargetCell = UsesSeparateMovePhase(enemy, action)
                        ? FindBestActionOrigin(state, enemy, action, executionTargetCell, intent.TargetEntityId, hasTargetCell, hasTargetEntity)
                        : enemy.Position,
                    HasMoveTargetCell = UsesSeparateMovePhase(enemy, action),
                    BehaviorTargetCell = executionTargetCell,
                    BehaviorTargetEntityId = intent.TargetEntityId,
                    HasBehaviorTargetCell = hasTargetCell,
                    HasBehaviorTargetEntity = hasTargetEntity,
                };

                if (!TryExecuteUnitTurnCore(state, enemy, action, unitTurnRequest, events, false, out var failureReason))
                {
                    events.Add(CreateEvent(BattleEventType.IntentCancelled, $"{GetEntityName(enemy.TemplateId)} failed to execute its intent. {failureReason}", enemy.EntityId, intent.TargetEntityId, executionTargetCell, 0));
                    continue;
                }

                events.Add(CreateEvent(BattleEventType.EnemyActionExecuted, $"{GetEntityName(enemy.TemplateId)} executed {action.DisplayName}.", enemy.EntityId, intent.TargetEntityId, executionTargetCell, 0));
                events.Add(CreateEvent(BattleEventType.IntentResolved, intent.Summary, enemy.EntityId, intent.TargetEntityId, intent.TargetCell, 0));
                CheckForWinOrLoss(state, events);
                if (state.PlayerWon || state.PlayerLost)
                {
                    return;
                }
            }
        }

        private bool IntentStillValid(BattleState state, EntityInstance enemy, IntentState intent)
        {
            return TryRevalidateIntentCore(state, enemy, intent, out _, out _);
        }

        private IntentViewData BuildIntentViewData(BattleState state, IntentState intent)
        {
            return BuildIntentViewDataCore(state, intent);
        }

        private List<CellPreview> BuildCellPreviewsFromEvents(IEnumerable<BattleEvent> events)
        {
            var previews = new List<CellPreview>();
            foreach (var battleEvent in events)
            {
                switch (battleEvent.EventType)
                {
                    case BattleEventType.UnitMoved:
                        previews.Add(new CellPreview { Position = battleEvent.Position, Kind = CellPreviewKind.MovePath, Value = 0 });
                        break;
                    case BattleEventType.CollisionOccurred:
                        previews.Add(new CellPreview { Position = battleEvent.Position, Kind = CellPreviewKind.Collision, Value = battleEvent.Amount });
                        break;
                    case BattleEventType.DamageApplied:
                    case BattleEventType.HealingApplied:
                        previews.Add(new CellPreview { Position = battleEvent.Position, Kind = CellPreviewKind.Impact, Value = battleEvent.Amount });
                        break;
                }
            }

            return previews;
        }

        private void CheckForWinOrLoss(BattleState state, ICollection<BattleEvent> events)
        {
            if (state.PlayerLost || state.PlayerWon)
            {
                return;
            }

            if (!state.Entities.TryGetValue(state.CommanderEntityId, out var commander) || !commander.IsAlive)
            {
                state.PlayerLost = true;
                state.Phase = BattlePhase.Defeat;
                events.Add(CreateEvent(BattleEventType.Defeat, "The commander has fallen. Battle lost.", state.CommanderEntityId, -1, commander?.Position ?? default, 0));
                return;
            }

            if (!state.LivingEntities(TeamId.Enemy).Any())
            {
                state.PlayerWon = true;
                state.Phase = BattlePhase.Victory;
                events.Add(CreateEvent(BattleEventType.Victory, "All enemies were defeated. Battle won.", -1, -1, default, 0));
            }
        }

        private bool HasKeyword(EntityInstance entity, KeywordId keyword)
        {
            return entity.HasKeyword(_catalog, keyword, _catalog.GetEntity(entity.TemplateId));
        }

        private bool IsProtectedByTaunt(BattleState state, EntityInstance candidate)
        {
            foreach (var tauntUnit in state.LivingEntities(TeamId.Player))
            {
                if (!HasKeyword(tauntUnit, KeywordId.Taunt) || tauntUnit.EntityId == candidate.EntityId)
                {
                    continue;
                }

                if (GridMath.Chebyshev(tauntUnit.Position, candidate.Position) <= 1)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetEntityName(string entityTemplateId)
        {
            return _catalog.GetEntity(entityTemplateId)?.DisplayName ?? entityTemplateId;
        }

        private string GetStatusName(string statusId)
        {
            return _catalog.GetStatus(statusId)?.DisplayName ?? statusId;
        }

        private static BattleEvent CreateEvent(BattleEventType eventType, string message, int subjectEntityId, int secondaryEntityId, GridPosition position, int amount)
        {
            return new BattleEvent
            {
                EventType = eventType,
                Message = message,
                SubjectEntityId = subjectEntityId,
                SecondaryEntityId = secondaryEntityId,
                Position = position,
                Amount = amount,
            };
        }
    }
}
