using System;
using System.Collections.Generic;
using System.Linq;

namespace TactiRogue
{
    public sealed partial class TactiRogueEngine
    {
        private void RefreshEnemyIntents(BattleState state, ICollection<BattleEvent> events)
        {
            RefreshEnemyIntentState(state, events);
        }

        private void RefreshEnemyIntentState(BattleState state, ICollection<BattleEvent> events, bool rebuildFromScratch = false)
        {
            var previousIntents = state.EnemyIntents.ToDictionary(intent => intent.ActorEntityId, intent => intent.Clone());
            var refreshed = new List<IntentState>();

            foreach (var enemy in state.LivingEntities(TeamId.Enemy).OrderBy(entity => entity.EntityId))
            {
                previousIntents.TryGetValue(enemy.EntityId, out var previousIntent);

                IntentState nextIntent;
                if (rebuildFromScratch || previousIntent == null)
                {
                    nextIntent = DecideEnemyIntentCore(state, enemy);
                    if (nextIntent == null)
                    {
                        continue;
                    }

                    refreshed.Add(nextIntent);
                    events.Add(CreateEvent(BattleEventType.IntentGenerated, nextIntent.Summary, enemy.EntityId, nextIntent.TargetEntityId, nextIntent.TargetCell, 0));
                    continue;
                }

                nextIntent = RefreshIntentForActorCore(state, enemy, previousIntent, events);
                if (nextIntent != null)
                {
                    refreshed.Add(nextIntent);
                }
            }

            state.EnemyIntents = refreshed;
        }

        private IntentState DecideEnemyIntentCore(BattleState state, EntityInstance enemy)
        {
            var intentDefinition = _catalog.GetIntent(enemy.IntentDefinitionId);
            var action = GetIntentActionForEnemy(enemy, intentDefinition);
            if (intentDefinition == null || action == null)
            {
                return null;
            }

            var intent = new IntentState
            {
                ActorEntityId = enemy.EntityId,
                IntentDefinitionId = intentDefinition.Id,
                ActionId = action.Id,
                IntentKind = intentDefinition.IntentKind,
                TargetingMode = ResolveTargetingModeCore(intentDefinition),
                RevalidationPolicy = ResolveRevalidationPolicyCore(intentDefinition),
                FallbackMode = ResolveFallbackModeCore(intentDefinition),
                TargetEntityId = -1,
            };

            if (!TryLockIntentCore(state, enemy, action, intent, out var failureReason))
            {
                return intent.FallbackMode == IntentFallbackMode.SkipTurn
                    ? CreateCancelledIntentCore(state, enemy, intent, failureReason)
                    : null;
            }

            UpdateIntentPresentationCore(state, enemy, intent, action);
            return intent;
        }

        private IntentState RefreshIntentForActorCore(BattleState state, EntityInstance enemy, IntentState previousIntent, ICollection<BattleEvent> events)
        {
            if (previousIntent.IsCancelled && previousIntent.FallbackMode == IntentFallbackMode.SkipTurn)
            {
                return previousIntent.Clone();
            }

            if (TryRevalidateIntentCore(state, enemy, previousIntent, out var refreshedIntent, out var failureReason))
            {
                if (!IntentDisplayEqualsCore(previousIntent, refreshedIntent))
                {
                    events.Add(CreateEvent(BattleEventType.IntentRevalidated, refreshedIntent.Summary, enemy.EntityId, refreshedIntent.TargetEntityId, refreshedIntent.TargetCell, 0));
                }

                return refreshedIntent;
            }

            return ApplyIntentFallbackCore(state, enemy, previousIntent, failureReason, events);
        }

        private IntentState ApplyIntentFallbackCore(BattleState state, EntityInstance enemy, IntentState previousIntent, string failureReason, ICollection<BattleEvent> events)
        {
            if (previousIntent.FallbackMode == IntentFallbackMode.SkipTurn)
            {
                var cancelled = CreateCancelledIntentCore(state, enemy, previousIntent, failureReason);
                if (!previousIntent.IsCancelled || previousIntent.DebugReason != cancelled.DebugReason)
                {
                    events.Add(CreateEvent(BattleEventType.IntentCancelled, cancelled.Summary, enemy.EntityId, previousIntent.TargetEntityId, previousIntent.TargetCell, 0));
                }

                return cancelled;
            }

            events.Add(CreateEvent(BattleEventType.IntentCancelled, $"{GetEntityName(enemy.TemplateId)} lost its intent. {failureReason}", enemy.EntityId, previousIntent.TargetEntityId, previousIntent.TargetCell, 0));
            var retargeted = DecideEnemyIntentCore(state, enemy);
            if (retargeted != null)
            {
                events.Add(CreateEvent(BattleEventType.IntentGenerated, retargeted.Summary, enemy.EntityId, retargeted.TargetEntityId, retargeted.TargetCell, 0));
            }

            return retargeted;
        }

