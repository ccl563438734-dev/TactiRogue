using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    [CreateAssetMenu(fileName = "StatusTemplate", menuName = "TactiRogue/Status Template")]
    public sealed class StatusTemplate : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public KeywordId GrantedKeyword;
        public int AttackModifier;
        public int PushModifier;
        public int ActionsGrantedOnApply;
        public int DefaultDuration = 1;
        public StatusTickPhase TickPhase = StatusTickPhase.OwnerTurnEnd;
        public Color Tint = Color.white;
    }

    [CreateAssetMenu(fileName = "ActionDefinition", menuName = "TactiRogue/Action Definition")]
    public sealed class ActionDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public ActionKind ActionKind;
        public ActionTargetMode TargetMode;
        public ActionTargetFilter TargetFilter;
        public int MinRange;
        public int MaxRange = 1;
        public bool UseActorAttackValue = true;
        public int DamageAmount;
        public int HealAmount;
        public int PushForce;
        public int Radius;
        public int MoveRange;
        public MovementPattern MovePattern;
        public bool MoveBeforeEffect = true;
        public int ExtraActionsGranted;
        public string ApplyStatusId;
        public int OverrideStatusDuration = -1;
        public string SummonEntityId;
        public bool ConsumeActorAction = true;
        public bool AllowDiagonalTargeting = true;
        public bool SkipMovePhase;
        [HideInInspector] public int AuthoringVersion = 1;
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

    [CreateAssetMenu(fileName = "EntityTemplate", menuName = "TactiRogue/Entity Template")]
    public sealed class EntityTemplate : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public string ShortLabel;
        [TextArea] public string Description;
        public EntityKind EntityKind;
        public TeamId DefaultTeam;
        public int MaxHp = 1;
        public int Attack = 1;
        public int PushBonus;
        public string ActionId;
        public string IntentDefinitionId;
        public bool CanAct = true;
        public bool BlocksMovement = true;
        public bool OccupiesCell = true;
        public bool Targetable = true;
        public MoveProfile MoveProfile = new MoveProfile();
        public string[] StartingStatusIds = Array.Empty<string>();
        public Color Tint = Color.white;
        [HideInInspector] public int AuthoringVersion = 1;
    }

    [CreateAssetMenu(fileName = "CardTemplate", menuName = "TactiRogue/Card Template")]
    public sealed class CardTemplate : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public CardKind CardKind;
        public int Cost = 1;
        public string ActionId;
        public string SummonEntityId;
        public int SummonMinRange = 1;
        public int SummonMaxRange = 1;
        public Color Tint = Color.white;
    }

    [CreateAssetMenu(fileName = "IntentDefinition", menuName = "TactiRogue/Intent Definition")]
    public sealed class IntentDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public IntentKind IntentKind;
        public string ActionId;
        public int AcquireRange = 99;
        public bool PreferCommander = true;
        public IntentTargetingMode TargetingMode;
        public IntentRevalidationPolicy RevalidationPolicy;
        public IntentFallbackMode FallbackMode;
        public Color Tint = Color.white;
    }

    [CreateAssetMenu(fileName = "CardPieceVisualDefinition", menuName = "TactiRogue/Card Piece Visual Definition")]
    public sealed class CardPieceVisualDefinition : ScriptableObject
    {
        public string Id;
        public string ModelKey = "Assert/Model/sample";
        public string CardArtKey;
        public string BackArtKey = "Assert/Picture/卡背";
        public float IdleTiltAngle = 45f;
        public float DefaultScale = 1f;
        public float YOffset;
        public string FrameModelKey;
        public string FrameMaterialKey;
        public string IdleMotionKey;
        public string MoveMotionKey;
        public string AttackMotionKey;
        public string HitMotionKey;
        public string DeathMotionKey;
        public string SpawnMotionKey;
    }

    [CreateAssetMenu(fileName = "TactiRogueContentDatabase", menuName = "TactiRogue/Content Database")]
    public sealed class TactiRogueContentDatabase : ScriptableObject
    {
        public StatusTemplate[] StatusTemplates = Array.Empty<StatusTemplate>();
        public ActionDefinition[] ActionDefinitions = Array.Empty<ActionDefinition>();
        public EntityTemplate[] EntityTemplates = Array.Empty<EntityTemplate>();
        public CardTemplate[] CardTemplates = Array.Empty<CardTemplate>();
        public IntentDefinition[] IntentDefinitions = Array.Empty<IntentDefinition>();
        public CardPieceVisualDefinition[] CardPieceVisualDefinitions = Array.Empty<CardPieceVisualDefinition>();

        private Dictionary<string, StatusTemplate> _statusLookup;
        private Dictionary<string, ActionDefinition> _actionLookup;
        private Dictionary<string, EntityTemplate> _entityLookup;
        private Dictionary<string, CardTemplate> _cardLookup;
        private Dictionary<string, IntentDefinition> _intentLookup;
        private Dictionary<string, CardPieceVisualDefinition> _cardPieceVisualLookup;

        public IReadOnlyDictionary<string, StatusTemplate> StatusLookup => _statusLookup ??= BuildLookup(StatusTemplates, item => item.Id);
        public IReadOnlyDictionary<string, ActionDefinition> ActionLookup => _actionLookup ??= BuildLookup(ActionDefinitions, item => item.Id);
        public IReadOnlyDictionary<string, EntityTemplate> EntityLookup => _entityLookup ??= BuildLookup(EntityTemplates, item => item.Id);
        public IReadOnlyDictionary<string, CardTemplate> CardLookup => _cardLookup ??= BuildLookup(CardTemplates, item => item.Id);
        public IReadOnlyDictionary<string, IntentDefinition> IntentLookup => _intentLookup ??= BuildLookup(IntentDefinitions, item => item.Id);
        public IReadOnlyDictionary<string, CardPieceVisualDefinition> CardPieceVisualLookup => _cardPieceVisualLookup ??= BuildLookup(CardPieceVisualDefinitions, item => item.Id);

        public StatusTemplate GetStatus(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && StatusLookup.TryGetValue(id, out var result) ? result : null;
        }

        public ActionDefinition GetAction(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && ActionLookup.TryGetValue(id, out var result) ? result : null;
        }

        public EntityTemplate GetEntity(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && EntityLookup.TryGetValue(id, out var result) ? result : null;
        }

        public CardTemplate GetCard(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && CardLookup.TryGetValue(id, out var result) ? result : null;
        }

        public IntentDefinition GetIntent(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && IntentLookup.TryGetValue(id, out var result) ? result : null;
        }

        public CardPieceVisualDefinition GetCardPieceVisual(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && CardPieceVisualLookup.TryGetValue(id, out var result) ? result : null;
        }

        public string[] ValidateIds()
        {
            var issues = new List<string>();
            AddDuplicateErrors(StatusTemplates, item => item.Id, "status", issues);
            AddDuplicateErrors(ActionDefinitions, item => item.Id, "action", issues);
            AddDuplicateErrors(EntityTemplates, item => item.Id, "entity", issues);
            AddDuplicateErrors(CardTemplates, item => item.Id, "card", issues);
            AddDuplicateErrors(IntentDefinitions, item => item.Id, "intent", issues);
            AddDuplicateErrors(CardPieceVisualDefinitions, item => item.Id, "card piece visual", issues);
            return issues.ToArray();
        }

        private static Dictionary<string, T> BuildLookup<T>(IEnumerable<T> values, Func<T, string> selector) where T : ScriptableObject
        {
            return values
                .Where(value => value != null && !string.IsNullOrWhiteSpace(selector(value)))
                .GroupBy(selector)
                .ToDictionary(group => group.Key, group => group.First());
        }

        private static void AddDuplicateErrors<T>(IEnumerable<T> values, Func<T, string> selector, string label, ICollection<string> issues) where T : ScriptableObject
        {
            foreach (var duplicate in values
                .Where(value => value != null && !string.IsNullOrWhiteSpace(selector(value)))
                .GroupBy(selector)
                .Where(group => group.Count() > 1)
                .Select(group => $"{label} id duplicated: {group.Key}"))
            {
                issues.Add(duplicate);
            }
        }
    }
}
