using System;
using System.Collections.Generic;
using System.Linq;

namespace TactiRogue
{
    public enum BattlePhase
    {
        PlayerDrawPhase,
        PlayerAction,
        EnemyAction,
        Victory,
        Defeat,
    }

    public enum GridDirection
    {
        None,
        Up,
        Down,
        Left,
        Right,
        UpLeft,
        UpRight,
        DownLeft,
        DownRight,
    }

    [Serializable]
    public struct GridPosition : IEquatable<GridPosition>
    {
        public int X;
        public int Y;

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static GridPosition operator +(GridPosition left, GridPosition right)
        {
            return new GridPosition(left.X + right.X, left.Y + right.Y);
        }

        public static GridPosition operator -(GridPosition left, GridPosition right)
        {
            return new GridPosition(left.X - right.X, left.Y - right.Y);
        }

        public static bool operator ==(GridPosition left, GridPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GridPosition left, GridPosition right)
        {
            return !left.Equals(right);
        }

        public bool Equals(GridPosition other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString()
        {
            return $"{X},{Y}";
        }

        public static bool TryParse(string raw, out GridPosition position)
        {
            position = default;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var parts = raw.Split(',');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
            {
                return false;
            }

            position = new GridPosition(x, y);
            return true;
        }
    }

    public static class GridMath
    {
        private static readonly Dictionary<GridDirection, GridPosition> DirectionVectors = new Dictionary<GridDirection, GridPosition>
        {
            { GridDirection.Up, new GridPosition(0, 1) },
            { GridDirection.Down, new GridPosition(0, -1) },
            { GridDirection.Left, new GridPosition(-1, 0) },
            { GridDirection.Right, new GridPosition(1, 0) },
            { GridDirection.UpLeft, new GridPosition(-1, 1) },
            { GridDirection.UpRight, new GridPosition(1, 1) },
            { GridDirection.DownLeft, new GridPosition(-1, -1) },
            { GridDirection.DownRight, new GridPosition(1, -1) },
        };

        public static readonly GridDirection[] AllDirections =
        {
            GridDirection.Up,
            GridDirection.Down,
            GridDirection.Left,
            GridDirection.Right,
            GridDirection.UpLeft,
            GridDirection.UpRight,
            GridDirection.DownLeft,
            GridDirection.DownRight,
        };

        public static readonly GridDirection[] OrthogonalDirections =
        {
            GridDirection.Up,
            GridDirection.Down,
            GridDirection.Left,
            GridDirection.Right,
        };

        public static GridPosition DirectionToVector(GridDirection direction)
        {
            return DirectionVectors.TryGetValue(direction, out var vector) ? vector : default;
        }

        public static GridDirection VectorToDirection(GridPosition vector)
        {
            if (vector.X == 0 && vector.Y == 0)
            {
                return GridDirection.None;
            }

            var normalized = new GridPosition(Math.Sign(vector.X), Math.Sign(vector.Y));
            foreach (var pair in DirectionVectors)
            {
                if (pair.Value.Equals(normalized))
                {
                    return pair.Key;
                }
            }

            return GridDirection.None;
        }

        public static GridDirection DirectionBetween(GridPosition from, GridPosition to)
        {
            return VectorToDirection(to - from);
        }

        public static int Manhattan(GridPosition a, GridPosition b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }

        public static int Chebyshev(GridPosition a, GridPosition b)
        {
            return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        public static IEnumerable<GridPosition> GetNeighbors(GridPosition origin, bool includeDiagonals)
        {
            var directions = includeDiagonals ? AllDirections : OrthogonalDirections;
            foreach (var direction in directions)
            {
                yield return origin + DirectionToVector(direction);
            }
        }

        public static IEnumerable<GridPosition> EnumerateLine(GridPosition start, GridDirection direction, int length)
        {
            var step = DirectionToVector(direction);
            var current = start;
            for (var index = 0; index < length; index++)
            {
                current += step;
                yield return current;
            }
        }
    }

    [Serializable]
    public sealed class StatusInstance
    {
        public string TemplateId;
        public int RemainingTurns;

