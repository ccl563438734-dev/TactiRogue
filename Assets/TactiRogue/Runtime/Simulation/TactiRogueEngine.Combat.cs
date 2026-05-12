using System;
using System.Collections.Generic;
using System.Linq;

namespace TactiRogue
{
    public sealed partial class TactiRogueEngine
    {
        private void ExecuteAction(
            BattleState state,
            int actorEntityId,
            ActionDefinition action,
            GridPosition targetCell,
            int targetEntityId,
            bool hasTargetCell,
            bool hasTargetEntity,
            ICollection<BattleEvent> events,
            bool consumeActorAction)
        {
            if (!state.Entities.TryGetValue(actorEntityId, out var actor) || !actor.IsAlive || action == null)
            {
                return;
            }

            if (!hasTargetEntity && hasTargetCell)
            {
                hasTargetEntity = state.Grid.Occupancy.TryGetValue(targetCell, out targetEntityId);
            }

            events.Add(CreateEvent(BattleEventType.ActionUsed, $"{GetEntityName(actor.TemplateId)} used {action.DisplayName}.", actorEntityId, targetEntityId, hasTargetCell ? targetCell : actor.Position, 0));

            switch (action.ActionKind)
            {
                case ActionKind.Strike:
                case ActionKind.PushStrike:
                    ExecuteStrikeAction(state, actor, action, targetCell, targetEntityId, hasTargetCell, hasTargetEntity, events);
                    break;
                case ActionKind.Swap:
                    ExecuteSwapAction(state, actor, targetEntityId, hasTargetEntity, events);
                    break;
                case ActionKind.GrantAction:
                    ExecuteGrantAction(state, actor, action, targetEntityId, hasTargetEntity, events);
                    break;
                case ActionKind.ApplyStatus:
                    ExecuteApplyStatusAction(state, actor, action, targetEntityId, hasTargetEntity, events);
                    break;
                case ActionKind.Summon:
                    ExecuteSummonAction(state, actor, action, targetCell, hasTargetCell, events);
                    break;
                case ActionKind.AreaBlast:
                    ExecuteAreaBlastAction(state, actor, action, targetCell, hasTargetCell, events);
                    break;
                case ActionKind.Charge:
                    ExecuteChargeAction(state, actor, action, targetCell, targetEntityId, hasTargetCell, events);
                    break;
            }

            if (consumeActorAction && actor.Team == TeamId.Player && actor.CanAct)
            {
                actor.RemainingActions = Math.Max(0, actor.RemainingActions - 1);
            }
        }

        private void ExecuteStrikeAction(
            BattleState state,
            EntityInstance actor,
            ActionDefinition action,
            GridPosition targetCell,
            int targetEntityId,
            bool hasTargetCell,
            bool hasTargetEntity,
            ICollection<BattleEvent> events)
        {
            if (action.MoveRange > 0 && action.MovePattern != MovementPattern.None)
            {
                MoveActorTowardTarget(state, actor, hasTargetEntity && state.Entities.TryGetValue(targetEntityId, out var explicitTarget) ? explicitTarget.Position : targetCell, action, events);
            }

            if (!hasTargetEntity && hasTargetCell)
            {
                hasTargetEntity = state.Grid.Occupancy.TryGetValue(targetCell, out targetEntityId);
            }

            if (!hasTargetEntity || !state.Entities.TryGetValue(targetEntityId, out var target) || !target.IsAlive)
            {
                return;
            }

            var baseDamage = GetActionBaseDamage(actor, action);
            var direction = GridMath.DirectionBetween(actor.Position, target.Position);
            if (direction == GridDirection.None && hasTargetCell)
            {
                direction = GridMath.DirectionBetween(actor.Position, targetCell);
            }

            if (action.ActionKind == ActionKind.PushStrike && action.PushForce + actor.GetEffectivePushBonus(_catalog) > 0)
            {
                ResolveAttackWithForcedMovement(state, actor, target, baseDamage, Math.Max(0, action.PushForce + actor.GetEffectivePushBonus(_catalog)), direction, events);
            }
            else
            {
                ApplyDamage(state, actor.EntityId, target.EntityId, baseDamage, events);
            }
        }

