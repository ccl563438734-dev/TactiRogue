using System;
using System.Collections.Generic;
using System.Linq;

namespace TactiRogue
{
    public sealed partial class TactiRogueEngine
    {
        public ActionResult ResolveUnitTurn(BattleState state, UnitTurnRequest request)
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

            if (!TryExecuteUnitTurnCore(state, actor, action, request, result.Events, true, out var failureReason))
            {
                return Fail(result, failureReason);
            }

            RefreshEnemyIntents(state, result.Events);
            CheckForWinOrLoss(state, result.Events);
            return Finish(result, state);
        }

        public bool UsesSeparateMovePhase(BattleState state, int actorEntityId)
        {
            return state.Entities.TryGetValue(actorEntityId, out var actor)
                   && actor.IsAlive
                   && UsesSeparateMovePhase(actor, _catalog.GetAction(actor.ActionId));
        }

        public List<GridPosition> GetValidMoveTargetCells(BattleState state, int actorEntityId)
        {
            if (!state.Entities.TryGetValue(actorEntityId, out var actor) || !actor.IsAlive)
            {
                return new List<GridPosition>();
            }

            var action = _catalog.GetAction(actor.ActionId);
            return GetValidMoveTargetCells(state, actor, action);
        }

        public ActionResult ApplyTemporaryUnitMove(BattleState state, int actorEntityId, GridPosition targetCell)
        {
            var result = CreateBaseResult(state);
            if (state.Phase != BattlePhase.PlayerAction)
            {
                return Fail(result, "Player actions are only available during the player phase.");
            }

            if (!state.Entities.TryGetValue(actorEntityId, out var actor) || !actor.IsAlive || actor.Team != TeamId.Player)
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

            if (!UsesSeparateMovePhase(actor, action))
            {
                return Fail(result, "That unit does not use a separate move phase.");
            }

            if (!TryResolveMoveTargetCell(state, actor, action, targetCell, true, out var resolvedMoveCell, out var failureReason))
            {
                return Fail(result, failureReason);
            }

            MoveEntityTo(state, actor, resolvedMoveCell, result.Events);
            return Finish(result, state);
        }

        public ActionResult RollbackTemporaryUnitMove(BattleState state, int actorEntityId, GridPosition originalCell)
        {
            var result = CreateBaseResult(state);
            if (!state.Entities.TryGetValue(actorEntityId, out var actor) || !actor.IsAlive)
            {
                return Fail(result, "Invalid acting unit.");
            }

            if (!actor.OccupiesCell || !state.Grid.IsValid(originalCell))
            {
                return Fail(result, "Original move cell is not valid.");
            }

            if (state.Grid.Occupancy.TryGetValue(originalCell, out var occupantId) && occupantId != actor.EntityId)
            {
                return Fail(result, "Original move cell is occupied.");
            }

            MoveEntityTo(state, actor, originalCell, result.Events);
            return Finish(result, state);
        }

        public ActionResult EndUnitAction(BattleState state, int actorEntityId)
        {
            var result = CreateBaseResult(state);
            if (state.Phase != BattlePhase.PlayerAction)
            {
                return Fail(result, "Player actions are only available during the player phase.");
            }

            if (!state.Entities.TryGetValue(actorEntityId, out var actor) || !actor.IsAlive || actor.Team != TeamId.Player)
            {
                return Fail(result, "Invalid acting unit.");
            }

            if (!actor.CanAct || actor.RemainingActions <= 0)
            {
                return Fail(result, "That unit has no actions remaining.");
            }

            actor.RemainingActions = Math.Max(0, actor.RemainingActions - 1);
            result.Events.Add(CreateEvent(BattleEventType.Info, $"{GetEntityName(actor.TemplateId)} ended its action.", actor.EntityId, -1, actor.Position, actor.RemainingActions));
            RefreshEnemyIntents(state, result.Events);
            CheckForWinOrLoss(state, result.Events);
            return Finish(result, state);
        }

        private bool TryExecuteUnitTurnCore(
            BattleState state,
            EntityInstance actor,
            ActionDefinition action,
            UnitTurnRequest request,
            ICollection<BattleEvent> events,
            bool consumeActorAction,
            out string failureReason)
        {
            if (!TryResolveMoveTargetCell(state, actor, action, request.MoveTargetCell, request.HasMoveTargetCell, out var resolvedMoveCell, out failureReason))
            {
                return false;
            }

            if (!ValidateActionTarget(
                    state,
                    actor,
                    action,
                    request.BehaviorTargetCell,
                    request.BehaviorTargetEntityId,
                    request.HasBehaviorTargetCell,
                    request.HasBehaviorTargetEntity,
                    resolvedMoveCell))
            {
                failureReason = "Action target is not valid.";
                return false;
            }

            if (resolvedMoveCell != actor.Position)
            {
                MoveEntityTo(state, actor, resolvedMoveCell, events);
            }

            ExecuteAction(
                state,
                actor.EntityId,
                action,
                request.BehaviorTargetCell,
                request.BehaviorTargetEntityId,
                request.HasBehaviorTargetCell,
                request.HasBehaviorTargetEntity,
                events,
                consumeActorAction);
            failureReason = null;
            return true;
        }