        private bool TryRevalidateIntentCore(BattleState state, EntityInstance enemy, IntentState previousIntent, out IntentState refreshedIntent, out string failureReason)
        {
            refreshedIntent = previousIntent.Clone();
            refreshedIntent.IsCancelled = false;
            refreshedIntent.DebugReason = null;

            var action = _catalog.GetAction(previousIntent.ActionId);
            if (action == null)
            {
                failureReason = "The action definition is missing.";
                return false;
            }

            switch (previousIntent.RevalidationPolicy)
            {
                case IntentRevalidationPolicy.StrictLock:
                    if (!TryResolveStrictLockCore(state, enemy, action, refreshedIntent, out failureReason))
                    {
                        return false;
                    }

                    break;
                case IntentRevalidationPolicy.FixedArea:
                    if (!TryResolveFixedAreaCore(state, enemy, action, refreshedIntent, out failureReason))
                    {
                        return false;
                    }

                    break;
                case IntentRevalidationPolicy.FixedDirection:
                    if (!TryResolveFixedDirectionCore(state, enemy, action, refreshedIntent, out failureReason))
                    {
                        return false;
                    }

                    break;
                default:
                    failureReason = "The intent revalidation policy is not supported.";
                    return false;
            }

            UpdateIntentPresentationCore(state, enemy, refreshedIntent, action);
            failureReason = null;
            return true;
        }

        private ActionDefinition GetIntentActionForEnemy(EntityInstance enemy, IntentDefinition intentDefinition)
        {
            var actionId = !string.IsNullOrWhiteSpace(intentDefinition?.ActionId) ? intentDefinition.ActionId : enemy.ActionId;
            return _catalog.GetAction(actionId);
        }

        private IntentTargetingMode ResolveTargetingModeCore(IntentDefinition intentDefinition)
        {
            if (intentDefinition == null)
            {
                return IntentTargetingMode.Legacy;
            }

            if (intentDefinition.TargetingMode != IntentTargetingMode.Legacy)
            {
                return intentDefinition.TargetingMode;
            }

            switch (intentDefinition.IntentKind)
            {
                case IntentKind.UnitLock:
                    return intentDefinition.PreferCommander ? IntentTargetingMode.CommanderPriorityUnit : IntentTargetingMode.NearestUnit;
                case IntentKind.AreaLock:
                    return intentDefinition.PreferCommander ? IntentTargetingMode.CommanderCell : IntentTargetingMode.TargetUnitCell;
                case IntentKind.Directional:
                    return intentDefinition.PreferCommander ? IntentTargetingMode.CommanderDirection : IntentTargetingMode.TargetUnitDirection;
                default:
                    return IntentTargetingMode.Legacy;
            }
        }

        private static IntentRevalidationPolicy ResolveRevalidationPolicyCore(IntentDefinition intentDefinition)
        {
            if (intentDefinition == null)
            {
                return IntentRevalidationPolicy.Legacy;
            }

            if (intentDefinition.RevalidationPolicy != IntentRevalidationPolicy.Legacy)
            {
                return intentDefinition.RevalidationPolicy;
            }

            switch (intentDefinition.IntentKind)
            {
                case IntentKind.UnitLock:
                    return IntentRevalidationPolicy.StrictLock;
                case IntentKind.AreaLock:
                    return IntentRevalidationPolicy.FixedArea;
                case IntentKind.Directional:
                    return IntentRevalidationPolicy.FixedDirection;
                default:
                    return IntentRevalidationPolicy.Legacy;
            }
        }

        private static IntentFallbackMode ResolveFallbackModeCore(IntentDefinition intentDefinition)
        {
            if (intentDefinition == null)
            {
                return IntentFallbackMode.Legacy;
            }

            return intentDefinition.FallbackMode != IntentFallbackMode.Legacy
                ? intentDefinition.FallbackMode
                : IntentFallbackMode.Retarget;
        }