        public StatusInstance Clone()
        {
            return new StatusInstance
            {
                TemplateId = TemplateId,
                RemainingTurns = RemainingTurns,
            };
        }
    }

    [Serializable]
    public sealed class StatusContainer
    {
        public List<StatusInstance> ActiveStatuses = new List<StatusInstance>();

        public StatusContainer Clone()
        {
            var clone = new StatusContainer();
            clone.ActiveStatuses.AddRange(ActiveStatuses.Select(status => status.Clone()));
            return clone;
        }

        public void AddOrRefresh(string templateId, int duration)
        {
            var existing = ActiveStatuses.FirstOrDefault(status => status.TemplateId == templateId);
            if (existing != null)
            {
                existing.RemainingTurns = Math.Max(existing.RemainingTurns, duration);
                return;
            }

            ActiveStatuses.Add(new StatusInstance
            {
                TemplateId = templateId,
                RemainingTurns = duration,
            });
        }

        public void Remove(string templateId)
        {
            ActiveStatuses.RemoveAll(status => status.TemplateId == templateId);
        }

        public bool HasKeyword(TactiRogueContentDatabase catalog, KeywordId keyword)
        {
            return ResolveTemplates(catalog).Any(template => template.GrantedKeyword == keyword);
        }

        public int GetAttackModifier(TactiRogueContentDatabase catalog)
        {
            return ResolveTemplates(catalog).Sum(template => template.AttackModifier);
        }

        public int GetPushModifier(TactiRogueContentDatabase catalog)
        {
            return ResolveTemplates(catalog).Sum(template => template.PushModifier);
        }

        public int GetActionsGrantedOnApply(TactiRogueContentDatabase catalog, string statusId)
        {
            var template = catalog.GetStatus(statusId);
            return template == null ? 0 : template.ActionsGrantedOnApply;
        }

        public void Tick(TactiRogueContentDatabase catalog, StatusTickPhase tickPhase)
        {
            for (var index = ActiveStatuses.Count - 1; index >= 0; index--)
            {
                var template = catalog.GetStatus(ActiveStatuses[index].TemplateId);
                if (template == null || template.TickPhase != tickPhase)
                {
                    continue;
                }

                ActiveStatuses[index].RemainingTurns -= 1;
                if (ActiveStatuses[index].RemainingTurns <= 0)
                {
                    ActiveStatuses.RemoveAt(index);
                }
            }
        }

        public IEnumerable<StatusTemplate> ResolveTemplates(TactiRogueContentDatabase catalog)
        {
            foreach (var status in ActiveStatuses)
            {
                var template = catalog.GetStatus(status.TemplateId);
                if (template != null)
                {
                    yield return template;
                }
            }
        }

        public string ToDebugString()
        {
            return string.Join(", ", ActiveStatuses.Select(status => $"{status.TemplateId}:{status.RemainingTurns}"));
        }
    }

    [Serializable]
    public sealed class EntityInstance
    {
        public int EntityId;
        public string TemplateId;
        public TeamId Team;
        public EntityKind EntityKind;
        public GridPosition Position;
        public int CurrentHp;
        public int MaxHp;
        public int Attack;
        public int PushBonus;
        public bool IsAlive = true;
        public bool OccupiesCell = true;
        public bool BlocksMovement = true;
        public bool Targetable = true;
        public bool CanAct = true;
        public int RemainingActions;
        public string ActionId;
        public string IntentDefinitionId;
        public int SummonerEntityId = -1;
        public StatusContainer Statuses = new StatusContainer();

        public EntityInstance Clone()
        {
            return new EntityInstance
            {
                EntityId = EntityId,
                TemplateId = TemplateId,
                Team = Team,
                EntityKind = EntityKind,
                Position = Position,
                CurrentHp = CurrentHp,
                MaxHp = MaxHp,
                Attack = Attack,
                PushBonus = PushBonus,
                IsAlive = IsAlive,
                OccupiesCell = OccupiesCell,
                BlocksMovement = BlocksMovement,
                Targetable = Targetable,
                CanAct = CanAct,
                RemainingActions = RemainingActions,
                ActionId = ActionId,
                IntentDefinitionId = IntentDefinitionId,
                SummonerEntityId = SummonerEntityId,
                Statuses = Statuses.Clone(),
            };
        }