        private bool TryResolveMoveTargetCell(
            BattleState state,
            EntityInstance actor,
            ActionDefinition action,
            GridPosition requestedMoveCell,
            bool hasRequestedMoveCell,
            out GridPosition resolvedMoveCell,
            out string failureReason)
        {
            resolvedMoveCell = actor.Position;
            failureReason = null;

            if (!UsesSeparateMovePhase(actor, action))
            {
                return true;
            }

            var moveProfile = GetMoveProfile(actor);
            var validMoveCells = GetValidMoveTargetCells(state, actor, action);
            if (validMoveCells.Count == 0)
            {
                failureReason = "That unit has no valid move targets.";
                return false;
            }

            if (!hasRequestedMoveCell)
            {
                if (moveProfile.AllowStayInPlace && validMoveCells.Contains(actor.Position))
                {
                    return true;
                }

                failureReason = "A move target is required.";
                return false;
            }

            if (!validMoveCells.Contains(requestedMoveCell))
            {
                failureReason = "Move target is not valid.";
                return false;
            }

            resolvedMoveCell = requestedMoveCell;
            return true;
        }

        private bool UsesSeparateMovePhase(EntityInstance actor, ActionDefinition action)
        {
            var moveProfile = GetMoveProfile(actor);
            return moveProfile.UseSeparateMovePhase && action != null && !action.SkipMovePhase;
        }

        private MoveProfile GetMoveProfile(EntityInstance actor)
        {
            var template = _catalog.GetEntity(actor.TemplateId);
            return template?.MoveProfile?.Clone() ?? new MoveProfile
            {
                UseSeparateMovePhase = false,
                MoveRange = 0,
                MoveType = MoveType.None,
                AllowStayInPlace = true,
                AllowDiagonalMove = true,
                CanPassThroughUnits = false,
                CanPassThroughBuildings = false,
                RequirePath = false,
            };
        }

        private List<GridPosition> GetValidMoveTargetCells(BattleState state, EntityInstance actor, ActionDefinition action)
        {
            var moveProfile = GetMoveProfile(actor);
            if (!UsesSeparateMovePhase(actor, action))
            {
                return moveProfile.AllowStayInPlace ? new List<GridPosition> { actor.Position } : new List<GridPosition>();
            }

            if (moveProfile.MoveType == MoveType.None)
            {
                return moveProfile.AllowStayInPlace ? new List<GridPosition> { actor.Position } : new List<GridPosition>();
            }

            if (!TryBuildMovementSearch(state, actor, moveProfile, out var cameFrom, out var reachable))
            {
                return moveProfile.AllowStayInPlace ? new List<GridPosition> { actor.Position } : new List<GridPosition>();
            }

            var validCells = reachable
                .Where(cell => cell.Equals(actor.Position) || !state.Grid.IsOccupied(cell))
                .OrderBy(cell => cell.Y)
                .ThenBy(cell => cell.X)
                .ToList();

            if (moveProfile.AllowStayInPlace && !validCells.Contains(actor.Position))
            {
                validCells.Insert(0, actor.Position);
            }

            return validCells;
        }

        private bool TryBuildMovementSearch(
            BattleState state,
            EntityInstance actor,
            MoveProfile moveProfile,
            out Dictionary<GridPosition, GridPosition?> cameFrom,
            out HashSet<GridPosition> reachable)
        {
            cameFrom = new Dictionary<GridPosition, GridPosition?> { [actor.Position] = null };
            reachable = new HashSet<GridPosition> { actor.Position };

            if (moveProfile.MoveRange <= 0)
            {
                return true;
            }

            if (moveProfile.MoveType != MoveType.Walk || !moveProfile.RequirePath)
            {
                foreach (var cell in state.Grid.ValidCells)
                {
                    if (cell != actor.Position && state.Grid.IsOccupied(cell))
                    {
                        continue;
                    }

                    var distance = moveProfile.AllowDiagonalMove ? GridMath.Chebyshev(actor.Position, cell) : GridMath.Manhattan(actor.Position, cell);
                    if (distance > moveProfile.MoveRange)
                    {
                        continue;
                    }

                    reachable.Add(cell);
                    if (!cameFrom.ContainsKey(cell))
                    {
                        cameFrom[cell] = actor.Position;
                    }
                }

                return true;
            }

            var directions = moveProfile.AllowDiagonalMove;
            var frontier = new Queue<GridPosition>();
            var distanceMap = new Dictionary<GridPosition, int> { [actor.Position] = 0 };
            frontier.Enqueue(actor.Position);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                var distance = distanceMap[current];
                if (distance >= moveProfile.MoveRange)
                {
                    continue;
                }

                foreach (var neighbor in GridMath.GetNeighbors(current, directions))
                {
                    if (!state.Grid.IsValid(neighbor) || cameFrom.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    if (!CanTraverseMoveCell(state, actor, moveProfile, neighbor))
                    {
                        continue;
                    }

                    frontier.Enqueue(neighbor);
                    distanceMap[neighbor] = distance + 1;
                    cameFrom[neighbor] = current;
                    reachable.Add(neighbor);
                }
            }

            return true;
        }

