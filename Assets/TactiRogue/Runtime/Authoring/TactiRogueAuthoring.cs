using System;

namespace TactiRogue
{
    public enum TeamId
    {
        Player,
        Enemy,
        Neutral,
    }

    public enum EntityKind
    {
        Commander,
        Unit,
        Building,
    }

    public enum KeywordId
    {
        None,
        Taunt,
        Anchored,
        Breakthrough,
        Inspired,
    }

    public enum CardKind
    {
        Unit,
        Spell,
    }

    public enum CardZone
    {
        None,
        DrawPile,
        Hand,
        DiscardPile,
        InBattleUnit,
    }

    public enum ActionKind
    {
        Strike,
        PushStrike,
        Swap,
        GrantAction,
        ApplyStatus,
        Summon,
        AreaBlast,
        Charge,
    }

    public enum ActionTargetMode
    {
        None,
        Cell,
        Unit,
    }

    public enum ActionTargetFilter
    {
        None,
        Self,
        Ally,
        Enemy,
        AnyUnit,
        EmptyCell,
    }

    public enum MovementPattern
    {
        None,
        Walk,
        Dash,
        Teleport,
    }

    public enum MoveType
    {
        None,
        Walk,
        Jump,
        Float,
        DashLike,
    }

    public enum IntentKind
    {
        UnitLock,
        AreaLock,
        Directional,
    }

    public enum IntentTargetingMode
    {
        Legacy,
        CommanderPriorityUnit,
        NearestUnit,
        CommanderCell,
        TargetUnitCell,
        CommanderDirection,
        TargetUnitDirection,
    }

    public enum IntentRevalidationPolicy
    {
        Legacy,
        StrictLock,
        FixedArea,
        FixedDirection,
    }

    public enum IntentFallbackMode
    {
        Legacy,
        SkipTurn,
        Retarget,
    }

    public enum StatusTickPhase
    {
        None,
        OwnerTurnStart,
        OwnerTurnEnd,
    }

    [Serializable]
    public sealed class MoveProfile
    {
        public bool UseSeparateMovePhase = true;
        public int MoveRange = 2;
        public MoveType MoveType = MoveType.Walk;
        public bool AllowStayInPlace = true;
        public bool AllowDiagonalMove = true;
        public bool CanPassThroughUnits;
        public bool CanPassThroughBuildings;
        public bool RequirePath = true;

        public MoveProfile Clone()
        {
            return new MoveProfile
            {
                UseSeparateMovePhase = UseSeparateMovePhase,
                MoveRange = MoveRange,
                MoveType = MoveType,
                AllowStayInPlace = AllowStayInPlace,
                AllowDiagonalMove = AllowDiagonalMove,
                CanPassThroughUnits = CanPassThroughUnits,
                CanPassThroughBuildings = CanPassThroughBuildings,
                RequirePath = RequirePath,
            };
        }
    }
}