        private bool TryLockIntentCore(BattleState state, EntityInstance enemy, ActionDefinition action, IntentState intent, out string failureReason)
        {
            failureReason = null;

            switch (intent.TargetingMode)
            {
                case IntentTargetingMode.CommanderPriorityUnit:
                case IntentTargetingMode.NearestUnit:
                    var target = SelectUnitIntentTargetCore(state, enemy, action, intent.TargetingMode == IntentTargetingMode.CommanderPriorityUnit);
                    if (target == null)
                    {
                        failureReason = "No valid unit target is available.";
                        return false;
                    }

                    intent.TargetEntityId = target.EntityId;
                    intent.TargetCell = target.Position;
                    intent.Direction = GridMath.DirectionBetween(enemy.Position, target.Position);
                    return true;
                case IntentTargetingMode.CommanderCell:
                    if (!TryGetCommanderCore(state, out var commander))
                    {
                        failureReason = "The commander is missing.";
                        return false;
                    }

                    intent.TargetEntityId = -1;
                    intent.TargetCell = commander.Position;
                    intent.Direction = GridMath.DirectionBetween(enemy.Position, commander.Position);
                    if (!CanActorUseActionAfterMove(state, enemy, action, intent.TargetCell, -1, true, false))
                    {
                        failureReason = "The locked area is out of range.";
                        return false;
                    }

                    return true;
                case IntentTargetingMode.TargetUnitCell:
                    var areaTarget = SelectUnitIntentTargetCore(state, enemy, action, false);
                    if (areaTarget == null)
                    {
                        failureReason = "No valid area anchor is available.";
                        return false;
                    }

                    intent.TargetEntityId = areaTarget.EntityId;
                    intent.TargetCell = areaTarget.Position;
                    intent.Direction = GridMath.DirectionBetween(enemy.Position, areaTarget.Position);
                    if (!CanActorUseActionAfterMove(state, enemy, action, intent.TargetCell, -1, true, false))
                    {
                        failureReason = "The locked area is out of range.";
                        return false;
                    }

                    return true;
                case IntentTargetingMode.CommanderDirection:
                    if (!TryGetCommanderCore(state, out var commanderDirectionTarget))
                    {
                        failureReason = "The commander is missing.";
                        return false;
                    }

                    intent.Direction = GridMath.DirectionBetween(enemy.Position, commanderDirectionTarget.Position);
                    if (intent.Direction == GridDirection.None || !TryResolveDirectionalTargetCellCore(state, enemy.Position, intent.Direction, action, out intent.TargetCell))
                    {
                        failureReason = "The locked direction no longer has a valid lane.";
                        return false;
                    }

                    intent.TargetEntityId = -1;
                    return true;
                case IntentTargetingMode.TargetUnitDirection:
                    var directionalTarget = SelectUnitIntentTargetCore(state, enemy, action, false);
                    if (directionalTarget == null)
                    {
                        failureReason = "No valid direction target is available.";
                        return false;
                    }

                    intent.Direction = GridMath.DirectionBetween(enemy.Position, directionalTarget.Position);
                    if (intent.Direction == GridDirection.None || !TryResolveDirectionalTargetCellCore(state, enemy.Position, intent.Direction, action, out intent.TargetCell))
                    {
                        failureReason = "The locked direction no longer has a valid lane.";
                        return false;
                    }

                    intent.TargetEntityId = directionalTarget.EntityId;
                    return true;
                default:
                    failureReason = "The targeting mode is not supported.";
                    return false;
            }
        }

        private EntityInstance SelectUnitIntentTargetCore(BattleState state, EntityInstance enemy, ActionDefinition action, bool preferCommander)
        {
            var acquireRange = GetIntentAcquireRangeCore(enemy, action, _catalog.GetIntent(enemy.IntentDefinitionId));
            var candidates = GetEnemyTargetCandidates(state, enemy)
                .Where(candidate => GridMath.Chebyshev(enemy.Position, candidate.Position) <= acquireRange)
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            return preferCommander
                ? candidates.OrderByDescending(entity => entity.EntityId == state.CommanderEntityId).ThenBy(entity => GridMath.Chebyshev(enemy.Position, entity.Position)).First()
                : candidates.OrderBy(entity => GridMath.Chebyshev(enemy.Position, entity.Position)).First();
        }