        private void ExecuteSwapAction(BattleState state, EntityInstance actor, int targetEntityId, bool hasTargetEntity, ICollection<BattleEvent> events)
        {
            if (!hasTargetEntity || !state.Entities.TryGetValue(targetEntityId, out var target) || !target.IsAlive || !target.OccupiesCell)
            {
                return;
            }

            var actorPosition = actor.Position;
            var targetPosition = target.Position;
            state.Grid.Occupancy[actorPosition] = target.EntityId;
            state.Grid.Occupancy[targetPosition] = actor.EntityId;
            actor.Position = targetPosition;
            target.Position = actorPosition;
            events.Add(CreateEvent(BattleEventType.UnitMoved, $"{GetEntityName(actor.TemplateId)} moved to {actor.Position}.", actor.EntityId, target.EntityId, actor.Position, 0));
            events.Add(CreateEvent(BattleEventType.UnitMoved, $"{GetEntityName(target.TemplateId)} moved to {target.Position}.", target.EntityId, actor.EntityId, target.Position, 0));
            events.Add(CreateEvent(BattleEventType.Info, $"{GetEntityName(actor.TemplateId)} swapped positions with {GetEntityName(target.TemplateId)}.", actor.EntityId, target.EntityId, actor.Position, 0));
        }

        private void ExecuteGrantAction(BattleState state, EntityInstance actor, ActionDefinition action, int targetEntityId, bool hasTargetEntity, ICollection<BattleEvent> events)
        {
            if (!hasTargetEntity || !state.Entities.TryGetValue(targetEntityId, out var target) || !target.IsAlive)
            {
                return;
            }

            target.RemainingActions += Math.Max(1, action.ExtraActionsGranted);
            events.Add(CreateEvent(BattleEventType.Info, $"{GetEntityName(target.TemplateId)} gained an extra action.", target.EntityId, actor.EntityId, target.Position, target.RemainingActions));

            if (!string.IsNullOrWhiteSpace(action.ApplyStatusId))
            {
                ApplyStatus(target, action.ApplyStatusId, action.OverrideStatusDuration, events);
            }
        }

        private void ExecuteApplyStatusAction(BattleState state, EntityInstance actor, ActionDefinition action, int targetEntityId, bool hasTargetEntity, ICollection<BattleEvent> events)
        {
            if (!hasTargetEntity || !state.Entities.TryGetValue(targetEntityId, out var target) || !target.IsAlive)
            {
                return;
            }

            if (action.HealAmount > 0)
            {
                ApplyHealing(target, action.HealAmount, events);
            }

            if (!string.IsNullOrWhiteSpace(action.ApplyStatusId))
            {
                ApplyStatus(target, action.ApplyStatusId, action.OverrideStatusDuration, events);
            }
        }

        private void ExecuteSummonAction(BattleState state, EntityInstance actor, ActionDefinition action, GridPosition targetCell, bool hasTargetCell, ICollection<BattleEvent> events)
        {
            if (!hasTargetCell || !state.Grid.IsValid(targetCell) || state.Grid.IsOccupied(targetCell))
            {
                return;
            }

            var template = _catalog.GetEntity(action.SummonEntityId);
            if (template == null)
            {
                return;
            }

            var summoned = CreateEntityFromTemplate(state, template, actor.Team, targetCell, actor.EntityId, actor.Team == TeamId.Player);
            AddEntityToState(state, summoned);
            events.Add(CreateEvent(BattleEventType.EntitySummoned, $"{GetEntityName(template.Id)} was summoned at {targetCell}.", summoned.EntityId, actor.EntityId, targetCell, 0));
        }

        private void ExecuteAreaBlastAction(BattleState state, EntityInstance actor, ActionDefinition action, GridPosition targetCell, bool hasTargetCell, ICollection<BattleEvent> events)
        {
            if (!hasTargetCell)
            {
                return;
            }

            foreach (var entity in state.LivingEntities().Where(entity => entity.EntityId != actor.EntityId && entity.Targetable && GridMath.Chebyshev(entity.Position, targetCell) <= action.Radius).ToList())
            {
                ApplyDamage(state, actor.EntityId, entity.EntityId, GetActionBaseDamage(actor, action), events);
            }
        }

        private void ExecuteChargeAction(BattleState state, EntityInstance actor, ActionDefinition action, GridPosition targetCell, int targetEntityId, bool hasTargetCell, ICollection<BattleEvent> events)
        {
            if (!hasTargetCell)
            {
                return;
            }

            var direction = GridMath.DirectionBetween(actor.Position, targetCell);
            if (direction == GridDirection.None)
            {
                return;
            }

            var current = actor.Position;
            var nextStep = GridMath.DirectionToVector(direction);
            EntityInstance hitTarget = null;

            for (var step = 0; step < action.MoveRange; step++)
            {
                var next = current + nextStep;
                if (!state.Grid.IsValid(next))
                {
                    break;
                }

                if (state.Grid.Occupancy.TryGetValue(next, out var blockingEntityId))
                {
                    state.Entities.TryGetValue(blockingEntityId, out hitTarget);
                    break;
                }

                current = next;
            }

            if (current != actor.Position)
            {
                MoveEntityTo(state, actor, current, events);
            }

            if (hitTarget != null && hitTarget.IsAlive && hitTarget.Team != actor.Team)
            {
                ResolveAttackWithForcedMovement(state, actor, hitTarget, GetActionBaseDamage(actor, action), Math.Max(0, action.PushForce + actor.GetEffectivePushBonus(_catalog)), direction, events);
            }
        }