        public int GetEffectiveAttack(TactiRogueContentDatabase catalog)
        {
            return Math.Max(0, Attack + Statuses.GetAttackModifier(catalog));
        }

        public int GetEffectivePushBonus(TactiRogueContentDatabase catalog)
        {
            return PushBonus + Statuses.GetPushModifier(catalog);
        }

        public bool HasKeyword(TactiRogueContentDatabase catalog, KeywordId keyword, EntityTemplate template)
        {
            if (template != null && template.StartingStatusIds.Any(statusId => catalog.GetStatus(statusId)?.GrantedKeyword == keyword))
            {
                return true;
            }

            return Statuses.HasKeyword(catalog, keyword);
        }
    }

    [Serializable]
    public sealed class CardInstance
    {
        public int CardInstanceId;
        public string TemplateId;
        public CardZone CurrentZone;
        public int BoundEntityId = -1;

        public CardInstance Clone()
        {
            return new CardInstance
            {
                CardInstanceId = CardInstanceId,
                TemplateId = TemplateId,
                CurrentZone = CurrentZone,
                BoundEntityId = BoundEntityId,
            };
        }
    }

    [Serializable]
    public sealed class BoundUnitCardSnapshot
    {
        public int EntityId;
        public string EntityTemplateId;
        public int CardInstanceId;
        public string CardTemplateId;
    }

    [Serializable]
    public sealed class IntentState
    {
        public int ActorEntityId;
        public string IntentDefinitionId;
        public string ActionId;
        public IntentKind IntentKind;
        public IntentTargetingMode TargetingMode;
        public IntentRevalidationPolicy RevalidationPolicy;
        public IntentFallbackMode FallbackMode;
        public int TargetEntityId = -1;
        public GridPosition TargetCell;
        public GridDirection Direction;
        public bool IsCancelled;
        public string DebugReason;
        public string Summary;
        public List<GridPosition> DangerCells = new List<GridPosition>();

        public IntentState Clone()
        {
            return new IntentState
            {
                ActorEntityId = ActorEntityId,
                IntentDefinitionId = IntentDefinitionId,
                ActionId = ActionId,
                IntentKind = IntentKind,
                TargetingMode = TargetingMode,
                RevalidationPolicy = RevalidationPolicy,
                FallbackMode = FallbackMode,
                TargetEntityId = TargetEntityId,
                TargetCell = TargetCell,
                Direction = Direction,
                IsCancelled = IsCancelled,
                DebugReason = DebugReason,
                Summary = Summary,
                DangerCells = new List<GridPosition>(DangerCells),
            };
        }
    }

    [Serializable]
    public sealed class GridState
    {
        public int Width;
        public int Height;
        public HashSet<GridPosition> ValidCells = new HashSet<GridPosition>();
        public Dictionary<GridPosition, int> Occupancy = new Dictionary<GridPosition, int>();

        public GridState Clone()
        {
            return new GridState
            {
                Width = Width,
                Height = Height,
                ValidCells = new HashSet<GridPosition>(ValidCells),
                Occupancy = Occupancy.ToDictionary(pair => pair.Key, pair => pair.Value),
            };
        }

        public bool IsValid(GridPosition position)
        {
            return position.X >= 0
                   && position.Y >= 0
                   && position.X < Width
                   && position.Y < Height
                   && (ValidCells.Count == 0 || ValidCells.Contains(position));
        }

        public bool IsOccupied(GridPosition position)
        {
            return Occupancy.ContainsKey(position);
        }
    }

    [Serializable]
    public sealed class EntitySnapshot
    {
        public int EntityId;
        public string TemplateId;
        public string Team;
        public string Position;
        public int CurrentHp;
        public int RemainingActions;
        public string Statuses;
    }

