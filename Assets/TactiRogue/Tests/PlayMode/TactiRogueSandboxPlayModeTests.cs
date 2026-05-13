using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TactiRogue.Tests
{
    public sealed class TactiRogueSandboxPlayModeTests
    {
        [UnityTest]
        public IEnumerator SandboxBootstrapLoadsAndSwitchesScenarios()
        {
            var bootstrap = GetOrCreateBootstrap();
            yield return null;

            Assert.NotNull(bootstrap);
            Assert.True(bootstrap.IsInitialized);
            Assert.GreaterOrEqual(bootstrap.Scenarios.Count, 3);
            Assert.Greater(bootstrap.BoardCellCount, 0);
            Assert.AreEqual(bootstrap.State.LivingEntities().Count(entity => entity.OccupiesCell), bootstrap.UnitCardCount);

            for (var index = 0; index < bootstrap.Scenarios.Count; index++)
            {
                bootstrap.LoadScenarioByIndex(index);
                yield return null;
                Assert.AreEqual(bootstrap.Scenarios[index].Id, bootstrap.State.ScenarioId);
                Assert.Greater(bootstrap.BoardCellCount, 0);
                Assert.AreEqual(bootstrap.State.LivingEntities().Count(entity => entity.OccupiesCell), bootstrap.UnitCardCount);
                Assert.AreEqual(bootstrap.State.EnemyIntents.Count, bootstrap.State.CaptureSnapshot(bootstrap.Engine.Catalog).IntentDetails.Count);
            }

            bootstrap.HandleEndTurnClicked();
            yield return null;

            Assert.GreaterOrEqual(bootstrap.State.TurnNumber, 1);
            Assert.NotNull(bootstrap.State);
            Assert.AreEqual(bootstrap.State.EnemyIntents.Count, bootstrap.State.CaptureSnapshot(bootstrap.Engine.Catalog).IntentDetails.Count);
        }

        [UnityTest]
        public IEnumerator SandboxCreates3DCardPiecesAtGridCenters()
        {
            var bootstrap = GetOrCreateBootstrap();
            yield return null;

            var entity = bootstrap.State.LivingEntities()
                .Where(item => item.OccupiesCell)
                .OrderBy(item => item.EntityId)
                .First();
            var expectedPosition = new Vector3(
                -((bootstrap.State.Grid.Width - 1) * 0.5f) + entity.Position.X,
                0.05f,
                -((bootstrap.State.Grid.Height - 1) * 0.5f) + entity.Position.Y);

            Assert.True(bootstrap.TryGetUnitCardWorldPosition(entity.EntityId, out var worldPosition));
            Assert.Less(Vector3.Distance(expectedPosition, worldPosition), 0.01f);
            Assert.True(bootstrap.TryGetUnitCardIdleTiltAngle(entity.EntityId, out var idleTiltAngle));
            Assert.AreEqual(45f, idleTiltAngle, 0.01f);
            Assert.True(bootstrap.TryGetUnitPresentationHasStandardHierarchy(entity.EntityId));
            Assert.True(bootstrap.TryGetUnitPresentationUsesModelPortrait(entity.EntityId));
        }

        [UnityTest]
        public IEnumerator SandboxShowsDangerCellsAndSupportsTwoStageUnitInput()
        {
            var bootstrap = GetOrCreateBootstrap();
            yield return null;

            var scenarioIndex = -1;
            for (var index = 0; index < bootstrap.Scenarios.Count; index++)
            {
                bootstrap.LoadScenarioByIndex(index);
                yield return null;

                if (bootstrap.Engine.BuildIntentViewData(bootstrap.State).Any(intent => intent.DangerCells.Count > 0))
                {
                    scenarioIndex = index;
                    break;
                }
            }

            Assert.GreaterOrEqual(scenarioIndex, 0);

            var dangerCells = bootstrap.Engine.BuildIntentViewData(bootstrap.State)
                .SelectMany(intent => intent.DangerCells)
                .Distinct()
                .ToList();
            Assert.IsNotEmpty(dangerCells);

            var dangerCell = dangerCells.FirstOrDefault(cell => bootstrap.State.Grid.IsValid(cell) && !bootstrap.State.Grid.Occupancy.ContainsKey(cell));
            if (!dangerCells.Contains(dangerCell))
            {
                dangerCell = dangerCells[0];
            }

            var safeCell = bootstrap.State.Grid.ValidCells.First(cell => !dangerCells.Contains(cell) && !bootstrap.State.Grid.Occupancy.ContainsKey(cell));

            Assert.True(bootstrap.TryGetBoardCellBackground(dangerCell, out var dangerColor));
            Assert.True(bootstrap.TryGetBoardCellBackground(safeCell, out var safeColor));
            Assert.Greater(dangerColor.r, safeColor.r);

            var actingUnit = bootstrap.State.LivingEntities(TeamId.Player)
                .First(entity => entity.CanAct
                                 && entity.RemainingActions > 0
                                 && bootstrap.Engine.UsesSeparateMovePhase(bootstrap.State, entity.EntityId));

            bootstrap.HandleBoardCellClicked(actingUnit.Position);
            yield return null;

            Assert.AreEqual(BattleInputState.MoveTargeting, bootstrap.InputController.CurrentState);

            var moveTarget = actingUnit.Position;
            foreach (var cell in bootstrap.Engine.GetValidMoveTargetCells(bootstrap.State, actingUnit.EntityId))
            {
                if (cell != actingUnit.Position)
                {
                    moveTarget = cell;
                    break;
                }
            }

            bootstrap.HandleBoardCellClicked(moveTarget);
            yield return null;

            Assert.AreEqual(BattleInputState.BehaviorTargeting, bootstrap.InputController.CurrentState);
            Assert.True(bootstrap.SelectionController.HasCommittedMoveCell);
            Assert.AreEqual(moveTarget, bootstrap.SelectionController.CommittedMoveCell);

            bootstrap.HandleCancelClicked();
            yield return null;

            Assert.AreEqual(BattleInputState.MoveTargeting, bootstrap.InputController.CurrentState);

            bootstrap.HandleCancelClicked();
            yield return null;

            Assert.AreEqual(BattleInputState.Idle, bootstrap.InputController.CurrentState);
        }

        [UnityTest]
        public IEnumerator SandboxShowsPileCountsAndAllowsPileInspection()
        {
            var bootstrap = GetOrCreateBootstrap();
            yield return null;

            Assert.NotNull(bootstrap.State);
            Assert.GreaterOrEqual(bootstrap.State.DrawPile.Count, 0);
            Assert.GreaterOrEqual(bootstrap.State.DiscardPile.Count, 0);

            bootstrap.HandleDrawPileClicked();
            yield return null;

            Assert.AreEqual(CardZone.DrawPile, bootstrap.ViewedPileZone);
            Assert.AreEqual(bootstrap.State.DrawPile.Count, bootstrap.GetViewedPileEntries().Count);
            Assert.True(bootstrap.LogLines.Any(line => line.Contains("DrawPile")));

            bootstrap.HandleDiscardPileClicked();
            yield return null;

            Assert.AreEqual(CardZone.DiscardPile, bootstrap.ViewedPileZone);
            Assert.AreEqual(bootstrap.State.DiscardPile.Count, bootstrap.GetViewedPileEntries().Count);
            Assert.True(bootstrap.LogLines.Any(line => line.Contains("DiscardPile")));

            bootstrap.HandleClosePileViewerClicked();
            yield return null;

            Assert.AreEqual(CardZone.None, bootstrap.ViewedPileZone);
        }

        private static BattleSandboxBootstrap GetOrCreateBootstrap()
        {
            var bootstrap = Object.FindObjectOfType<BattleSandboxBootstrap>();
            if (bootstrap != null)
            {
                return bootstrap;
            }

            var root = new GameObject("PlayModeBootstrap");
            return root.AddComponent<BattleSandboxBootstrap>();
        }
    }
}
