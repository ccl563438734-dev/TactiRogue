using System.IO;
using System.Linq;
using NUnit.Framework;

namespace TactiRogue.Tests
{
    public sealed class TactiRogueCoreTests
    {
        private TactiRogueEngine _engine;

        [SetUp]
        public void SetUp()
        {
            _engine = new TactiRogueEngine(TactiRogueContentProvider.CreateBuiltInDatabase());
        }

        [Test]
        public void SummonRangeUsesCommanderPosition()
        {
            var scenario = CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 5, 1));
            scenario.StartingDeck = new[] { "call_guardian" };
            scenario.CardsPerTurn = 1;
            var state = _engine.CreateBattle(scenario);

            var guardianCardId = GetHandCardId(state, "call_guardian");
            var validCells = _engine.GetValidCardTargetCells(state, guardianCardId);

            Assert.Contains(new GridPosition(0, 0), validCells);
            Assert.Contains(new GridPosition(2, 2), validCells);
            Assert.False(validCells.Contains(new GridPosition(4, 4)));
        }

        [Test]
        public void PushStrikeIntoUnitDealsCritToBoth()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 0, 0),
                ("vanguard", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 2, 1),
                ("hunter", TeamId.Enemy, 3, 1)));

            var attacker = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "vanguard");
            var primary = state.LivingEntities(TeamId.Enemy).OrderBy(entity => entity.Position.X).First();
            var blocker = state.LivingEntities(TeamId.Enemy).OrderBy(entity => entity.Position.X).Last();

            _engine.ResolveAction(state, new ActionRequest
            {
                ActorEntityId = attacker.EntityId,
                TargetEntityId = primary.EntityId,
                TargetCell = primary.Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            Assert.AreEqual(1, state.Entities[primary.EntityId].CurrentHp);
            Assert.AreEqual(1, state.Entities[blocker.EntityId].CurrentHp);
        }

        [Test]
        public void PushTwoTransmitsToTheThirdUnit()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 0, 0),
                ("vanguard", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 2, 1),
                ("hunter", TeamId.Enemy, 3, 1),
                ("hunter", TeamId.Enemy, 4, 1)));

            var attacker = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "vanguard");
            var enemies = state.LivingEntities(TeamId.Enemy).OrderBy(entity => entity.Position.X).ToArray();

            _engine.ResolveAction(state, new ActionRequest
            {
                ActorEntityId = attacker.EntityId,
                TargetEntityId = enemies[0].EntityId,
                TargetCell = enemies[0].Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            Assert.AreEqual(1, state.Entities[enemies[0].EntityId].CurrentHp);
            Assert.AreEqual(1, state.Entities[enemies[1].EntityId].CurrentHp);
            Assert.AreEqual(1, state.Entities[enemies[2].EntityId].CurrentHp);
        }

        [Test]
        public void PushIntoBoundaryDealsDoubleDamage()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 2, 2),
                ("guardian", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 0, 1)));

            var attacker = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "guardian");
            var target = state.LivingEntities(TeamId.Enemy).First();

            _engine.ResolveAction(state, new ActionRequest
            {
                ActorEntityId = attacker.EntityId,
                TargetEntityId = target.EntityId,
                TargetCell = target.Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            Assert.False(state.Entities[target.EntityId].IsAlive);
        }

        [Test]
        public void AnchoredTargetStopsForcedMovement()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 0, 0),
                ("vanguard", TeamId.Player, 1, 1),
                ("anchor_warden", TeamId.Enemy, 2, 1)));

            var attacker = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "vanguard");
            var target = state.LivingEntities(TeamId.Enemy).First();

            _engine.ResolveAction(state, new ActionRequest
            {
                ActorEntityId = attacker.EntityId,
                TargetEntityId = target.EntityId,
                TargetCell = target.Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            Assert.AreEqual(new GridPosition(2, 1), state.Entities[target.EntityId].Position);
            Assert.AreEqual(4, state.Entities[target.EntityId].CurrentHp);
        }

        [Test]
        public void TauntRedirectsEnemyUnitLockIntent()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("guardian", TeamId.Player, 1, 2),
                ("hunter", TeamId.Enemy, 5, 1)));

            var guardian = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "guardian");
            var intent = state.EnemyIntents.Single();

            Assert.AreEqual(guardian.EntityId, intent.TargetEntityId);
        }

        [Test]
        public void BreakthroughGrantsExtraActionOnCollision()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 0, 0),
                ("vanguard", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 2, 1),
                ("hunter", TeamId.Enemy, 3, 1)));

            var attacker = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "vanguard");
            var target = state.LivingEntities(TeamId.Enemy).OrderBy(entity => entity.Position.X).First();

            _engine.ResolveAction(state, new ActionRequest
            {
                ActorEntityId = attacker.EntityId,
                TargetEntityId = target.EntityId,
                TargetCell = target.Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            Assert.AreEqual(1, state.Entities[attacker.EntityId].RemainingActions);
        }

        [Test]
        public void UnitMoveTargetsIncludeCurrentCellAndStayInPlaceTurnResolves()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 0, 0),
                ("guardian", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 2, 1)));

            var guardian = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "guardian");
            var target = state.LivingEntities(TeamId.Enemy).First();
            var validMoveCells = _engine.GetValidMoveTargetCells(state, guardian.EntityId);

            Assert.Contains(guardian.Position, validMoveCells);

            var result = _engine.ResolveUnitTurn(state, new UnitTurnRequest
            {
                ActorEntityId = guardian.EntityId,
                MoveTargetCell = guardian.Position,
                HasMoveTargetCell = true,
                BehaviorTargetEntityId = target.EntityId,
                BehaviorTargetCell = target.Position,
                HasBehaviorTargetCell = true,
                HasBehaviorTargetEntity = true,
            });

            Assert.True(result.Success);
            Assert.AreEqual(new GridPosition(1, 1), state.Entities[guardian.EntityId].Position);
            Assert.AreEqual(2, state.Entities[target.EntityId].CurrentHp);
        }

        [Test]
        public void SkirmisherSkipsSeparateMovePhase()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 0, 0),
                ("skirmisher", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 3, 1)));

            var skirmisher = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "skirmisher");
            var validMoveCells = _engine.GetValidMoveTargetCells(state, skirmisher.EntityId);

            Assert.False(_engine.UsesSeparateMovePhase(state, skirmisher.EntityId));
            Assert.AreEqual(1, validMoveCells.Count);
            Assert.AreEqual(skirmisher.Position, validMoveCells[0]);
        }

        [Test]
        public void CommanderCanMoveThenGrantExtraAction()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("guardian", TeamId.Player, 4, 1),
                ("hunter", TeamId.Enemy, 6, 1)));

            var commander = state.Entities[state.CommanderEntityId];
            var ally = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "guardian");

            var result = _engine.ResolveUnitTurn(state, new UnitTurnRequest
            {
                ActorEntityId = commander.EntityId,
                MoveTargetCell = new GridPosition(2, 1),
                HasMoveTargetCell = true,
                BehaviorTargetEntityId = ally.EntityId,
                BehaviorTargetCell = ally.Position,
                HasBehaviorTargetCell = true,
                HasBehaviorTargetEntity = true,
            });

            Assert.True(result.Success);
            Assert.AreEqual(new GridPosition(2, 1), state.Entities[commander.EntityId].Position);
            Assert.AreEqual(2, state.Entities[ally.EntityId].RemainingActions);
        }

        [Test]
        public void HunterMovesBeforeAttackingDuringEnemyTurn()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 4, 1)));

            var hunter = state.LivingEntities(TeamId.Enemy).Single();
            var commander = state.Entities[state.CommanderEntityId];

            var endTurn = _engine.EndTurn(state);

            Assert.True(endTurn.Success);
            Assert.AreEqual(new GridPosition(2, 1), state.Entities[hunter.EntityId].Position);
            Assert.Less(state.Entities[commander.EntityId].CurrentHp, state.Entities[commander.EntityId].MaxHp);
            Assert.True(endTurn.Events.Any(evt => evt.EventType == BattleEventType.UnitMoved && evt.SubjectEntityId == hunter.EntityId));
            Assert.True(endTurn.Events.Any(evt => evt.EventType == BattleEventType.EnemyActionExecuted && evt.SubjectEntityId == hunter.EntityId));
        }

        [Test]
        public void BombardierMovesBeforeExecutingAreaIntent()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 0, 0),
                ("bombardier", TeamId.Enemy, 5, 0)));

            var bombardier = state.LivingEntities(TeamId.Enemy).Single();

            var endTurn = _engine.EndTurn(state);

            Assert.True(endTurn.Success);
            Assert.AreEqual(new GridPosition(4, 0), state.Entities[bombardier.EntityId].Position);
            Assert.True(endTurn.Events.Any(evt => evt.EventType == BattleEventType.UnitMoved && evt.SubjectEntityId == bombardier.EntityId));
            Assert.True(endTurn.Events.Any(evt => evt.EventType == BattleEventType.EnemyActionExecuted && evt.SubjectEntityId == bombardier.EntityId));
        }

        [Test]
        public void PreviewMatchesResolvedAction()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 0, 0),
                ("vanguard", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 2, 1),
                ("hunter", TeamId.Enemy, 3, 1)));

            var attacker = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "vanguard");
            var target = state.LivingEntities(TeamId.Enemy).OrderBy(entity => entity.Position.X).First();
            var preview = _engine.Preview(state, new PreviewRequest
            {
                SourceKind = PreviewSourceKind.UnitAction,
                ActorEntityId = attacker.EntityId,
                TargetEntityId = target.EntityId,
                TargetCell = target.Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            var resolved = _engine.ResolveAction(state, new ActionRequest
            {
                ActorEntityId = attacker.EntityId,
                TargetEntityId = target.EntityId,
                TargetCell = target.Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            var previewState = preview.SimulatedResult.State;
            Assert.True(preview.Valid);
            Assert.True(resolved.Success);
            Assert.AreEqual(previewState.Entities[target.EntityId].CurrentHp, state.Entities[target.EntityId].CurrentHp);
            Assert.AreEqual(previewState.Entities[target.EntityId].Position, state.Entities[target.EntityId].Position);
        }

        [Test]
        public void PlayerCardRefreshesEnemyIntentImmediately()
        {
            var scenario = CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("banner", TeamId.Player, 1, 2),
                ("hunter", TeamId.Enemy, 5, 1));
            scenario.StartingDeck = new[] { "spell_fortify" };
            scenario.CardsPerTurn = 1;

            var state = _engine.CreateBattle(scenario);
            var ally = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "banner");
            var cardId = GetHandCardId(state, "spell_fortify");

            Assert.AreEqual(state.CommanderEntityId, state.EnemyIntents.Single().TargetEntityId);

            var result = _engine.ResolveCard(state, new PlayCardRequest
            {
                CardInstanceId = cardId,
                TargetEntityId = ally.EntityId,
                TargetCell = ally.Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            Assert.True(result.Success);
            Assert.AreEqual(ally.EntityId, state.EnemyIntents.Single().TargetEntityId);
            Assert.True(result.Events.Any(evt => evt.EventType == BattleEventType.IntentGenerated || evt.EventType == BattleEventType.IntentRevalidated));
        }

        [Test]
        public void BombardierIntentKeepsOriginalAreaWhenCommanderMoves()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("skirmisher", TeamId.Player, 1, 2),
                ("bombardier", TeamId.Enemy, 5, 1)));

            var skirmisher = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "skirmisher");
            var commander = state.Entities[state.CommanderEntityId];
            var lockedCell = state.EnemyIntents.Single().TargetCell;

            var result = _engine.ResolveAction(state, new ActionRequest
            {
                ActorEntityId = skirmisher.EntityId,
                TargetEntityId = commander.EntityId,
                TargetCell = commander.Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            Assert.True(result.Success);
            Assert.AreEqual(new GridPosition(1, 2), state.Entities[commander.EntityId].Position);
            Assert.AreEqual(lockedCell, state.EnemyIntents.Single().TargetCell);
            Assert.AreNotEqual(state.Entities[commander.EntityId].Position, state.EnemyIntents.Single().TargetCell);
        }

        [Test]
        public void BombardierSkipsTurnWhenLockedAreaBecomesIllegal()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 0, 0),
                ("vanguard", TeamId.Player, 3, 0),
                ("bombardier", TeamId.Enemy, 4, 0)));

            var attacker = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "vanguard");
            var bombardier = state.LivingEntities(TeamId.Enemy).First(entity => entity.TemplateId == "bombardier");

            var result = _engine.ResolveAction(state, new ActionRequest
            {
                ActorEntityId = attacker.EntityId,
                TargetEntityId = bombardier.EntityId,
                TargetCell = bombardier.Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            var cancelledIntent = state.EnemyIntents.Single();
            Assert.True(result.Success);
            Assert.True(cancelledIntent.IsCancelled);
            Assert.AreEqual(IntentFallbackMode.SkipTurn, cancelledIntent.FallbackMode);
            Assert.True(result.Events.Any(evt => evt.EventType == BattleEventType.IntentCancelled));

            var endTurn = _engine.EndTurn(state);
            Assert.False(endTurn.Events.Any(evt => evt.EventType == BattleEventType.EnemyActionExecuted && evt.SubjectEntityId == bombardier.EntityId));
        }

        [Test]
        public void ChargerKeepsLockedDirectionAfterBeingMoved()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("skirmisher", TeamId.Player, 4, 2),
                ("charger", TeamId.Enemy, 5, 2)));

            var skirmisher = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "skirmisher");
            var charger = state.LivingEntities(TeamId.Enemy).First(entity => entity.TemplateId == "charger");
            var originalDirection = state.EnemyIntents.Single().Direction;

            var result = _engine.ResolveAction(state, new ActionRequest
            {
                ActorEntityId = skirmisher.EntityId,
                TargetEntityId = charger.EntityId,
                TargetCell = charger.Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            Assert.True(result.Success);
            Assert.AreEqual(originalDirection, state.EnemyIntents.Single().Direction);

            var endTurn = _engine.EndTurn(state);
            Assert.AreEqual(new GridPosition(2, 0), state.Entities[charger.EntityId].Position);
            Assert.True(endTurn.Events.Any(evt => evt.EventType == BattleEventType.EnemyActionExecuted && evt.SubjectEntityId == charger.EntityId));
        }

        [Test]
        public void BehaviorPreviewUsesCommittedMoveCell()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 0, 0),
                ("guardian", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 3, 1)));

            var guardian = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "guardian");
            var target = state.LivingEntities(TeamId.Enemy).Single();
            var moveCell = new GridPosition(2, 1);
            var preview = _engine.Preview(state, new PreviewRequest
            {
                SourceKind = PreviewSourceKind.UnitAction,
                Stage = UnitTurnStage.BehaviorTargeting,
                ActorEntityId = guardian.EntityId,
                CommittedMoveCell = moveCell,
                HasCommittedMoveCell = true,
                TargetEntityId = target.EntityId,
                TargetCell = target.Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            var resolved = _engine.ResolveUnitTurn(state, new UnitTurnRequest
            {
                ActorEntityId = guardian.EntityId,
                MoveTargetCell = moveCell,
                HasMoveTargetCell = true,
                BehaviorTargetEntityId = target.EntityId,
                BehaviorTargetCell = target.Position,
                HasBehaviorTargetCell = true,
                HasBehaviorTargetEntity = true,
            });

            Assert.True(preview.Valid);
            Assert.True(resolved.Success);
            Assert.AreEqual(moveCell, preview.SimulatedResult.State.Entities[guardian.EntityId].Position);
            Assert.AreEqual(preview.SimulatedResult.State.Entities[guardian.EntityId].Position, state.Entities[guardian.EntityId].Position);
            Assert.AreEqual(preview.SimulatedResult.State.Entities[target.EntityId].CurrentHp, state.Entities[target.EntityId].CurrentHp);
        }

        [Test]
        public void PreviewUsesSimulatedEnemyIntents()
        {
            var scenario = CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("banner", TeamId.Player, 1, 2),
                ("hunter", TeamId.Enemy, 5, 1));
            scenario.StartingDeck = new[] { "spell_fortify" };
            scenario.CardsPerTurn = 1;

            var state = _engine.CreateBattle(scenario);
            var ally = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "banner");
            var cardId = GetHandCardId(state, "spell_fortify");

            var preview = _engine.Preview(state, new PreviewRequest
            {
                SourceKind = PreviewSourceKind.Card,
                CardInstanceId = cardId,
                TargetEntityId = ally.EntityId,
                TargetCell = ally.Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            Assert.True(preview.Valid);
            Assert.AreEqual(ally.EntityId, preview.EnemyIntents.Single().TargetEntityId);
            Assert.AreEqual(ally.EntityId, preview.SimulatedResult.State.EnemyIntents.Single().TargetEntityId);
        }

        [Test]
        public void BuiltInDatabaseContainsCardPieceVisuals()
        {
            var database = TactiRogueContentProvider.CreateBuiltInDatabase();
            var guardianVisual = database.GetCardPieceVisual("guardian");

            Assert.NotNull(guardianVisual);
            Assert.GreaterOrEqual(database.CardPieceVisualDefinitions.Length, database.EntityTemplates.Length);
            Assert.AreEqual("Assert/Model/sample", guardianVisual.ModelKey);
            Assert.AreEqual("Assert/Model/F_Unit", guardianVisual.FrameModelKey);
            Assert.AreEqual("Assert/Picture/守卫", guardianVisual.CardArtKey);
            Assert.AreEqual("Assert/Picture/卡背", guardianVisual.BackArtKey);
            Assert.AreEqual(45f, guardianVisual.IdleTiltAngle);
        }

        [Test]
        public void MotionDefinitionTotalDurationUsesEnabledSegments()
        {
            var definition = UnityEngine.ScriptableObject.CreateInstance<MotionDefinition>();
            definition.Segments.Add(new MotionSegment
            {
                Enabled = true,
                Delay = 0.1f,
                Duration = 0.25f,
                TargetLayer = MotionTargetLayer.MotionRoot,
            });
            definition.Segments.Add(new MotionSegment
            {
                Enabled = false,
                Delay = 10f,
                Duration = 10f,
                TargetLayer = MotionTargetLayer.Portrait,
            });
            definition.Segments.Add(new MotionSegment
            {
                Enabled = true,
                Delay = 0.05f,
                Duration = 0.2f,
                TargetLayer = MotionTargetLayer.ScaleRoot,
            });

            Assert.AreEqual(0.6f, definition.TotalDuration, 0.001f);
            Assert.AreEqual(MotionTargetLayer.Portrait, definition.Segments[1].TargetLayer);
        }

        [Test]
        public void ExcelExportRoundTripBuildsEquivalentContent()
        {
            var workbookPath = Path.Combine(Path.GetTempPath(), "TactiRogue_excel_roundtrip.xlsx");
            if (File.Exists(workbookPath))
            {
                File.Delete(workbookPath);
            }

            TactiRogueExcelExporter.ExportCurrentDataToWorkbook(workbookPath);
            Assert.True(TactiRogueExcelValidator.TryReadWorkbook(workbookPath, out var workbookData, out var report), report.ToDisplayString(64));

            var bundle = TactiRogueExcelImporter.BuildImportBundle(workbookData);
            TactiRogueContentProvider.ResetCache();
            TactiRogueScenarioRepository.ResetCache();
            var currentDatabase = TactiRogueContentProvider.LoadOrCreateDatabase();
            var currentScenarios = TactiRogueScenarioRepository.LoadAll();

            Assert.AreEqual(currentDatabase.StatusTemplates.Length, bundle.Database.StatusTemplates.Length);
            Assert.AreEqual(currentDatabase.ActionDefinitions.Length, bundle.Database.ActionDefinitions.Length);
            Assert.AreEqual(currentDatabase.EntityTemplates.Length, bundle.Database.EntityTemplates.Length);
            Assert.AreEqual(currentDatabase.CardTemplates.Length, bundle.Database.CardTemplates.Length);
            Assert.AreEqual(currentDatabase.IntentDefinitions.Length, bundle.Database.IntentDefinitions.Length);
            Assert.AreEqual(currentDatabase.CardPieceVisualDefinitions.Length, bundle.Database.CardPieceVisualDefinitions.Length);

            Assert.AreEqual(currentDatabase.GetEntity("guardian").DisplayName, bundle.Database.GetEntity("guardian").DisplayName);
            Assert.AreEqual(currentDatabase.GetEntity("bombardier").MoveProfile.MoveRange, bundle.Database.GetEntity("bombardier").MoveProfile.MoveRange);
            Assert.AreEqual(currentDatabase.GetAction("charger_rush").SkipMovePhase, bundle.Database.GetAction("charger_rush").SkipMovePhase);
            Assert.AreEqual(currentDatabase.GetIntent("hunter_lock").TargetingMode, bundle.Database.GetIntent("hunter_lock").TargetingMode);
            Assert.AreEqual(currentDatabase.GetCardPieceVisual("guardian").CardArtKey, bundle.Database.GetCardPieceVisual("guardian").CardArtKey);
            Assert.AreEqual(currentDatabase.GetCardPieceVisual("guardian").FrameModelKey, bundle.Database.GetCardPieceVisual("guardian").FrameModelKey);
            Assert.AreEqual(45f, bundle.Database.GetCardPieceVisual("guardian").IdleTiltAngle);

            CollectionAssert.AreEqual(currentScenarios.Select(item => item.Id).ToArray(), bundle.Scenarios.Select(item => item.Id).ToArray());
            CollectionAssert.AreEqual(
                currentScenarios.First(item => item.Id == "mixed_intents").StartingDeck,
                bundle.Scenarios.First(item => item.Id == "mixed_intents").StartingDeck);
        }

        [Test]
        public void ExcelValidatorRejectsMissingRequiredSheet()
        {
            var workbookPath = Path.Combine(Path.GetTempPath(), "TactiRogue_excel_missing_sheet.xlsx");
            if (File.Exists(workbookPath))
            {
                File.Delete(workbookPath);
            }

            var workbook = new TactiRogueExcelWorkbook();
            var statusSheet = new TactiRogueExcelSheet("Status");
            statusSheet.AddRow(StatusRow.Headers);
            workbook.Sheets.Add(statusSheet);
            TactiRogueSimpleXlsx.Save(workbookPath, workbook);

            var report = TactiRogueExcelValidator.ValidateWorkbook(workbookPath);
            Assert.False(report.IsValid);
            Assert.True(report.Errors.Any(error => error.Contains("missing sheet 'Action'")));
        }

        [Test]
        public void ExcelWorkbookDataValidationRejectsBrokenMoveProfileReference()
        {
            var workbookData = TactiRogueExcelExporter.BuildWorkbookDataFromCurrentContent();
            workbookData.EntityRows.First(row => row.Id == "guardian").MoveProfileId = "missing_move_profile";

            var report = TactiRogueExcelValidator.ValidateWorkbookData(workbookData);
            Assert.False(report.IsValid);
            Assert.True(report.Errors.Any(error => error.Contains("MoveProfileId")));
        }

        [Test]
        public void ExcelWorkbookDataValidationRejectsDuplicateCardPieceVisual()
        {
            var workbookData = TactiRogueExcelExporter.BuildWorkbookDataFromCurrentContent();
            var guardianVisual = workbookData.CardPieceVisualRows.First(row => row.Id == "guardian");
            workbookData.CardPieceVisualRows.Add(new CardPieceVisualRow
            {
                Id = guardianVisual.Id,
                ModelKey = guardianVisual.ModelKey,
                CardArtKey = guardianVisual.CardArtKey,
                BackArtKey = guardianVisual.BackArtKey,
                IdleTiltAngle = guardianVisual.IdleTiltAngle,
                DefaultScale = guardianVisual.DefaultScale,
                YOffset = guardianVisual.YOffset,
            });

            var report = TactiRogueExcelValidator.ValidateWorkbookData(workbookData);
            Assert.False(report.IsValid);
            Assert.True(report.Errors.Any(error => error.Contains("CardPieceVisual") && error.Contains("guardian")));
        }

        [Test]
        public void CreateBattleDistributesCardsAcrossDrawPileAndHand()
        {
            var scenario = CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 7, 5));
            scenario.StartingDeck = new[] { "call_guardian", "spell_fortify", "spell_breakthrough" };
            scenario.CardsPerTurn = 2;

            var state = _engine.CreateBattle(scenario);

            Assert.AreEqual(3, state.CardInstances.Count);
            Assert.AreEqual(2, state.Hand.Count);
            Assert.AreEqual(1, state.DrawPile.Count);
            Assert.AreEqual(0, state.DiscardPile.Count);
            Assert.AreEqual(0, state.InBattleUnitCards.Count);
            Assert.True(state.GetHandCards().All(card => card.CurrentZone == CardZone.Hand));
            Assert.True(state.GetDrawPileCards().All(card => card.CurrentZone == CardZone.DrawPile));
        }

        [Test]
        public void EndTurnDiscardsRemainingHandCards()
        {
            var scenario = CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 7, 5));
            scenario.StartingDeck = new[] { "call_guardian", "spell_fortify" };
            scenario.CardsPerTurn = 2;

            var state = _engine.CreateBattle(scenario);
            var originalHand = state.Hand.ToArray();
            state.CardsPerTurn = 0;

            var result = _engine.EndTurn(state);

            Assert.True(result.Success);
            Assert.AreEqual(0, state.Hand.Count);
            CollectionAssert.AreEquivalent(originalHand, state.DiscardPile);
            Assert.True(originalHand.All(cardId => state.GetCardInstance(cardId).CurrentZone == CardZone.DiscardPile));
        }

        [Test]
        public void DrawPhaseShufflesDiscardIntoDrawPileWhenNeeded()
        {
            var scenario = CreateScenario(
                ("commander_core", TeamId.Player, 0, 0),
                ("hunter", TeamId.Enemy, 7, 5));
            scenario.StartingDeck = new[] { "call_guardian", "spell_fortify" };
            scenario.CardsPerTurn = 1;

            var state = _engine.CreateBattle(scenario);
            state.CardsPerTurn = 2;

            var result = _engine.EndTurn(state);

            Assert.True(result.Success);
            Assert.True(result.Events.Any(evt => evt.EventType == BattleEventType.DrawPhaseStarted));
            Assert.True(result.Events.Any(evt => evt.EventType == BattleEventType.ShuffleDiscardIntoDrawPile));
            Assert.AreEqual(2, state.Hand.Count);
            Assert.AreEqual(0, state.DrawPile.Count);
            Assert.AreEqual(0, state.DiscardPile.Count);
            Assert.True(state.GetHandCards().All(card => card.CurrentZone == CardZone.Hand));
        }

        [Test]
        public void DrawPhaseStopsGracefullyWhenAllPilesAreEmpty()
        {
            var scenario = CreateScenario(
                ("commander_core", TeamId.Player, 0, 0),
                ("hunter", TeamId.Enemy, 7, 5));
            scenario.StartingDeck = new[] { "spell_fortify" };
            scenario.CardsPerTurn = 3;

            var state = _engine.CreateBattle(scenario);

            Assert.AreEqual(1, state.Hand.Count);
            Assert.AreEqual(0, state.DrawPile.Count);
            Assert.AreEqual(0, state.DiscardPile.Count);
            Assert.AreEqual(CardZone.Hand, state.GetCardInstance(state.Hand[0]).CurrentZone);
        }

        [Test]
        public void SpellCardsMoveFromHandToDiscardAfterSuccessfulPlay()
        {
            var scenario = CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("banner", TeamId.Player, 1, 2),
                ("hunter", TeamId.Enemy, 5, 1));
            scenario.StartingDeck = new[] { "spell_fortify" };
            scenario.CardsPerTurn = 1;

            var state = _engine.CreateBattle(scenario);
            var ally = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "banner");
            var cardId = GetHandCardId(state, "spell_fortify");

            var result = _engine.ResolveCard(state, new PlayCardRequest
            {
                CardInstanceId = cardId,
                TargetEntityId = ally.EntityId,
                TargetCell = ally.Position,
                HasTargetCell = true,
                HasTargetEntity = true,
            });

            Assert.True(result.Success);
            Assert.False(state.Hand.Contains(cardId));
            Assert.True(state.DiscardPile.Contains(cardId));
            Assert.AreEqual(CardZone.DiscardPile, state.GetCardInstance(cardId).CurrentZone);
            Assert.True(result.Events.Any(evt => evt.EventType == BattleEventType.PlaySpellToDiscard));
        }

        [Test]
        public void UnitCardsBindToSummonedEntityInsteadOfEnteringDiscard()
        {
            var scenario = CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 5, 1));
            scenario.StartingDeck = new[] { "call_guardian" };
            scenario.CardsPerTurn = 1;

            var state = _engine.CreateBattle(scenario);
            var cardId = GetHandCardId(state, "call_guardian");
            var summonCell = _engine.GetValidCardTargetCells(state, cardId).First(cell => cell != state.Entities[state.CommanderEntityId].Position);

            var result = _engine.ResolveCard(state, new PlayCardRequest
            {
                CardInstanceId = cardId,
                TargetCell = summonCell,
                HasTargetCell = true,
            });

            var summoned = state.LivingEntities(TeamId.Player)
                .Where(entity => entity.TemplateId == "guardian" && entity.EntityId != state.CommanderEntityId)
                .Single();

            Assert.True(result.Success);
            Assert.False(state.Hand.Contains(cardId));
            Assert.False(state.DiscardPile.Contains(cardId));
            Assert.True(state.InBattleUnitCards.TryGetValue(summoned.EntityId, out var boundCardId));
            Assert.AreEqual(cardId, boundCardId);
            Assert.AreEqual(CardZone.InBattleUnit, state.GetCardInstance(cardId).CurrentZone);
            Assert.AreEqual(summoned.EntityId, state.GetCardInstance(cardId).BoundEntityId);
            Assert.True(result.Events.Any(evt => evt.EventType == BattleEventType.PlayUnitToBattle));
        }

        [Test]
        public void BoundUnitCardReturnsToDiscardWhenSummonedUnitDies()
        {
            var scenario = CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 3, 2));
            scenario.StartingDeck = new[] { "call_guardian" };
            scenario.CardsPerTurn = 1;

            var state = _engine.CreateBattle(scenario);
            var cardId = GetHandCardId(state, "call_guardian");
            var hunter = state.LivingEntities(TeamId.Enemy).Single();
            var summonCell = _engine.GetValidCardTargetCells(state, cardId)
                .First(cell => GridMath.Chebyshev(cell, hunter.Position) == 1);

            var playResult = _engine.ResolveCard(state, new PlayCardRequest
            {
                CardInstanceId = cardId,
                TargetCell = summonCell,
                HasTargetCell = true,
            });

            var summoned = state.LivingEntities(TeamId.Player)
                .First(entity => entity.TemplateId == "guardian" && entity.EntityId != state.CommanderEntityId);
            state.Entities[summoned.EntityId].CurrentHp = 1;
            state.CardsPerTurn = 0;

            var endTurn = _engine.EndTurn(state);

            Assert.True(playResult.Success);
            Assert.False(state.Entities[summoned.EntityId].IsAlive);
            Assert.False(state.InBattleUnitCards.ContainsKey(summoned.EntityId));
            Assert.True(state.DiscardPile.Contains(cardId));
            Assert.AreEqual(CardZone.DiscardPile, state.GetCardInstance(cardId).CurrentZone);
            Assert.AreEqual(-1, state.GetCardInstance(cardId).BoundEntityId);
            Assert.AreEqual(1, endTurn.Events.Count(evt => evt.EventType == BattleEventType.UnitCardReturnedToDiscard));
        }

        [Test]
        public void ScenarioSpawnDeathDoesNotReturnAnyCardToDiscard()
        {
            var state = _engine.CreateBattle(CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("guardian", TeamId.Player, 1, 2),
                ("hunter", TeamId.Enemy, 2, 2)));

            var guardian = state.LivingEntities(TeamId.Player).First(entity => entity.TemplateId == "guardian");
            state.Entities[guardian.EntityId].CurrentHp = 1;
            state.CardsPerTurn = 0;

            var endTurn = _engine.EndTurn(state);

            Assert.False(state.Entities[guardian.EntityId].IsAlive);
            Assert.AreEqual(0, state.InBattleUnitCards.Count);
            Assert.False(endTurn.Events.Any(evt => evt.EventType == BattleEventType.UnitCardReturnedToDiscard));
        }

        [Test]
        public void CardPreviewSimulatesZoneChangesWithoutMutatingRealState()
        {
            var scenario = CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 5, 1));
            scenario.StartingDeck = new[] { "call_guardian" };
            scenario.CardsPerTurn = 1;

            var state = _engine.CreateBattle(scenario);
            var cardId = GetHandCardId(state, "call_guardian");
            var summonCell = _engine.GetValidCardTargetCells(state, cardId).First(cell => cell != state.Entities[state.CommanderEntityId].Position);

            var preview = _engine.Preview(state, new PreviewRequest
            {
                SourceKind = PreviewSourceKind.Card,
                CardInstanceId = cardId,
                TargetCell = summonCell,
                HasTargetCell = true,
            });

            Assert.True(preview.Valid);
            Assert.NotNull(preview.SimulatedResult);
            Assert.True(preview.SimulatedResult.State.InBattleUnitCards.Count == 1);
            Assert.AreEqual(CardZone.InBattleUnit, preview.SimulatedResult.State.CardInstances[cardId].CurrentZone);
            Assert.True(state.Hand.Contains(cardId));
            Assert.AreEqual(0, state.InBattleUnitCards.Count);
            Assert.AreEqual(0, state.DiscardPile.Count);
            Assert.AreEqual(CardZone.Hand, state.GetCardInstance(cardId).CurrentZone);
        }

        [Test]
        public void SnapshotIncludesCardPilesAndBoundUnitCards()
        {
            var scenario = CreateScenario(
                ("commander_core", TeamId.Player, 1, 1),
                ("hunter", TeamId.Enemy, 5, 1));
            scenario.StartingDeck = new[] { "call_guardian", "spell_fortify" };
            scenario.CardsPerTurn = 2;

            var state = _engine.CreateBattle(scenario);
            var cardId = GetHandCardId(state, "call_guardian");
            var summonCell = _engine.GetValidCardTargetCells(state, cardId).First(cell => cell != state.Entities[state.CommanderEntityId].Position);

            var playResult = _engine.ResolveCard(state, new PlayCardRequest
            {
                CardInstanceId = cardId,
                TargetCell = summonCell,
                HasTargetCell = true,
            });

            var snapshot = playResult.Snapshot;

            Assert.NotNull(snapshot);
            Assert.AreEqual(state.Hand.Count, snapshot.Hand.Count);
            Assert.AreEqual(state.DrawPile.Count, snapshot.DrawPile.Count);
            Assert.AreEqual(state.DiscardPile.Count, snapshot.DiscardPile.Count);
            Assert.AreEqual(1, snapshot.InBattleUnitCards.Count);
            Assert.AreEqual("call_guardian", snapshot.InBattleUnitCards[0].CardTemplateId);
        }

        private static int GetHandCardId(BattleState state, string templateId)
        {
            return state.GetHandCards().Single(card => card.TemplateId == templateId).CardInstanceId;
        }

        private static ScenarioDefinition CreateScenario(params (string templateId, TeamId team, int x, int y)[] spawns)
        {
            return new ScenarioDefinition
            {
                Id = "test",
                DisplayName = "Test",
                Width = 8,
                Height = 6,
                StartingMana = 3,
                MaxMana = 3,
                CardsPerTurn = 5,
                StartingDeck = new[]
                {
                    "call_guardian",
                    "call_vanguard",
                    "call_skirmisher",
                    "call_slinger",
                    "call_banner",
                    "deploy_barricade",
                    "spell_forced_march",
                    "spell_command_shock",
                    "spell_fortify",
                    "spell_breakthrough",
                },
                Spawns = spawns.Select(spawn => new ScenarioEntitySpawn
                {
                    TemplateId = spawn.templateId,
                    Team = spawn.team.ToString(),
                    X = spawn.x,
                    Y = spawn.y,
                }).ToArray(),
            };
        }
    }
}