        private bool CanTraverseMoveCell(BattleState state, EntityInstance actor, MoveProfile moveProfile, GridPosition cell)
        {
            if (!state.Grid.IsOccupied(cell))
            {
                return true;
            }

            if (!state.Grid.Occupancy.TryGetValue(cell, out var occupantId) || occupantId == actor.EntityId)
            {
                return true;
            }

            if (!state.Entities.TryGetValue(occupantId, out var occupant))
            {
                return false;
            }

            if (occupant.EntityKind == EntityKind.Building)
            {
                return moveProfile.CanPassThroughBuildings;
            }

            return moveProfile.CanPassThroughUnits;
        }

        private List<GridPosition> ReconstructMovePath(
            GridPosition destination,
            IReadOnlyDictionary<GridPosition, GridPosition?> cameFrom)
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

        private bool TryBuildMovePath(
            BattleState state,
            EntityInstance actor,
            GridPosition destination,
            out List<GridPosition> path)
        {
            path = new List<GridPosition>();
            var action = _catalog.GetAction(actor.ActionId);
            var moveProfile = GetMoveProfile(actor);
            if (!UsesSeparateMovePhase(actor, action))
            {
                path.Add(actor.Position);
                return actor.Position.Equals(destination);
            }

            if (!TryBuildMovementSearch(state, actor, moveProfile, out var cameFrom, out var reachable) || !reachable.Contains(destination))
            {
                return false;
            }

            path = ReconstructMovePath(destination, cameFrom);
            return true;
        }

        private GridPosition GetResolvedBehaviorOrigin(EntityInstance actor, ActionDefinition action, GridPosition committedMoveCell, bool hasCommittedMoveCell)
        {
            if (!UsesSeparateMovePhase(actor, action))
            {
                return actor.Position;
            }

            return hasCommittedMoveCell ? committedMoveCell : actor.Position;
        }

        private List<GridPosition> GetValidBehaviorTargetCells(BattleState state, EntityInstance actor, ActionDefinition action, GridPosition committedMoveCell, bool hasCommittedMoveCell)
        {
            var behaviorState = state;
            if (UsesSeparateMovePhase(actor, action) && hasCommittedMoveCell && committedMoveCell != actor.Position)
            {
                behaviorState = state.Clone();
                var clonedActor = behaviorState.Entities[actor.EntityId];
                MoveEntityTo(behaviorState, clonedActor, committedMoveCell, new List<BattleEvent>());
                actor = clonedActor;
            }

            return GetValidTargetCells(behaviorState, actor, action);
        }

        private bool CanActorUseActionAfterMove(
            BattleState state,
            EntityInstance actor,
            ActionDefinition action,
            GridPosition targetCell,
            int targetEntityId,
            bool hasTargetCell,
            bool hasTargetEntity)
        {
            foreach (var candidateOrigin in GetActionOriginsForActor(state, actor, action))
            {
                if (ValidateActionTarget(state, actor, action, targetCell, targetEntityId, hasTargetCell, hasTargetEntity, candidateOrigin))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<GridPosition> GetActionOriginsForActor(BattleState state, EntityInstance actor, ActionDefinition action)
        {
            return UsesSeparateMovePhase(actor, action)
                ? GetValidMoveTargetCells(state, actor, action)
                : (IEnumerable<GridPosition>)new[] { actor.Position };
        }

        private GridPosition FindBestActionOrigin(
            BattleState state,
            EntityInstance actor,
            ActionDefinition action,
            GridPosition targetCell,
            int targetEntityId,
            bool hasTargetCell,
            bool hasTargetEntity)
        {
            var originCandidates = GetActionOriginsForActor(state, actor, action)
                .Where(origin => ValidateActionTarget(state, actor, action, targetCell, targetEntityId, hasTargetCell, hasTargetEntity, origin))
                .ToList();

            if (originCandidates.Count == 0)
            {
                return actor.Position;
            }

            var anchorCell = targetCell;
            if (!hasTargetCell && hasTargetEntity && state.Entities.TryGetValue(targetEntityId, out var targetEntity))
            {
                anchorCell = targetEntity.Position;
            }

            return originCandidates
                .OrderBy(origin => GridMath.Chebyshev(origin, anchorCell))
                .ThenBy(origin => GridMath.Chebyshev(actor.Position, origin))
                .First();
        }
    }
}