        private void MoveActorTowardTarget(BattleState state, EntityInstance actor, GridPosition targetCell, ActionDefinition action, ICollection<BattleEvent> events)
        {
            if (action.MovePattern != MovementPattern.Walk || action.MoveRange <= 0)
            {
                return;
            }

            var desiredCells = state.Grid.ValidCells
                .Where(cell => !state.Grid.IsOccupied(cell) && IsWithinRange(cell, targetCell, action.MinRange, action.MaxRange, action.AllowDiagonalTargeting))
                .OrderBy(cell => GridMath.Chebyshev(cell, targetCell))
                .ToList();

            var bestPath = FindBestPath(state, actor.Position, desiredCells);
            if (bestPath == null || bestPath.Count <= 1)
            {
                return;
            }

            var destinationIndex = Math.Min(action.MoveRange, bestPath.Count - 1);
            var destination = bestPath[destinationIndex];
            MoveEntityTo(state, actor, destination, events);
        }

        private List<GridPosition> FindBestPath(BattleState state, GridPosition start, IEnumerable<GridPosition> candidateDestinations)
        {
            var destinations = new HashSet<GridPosition>(candidateDestinations);
            if (destinations.Count == 0)
            {
                return null;
            }

            var frontier = new Queue<GridPosition>();
            frontier.Enqueue(start);
            var cameFrom = new Dictionary<GridPosition, GridPosition?> { [start] = null };

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                if (destinations.Contains(current))
                {
                    return ReconstructPath(current, cameFrom);
                }

                foreach (var neighbor in GridMath.GetNeighbors(current, true))
                {
                    if (!state.Grid.IsValid(neighbor) || cameFrom.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    if (state.Grid.IsOccupied(neighbor))
                    {
                        continue;
                    }

                    frontier.Enqueue(neighbor);
                    cameFrom[neighbor] = current;
                }
            }

            return null;
        }

        private static List<GridPosition> ReconstructPath(GridPosition destination, IReadOnlyDictionary<GridPosition, GridPosition?> cameFrom)
        {
            var path = new List<GridPosition>();
            var current = destination;
            while (true)
            {
                path.Add(current);
                if (!cameFrom.TryGetValue(current, out var previous) || previous == null)
                {
                    break;
                }

                current = previous.Value;
            }

            path.Reverse();
            return path;
        }

        private int GetActionBaseDamage(EntityInstance actor, ActionDefinition action)
        {
            return action.UseActorAttackValue ? actor.GetEffectiveAttack(_catalog) + action.DamageAmount : action.DamageAmount;
        }

        private void ResolveAttackWithForcedMovement(
            BattleState state,
            EntityInstance actor,
            EntityInstance target,
            int baseDamage,
            int pushForce,
            GridDirection direction,
            ICollection<BattleEvent> events)
        {
            if (baseDamage <= 0)
            {
                return;
            }

            if (direction == GridDirection.None)
            {
                ApplyDamage(state, actor.EntityId, target.EntityId, baseDamage, events);
                return;
            }

            if (HasKeyword(target, KeywordId.Anchored) || pushForce <= 0)
            {
                ApplyDamage(state, actor.EntityId, target.EntityId, baseDamage, events);
                return;
            }

            var movementVector = GridMath.DirectionToVector(direction);
            var finalPosition = target.Position;
            EntityInstance collisionEntity = null;
            var collidedWithBoundary = false;

            for (var step = 0; step < pushForce; step++)
            {
                var next = finalPosition + movementVector;
                if (!state.Grid.IsValid(next))
                {
                    collidedWithBoundary = true;
                    break;
                }

                if (state.Grid.Occupancy.TryGetValue(next, out var blockerId) && blockerId != target.EntityId)
                {
                    state.Entities.TryGetValue(blockerId, out collisionEntity);
                    break;
                }

                finalPosition = next;
            }

            if (finalPosition != target.Position)
            {
                MoveEntityTo(state, target, finalPosition, events);
            }

            if (collisionEntity == null && !collidedWithBoundary)
            {
                ApplyDamage(state, actor.EntityId, target.EntityId, baseDamage, events);
                return;
            }

            if (collisionEntity != null && collisionEntity.EntityKind != EntityKind.Building)
            {
                var amplifiedDamage = Ceil(baseDamage * 1.5f);
                events.Add(CreateEvent(BattleEventType.CollisionOccurred, "Collision crit against a unit.", target.EntityId, collisionEntity.EntityId, collisionEntity.Position, amplifiedDamage));
                ApplyDamage(state, actor.EntityId, target.EntityId, amplifiedDamage, events);
                ApplyDamage(state, actor.EntityId, collisionEntity.EntityId, amplifiedDamage, events);

                for (var offset = 1; offset < pushForce; offset++)
                {
                    var chainCell = collisionEntity.Position + new GridPosition(movementVector.X * offset, movementVector.Y * offset);
                    if (!state.Grid.IsValid(chainCell) || !state.Grid.Occupancy.TryGetValue(chainCell, out var chainedId))
                    {
                        break;
                    }

                    ApplyDamage(state, actor.EntityId, chainedId, amplifiedDamage, events);
                }

                GrantBreakthroughIfNeeded(actor, events);
                return;
            }

            var doubleDamage = baseDamage * 2;
            events.Add(CreateEvent(BattleEventType.CollisionOccurred, collisionEntity == null ? "Collision crit against the boundary." : "Collision crit against a building.", target.EntityId, collisionEntity?.EntityId ?? -1, finalPosition, doubleDamage));
            ApplyDamage(state, actor.EntityId, target.EntityId, doubleDamage, events);
            GrantBreakthroughIfNeeded(actor, events);
        }

