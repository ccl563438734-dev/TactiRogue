using System;
using System.Collections.Generic;
using UnityEngine;

namespace TactiRogue
{
    public enum BattleEventType
    {
        StateLoaded,
        DrawPhaseStarted,
        TurnStarted,
        TurnEnded,
        CardDrawn,
        CardPlayed,
        PlaySpellToDiscard,
        PlayUnitToBattle,
        ShuffleDiscardIntoDrawPile,
        UnitCardReturnedToDiscard,
        DrawPileClicked,
        DiscardPileClicked,
        EntitySummoned,
        UnitMoved,
        ActionUsed,
        CollisionOccurred,
        DamageApplied,
        HealingApplied,
        StatusApplied,
        StatusExpired,
        IntentGenerated,
        IntentRevalidated,
        IntentCancelled,
        IntentResolved,
        EnemyActionExecuted,
        UnitDied,
        Victory,
        Defeat,
        Info,
    }

    public enum CellPreviewKind
    {
        None,
        ValidTarget,
        MovePath,
        Impact,
        Collision,
        Danger,
        Selection,
    }

    public enum PreviewSourceKind
    {
        UnitAction,
        Card,
    }

    public enum UnitTurnStage
    {
        None,
        MoveTargeting,
        BehaviorTargeting,
    }

    [Serializable]
    public sealed class BattleEvent
    {
        public BattleEventType EventType;
        public string Message;
        public int SubjectEntityId = -1;
        public int SecondaryEntityId = -1;
        public GridPosition Position;
        public int Amount;
    }

    [Serializable]
    public struct ActionRequest
    {
        public int ActorEntityId;
        public int TargetEntityId;
        public GridPosition TargetCell;
        public bool HasTargetCell;
        public bool HasTargetEntity;
    }

    [Serializable]
    public struct UnitTurnRequest
    {
        public int ActorEntityId;
        public GridPosition MoveTargetCell;
        public bool HasMoveTargetCell;
        public int BehaviorTargetEntityId;
        public GridPosition BehaviorTargetCell;
        public bool HasBehaviorTargetCell;
        public bool HasBehaviorTargetEntity;
    }

    [Serializable]
    public struct PlayCardRequest
    {
        public int CardInstanceId;
        public int TargetEntityId;
        public GridPosition TargetCell;
        public bool HasTargetCell;
        public bool HasTargetEntity;
    }

    [Serializable]
    public struct EndTurnRequest
    {
    }

    [Serializable]
    public struct PreviewRequest
    {
        public PreviewSourceKind SourceKind;
        public UnitTurnStage Stage;
        public int ActorEntityId;
        public int CardInstanceId;
        public int TargetEntityId;
        public GridPosition TargetCell;
        public GridPosition CommittedMoveCell;
        public bool HasTargetCell;
        public bool HasTargetEntity;
        public bool HasCommittedMoveCell;
    }

    [Serializable]
    public sealed class CellPreview
    {
        public GridPosition Position;
        public CellPreviewKind Kind;
        public int Value;
    }

    [Serializable]
    public sealed class IntentViewData
    {
        public int ActorEntityId;
        public int TargetEntityId = -1;
        public string IntentDefinitionId;
        public string ActionId;
        public string Summary;
        public string TargetingMode;
        public string RevalidationPolicy;
        public string FallbackMode;
        public bool IsCancelled;
        public string DebugReason;
        public GridDirection Direction;
        public GridPosition TargetCell;
        public Color Tint = Color.white;
        public List<GridPosition> DangerCells = new List<GridPosition>();
    }

    [Serializable]
    public sealed class CardPileViewEntry
    {
        public int CardInstanceId;
        public string TemplateId;
        public string DisplayName;
        public int Cost;
        public string CardKind;
        public CardZone Zone;
        public int BoundEntityId = -1;
    }

    [Serializable]
    public sealed class ActionResult
    {
        public bool Success;
        public string FailureReason;
        public BattleState State;
        public List<BattleEvent> Events = new List<BattleEvent>();
        public BattleSnapshot Snapshot;
    }

    [Serializable]
    public sealed class PreviewResult
    {
        public bool Valid;
        public string FailureReason;
        public List<GridPosition> ValidTargetCells = new List<GridPosition>();
        public List<CellPreview> CellPreviews = new List<CellPreview>();
        public List<IntentViewData> EnemyIntents = new List<IntentViewData>();
        public ActionResult SimulatedResult;
    }
}