        private bool TryResolveStrictLockCore(BattleState state, EntityInstance enemy, ActionDefinition action, IntentState intent, out string failureReason)
        {
            if (intent.TargetEntityId < 0 || !state.Entities.TryGetValue(intent.TargetEntityId, out var target) || !target.IsAlive || !target.Targetable)
            {
                failureReason = "The locked unit is no longer available.";
                return false;
            }

            if (IsProtectedByTaunt(state, target) && !HasKeyword(target, KeywordId.Taunt))
            {
                failureReason = "A taunt unit is now protecting the original target.";
                return false;
            }

            intent.TargetCell = target.Position;
            intent.Direction = GridMath.DirectionBetween(enemy.Position, target.Position);
            if (!CanActorUseActionAfterMove(state, enemy, action, intent.TargetCell, intent.TargetEntityId, true, true))
            {
                failureReason = "The locked unit is no longer reachable.";
                return false;
            }

            failureReason = null;
            return true;
        }

        private bool TryResolveFixedAreaCore(BattleState state, EntityInstance enemy, ActionDefinition action, IntentState intent, out string failureReason)
        {
            if (!state.Grid.IsValid(intent.TargetCell))
            {
                failureReason = "The locked area is outside the board.";
                return false;
            }

            if (!CanActorUseActionAfterMove(state, enemy, action, intent.TargetCell, -1, true, false))
            {
                failureReason = "The locked area is no longer reachable.";
                return false;
            }

            failureReason = null;
            return true;
        }

        private bool TryResolveFixedDirectionCore(BattleState state, EntityInstance enemy, ActionDefinition action, IntentState intent, out string failureReason)
        {
            if (intent.Direction == GridDirection.None)
            {
                failureReason = "The stored direction is missing.";
                return false;
            }

            if (!TryResolveDirectionalTargetCellCore(state, enemy.Position, intent.Direction, action, out intent.TargetCell))
            {
                failureReason = "The locked direction no longer has a valid lane.";
                return false;
            }

            failureReason = null;
            return true;
        }

        private void UpdateIntentPresentationCore(BattleState state, EntityInstance enemy, IntentState intent, ActionDefinition action)
        {
            if (intent.IsCancelled)
            {
                intent.DangerCells.Clear();
                return;
            }

            switch (intent.IntentKind)
            {
                case IntentKind.UnitLock:
                    var targetName = intent.TargetEntityId >= 0 && state.Entities.TryGetValue(intent.TargetEntityId, out var target)
                        ? GetEntityName(target.TemplateId)
                        : "the target";
                    intent.Summary = $"{GetEntityName(enemy.TemplateId)} will lock {targetName}.";
                    break;
                case IntentKind.AreaLock:
                    intent.Summary = $"{GetEntityName(enemy.TemplateId)} will bombard {intent.TargetCell}.";
                    break;
                case IntentKind.Directional:
                    intent.Summary = $"{GetEntityName(enemy.TemplateId)} will charge {intent.Direction}.";
                    break;
            }

            intent.DangerCells = BuildDangerCellsForIntentCore(state, enemy, intent, action);
        }

        private List<GridPosition> BuildDangerCellsForIntentCore(BattleState state, EntityInstance enemy, IntentState intent, ActionDefinition action)
        {
            if (intent.IsCancelled)
            {
                return new List<GridPosition>();
            }

            switch (intent.IntentKind)
            {
                case IntentKind.UnitLock:
                    return intent.TargetEntityId >= 0 && state.Entities.TryGetValue(intent.TargetEntityId, out var target)
                        ? new List<GridPosition> { target.Position }
                        : new List<GridPosition> { intent.TargetCell };
                case IntentKind.AreaLock:
                    return GetAreaCells(state, intent.TargetCell, action.Radius).ToList();
                case IntentKind.Directional:
                    return GridMath.EnumerateLine(enemy.Position, intent.Direction, Math.Max(1, action.MoveRange)).Where(state.Grid.IsValid).ToList();
                default:
                    return new List<GridPosition>();
            }
        }

        private static bool IntentDisplayEqualsCore(IntentState left, IntentState right)
        {
            return left.TargetEntityId == right.TargetEntityId
                   && left.TargetCell == right.TargetCell
                   && left.Direction == right.Direction
                   && left.IsCancelled == right.IsCancelled
                   && string.Equals(left.DebugReason, right.DebugReason, StringComparison.Ordinal)
                   && string.Equals(left.Summary, right.Summary, StringComparison.Ordinal)
                   && left.DangerCells.SequenceEqual(right.DangerCells);
        }