        private void GrantBreakthroughIfNeeded(EntityInstance actor, ICollection<BattleEvent> events)
        {
            if (!HasKeyword(actor, KeywordId.Breakthrough))
            {
                return;
            }

            if (actor.Team == TeamId.Player)
            {
                actor.RemainingActions += 1;
            }

            events.Add(CreateEvent(BattleEventType.Info, $"{GetEntityName(actor.TemplateId)} triggered Breakthrough and gained an extra action.", actor.EntityId, -1, actor.Position, actor.RemainingActions));
        }

        private void ApplyStatus(EntityInstance target, string statusId, int overrideDuration, ICollection<BattleEvent> events)
        {
            var template = _catalog.GetStatus(statusId);
            if (template == null)
            {
                return;
            }

            var duration = overrideDuration > 0 ? overrideDuration : template.DefaultDuration;
            target.Statuses.AddOrRefresh(statusId, duration);
            if (target.Team == TeamId.Player)
            {
                target.RemainingActions += template.ActionsGrantedOnApply;
            }

            events.Add(CreateEvent(BattleEventType.StatusApplied, $"{GetEntityName(target.TemplateId)} gained {template.DisplayName}.", target.EntityId, -1, target.Position, duration));
        }

        private void ApplyHealing(EntityInstance target, int amount, ICollection<BattleEvent> events)
        {
            if (amount <= 0 || !target.IsAlive)
            {
                return;
            }

            var healedAmount = Math.Min(amount, target.MaxHp - target.CurrentHp);
            if (healedAmount <= 0)
            {
                return;
            }

            target.CurrentHp += healedAmount;
            events.Add(CreateEvent(BattleEventType.HealingApplied, $"{GetEntityName(target.TemplateId)} healed {healedAmount} HP.", target.EntityId, -1, target.Position, healedAmount));
        }

        private void ApplyDamage(BattleState state, int sourceEntityId, int targetEntityId, int amount, ICollection<BattleEvent> events)
        {
            if (amount <= 0 || !state.Entities.TryGetValue(targetEntityId, out var target) || !target.IsAlive)
            {
                return;
            }

            target.CurrentHp -= amount;
            events.Add(CreateEvent(BattleEventType.DamageApplied, $"{GetEntityName(target.TemplateId)} took {amount} damage.", targetEntityId, sourceEntityId, target.Position, amount));
            if (target.CurrentHp > 0)
            {
                return;
            }

            target.IsAlive = false;
            target.CurrentHp = 0;
            if (target.OccupiesCell)
            {
                state.Grid.Occupancy.Remove(target.Position);
            }

            events.Add(CreateEvent(BattleEventType.UnitDied, $"{GetEntityName(target.TemplateId)} was defeated.", targetEntityId, sourceEntityId, target.Position, 0));
            ReturnBoundUnitCardToDiscard(state, targetEntityId, events);
        }

        private void MoveEntityTo(BattleState state, EntityInstance entity, GridPosition destination, ICollection<BattleEvent> events)
        {
            if (!entity.OccupiesCell || entity.Position.Equals(destination))
            {
                return;
            }

            state.Grid.Occupancy.Remove(entity.Position);
            entity.Position = destination;
            state.Grid.Occupancy[destination] = entity.EntityId;
            events.Add(CreateEvent(BattleEventType.UnitMoved, $"{GetEntityName(entity.TemplateId)} moved to {destination}.", entity.EntityId, -1, destination, 0));
        }

        private static int Ceil(float value)
        {
            return (int)Math.Ceiling(value);
        }
    }
}