    [Serializable]
    public sealed class BattleSnapshot
    {
        public string ScenarioId;
        public int TurnNumber;
        public string Phase;
        public int Mana;
        public List<string> Hand = new List<string>();
        public List<string> DrawPile = new List<string>();
        public List<string> DiscardPile = new List<string>();
        public List<BoundUnitCardSnapshot> InBattleUnitCards = new List<BoundUnitCardSnapshot>();
        public List<string> Intents = new List<string>();
        public List<IntentSnapshot> IntentDetails = new List<IntentSnapshot>();
        public List<EntitySnapshot> Entities = new List<EntitySnapshot>();
    }

    [Serializable]
    public sealed class IntentSnapshot
    {
        public int ActorEntityId;
        public string ActorTemplateId;
        public string IntentDefinitionId;
        public string ActionId;
        public string IntentKind;
        public string TargetingMode;
        public string RevalidationPolicy;
        public string FallbackMode;
        public int TargetEntityId;
        public string TargetEntityTemplateId;
        public string TargetCell;
        public string Direction;
        public bool IsCancelled;
        public string DebugReason;
        public string Summary;
        public List<string> DangerCells = new List<string>();
    }

    [Serializable]
    public sealed class BattleState
    {
        public string ScenarioId;
        public string ScenarioDisplayName;
        public GridState Grid = new GridState();
        public Dictionary<int, EntityInstance> Entities = new Dictionary<int, EntityInstance>();
        public Dictionary<int, CardInstance> CardInstances = new Dictionary<int, CardInstance>();
        public List<int> DrawPile = new List<int>();
        public List<int> DiscardPile = new List<int>();
        public List<int> Hand = new List<int>();
        public Dictionary<int, int> InBattleUnitCards = new Dictionary<int, int>();
        public List<IntentState> EnemyIntents = new List<IntentState>();
        public int CommanderEntityId = -1;
        public int NextEntityId = 1;
        public int NextCardId = 1;
        public int TurnNumber = 1;
        public BattlePhase Phase = BattlePhase.PlayerAction;
        public TeamId ActiveTeam = TeamId.Player;
        public int CurrentMana = 3;
        public int MaxMana = 3;
        public int CardsPerTurn = 5;
        public int RandomSeed = 12345;
        public bool PlayerWon;
        public bool PlayerLost;

        public BattleState Clone()
        {
            return new BattleState
            {
                ScenarioId = ScenarioId,
                ScenarioDisplayName = ScenarioDisplayName,
                Grid = Grid.Clone(),
                Entities = Entities.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()),
                CardInstances = CardInstances.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()),
                DrawPile = new List<int>(DrawPile),
                DiscardPile = new List<int>(DiscardPile),
                Hand = new List<int>(Hand),
                InBattleUnitCards = InBattleUnitCards.ToDictionary(pair => pair.Key, pair => pair.Value),
                EnemyIntents = EnemyIntents.Select(intent => intent.Clone()).ToList(),
                CommanderEntityId = CommanderEntityId,
                NextEntityId = NextEntityId,
                NextCardId = NextCardId,
                TurnNumber = TurnNumber,
                Phase = Phase,
                ActiveTeam = ActiveTeam,
                CurrentMana = CurrentMana,
                MaxMana = MaxMana,
                CardsPerTurn = CardsPerTurn,
                RandomSeed = RandomSeed,
                PlayerWon = PlayerWon,
                PlayerLost = PlayerLost,
            };
        }

        public IEnumerable<EntityInstance> LivingEntities()
        {
            return Entities.Values.Where(entity => entity.IsAlive);
        }

        public IEnumerable<EntityInstance> LivingEntities(TeamId team)
        {
            return LivingEntities().Where(entity => entity.Team == team);
        }

        public bool TryGetCardInstance(int cardInstanceId, out CardInstance cardInstance)
        {
            return CardInstances.TryGetValue(cardInstanceId, out cardInstance);
        }

        public CardInstance GetCardInstance(int cardInstanceId)
        {
            return CardInstances.TryGetValue(cardInstanceId, out var cardInstance) ? cardInstance : null;
        }

