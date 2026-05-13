using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TactiRogue
{
    public static class TactiRogueContentProvider
    {
        private const string DatabaseResourcePath = "TactiRogue/Content/TactiRogueContentDatabase";

        private static TactiRogueContentDatabase _cachedDatabase;

        public static void ResetCache()
        {
            _cachedDatabase = null;
        }

        public static TactiRogueContentDatabase LoadOrCreateDatabase()
        {
            if (_cachedDatabase != null)
            {
                return _cachedDatabase;
            }

            _cachedDatabase = Resources.Load<TactiRogueContentDatabase>(DatabaseResourcePath);
            if (_cachedDatabase == null)
            {
                _cachedDatabase = CreateBuiltInDatabase();
            }
            else
            {
                _cachedDatabase = NormalizeDatabase(_cachedDatabase);
            }

            return _cachedDatabase;
        }

        public static TactiRogueContentDatabase CreateBuiltInDatabase()
        {
            var database = ScriptableObject.CreateInstance<TactiRogueContentDatabase>();
            database.name = "BuiltInContentDatabase";

            database.StatusTemplates = new[]
            {
                CreateStatus("taunt", "嘲讽", "保护附近友军免受敌方指向性意图锁定。", KeywordId.Taunt, 0, 0, 0, 99, StatusTickPhase.None, Hex("4F9D69")),
                CreateStatus("anchored", "锚定", "无法被位移，并会作为稳定的碰撞点。", KeywordId.Anchored, 0, 0, 0, 99, StatusTickPhase.None, Hex("666666")),
                CreateStatus("breakthrough", "突破", "造成碰撞暴击后，获得 1 个额外行动。", KeywordId.Breakthrough, 0, 0, 0, 2, StatusTickPhase.OwnerTurnEnd, Hex("C85E4A")),
                CreateStatus("inspired", "鼓舞", "攻击力 +1，持续到回合结束。", KeywordId.Inspired, 1, 0, 0, 1, StatusTickPhase.OwnerTurnEnd, Hex("D6B14C")),
            };

            database.ActionDefinitions = new[]
            {
                CreateAction("guardian_bash", "守卫猛击", "近战攻击，并将目标击退 1 格。", ActionKind.PushStrike, ActionTargetMode.Unit, ActionTargetFilter.Enemy, 1, 1, true, 0, 0, 1, 0, 0, MovementPattern.None),
                CreateAction("vanguard_ram", "先锋冲撞", "高推力近战打击，擅长制造碰撞。", ActionKind.PushStrike, ActionTargetMode.Unit, ActionTargetFilter.Enemy, 1, 1, true, 0, 0, 2, 0, 0, MovementPattern.None),
                CreateAction("skirmisher_swap", "战术换位", "与另一名单位交换位置。", ActionKind.Swap, ActionTargetMode.Unit, ActionTargetFilter.AnyUnit, 1, 2, false, 0, 0, 0, 0, 0, MovementPattern.None),
                CreateAction("slinger_shot", "震击投石", "远程攻击，并将目标击退 1 格。", ActionKind.PushStrike, ActionTargetMode.Unit, ActionTargetFilter.Enemy, 1, 3, true, 0, 0, 1, 0, 0, MovementPattern.None),
                CreateAction("banner_rally", "战旗号令", "令一名友军获得 1 个额外行动并施加鼓舞。", ActionKind.GrantAction, ActionTargetMode.Unit, ActionTargetFilter.Ally, 1, 2, false, 0, 0, 0, 0, 0, MovementPattern.None, "inspired", 1),
                CreateAction("hunter_slash", "追猎斩击", "先走入攻击范围，再发动打击并击退。", ActionKind.PushStrike, ActionTargetMode.Unit, ActionTargetFilter.Enemy, 1, 1, true, 0, 0, 1, 0, 1, MovementPattern.Walk),
                CreateAction("bombardier_bomb", "轰炸", "标记并伤害一小片区域。", ActionKind.AreaBlast, ActionTargetMode.Cell, ActionTargetFilter.None, 1, 4, false, 2, 0, 0, 1, 0, MovementPattern.None),
                CreateAction("charger_rush", "直线冲锋", "向前冲锋，并猛击命中的第一个目标。", ActionKind.Charge, ActionTargetMode.Cell, ActionTargetFilter.None, 1, 4, true, 0, 0, 2, 0, 3, MovementPattern.Dash),
                CreateAction("warden_bolt", "锚钉射击", "固定型远程击退射击。", ActionKind.PushStrike, ActionTargetMode.Unit, ActionTargetFilter.Enemy, 1, 3, true, 0, 0, 1, 0, 0, MovementPattern.None),
                CreateAction("command_shock", "指挥震击", "具有固定伤害的法术击退。", ActionKind.PushStrike, ActionTargetMode.Unit, ActionTargetFilter.Enemy, 1, 3, false, 2, 0, 2, 0, 0, MovementPattern.None),
                CreateAction("forced_march", "强行军", "令一名友军获得 1 个额外行动。", ActionKind.GrantAction, ActionTargetMode.Unit, ActionTargetFilter.Ally, 1, 3, false, 0, 0, 0, 0, 0, MovementPattern.None),
                CreateAction("fortify_order", "固守号令", "治疗 1 点生命，并施加嘲讽。", ActionKind.ApplyStatus, ActionTargetMode.Unit, ActionTargetFilter.Ally, 1, 3, false, 0, 1, 0, 0, 0, MovementPattern.None, "taunt", 2),
                CreateAction("breakthrough_order", "突破号令", "施加持续 2 回合的突破。", ActionKind.ApplyStatus, ActionTargetMode.Unit, ActionTargetFilter.Ally, 1, 3, false, 0, 0, 0, 0, 0, MovementPattern.None, "breakthrough", 2),
            };

            database.IntentDefinitions = new[]
            {
                CreateIntent("hunter_lock", "追猎", "锁定一名单位，并走近后发动打击。", IntentKind.UnitLock, "hunter_slash", true, IntentTargetingMode.CommanderPriorityUnit, IntentRevalidationPolicy.StrictLock, IntentFallbackMode.Retarget, Hex("B95555")),
                CreateIntent("bombard_lock", "轰炸", "为下个敌方回合标记一片冲击区域。", IntentKind.AreaLock, "bombardier_bomb", true, IntentTargetingMode.CommanderCell, IntentRevalidationPolicy.FixedArea, IntentFallbackMode.SkipTurn, Hex("CE8E42")),
                CreateIntent("charge_line", "冲锋", "沿固定方向突进。", IntentKind.Directional, "charger_rush", true, IntentTargetingMode.CommanderDirection, IntentRevalidationPolicy.FixedDirection, IntentFallbackMode.SkipTurn, Hex("D0B347")),
                CreateIntent("warden_lock", "弩击", "锁定一名单位，并发射远程击退攻击。", IntentKind.UnitLock, "warden_bolt", true, IntentTargetingMode.CommanderPriorityUnit, IntentRevalidationPolicy.StrictLock, IntentFallbackMode.Retarget, Hex("8D78C8")),
            };

            database.EntityTemplates = new[]
            {
                CreateEntity("commander_core", "指挥官", "指", "玩家核心，也是召唤起点。", EntityKind.Commander, TeamId.Player, 10, 1, 0, null, null, false, true, true, true, Hex("2C8AA3")),
                CreateEntity("guardian", "守卫", "守", "前线嘲讽护卫。", EntityKind.Unit, TeamId.Player, 5, 2, 0, "guardian_bash", null, true, true, true, true, Hex("5E9A74"), "taunt"),
                CreateEntity("vanguard", "先锋", "锋", "主要的碰撞输出单位。", EntityKind.Unit, TeamId.Player, 4, 2, 0, "vanguard_ram", null, true, true, true, true, Hex("C96B43"), "breakthrough"),
                CreateEntity("skirmisher", "散兵", "散", "通过换位来调整站位。", EntityKind.Unit, TeamId.Player, 3, 1, 0, "skirmisher_swap", null, true, true, true, true, Hex("4C7BC2")),
                CreateEntity("slinger", "投石手", "石", "远程骚扰并击退敌人。", EntityKind.Unit, TeamId.Player, 3, 1, 0, "slinger_shot", null, true, true, true, true, Hex("8D56BF")),
                CreateEntity("banner", "旗手", "旗", "提供额外行动并施加鼓舞。", EntityKind.Unit, TeamId.Player, 4, 1, 0, "banner_rally", null, true, true, true, true, Hex("CF9C42")),
                CreateEntity("hunter", "猎手", "猎", "追击单体目标的攻击手。", EntityKind.Unit, TeamId.Enemy, 4, 2, 0, "hunter_slash", "hunter_lock", true, true, true, true, Hex("A64F4F")),
                CreateEntity("bombardier", "轰炸兵", "轰", "制造区域压力的敌军。", EntityKind.Unit, TeamId.Enemy, 4, 2, 0, "bombardier_bomb", "bombard_lock", true, true, true, true, Hex("B07A30")),
                CreateEntity("charger", "冲锋兵", "冲", "沿直线冲锋的敌军。", EntityKind.Unit, TeamId.Enemy, 5, 2, 0, "charger_rush", "charge_line", true, true, true, true, Hex("A5902E")),
                CreateEntity("anchor_warden", "锚卫", "锚", "带有锚定的远程威胁。", EntityKind.Unit, TeamId.Enemy, 6, 2, 0, "warden_bolt", "warden_lock", true, true, true, true, Hex("775BAE"), "anchored"),
                CreateEntity("stone_pillar", "石柱", "柱", "稳定的碰撞物。", EntityKind.Building, TeamId.Neutral, 99, 0, 0, null, null, false, true, true, true, Hex("707070"), "anchored"),
                CreateEntity("barricade", "路障", "障", "可召唤的临时阻挡物。", EntityKind.Building, TeamId.Player, 4, 0, 0, null, null, false, true, true, true, Hex("836445"), "anchored"),
            };

            database.CardTemplates = new[]
            {
                CreateCard("call_guardian", "召唤守卫", "在指挥官 1 格范围内召唤一名守卫。", CardKind.Unit, 1, null, "guardian", 1, 1, Hex("5E9A74")),
                CreateCard("call_vanguard", "召唤先锋", "在 2 格范围内部署一名先锋。", CardKind.Unit, 2, null, "vanguard", 1, 2, Hex("C96B43")),
                CreateCard("call_skirmisher", "召唤散兵", "在 2 格范围内部署一名散兵。", CardKind.Unit, 1, null, "skirmisher", 1, 2, Hex("4C7BC2")),
                CreateCard("call_slinger", "召唤投石手", "在 2 格范围内部署一名投石手。", CardKind.Unit, 2, null, "slinger", 1, 2, Hex("8D56BF")),
                CreateCard("call_banner", "召唤旗手", "在 1 格范围内部署一名旗手。", CardKind.Unit, 1, null, "banner", 1, 1, Hex("CF9C42")),
                CreateCard("deploy_barricade", "部署路障", "在 2 格范围内放置一个路障。", CardKind.Unit, 1, null, "barricade", 1, 2, Hex("836445")),
                CreateCard("spell_forced_march", "强行军", "令一名友军获得 1 个额外行动。", CardKind.Spell, 1, "forced_march", null, 0, 0, Hex("4D9CC3")),
                CreateCard("spell_command_shock", "指挥震击", "以固定法术伤害击退一名敌人。", CardKind.Spell, 2, "command_shock", null, 0, 0, Hex("D05D45")),
                CreateCard("spell_fortify", "固守号令", "治疗一名友军并施加嘲讽。", CardKind.Spell, 1, "fortify_order", null, 0, 0, Hex("69906B")),
                CreateCard("spell_breakthrough", "突破号令", "对一名友军施加突破。", CardKind.Spell, 1, "breakthrough_order", null, 0, 0, Hex("B7654E")),
            };

            database.CardPieceVisualDefinitions = CreateDefaultCardPieceVisualDefinitions();

            return NormalizeDatabase(database);
        }

        private static TactiRogueContentDatabase NormalizeDatabase(TactiRogueContentDatabase database)
        {
            database.StatusTemplates ??= Array.Empty<StatusTemplate>();
            database.ActionDefinitions ??= Array.Empty<ActionDefinition>();
            database.EntityTemplates ??= Array.Empty<EntityTemplate>();
            database.CardTemplates ??= Array.Empty<CardTemplate>();
            database.IntentDefinitions ??= Array.Empty<IntentDefinition>();
            database.CardPieceVisualDefinitions ??= Array.Empty<CardPieceVisualDefinition>();

            EnsureCommanderAction(database);
            EnsureDefaultCardPieceVisuals(database);
            ApplyBoardVisualDefaults(database);
            ApplyCardPieceVisualDefaults(database);

            foreach (var action in database.ActionDefinitions.Where(item => item != null))
            {
                if (action.AuthoringVersion <= 0)
                {
                    ApplyLegacyActionDefaults(action);
                    action.AuthoringVersion = 1;
                }
            }

            foreach (var entity in database.EntityTemplates.Where(item => item != null))
            {
                if (entity.AuthoringVersion <= 0)
                {
                    ApplyLegacyEntityDefaults(entity);
                    entity.AuthoringVersion = 1;
                }
                else
                {
                    entity.MoveProfile ??= new MoveProfile();
                }
            }

            return database;
        }

        private static void EnsureDefaultCardPieceVisuals(TactiRogueContentDatabase database)
        {
            var defaults = CreateDefaultCardPieceVisualDefinitions();
            if (database.CardPieceVisualDefinitions.Length == 0)
            {
                database.CardPieceVisualDefinitions = defaults;
                return;
            }

            var existingIds = new HashSet<string>(
                database.CardPieceVisualDefinitions
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                    .Select(item => item.Id),
                StringComparer.Ordinal);
            var missingDefaults = defaults
                .Where(item => !existingIds.Contains(item.Id))
                .ToArray();
            if (missingDefaults.Length == 0)
            {
                return;
            }

            database.CardPieceVisualDefinitions = database.CardPieceVisualDefinitions
                .Concat(missingDefaults)
                .ToArray();
        }

        private static void ApplyCardPieceVisualDefaults(TactiRogueContentDatabase database)
        {
            foreach (var visual in database.CardPieceVisualDefinitions.Where(item => item != null))
            {
                if (string.IsNullOrWhiteSpace(visual.FrameModelKey))
                {
                    visual.FrameModelKey = GetDefaultFrameModelKey(visual.Id);
                }

                if (string.IsNullOrWhiteSpace(visual.BackArtKey))
                {
                    visual.BackArtKey = "Assert/Picture/鍗¤儗";
                }

                if (visual.DefaultScale <= 0f)
                {
                    visual.DefaultScale = 1f;
                }
            }
        }

        private static void ApplyBoardVisualDefaults(TactiRogueContentDatabase database)
        {
            if (database.BoardCellSize <= 0f)
            {
                database.BoardCellSize = TactiRogueContentDatabase.DefaultBoardCellSize;
            }

            if (database.BoardCellGap < 0f)
            {
                database.BoardCellGap = 0f;
            }

            var maxGap = Mathf.Max(0f, database.BoardCellSize - 0.01f);
            if (database.BoardCellGap > maxGap)
            {
                database.BoardCellGap = maxGap;
            }

            if (database.BoardCellHeight <= 0f)
            {
                database.BoardCellHeight = TactiRogueContentDatabase.DefaultBoardCellHeight;
            }
        }

        private static void EnsureCommanderAction(TactiRogueContentDatabase database)
        {
            if (database.ActionDefinitions.Any(action => action != null && action.Id == "commander_command"))
            {
                return;
            }

            var commanderAction = CreateAction(
                "commander_command",
                "指挥调度",
                "为范围内一名友军提供 1 个额外行动。",
                ActionKind.GrantAction,
                ActionTargetMode.Unit,
                ActionTargetFilter.Ally,
                1,
                2,
                false,
                0,
                0,
                0,
                0,
                0,
                MovementPattern.None);
            commanderAction.SkipMovePhase = false;
            database.ActionDefinitions = database.ActionDefinitions.Concat(new[] { commanderAction }).ToArray();
        }

        private static void ApplyLegacyActionDefaults(ActionDefinition action)
        {
            switch (action.Id)
            {
                case "guardian_bash":
                case "vanguard_ram":
                case "slinger_shot":
                case "banner_rally":
                case "bombardier_bomb":
                case "warden_bolt":
                case "commander_command":
                    action.SkipMovePhase = false;
                    break;
                case "skirmisher_swap":
                case "charger_rush":
                    action.SkipMovePhase = true;
                    break;
                case "hunter_slash":
                    action.SkipMovePhase = false;
                    action.MaxRange = 2;
                    action.MoveRange = 0;
                    action.MovePattern = MovementPattern.None;
                    break;
            }
        }

        private static void ApplyLegacyEntityDefaults(EntityTemplate entity)
        {
            var moveProfileLooksLegacy = entity.MoveProfile == null || IsDefaultMoveProfile(entity.MoveProfile);
            entity.MoveProfile ??= new MoveProfile();

            switch (entity.Id)
            {
                case "commander_core":
                    entity.CanAct = true;
                    if (string.IsNullOrWhiteSpace(entity.ActionId))
                    {
                        entity.ActionId = "commander_command";
                    }

                    if (moveProfileLooksLegacy)
                    {
                        entity.MoveProfile = CreateMoveProfile(true, 2, MoveType.Walk, true, true, false, false, true);
                    }
                    break;
                case "guardian":
                case "vanguard":
                case "slinger":
                case "banner":
                    if (moveProfileLooksLegacy)
                    {
                        entity.MoveProfile = CreateMoveProfile(true, 2, MoveType.Walk, true, true, false, false, true);
                    }

                    break;
                case "hunter":
                    if (moveProfileLooksLegacy)
                    {
                        entity.MoveProfile = CreateMoveProfile(true, 2, MoveType.Walk, true, false, false, false, true);
                    }

                    break;
                case "bombardier":
                    if (moveProfileLooksLegacy)
                    {
                        entity.MoveProfile = CreateMoveProfile(true, 1, MoveType.Walk, true, true, false, false, true);
                    }

                    break;
                case "anchor_warden":
                    if (moveProfileLooksLegacy)
                    {
                        entity.MoveProfile = CreateMoveProfile(true, 0, MoveType.Walk, true, true, false, false, true);
                    }

                    break;
                case "skirmisher":
                case "charger":
                    if (moveProfileLooksLegacy)
                    {
                        entity.MoveProfile = CreateMoveProfile(false, 0, MoveType.None, true, true, false, false, false);
                    }

                    break;
                default:
                    if (!entity.CanAct && moveProfileLooksLegacy)
                    {
                        entity.MoveProfile = CreateMoveProfile(false, 0, MoveType.None, true, true, false, false, false);
                    }

                    break;
            }
        }

        private static StatusTemplate CreateStatus(
            string id,
            string displayName,
            string description,
            KeywordId keyword,
            int attackModifier,
            int pushModifier,
            int actionsOnApply,
            int duration,
            StatusTickPhase tickPhase,
            Color tint)
        {
            var status = ScriptableObject.CreateInstance<StatusTemplate>();
            status.name = id;
            status.Id = id;
            status.DisplayName = displayName;
            status.Description = description;
            status.GrantedKeyword = keyword;
            status.AttackModifier = attackModifier;
            status.PushModifier = pushModifier;
            status.ActionsGrantedOnApply = actionsOnApply;
            status.DefaultDuration = duration;
            status.TickPhase = tickPhase;
            status.Tint = tint;
            return status;
        }

        private static ActionDefinition CreateAction(
            string id,
            string displayName,
            string description,
            ActionKind actionKind,
            ActionTargetMode targetMode,
            ActionTargetFilter targetFilter,
            int minRange,
            int maxRange,
            bool useActorAttack,
            int damageAmount,
            int healAmount,
            int pushForce,
            int radius,
            int moveRange,
            MovementPattern movementPattern,
            string applyStatusId = null,
            int overrideStatusDuration = -1)
        {
            var action = ScriptableObject.CreateInstance<ActionDefinition>();
            action.name = id;
            action.Id = id;
            action.DisplayName = displayName;
            action.Description = description;
            action.ActionKind = actionKind;
            action.TargetMode = targetMode;
            action.TargetFilter = targetFilter;
            action.MinRange = minRange;
            action.MaxRange = maxRange;
            action.UseActorAttackValue = useActorAttack;
            action.DamageAmount = damageAmount;
            action.HealAmount = healAmount;
            action.PushForce = pushForce;
            action.Radius = radius;
            action.MoveRange = moveRange;
            action.MovePattern = movementPattern;
            action.MoveBeforeEffect = true;
            action.ExtraActionsGranted = actionKind == ActionKind.GrantAction ? 1 : 0;
            action.ApplyStatusId = applyStatusId;
            action.OverrideStatusDuration = overrideStatusDuration;
            action.ConsumeActorAction = actionKind != ActionKind.Summon;
            action.AuthoringVersion = 0;
            return action;
        }

        private static IntentDefinition CreateIntent(
            string id,
            string displayName,
            string description,
            IntentKind intentKind,
            string actionId,
            bool preferCommander,
            IntentTargetingMode targetingMode,
            IntentRevalidationPolicy revalidationPolicy,
            IntentFallbackMode fallbackMode,
            Color tint)
        {
            var intent = ScriptableObject.CreateInstance<IntentDefinition>();
            intent.name = id;
            intent.Id = id;
            intent.DisplayName = displayName;
            intent.Description = description;
            intent.IntentKind = intentKind;
            intent.ActionId = actionId;
            intent.PreferCommander = preferCommander;
            intent.TargetingMode = targetingMode;
            intent.RevalidationPolicy = revalidationPolicy;
            intent.FallbackMode = fallbackMode;
            intent.Tint = tint;
            return intent;
        }

        private static EntityTemplate CreateEntity(
            string id,
            string displayName,
            string shortLabel,
            string description,
            EntityKind entityKind,
            TeamId defaultTeam,
            int maxHp,
            int attack,
            int pushBonus,
            string actionId,
            string intentDefinitionId,
            bool canAct,
            bool blocksMovement,
            bool occupiesCell,
            bool targetable,
            Color tint,
            params string[] startingStatusIds)
        {
            var entity = ScriptableObject.CreateInstance<EntityTemplate>();
            entity.name = id;
            entity.Id = id;
            entity.DisplayName = displayName;
            entity.ShortLabel = shortLabel;
            entity.Description = description;
            entity.EntityKind = entityKind;
            entity.DefaultTeam = defaultTeam;
            entity.MaxHp = maxHp;
            entity.Attack = attack;
            entity.PushBonus = pushBonus;
            entity.ActionId = actionId;
            entity.IntentDefinitionId = intentDefinitionId;
            entity.CanAct = canAct;
            entity.BlocksMovement = blocksMovement;
            entity.OccupiesCell = occupiesCell;
            entity.Targetable = targetable;
            entity.StartingStatusIds = startingStatusIds ?? Array.Empty<string>();
            entity.Tint = tint;
            entity.AuthoringVersion = 0;
            return entity;
        }

        private static bool IsDefaultMoveProfile(MoveProfile profile)
        {
            return profile != null
                && profile.UseSeparateMovePhase
                && profile.MoveRange == 2
                && profile.MoveType == MoveType.Walk
                && profile.AllowStayInPlace
                && profile.AllowDiagonalMove
                && !profile.CanPassThroughUnits
                && !profile.CanPassThroughBuildings
                && profile.RequirePath;
        }

        private static CardTemplate CreateCard(
            string id,
            string displayName,
            string description,
            CardKind cardKind,
            int cost,
            string actionId,
            string summonEntityId,
            int summonMinRange,
            int summonMaxRange,
            Color tint)
        {
            var card = ScriptableObject.CreateInstance<CardTemplate>();
            card.name = id;
            card.Id = id;
            card.DisplayName = displayName;
            card.Description = description;
            card.CardKind = cardKind;
            card.Cost = cost;
            card.ActionId = actionId;
            card.SummonEntityId = summonEntityId;
            card.SummonMinRange = summonMinRange;
            card.SummonMaxRange = summonMaxRange;
            card.Tint = tint;
            return card;
        }

        private static CardPieceVisualDefinition[] CreateDefaultCardPieceVisualDefinitions()
        {
            return new[]
            {
                CreateCardPieceVisual("commander_core", "Assert/Picture/指挥官"),
                CreateCardPieceVisual("guardian", "Assert/Picture/守卫"),
                CreateCardPieceVisual("vanguard", "Assert/Picture/先锋"),
                CreateCardPieceVisual("skirmisher", "Assert/Picture/散兵"),
                CreateCardPieceVisual("slinger", "Assert/Picture/投石手"),
                CreateCardPieceVisual("banner", "Assert/Picture/旗手"),
                CreateCardPieceVisual("hunter", "Assert/Picture/猎手"),
                CreateCardPieceVisual("bombardier", "Assert/Picture/轰炸兵"),
                CreateCardPieceVisual("charger", "Assert/Picture/冲锋兵"),
                CreateCardPieceVisual("anchor_warden", "Assert/Picture/锚卫"),
                CreateCardPieceVisual("stone_pillar", "Assert/Picture/石柱"),
                CreateCardPieceVisual("barricade", "Assert/Picture/路障"),
            };
        }

        private static CardPieceVisualDefinition CreateCardPieceVisual(string id, string cardArtKey)
        {
            var visual = ScriptableObject.CreateInstance<CardPieceVisualDefinition>();
            visual.name = $"CardPieceVisual_{id}";
            visual.Id = id;
            visual.ModelKey = "Assert/Model/sample";
            visual.CardArtKey = cardArtKey;
            visual.BackArtKey = "Assert/Picture/卡背";
            visual.IdleTiltAngle = 45f;
            visual.DefaultRotationEuler = UnitPresentationView.DefaultRotationFromIdleTilt(visual.IdleTiltAngle);
            visual.DefaultScale = 1f;
            visual.YOffset = 0.05f;
            visual.FrameModelKey = GetDefaultFrameModelKey(id);
            return visual;
        }

        private static string GetDefaultFrameModelKey(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "Assert/Model/F_Base";
            }

            var normalizedId = id.ToLowerInvariant();
            if (normalizedId.Contains("structure") ||
                normalizedId.Contains("building") ||
                normalizedId.Contains("obstacle") ||
                normalizedId.Contains("barricade") ||
                normalizedId.Contains("pillar") ||
                normalizedId.Contains("wall"))
            {
                return "Assert/Model/F_Structure";
            }

            return "Assert/Model/F_Unit";
        }

        private static MoveProfile CreateMoveProfile(
            bool useSeparateMovePhase,
            int moveRange,
            MoveType moveType,
            bool allowStayInPlace,
            bool allowDiagonalMove,
            bool canPassThroughUnits,
            bool canPassThroughBuildings,
            bool requirePath)
        {
            return new MoveProfile
            {
                UseSeparateMovePhase = useSeparateMovePhase,
                MoveRange = moveRange,
                MoveType = moveType,
                AllowStayInPlace = allowStayInPlace,
                AllowDiagonalMove = allowDiagonalMove,
                CanPassThroughUnits = canPassThroughUnits,
                CanPassThroughBuildings = canPassThroughBuildings,
                RequirePath = requirePath,
            };
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString($"#{hex}", out var color) ? color : Color.white;
        }
    }
}