        private IntentState CreateCancelledIntentCore(BattleState state, EntityInstance enemy, IntentState previousIntent, string failureReason)
        {
            var cancelled = previousIntent.Clone();
            cancelled.IsCancelled = true;
            cancelled.DebugReason = failureReason;
            cancelled.Summary = $"{GetEntityName(enemy.TemplateId)} will skip its action.";
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                cancelled.Summary += $" ({failureReason})";
            }

            cancelled.DangerCells.Clear();
            return cancelled;
        }

        private bool TryResolveDirectionalTargetCellCore(BattleState state, GridPosition origin, GridDirection direction, ActionDefinition action, out GridPosition targetCell)
        {
            targetCell = default;
            if (direction == GridDirection.None)
            {
                return false;
            }

            var maxDistance = Math.Max(1, Math.Max(action.MaxRange, action.MoveRange));
            var foundValidCell = false;
            foreach (var candidate in GridMath.EnumerateLine(origin, direction, maxDistance))
            {
                if (!state.Grid.IsValid(candidate))
                {
                    break;
                }

                targetCell = candidate;
                foundValidCell = true;
            }

            return foundValidCell;
        }

        private int GetIntentAcquireRangeCore(EntityInstance enemy, ActionDefinition action, IntentDefinition intentDefinition)
        {
            var actionRange = action.MaxRange + Math.Max(0, action.SkipMovePhase ? action.MoveRange : GetMoveProfile(enemy).MoveRange);
            if (intentDefinition == null || intentDefinition.AcquireRange <= 0)
            {
                return actionRange;
            }

            return Math.Min(intentDefinition.AcquireRange, actionRange);
        }

        private static void ReplaceEnemyIntentCore(BattleState state, IntentState intent)
        {
            state.EnemyIntents.RemoveAll(candidate => candidate.ActorEntityId == intent.ActorEntityId);
            state.EnemyIntents.Add(intent);
        }

        private static bool TryGetCommanderCore(BattleState state, out EntityInstance commander)
        {
            commander = null;
            return state.Entities.TryGetValue(state.CommanderEntityId, out commander) && commander.IsAlive;
        }

        private static GridPosition ResolveExecutionTargetCellCore(BattleState state, EntityInstance enemy, IntentState intent, ActionDefinition action)
        {
            if (intent.IntentKind == IntentKind.UnitLock && intent.TargetEntityId >= 0 && state.Entities.TryGetValue(intent.TargetEntityId, out var target))
            {
                return target.Position;
            }

            if (intent.IntentKind == IntentKind.Directional && intent.Direction != GridDirection.None)
            {
                var maxDistance = Math.Max(1, Math.Max(action.MaxRange, action.MoveRange));
                return GridMath.EnumerateLine(enemy.Position, intent.Direction, maxDistance).Where(state.Grid.IsValid).DefaultIfEmpty(intent.TargetCell).Last();
            }

            return intent.TargetCell;
        }

        private IntentViewData BuildIntentViewDataCore(BattleState state, IntentState intent)
        {
            var definition = _catalog.GetIntent(intent.IntentDefinitionId);
            var actor = state.Entities.TryGetValue(intent.ActorEntityId, out var actorEntity) ? actorEntity : null;
            var action = _catalog.GetAction(intent.ActionId);
            var dangerCells = actor != null && action != null
                ? BuildDangerCellsForIntentCore(state, actor, intent, action)
                : new List<GridPosition>(intent.DangerCells);

            return new IntentViewData
            {
                ActorEntityId = intent.ActorEntityId,
                TargetEntityId = intent.TargetEntityId,
                IntentDefinitionId = intent.IntentDefinitionId,
                ActionId = intent.ActionId,
                Summary = intent.Summary,
                TargetingMode = intent.TargetingMode.ToString(),
                RevalidationPolicy = intent.RevalidationPolicy.ToString(),
                FallbackMode = intent.FallbackMode.ToString(),
                IsCancelled = intent.IsCancelled,
                DebugReason = intent.DebugReason,
                Direction = intent.Direction,
                TargetCell = intent.TargetCell,
                Tint = definition != null ? definition.Tint : default,
                DangerCells = dangerCells,
            };
        }
    }
}