        public IEnumerable<CardInstance> ResolveCardIds(IEnumerable<int> cardInstanceIds)
        {
            foreach (var cardInstanceId in cardInstanceIds ?? Enumerable.Empty<int>())
            {
                if (CardInstances.TryGetValue(cardInstanceId, out var cardInstance))
                {
                    yield return cardInstance;
                }
            }
        }

        public IEnumerable<CardInstance> GetHandCards()
        {
            return ResolveCardIds(Hand);
        }

        public IEnumerable<CardInstance> GetDrawPileCards()
        {
            return ResolveCardIds(DrawPile);
        }

        public IEnumerable<CardInstance> GetDiscardPileCards()
        {
            return ResolveCardIds(DiscardPile);
        }

        public BattleSnapshot CaptureSnapshot(TactiRogueContentDatabase catalog)
        {
            var snapshot = new BattleSnapshot
            {
                ScenarioId = ScenarioId,
                TurnNumber = TurnNumber,
                Phase = Phase.ToString(),
                Mana = CurrentMana,
                Hand = GetHandCards().Select(card => card.TemplateId).ToList(),
                DrawPile = GetDrawPileCards().Select(card => card.TemplateId).ToList(),
                DiscardPile = GetDiscardPileCards().Select(card => card.TemplateId).ToList(),
                Intents = EnemyIntents.Select(intent => intent.Summary).Where(summary => !string.IsNullOrEmpty(summary)).ToList(),
            };

            foreach (var pair in InBattleUnitCards.OrderBy(item => item.Key))
            {
                if (!CardInstances.TryGetValue(pair.Value, out var cardInstance))
                {
                    continue;
                }

                Entities.TryGetValue(pair.Key, out var entity);
                snapshot.InBattleUnitCards.Add(new BoundUnitCardSnapshot
                {
                    EntityId = pair.Key,
                    EntityTemplateId = entity?.TemplateId ?? string.Empty,
                    CardInstanceId = pair.Value,
                    CardTemplateId = cardInstance.TemplateId,
                });
            }

            foreach (var intent in EnemyIntents)
            {
                Entities.TryGetValue(intent.ActorEntityId, out var actor);
                var targetTemplateId = intent.TargetEntityId >= 0 && Entities.TryGetValue(intent.TargetEntityId, out var targetEntity)
                    ? targetEntity.TemplateId
                    : string.Empty;

                snapshot.IntentDetails.Add(new IntentSnapshot
                {
                    ActorEntityId = intent.ActorEntityId,
                    ActorTemplateId = actor?.TemplateId ?? string.Empty,
                    IntentDefinitionId = intent.IntentDefinitionId,
                    ActionId = intent.ActionId,
                    IntentKind = intent.IntentKind.ToString(),
                    TargetingMode = intent.TargetingMode.ToString(),
                    RevalidationPolicy = intent.RevalidationPolicy.ToString(),
                    FallbackMode = intent.FallbackMode.ToString(),
                    TargetEntityId = intent.TargetEntityId,
                    TargetEntityTemplateId = targetTemplateId,
                    TargetCell = intent.TargetCell.ToString(),
                    Direction = intent.Direction.ToString(),
                    IsCancelled = intent.IsCancelled,
                    DebugReason = intent.DebugReason,
                    Summary = intent.Summary,
                    DangerCells = intent.DangerCells.Select(cell => cell.ToString()).ToList(),
                });
            }

            foreach (var entity in LivingEntities().OrderBy(entity => entity.Team).ThenBy(entity => entity.EntityId))
            {
                snapshot.Entities.Add(new EntitySnapshot
                {
                    EntityId = entity.EntityId,
                    TemplateId = entity.TemplateId,
                    Team = entity.Team.ToString(),
                    Position = entity.Position.ToString(),
                    CurrentHp = entity.CurrentHp,
                    RemainingActions = entity.RemainingActions,
                    Statuses = entity.Statuses.ToDebugString(),
                });
            }

            return snapshot;
        }
    }
}
