using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TactiRogue
{
    internal static class TactiRogueExcelPaths
    {
        public const string WorkbookAssetPath = "Assets/TactiRogue/DataAuthoring/TactiRogueData.xlsx";
        public const string ContentRoot = "Assets/Resources/TactiRogue/Content";
        public const string GeneratedRoot = "Assets/Resources/TactiRogue/Content/Generated";
        public const string DatabaseAssetPath = "Assets/Resources/TactiRogue/Content/TactiRogueContentDatabase.asset";
        public const string ScenarioRoot = "Assets/Resources/TactiRogue/Scenarios";

        public static string ToAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        public static string ToProjectRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var absolutePath = ToAbsolutePath(path);
            var projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!absolutePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return absolutePath.Replace("\\", "/");
            }

            var relativePath = absolutePath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relativePath.Replace("\\", "/");
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var normalized = path.Replace("\\", "/");
            var separatorIndex = normalized.LastIndexOf('/');
            var parent = separatorIndex > 0 ? normalized.Substring(0, separatorIndex) : string.Empty;
            var folderName = separatorIndex >= 0 ? normalized.Substring(separatorIndex + 1) : normalized;
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!string.IsNullOrEmpty(parent))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }

    public sealed class TactiRogueExcelValidationReport
    {
        public TactiRogueExcelValidationReport(string workbookPath)
        {
            WorkbookPath = workbookPath ?? string.Empty;
        }

        public string WorkbookPath { get; }
        public List<string> Errors { get; } = new List<string>();
        public bool IsValid => Errors.Count == 0;

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Errors.Add(message);
            }
        }

        public string ToDisplayString(int maxErrors = 16)
        {
            if (IsValid)
            {
                return $"Workbook valid: {WorkbookPath}";
            }

            var lines = Errors.Take(maxErrors).ToList();
            if (Errors.Count > maxErrors)
            {
                lines.Add($"... and {Errors.Count - maxErrors} more.");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    public sealed class TactiRogueExcelWorkbookData
    {
        public List<BoardVisualRow> BoardVisualRows { get; } = new List<BoardVisualRow>();
        public List<StatusRow> StatusRows { get; } = new List<StatusRow>();
        public List<ActionRow> ActionRows { get; } = new List<ActionRow>();
        public List<MoveProfileRow> MoveProfileRows { get; } = new List<MoveProfileRow>();
        public List<EntityRow> EntityRows { get; } = new List<EntityRow>();
        public List<EntityStatusRow> EntityStatusRows { get; } = new List<EntityStatusRow>();
        public List<IntentRow> IntentRows { get; } = new List<IntentRow>();
        public List<CardRow> CardRows { get; } = new List<CardRow>();
        public List<CardPieceVisualRow> CardPieceVisualRows { get; } = new List<CardPieceVisualRow>();
        public List<ScenarioRow> ScenarioRows { get; } = new List<ScenarioRow>();
        public List<ScenarioSpawnRow> ScenarioSpawnRows { get; } = new List<ScenarioSpawnRow>();
        public List<ScenarioDeckRow> ScenarioDeckRows { get; } = new List<ScenarioDeckRow>();
        public List<ScenarioVoidCellRow> ScenarioVoidCellRows { get; } = new List<ScenarioVoidCellRow>();

        public TactiRogueExcelWorkbook ToWorkbook()
        {
            var workbook = new TactiRogueExcelWorkbook();
            workbook.Sheets.Add(CreateSheet("BoardVisual", BoardVisualRow.Headers, BoardVisualRows.Select(row => row.ToCells())));
            workbook.Sheets.Add(CreateSheet("Status", StatusRow.Headers, StatusRows.Select(row => row.ToCells())));
            workbook.Sheets.Add(CreateSheet("Action", ActionRow.Headers, ActionRows.Select(row => row.ToCells())));
            workbook.Sheets.Add(CreateSheet("MoveProfile", MoveProfileRow.Headers, MoveProfileRows.Select(row => row.ToCells())));
            workbook.Sheets.Add(CreateSheet("Entity", EntityRow.Headers, EntityRows.Select(row => row.ToCells())));
            workbook.Sheets.Add(CreateSheet("EntityStatus", EntityStatusRow.Headers, EntityStatusRows.Select(row => row.ToCells())));
            workbook.Sheets.Add(CreateSheet("Intent", IntentRow.Headers, IntentRows.Select(row => row.ToCells())));
            workbook.Sheets.Add(CreateSheet("Card", CardRow.Headers, CardRows.Select(row => row.ToCells())));
            workbook.Sheets.Add(CreateSheet("CardPieceVisual", CardPieceVisualRow.Headers, CardPieceVisualRows.Select(row => row.ToCells())));
            workbook.Sheets.Add(CreateSheet("Scenario", ScenarioRow.Headers, ScenarioRows.Select(row => row.ToCells())));
            workbook.Sheets.Add(CreateSheet("ScenarioSpawn", ScenarioSpawnRow.Headers, ScenarioSpawnRows.Select(row => row.ToCells())));
            workbook.Sheets.Add(CreateSheet("ScenarioDeck", ScenarioDeckRow.Headers, ScenarioDeckRows.Select(row => row.ToCells())));
            workbook.Sheets.Add(CreateSheet("ScenarioVoidCell", ScenarioVoidCellRow.Headers, ScenarioVoidCellRows.Select(row => row.ToCells())));
            return workbook;
        }

        private static TactiRogueExcelSheet CreateSheet(string sheetName, IReadOnlyList<string> headers, IEnumerable<string[]> rows)
        {
            var sheet = new TactiRogueExcelSheet(sheetName);
            sheet.AddRow(headers);
            foreach (var row in rows)
            {
                sheet.AddRow(row);
            }

            return sheet;
        }
    }

    public sealed class TactiRogueExcelImportBundle
    {
        public TactiRogueContentDatabase Database;
        public List<ScenarioDefinition> Scenarios = new List<ScenarioDefinition>();
    }

    public sealed class BoardVisualRow
    {
        public const string DefaultId = "default";

        public static readonly string[] Headers =
        {
            "Id", "CellSize", "CellGap", "CellHeight",
        };

        public string Id;
        public float CellSize;
        public float CellGap;
        public float CellHeight;

        public static BoardVisualRow CreateDefault()
        {
            return new BoardVisualRow
            {
                Id = DefaultId,
                CellSize = TactiRogueContentDatabase.DefaultBoardCellSize,
                CellGap = TactiRogueContentDatabase.DefaultBoardCellGap,
                CellHeight = TactiRogueContentDatabase.DefaultBoardCellHeight,
            };
        }

        public string[] ToCells()
        {
            return new[]
            {
                Id,
                CellSize.ToString(CultureInfo.InvariantCulture),
                CellGap.ToString(CultureInfo.InvariantCulture),
                CellHeight.ToString(CultureInfo.InvariantCulture),
            };
        }
    }

    public sealed class StatusRow
    {
        public static readonly string[] Headers =
        {
            "Id", "DisplayName", "Description", "GrantedKeyword", "AttackModifier", "PushModifier",
            "ActionsGrantedOnApply", "DefaultDuration", "TickPhase", "Tint",
        };

        public string Id;
        public string DisplayName;
        public string Description;
        public KeywordId GrantedKeyword;
        public int AttackModifier;
        public int PushModifier;
        public int ActionsGrantedOnApply;
        public int DefaultDuration;
        public StatusTickPhase TickPhase;
        public Color Tint;

        public string[] ToCells()
        {
            return new[]
            {
                Id,
                DisplayName,
                Description,
                GrantedKeyword.ToString(),
                AttackModifier.ToString(CultureInfo.InvariantCulture),
                PushModifier.ToString(CultureInfo.InvariantCulture),
                ActionsGrantedOnApply.ToString(CultureInfo.InvariantCulture),
                DefaultDuration.ToString(CultureInfo.InvariantCulture),
                TickPhase.ToString(),
                TactiRogueExcelShared.ColorToHex(Tint),
            };
        }
    }

    public sealed class ActionRow
    {
        public static readonly string[] Headers =
        {
            "Id", "DisplayName", "Description", "ActionKind", "TargetMode", "TargetFilter", "CanTargetEmptyCell",
            "MinRange", "MaxRange", "UseActorAttackValue", "DamageAmount", "HealAmount", "PushForce", "Radius", "MoveRange", "MovePattern",
            "MoveBeforeEffect", "ExtraActionsGranted", "ApplyStatusId", "OverrideStatusDuration", "SummonEntityId",
            "ConsumeActorAction", "AllowDiagonalTargeting", "SkipMovePhase",
        };

        public string Id;
        public string DisplayName;
        public string Description;
        public ActionKind ActionKind;
        public ActionTargetMode TargetMode;
        public ActionTargetFilter TargetFilter;
        public bool CanTargetEmptyCell;
        public int MinRange;
        public int MaxRange;
        public bool UseActorAttackValue;
        public int DamageAmount;
        public int HealAmount;
        public int PushForce;
        public int Radius;
        public int MoveRange;
        public MovementPattern MovePattern;
        public bool MoveBeforeEffect;
        public int ExtraActionsGranted;
        public string ApplyStatusId;
        public int OverrideStatusDuration;
        public string SummonEntityId;
        public bool ConsumeActorAction;
        public bool AllowDiagonalTargeting;
        public bool SkipMovePhase;

        public string[] ToCells()
        {
            return new[]
            {
                Id,
                DisplayName,
                Description,
                ActionKind.ToString(),
                TargetMode.ToString(),
                TargetFilter.ToString(),
                TactiRogueExcelShared.BoolToText(CanTargetEmptyCell),
                MinRange.ToString(CultureInfo.InvariantCulture),
                MaxRange.ToString(CultureInfo.InvariantCulture),
                TactiRogueExcelShared.BoolToText(UseActorAttackValue),
                DamageAmount.ToString(CultureInfo.InvariantCulture),
                HealAmount.ToString(CultureInfo.InvariantCulture),
                PushForce.ToString(CultureInfo.InvariantCulture),
                Radius.ToString(CultureInfo.InvariantCulture),
                MoveRange.ToString(CultureInfo.InvariantCulture),
                MovePattern.ToString(),
                TactiRogueExcelShared.BoolToText(MoveBeforeEffect),
                ExtraActionsGranted.ToString(CultureInfo.InvariantCulture),
                ApplyStatusId,
                OverrideStatusDuration.ToString(CultureInfo.InvariantCulture),
                SummonEntityId,
                TactiRogueExcelShared.BoolToText(ConsumeActorAction),
                TactiRogueExcelShared.BoolToText(AllowDiagonalTargeting),
                TactiRogueExcelShared.BoolToText(SkipMovePhase),
            };
        }
    }

    public sealed class MoveProfileRow
    {
        public static readonly string[] Headers =
        {
            "Id", "UseSeparateMovePhase", "MoveRange", "MoveType", "AllowStayInPlace", "AllowDiagonalMove",
            "CanPassThroughUnits", "CanPassThroughBuildings", "RequirePath",
        };

        public string Id;
        public bool UseSeparateMovePhase;
        public int MoveRange;
        public MoveType MoveType;
        public bool AllowStayInPlace;
        public bool AllowDiagonalMove;
        public bool CanPassThroughUnits;
        public bool CanPassThroughBuildings;
        public bool RequirePath;

        public string[] ToCells()
        {
            return new[]
            {
                Id,
                TactiRogueExcelShared.BoolToText(UseSeparateMovePhase),
                MoveRange.ToString(CultureInfo.InvariantCulture),
                MoveType.ToString(),
                TactiRogueExcelShared.BoolToText(AllowStayInPlace),
                TactiRogueExcelShared.BoolToText(AllowDiagonalMove),
                TactiRogueExcelShared.BoolToText(CanPassThroughUnits),
                TactiRogueExcelShared.BoolToText(CanPassThroughBuildings),
                TactiRogueExcelShared.BoolToText(RequirePath),
            };
        }
    }

    public sealed class EntityRow
    {
        public static readonly string[] Headers =
        {
            "Id", "DisplayName", "ShortLabel", "Description", "EntityKind", "DefaultTeam", "MaxHp", "Attack",
            "PushBonus", "ActionId", "IntentDefinitionId", "CanAct", "BlocksMovement", "OccupiesCell", "Targetable",
            "MoveProfileId", "Tint",
        };

        public string Id;
        public string DisplayName;
        public string ShortLabel;
        public string Description;
        public EntityKind EntityKind;
        public TeamId DefaultTeam;
        public int MaxHp;
        public int Attack;
        public int PushBonus;
        public string ActionId;
        public string IntentDefinitionId;
        public bool CanAct;
        public bool BlocksMovement;
        public bool OccupiesCell;
        public bool Targetable;
        public string MoveProfileId;
        public Color Tint;

        public string[] ToCells()
        {
            return new[]
            {
                Id,
                DisplayName,
                ShortLabel,
                Description,
                EntityKind.ToString(),
                DefaultTeam.ToString(),
                MaxHp.ToString(CultureInfo.InvariantCulture),
                Attack.ToString(CultureInfo.InvariantCulture),
                PushBonus.ToString(CultureInfo.InvariantCulture),
                ActionId,
                IntentDefinitionId,
                TactiRogueExcelShared.BoolToText(CanAct),
                TactiRogueExcelShared.BoolToText(BlocksMovement),
                TactiRogueExcelShared.BoolToText(OccupiesCell),
                TactiRogueExcelShared.BoolToText(Targetable),
                MoveProfileId,
                TactiRogueExcelShared.ColorToHex(Tint),
            };
        }
    }

    public sealed class EntityStatusRow
    {
        public static readonly string[] Headers = { "EntityId", "Order", "StatusId" };

        public string EntityId;
        public int Order;
        public string StatusId;

        public string[] ToCells()
        {
            return new[]
            {
                EntityId,
                Order.ToString(CultureInfo.InvariantCulture),
                StatusId,
            };
        }
    }

    public sealed class IntentRow
    {
        public static readonly string[] Headers =
        {
            "Id", "DisplayName", "Description", "IntentKind", "ActionId", "AcquireRange", "PreferCommander",
            "TargetingMode", "RevalidationPolicy", "FallbackMode", "Tint",
        };

        public string Id;
        public string DisplayName;
        public string Description;
        public IntentKind IntentKind;
        public string ActionId;
        public int AcquireRange;
        public bool PreferCommander;
        public IntentTargetingMode TargetingMode;
        public IntentRevalidationPolicy RevalidationPolicy;
        public IntentFallbackMode FallbackMode;
        public Color Tint;

        public string[] ToCells()
        {
            return new[]
            {
                Id,
                DisplayName,
                Description,
                IntentKind.ToString(),
                ActionId,
                AcquireRange.ToString(CultureInfo.InvariantCulture),
                TactiRogueExcelShared.BoolToText(PreferCommander),
                TargetingMode.ToString(),
                RevalidationPolicy.ToString(),
                FallbackMode.ToString(),
                TactiRogueExcelShared.ColorToHex(Tint),
            };
        }
    }

    public sealed class CardRow
    {
        public static readonly string[] Headers =
        {
            "Id", "DisplayName", "Description", "CardKind", "Cost", "ActionId", "SummonEntityId", "SummonMinRange",
            "SummonMaxRange", "Tint",
        };

        public string Id;
        public string DisplayName;
        public string Description;
        public CardKind CardKind;
        public int Cost;
        public string ActionId;
        public string SummonEntityId;
        public int SummonMinRange;
        public int SummonMaxRange;
        public Color Tint;

        public string[] ToCells()
        {
            return new[]
            {
                Id,
                DisplayName,
                Description,
                CardKind.ToString(),
                Cost.ToString(CultureInfo.InvariantCulture),
                ActionId,
                SummonEntityId,
                SummonMinRange.ToString(CultureInfo.InvariantCulture),
                SummonMaxRange.ToString(CultureInfo.InvariantCulture),
                TactiRogueExcelShared.ColorToHex(Tint),
            };
        }
    }

    public sealed class CardPieceVisualRow
    {
        public static readonly string[] Headers =
        {
            "Id", "ModelKey", "CardArtKey", "BackArtKey", "IdleTiltAngle",
            "DefaultRotationX", "DefaultRotationY", "DefaultRotationZ", "DefaultScale", "YOffset",
            "FrameModelKey", "FrameMaterialKey", "IdleMotionKey", "MoveMotionKey", "AttackMotionKey",
            "HitMotionKey", "DeathMotionKey", "SpawnMotionKey",
        };

        public static readonly string[] RequiredHeaders =
        {
            "Id", "ModelKey", "CardArtKey", "BackArtKey", "IdleTiltAngle", "DefaultScale", "YOffset",
        };

        public string Id;
        public string ModelKey;
        public string CardArtKey;
        public string BackArtKey;
        public float IdleTiltAngle;
        public float DefaultRotationX;
        public float DefaultRotationY;
        public float DefaultRotationZ;
        public float DefaultScale;
        public float YOffset;
        public string FrameModelKey;
        public string FrameMaterialKey;
        public string IdleMotionKey;
        public string MoveMotionKey;
        public string AttackMotionKey;
        public string HitMotionKey;
        public string DeathMotionKey;
        public string SpawnMotionKey;

        public string[] ToCells()
        {
            return new[]
            {
                Id,
                ModelKey,
                CardArtKey,
                BackArtKey,
                IdleTiltAngle.ToString(CultureInfo.InvariantCulture),
                DefaultRotationX.ToString(CultureInfo.InvariantCulture),
                DefaultRotationY.ToString(CultureInfo.InvariantCulture),
                DefaultRotationZ.ToString(CultureInfo.InvariantCulture),
                DefaultScale.ToString(CultureInfo.InvariantCulture),
                YOffset.ToString(CultureInfo.InvariantCulture),
                FrameModelKey,
                FrameMaterialKey,
                IdleMotionKey,
                MoveMotionKey,
                AttackMotionKey,
                HitMotionKey,
                DeathMotionKey,
                SpawnMotionKey,
            };
        }
    }

    public sealed class ScenarioRow
    {
        public static readonly string[] Headers =
        {
            "Id", "DisplayName", "Description", "DisplayOrder", "Width", "Height", "RandomSeed", "StartingMana",
            "MaxMana", "CardsPerTurn",
        };

        public string Id;
        public string DisplayName;
        public string Description;
        public int DisplayOrder;
        public int Width;
        public int Height;
        public int RandomSeed;
        public int StartingMana;
        public int MaxMana;
        public int CardsPerTurn;

        public string[] ToCells()
        {
            return new[]
            {
                Id,
                DisplayName,
                Description,
                DisplayOrder.ToString(CultureInfo.InvariantCulture),
                Width.ToString(CultureInfo.InvariantCulture),
                Height.ToString(CultureInfo.InvariantCulture),
                RandomSeed.ToString(CultureInfo.InvariantCulture),
                StartingMana.ToString(CultureInfo.InvariantCulture),
                MaxMana.ToString(CultureInfo.InvariantCulture),
                CardsPerTurn.ToString(CultureInfo.InvariantCulture),
            };
        }
    }

    public sealed class ScenarioSpawnRow
    {
        public static readonly string[] Headers = { "ScenarioId", "Order", "TemplateId", "Team", "X", "Y" };

        public string ScenarioId;
        public int Order;
        public string TemplateId;
        public TeamId Team;
        public int X;
        public int Y;

        public string[] ToCells()
        {
            return new[]
            {
                ScenarioId,
                Order.ToString(CultureInfo.InvariantCulture),
                TemplateId,
                Team.ToString(),
                X.ToString(CultureInfo.InvariantCulture),
                Y.ToString(CultureInfo.InvariantCulture),
            };
        }
    }

    public sealed class ScenarioDeckRow
    {
        public static readonly string[] Headers = { "ScenarioId", "Order", "CardId" };

        public string ScenarioId;
        public int Order;
        public string CardId;

        public string[] ToCells()
        {
            return new[]
            {
                ScenarioId,
                Order.ToString(CultureInfo.InvariantCulture),
                CardId,
            };
        }
    }

    public sealed class ScenarioVoidCellRow
    {
        public static readonly string[] Headers = { "ScenarioId", "Order", "X", "Y" };

        public string ScenarioId;
        public int Order;
        public int X;
        public int Y;

        public string[] ToCells()
        {
            return new[]
            {
                ScenarioId,
                Order.ToString(CultureInfo.InvariantCulture),
                X.ToString(CultureInfo.InvariantCulture),
                Y.ToString(CultureInfo.InvariantCulture),
            };
        }
    }

    internal static class TactiRogueExcelShared
    {
        public static string BoolToText(bool value)
        {
            return value ? "true" : "false";
        }

        public static string ColorToHex(Color color)
        {
            var alpha = Mathf.RoundToInt(color.a * 255f);
            return alpha >= 255
                ? $"#{ColorUtility.ToHtmlStringRGB(color)}"
                : $"#{ColorUtility.ToHtmlStringRGBA(color)}";
        }

        public static bool TryParseColor(string raw, out Color color)
        {
            color = Color.white;
            return !string.IsNullOrWhiteSpace(raw) && ColorUtility.TryParseHtmlString(raw.Trim(), out color);
        }
    }

    public static class TactiRogueExcelExporter
    {
        public static void ExportCurrentDataToWorkbook(string workbookPath = null)
        {
            var exportPath = string.IsNullOrWhiteSpace(workbookPath) ? TactiRogueExcelPaths.WorkbookAssetPath : workbookPath;
            var absolutePath = TactiRogueExcelPaths.ToAbsolutePath(exportPath);
            var projectPath = TactiRogueExcelPaths.ToProjectRelativePath(absolutePath);

            if (projectPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                var folderPath = Path.GetDirectoryName(projectPath)?.Replace("\\", "/");
                if (!string.IsNullOrWhiteSpace(folderPath))
                {
                    TactiRogueExcelPaths.EnsureFolder(folderPath);
                }
            }

            var workbookData = BuildWorkbookDataFromCurrentContent();
            TactiRogueSimpleXlsx.Save(absolutePath, workbookData.ToWorkbook());

            if (projectPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.Refresh();
            }
        }

        public static TactiRogueExcelWorkbookData BuildWorkbookDataFromCurrentContent()
        {
            TactiRogueContentProvider.ResetCache();
            TactiRogueScenarioRepository.ResetCache();

            var database = TactiRogueContentProvider.LoadOrCreateDatabase();
            var scenarios = TactiRogueScenarioRepository.LoadAll();
            var workbookData = new TactiRogueExcelWorkbookData();

            workbookData.BoardVisualRows.Add(new BoardVisualRow
            {
                Id = BoardVisualRow.DefaultId,
                CellSize = database.GetBoardCellSize(),
                CellGap = database.GetBoardCellGap(),
                CellHeight = database.GetBoardCellHeight(),
            });

            foreach (var status in database.StatusTemplates.Where(item => item != null))
            {
                workbookData.StatusRows.Add(new StatusRow
                {
                    Id = status.Id,
                    DisplayName = status.DisplayName,
                    Description = status.Description,
                    GrantedKeyword = status.GrantedKeyword,
                    AttackModifier = status.AttackModifier,
                    PushModifier = status.PushModifier,
                    ActionsGrantedOnApply = status.ActionsGrantedOnApply,
                    DefaultDuration = status.DefaultDuration,
                    TickPhase = status.TickPhase,
                    Tint = status.Tint,
                });
            }

            foreach (var action in database.ActionDefinitions.Where(item => item != null))
            {
                workbookData.ActionRows.Add(new ActionRow
                {
                    Id = action.Id,
                    DisplayName = action.DisplayName,
                    Description = action.Description,
                    ActionKind = action.ActionKind,
                    TargetMode = action.TargetMode,
                    TargetFilter = action.TargetFilter,
                    CanTargetEmptyCell = action.CanTargetEmptyCell,
                    MinRange = action.MinRange,
                    MaxRange = action.MaxRange,
                    UseActorAttackValue = action.UseActorAttackValue,
                    DamageAmount = action.DamageAmount,
                    HealAmount = action.HealAmount,
                    PushForce = action.PushForce,
                    Radius = action.Radius,
                    MoveRange = action.MoveRange,
                    MovePattern = action.MovePattern,
                    MoveBeforeEffect = action.MoveBeforeEffect,
                    ExtraActionsGranted = action.ExtraActionsGranted,
                    ApplyStatusId = action.ApplyStatusId,
                    OverrideStatusDuration = action.OverrideStatusDuration,
                    SummonEntityId = action.SummonEntityId,
                    ConsumeActorAction = action.ConsumeActorAction,
                    AllowDiagonalTargeting = action.AllowDiagonalTargeting,
                    SkipMovePhase = action.SkipMovePhase,
                });
            }

            foreach (var entity in database.EntityTemplates.Where(item => item != null))
            {
                var moveProfileId = $"{entity.Id}_move";
                var moveProfile = entity.MoveProfile?.Clone() ?? new MoveProfile();
                workbookData.MoveProfileRows.Add(new MoveProfileRow
                {
                    Id = moveProfileId,
                    UseSeparateMovePhase = moveProfile.UseSeparateMovePhase,
                    MoveRange = moveProfile.MoveRange,
                    MoveType = moveProfile.MoveType,
                    AllowStayInPlace = moveProfile.AllowStayInPlace,
                    AllowDiagonalMove = moveProfile.AllowDiagonalMove,
                    CanPassThroughUnits = moveProfile.CanPassThroughUnits,
                    CanPassThroughBuildings = moveProfile.CanPassThroughBuildings,
                    RequirePath = moveProfile.RequirePath,
                });

                workbookData.EntityRows.Add(new EntityRow
                {
                    Id = entity.Id,
                    DisplayName = entity.DisplayName,
                    ShortLabel = entity.ShortLabel,
                    Description = entity.Description,
                    EntityKind = entity.EntityKind,
                    DefaultTeam = entity.DefaultTeam,
                    MaxHp = entity.MaxHp,
                    Attack = entity.Attack,
                    PushBonus = entity.PushBonus,
                    ActionId = entity.ActionId,
                    IntentDefinitionId = entity.IntentDefinitionId,
                    CanAct = entity.CanAct,
                    BlocksMovement = entity.BlocksMovement,
                    OccupiesCell = entity.OccupiesCell,
                    Targetable = entity.Targetable,
                    MoveProfileId = moveProfileId,
                    Tint = entity.Tint,
                });

                for (var statusIndex = 0; statusIndex < (entity.StartingStatusIds?.Length ?? 0); statusIndex++)
                {
                    workbookData.EntityStatusRows.Add(new EntityStatusRow
                    {
                        EntityId = entity.Id,
                        Order = statusIndex,
                        StatusId = entity.StartingStatusIds[statusIndex],
                    });
                }
            }

            foreach (var intent in database.IntentDefinitions.Where(item => item != null))
            {
                workbookData.IntentRows.Add(new IntentRow
                {
                    Id = intent.Id,
                    DisplayName = intent.DisplayName,
                    Description = intent.Description,
                    IntentKind = intent.IntentKind,
                    ActionId = intent.ActionId,
                    AcquireRange = intent.AcquireRange,
                    PreferCommander = intent.PreferCommander,
                    TargetingMode = intent.TargetingMode,
                    RevalidationPolicy = intent.RevalidationPolicy,
                    FallbackMode = intent.FallbackMode,
                    Tint = intent.Tint,
                });
            }

            foreach (var card in database.CardTemplates.Where(item => item != null))
            {
                workbookData.CardRows.Add(new CardRow
                {
                    Id = card.Id,
                    DisplayName = card.DisplayName,
                    Description = card.Description,
                    CardKind = card.CardKind,
                    Cost = card.Cost,
                    ActionId = card.ActionId,
                    SummonEntityId = card.SummonEntityId,
                    SummonMinRange = card.SummonMinRange,
                    SummonMaxRange = card.SummonMaxRange,
                    Tint = card.Tint,
                });
            }

            foreach (var visual in database.CardPieceVisualDefinitions.Where(item => item != null))
            {
                workbookData.CardPieceVisualRows.Add(new CardPieceVisualRow
                {
                    Id = visual.Id,
                    ModelKey = visual.ModelKey,
                    CardArtKey = visual.CardArtKey,
                    BackArtKey = visual.BackArtKey,
                    IdleTiltAngle = visual.IdleTiltAngle,
                    DefaultRotationX = visual.DefaultRotationEuler.x,
                    DefaultRotationY = visual.DefaultRotationEuler.y,
                    DefaultRotationZ = visual.DefaultRotationEuler.z,
                    DefaultScale = visual.DefaultScale,
                    YOffset = visual.YOffset,
                    FrameModelKey = visual.FrameModelKey,
                    FrameMaterialKey = visual.FrameMaterialKey,
                    IdleMotionKey = visual.IdleMotionKey,
                    MoveMotionKey = visual.MoveMotionKey,
                    AttackMotionKey = visual.AttackMotionKey,
                    HitMotionKey = visual.HitMotionKey,
                    DeathMotionKey = visual.DeathMotionKey,
                    SpawnMotionKey = visual.SpawnMotionKey,
                });
            }

            foreach (var scenario in scenarios.OrderBy(item => item.DisplayOrder))
            {
                workbookData.ScenarioRows.Add(new ScenarioRow
                {
                    Id = scenario.Id,
                    DisplayName = scenario.DisplayName,
                    Description = scenario.Description,
                    DisplayOrder = scenario.DisplayOrder,
                    Width = scenario.Width,
                    Height = scenario.Height,
                    RandomSeed = scenario.RandomSeed,
                    StartingMana = scenario.StartingMana,
                    MaxMana = scenario.MaxMana,
                    CardsPerTurn = scenario.CardsPerTurn,
                });

                for (var spawnIndex = 0; spawnIndex < (scenario.Spawns?.Length ?? 0); spawnIndex++)
                {
                    var spawn = scenario.Spawns[spawnIndex];
                    if (spawn == null)
                    {
                        continue;
                    }

                    workbookData.ScenarioSpawnRows.Add(new ScenarioSpawnRow
                    {
                        ScenarioId = scenario.Id,
                        Order = spawnIndex,
                        TemplateId = spawn.TemplateId,
                        Team = Enum.TryParse(spawn.Team, out TeamId parsedTeam) ? parsedTeam : TeamId.Neutral,
                        X = spawn.X,
                        Y = spawn.Y,
                    });
                }

                for (var deckIndex = 0; deckIndex < (scenario.StartingDeck?.Length ?? 0); deckIndex++)
                {
                    workbookData.ScenarioDeckRows.Add(new ScenarioDeckRow
                    {
                        ScenarioId = scenario.Id,
                        Order = deckIndex,
                        CardId = scenario.StartingDeck[deckIndex],
                    });
                }

                for (var voidIndex = 0; voidIndex < (scenario.VoidCells?.Length ?? 0); voidIndex++)
                {
                    if (!GridPosition.TryParse(scenario.VoidCells[voidIndex], out var position))
                    {
                        continue;
                    }

                    workbookData.ScenarioVoidCellRows.Add(new ScenarioVoidCellRow
                    {
                        ScenarioId = scenario.Id,
                        Order = voidIndex,
                        X = position.X,
                        Y = position.Y,
                    });
                }
            }

            return workbookData;
        }
    }

    public static class TactiRogueExcelValidator
    {
        public static TactiRogueExcelValidationReport ValidateWorkbook(string workbookPath = null)
        {
            TryReadWorkbook(workbookPath, out _, out var report);
            return report;
        }

        public static bool TryReadWorkbook(string workbookPath, out TactiRogueExcelWorkbookData workbookData, out TactiRogueExcelValidationReport report)
        {
            var effectivePath = string.IsNullOrWhiteSpace(workbookPath) ? TactiRogueExcelPaths.WorkbookAssetPath : workbookPath;
            var absolutePath = TactiRogueExcelPaths.ToAbsolutePath(effectivePath);
            report = new TactiRogueExcelValidationReport(TactiRogueExcelPaths.ToProjectRelativePath(absolutePath));
            workbookData = null;

            if (!File.Exists(absolutePath))
            {
                report.AddError($"Workbook not found: {report.WorkbookPath}");
                return false;
            }

            TactiRogueExcelWorkbook workbook;
            try
            {
                workbook = TactiRogueSimpleXlsx.Load(absolutePath);
            }
            catch (Exception exception)
            {
                report.AddError($"Failed to read workbook '{report.WorkbookPath}': {exception.Message}");
                return false;
            }

            var sheets = workbook.Sheets.ToDictionary(sheet => sheet.Name, sheet => sheet, StringComparer.Ordinal);
            var requiredSheets = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                { "Status", StatusRow.Headers },
                { "Action", ActionRow.Headers },
                { "MoveProfile", MoveProfileRow.Headers },
                { "Entity", EntityRow.Headers },
                { "EntityStatus", EntityStatusRow.Headers },
                { "Intent", IntentRow.Headers },
                { "Card", CardRow.Headers },
                { "CardPieceVisual", CardPieceVisualRow.RequiredHeaders },
                { "Scenario", ScenarioRow.Headers },
                { "ScenarioSpawn", ScenarioSpawnRow.Headers },
                { "ScenarioDeck", ScenarioDeckRow.Headers },
                { "ScenarioVoidCell", ScenarioVoidCellRow.Headers },
            };
            var optionalSheets = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                { "BoardVisual", BoardVisualRow.Headers },
            };

            var accessors = new Dictionary<string, SheetAccessor>(StringComparer.Ordinal);
            foreach (var pair in requiredSheets)
            {
                if (!sheets.TryGetValue(pair.Key, out var sheet))
                {
                    report.AddError($"Workbook is missing sheet '{pair.Key}'.");
                    continue;
                }

                var accessor = new SheetAccessor(sheet, pair.Value, report);
                accessors[pair.Key] = accessor;
                accessor.ValidateCells(report);
            }

            foreach (var pair in optionalSheets)
            {
                if (!sheets.TryGetValue(pair.Key, out var sheet))
                {
                    continue;
                }

                var accessor = new SheetAccessor(sheet, pair.Value, report);
                accessors[pair.Key] = accessor;
                accessor.ValidateCells(report);
            }

            if (!report.IsValid)
            {
                return false;
            }

            workbookData = new TactiRogueExcelWorkbookData();
            if (accessors.TryGetValue("BoardVisual", out var boardVisualAccessor))
            {
                ParseRows(boardVisualAccessor, workbookData.BoardVisualRows, ParseBoardVisualRow, report);
            }
            else
            {
                workbookData.BoardVisualRows.Add(BoardVisualRow.CreateDefault());
            }

            ParseRows(accessors["Status"], workbookData.StatusRows, ParseStatusRow, report);
            ParseRows(accessors["Action"], workbookData.ActionRows, ParseActionRow, report);
            ParseRows(accessors["MoveProfile"], workbookData.MoveProfileRows, ParseMoveProfileRow, report);
            ParseRows(accessors["Entity"], workbookData.EntityRows, ParseEntityRow, report);
            ParseRows(accessors["EntityStatus"], workbookData.EntityStatusRows, ParseEntityStatusRow, report);
            ParseRows(accessors["Intent"], workbookData.IntentRows, ParseIntentRow, report);
            ParseRows(accessors["Card"], workbookData.CardRows, ParseCardRow, report);
            ParseRows(accessors["CardPieceVisual"], workbookData.CardPieceVisualRows, ParseCardPieceVisualRow, report);
            ParseRows(accessors["Scenario"], workbookData.ScenarioRows, ParseScenarioRow, report);
            ParseRows(accessors["ScenarioSpawn"], workbookData.ScenarioSpawnRows, ParseScenarioSpawnRow, report);
            ParseRows(accessors["ScenarioDeck"], workbookData.ScenarioDeckRows, ParseScenarioDeckRow, report);
            ParseRows(accessors["ScenarioVoidCell"], workbookData.ScenarioVoidCellRows, ParseScenarioVoidCellRow, report);

            ValidateWorkbookData(workbookData, report);
            return report.IsValid;
        }

        public static TactiRogueExcelValidationReport ValidateWorkbookData(TactiRogueExcelWorkbookData workbookData)
        {
            var report = new TactiRogueExcelValidationReport(TactiRogueExcelPaths.WorkbookAssetPath);
            ValidateWorkbookData(workbookData, report);
            return report;
        }

        private static void ValidateWorkbookData(TactiRogueExcelWorkbookData workbookData, TactiRogueExcelValidationReport report)
        {
            if (workbookData == null)
            {
                report.AddError("Workbook data is null.");
                return;
            }

            ValidateBoardVisualRows(workbookData.BoardVisualRows, report);
            ValidateDuplicates(workbookData.StatusRows, row => row.Id, "Status", report);
            ValidateDuplicates(workbookData.ActionRows, row => row.Id, "Action", report);
            ValidateDuplicates(workbookData.MoveProfileRows, row => row.Id, "MoveProfile", report);
            ValidateDuplicates(workbookData.EntityRows, row => row.Id, "Entity", report);
            ValidateDuplicates(workbookData.IntentRows, row => row.Id, "Intent", report);
            ValidateDuplicates(workbookData.CardRows, row => row.Id, "Card", report);
            ValidateDuplicates(workbookData.CardPieceVisualRows, row => row.Id, "CardPieceVisual", report);
            ValidateDuplicates(workbookData.ScenarioRows, row => row.Id, "Scenario", report);

            ValidateOrderedChildren(workbookData.EntityStatusRows, row => $"{row.EntityId}:{row.Order}", "EntityStatus", report);
            ValidateOrderedChildren(workbookData.ScenarioSpawnRows, row => $"{row.ScenarioId}:{row.Order}", "ScenarioSpawn", report);
            ValidateOrderedChildren(workbookData.ScenarioDeckRows, row => $"{row.ScenarioId}:{row.Order}", "ScenarioDeck", report);
            ValidateOrderedChildren(workbookData.ScenarioVoidCellRows, row => $"{row.ScenarioId}:{row.Order}", "ScenarioVoidCell", report);

            var statusIds = new HashSet<string>(workbookData.StatusRows.Select(row => row.Id), StringComparer.Ordinal);
            var actionIds = new HashSet<string>(workbookData.ActionRows.Select(row => row.Id), StringComparer.Ordinal);
            var moveProfileIds = new HashSet<string>(workbookData.MoveProfileRows.Select(row => row.Id), StringComparer.Ordinal);
            var entityIds = new HashSet<string>(workbookData.EntityRows.Select(row => row.Id), StringComparer.Ordinal);
            var intentIds = new HashSet<string>(workbookData.IntentRows.Select(row => row.Id), StringComparer.Ordinal);
            var cardIds = new HashSet<string>(workbookData.CardRows.Select(row => row.Id), StringComparer.Ordinal);
            var scenarioIds = new HashSet<string>(workbookData.ScenarioRows.Select(row => row.Id), StringComparer.Ordinal);

            foreach (var row in workbookData.ActionRows)
            {
                ValidateReference("Action", row.Id, "ApplyStatusId", row.ApplyStatusId, statusIds, report);
                ValidateReference("Action", row.Id, "SummonEntityId", row.SummonEntityId, entityIds, report);
            }

            foreach (var row in workbookData.EntityRows)
            {
                ValidateRequiredReference("Entity", row.Id, "MoveProfileId", row.MoveProfileId, moveProfileIds, report);
                ValidateReference("Entity", row.Id, "ActionId", row.ActionId, actionIds, report);
                ValidateReference("Entity", row.Id, "IntentDefinitionId", row.IntentDefinitionId, intentIds, report);
            }

            foreach (var row in workbookData.EntityStatusRows)
            {
                ValidateRequiredReference("EntityStatus", row.EntityId, "EntityId", row.EntityId, entityIds, report);
                ValidateRequiredReference("EntityStatus", $"{row.EntityId}:{row.Order}", "StatusId", row.StatusId, statusIds, report);
            }

            foreach (var row in workbookData.IntentRows)
            {
                ValidateRequiredReference("Intent", row.Id, "ActionId", row.ActionId, actionIds, report);
            }

            foreach (var row in workbookData.CardRows)
            {
                ValidateReference("Card", row.Id, "ActionId", row.ActionId, actionIds, report);
                ValidateReference("Card", row.Id, "SummonEntityId", row.SummonEntityId, entityIds, report);
            }

            foreach (var row in workbookData.ScenarioSpawnRows)
            {
                ValidateRequiredReference("ScenarioSpawn", $"{row.ScenarioId}:{row.Order}", "ScenarioId", row.ScenarioId, scenarioIds, report);
                ValidateRequiredReference("ScenarioSpawn", $"{row.ScenarioId}:{row.Order}", "TemplateId", row.TemplateId, entityIds, report);
            }

            foreach (var row in workbookData.ScenarioDeckRows)
            {
                ValidateRequiredReference("ScenarioDeck", $"{row.ScenarioId}:{row.Order}", "ScenarioId", row.ScenarioId, scenarioIds, report);
                ValidateRequiredReference("ScenarioDeck", $"{row.ScenarioId}:{row.Order}", "CardId", row.CardId, cardIds, report);
            }

            foreach (var row in workbookData.ScenarioVoidCellRows)
            {
                ValidateRequiredReference("ScenarioVoidCell", $"{row.ScenarioId}:{row.Order}", "ScenarioId", row.ScenarioId, scenarioIds, report);
            }
        }

        private static void ValidateBoardVisualRows(IReadOnlyList<BoardVisualRow> rows, TactiRogueExcelValidationReport report)
        {
            if (rows.Count == 0)
            {
                report.AddError("Sheet 'BoardVisual' requires exactly one settings row.");
                return;
            }

            if (rows.Count > 1)
            {
                report.AddError("Sheet 'BoardVisual' supports exactly one settings row.");
            }

            ValidateDuplicates(rows, row => row.Id, "BoardVisual", report);
            var row = rows[0];
            var rowId = string.IsNullOrWhiteSpace(row.Id) ? BoardVisualRow.DefaultId : row.Id;
            if (row.CellSize <= 0f)
            {
                report.AddError($"Sheet 'BoardVisual' row '{rowId}' requires CellSize > 0.");
            }

            if (row.CellGap < 0f)
            {
                report.AddError($"Sheet 'BoardVisual' row '{rowId}' requires CellGap >= 0.");
            }

            if (row.CellGap >= row.CellSize)
            {
                report.AddError($"Sheet 'BoardVisual' row '{rowId}' requires CellGap smaller than CellSize.");
            }

            if (row.CellHeight <= 0f)
            {
                report.AddError($"Sheet 'BoardVisual' row '{rowId}' requires CellHeight > 0.");
            }
        }

        private static void ParseRows<TRow>(SheetAccessor sheet, ICollection<TRow> destination, Func<RowAccessor, TactiRogueExcelValidationReport, TRow> parser, TactiRogueExcelValidationReport report)
            where TRow : class
        {
            foreach (var row in sheet.DataRows())
            {
                if (row.IsEmpty)
                {
                    continue;
                }

                var parsed = parser(row, report);
                if (parsed != null)
                {
                    destination.Add(parsed);
                }
            }
        }

        private static StatusRow ParseStatusRow(RowAccessor row, TactiRogueExcelValidationReport report)
        {
            if (!TryReadRequiredString(row, "Id", report, out var id)
                || !TryReadRequiredString(row, "DisplayName", report, out var displayName)
                || !TryReadEnum(row, "GrantedKeyword", report, out KeywordId keyword)
                || !TryReadInt(row, "AttackModifier", report, out var attackModifier)
                || !TryReadInt(row, "PushModifier", report, out var pushModifier)
                || !TryReadInt(row, "ActionsGrantedOnApply", report, out var actionsGranted)
                || !TryReadInt(row, "DefaultDuration", report, out var defaultDuration)
                || !TryReadEnum(row, "TickPhase", report, out StatusTickPhase tickPhase)
                || !TryReadColor(row, "Tint", report, out var tint))
            {
                return null;
            }

            return new StatusRow
            {
                Id = id,
                DisplayName = displayName,
                Description = row.Get("Description"),
                GrantedKeyword = keyword,
                AttackModifier = attackModifier,
                PushModifier = pushModifier,
                ActionsGrantedOnApply = actionsGranted,
                DefaultDuration = defaultDuration,
                TickPhase = tickPhase,
                Tint = tint,
            };
        }

        private static ActionRow ParseActionRow(RowAccessor row, TactiRogueExcelValidationReport report)
        {
            if (!TryReadRequiredString(row, "Id", report, out var id)
                || !TryReadRequiredString(row, "DisplayName", report, out var displayName)
                || !TryReadEnum(row, "ActionKind", report, out ActionKind actionKind)
                || !TryReadEnum(row, "TargetMode", report, out ActionTargetMode targetMode)
                || !TryReadEnum(row, "TargetFilter", report, out ActionTargetFilter targetFilter)
                || !TryReadBool(row, "CanTargetEmptyCell", report, out var canTargetEmptyCell)
                || !TryReadInt(row, "MinRange", report, out var minRange)
                || !TryReadInt(row, "MaxRange", report, out var maxRange)
                || !TryReadBool(row, "UseActorAttackValue", report, out var useActorAttackValue)
                || !TryReadInt(row, "DamageAmount", report, out var damageAmount)
                || !TryReadInt(row, "HealAmount", report, out var healAmount)
                || !TryReadInt(row, "PushForce", report, out var pushForce)
                || !TryReadInt(row, "Radius", report, out var radius)
                || !TryReadInt(row, "MoveRange", report, out var moveRange)
                || !TryReadEnum(row, "MovePattern", report, out MovementPattern movePattern)
                || !TryReadBool(row, "MoveBeforeEffect", report, out var moveBeforeEffect)
                || !TryReadInt(row, "ExtraActionsGranted", report, out var extraActionsGranted)
                || !TryReadInt(row, "OverrideStatusDuration", report, out var overrideStatusDuration)
                || !TryReadBool(row, "ConsumeActorAction", report, out var consumeActorAction)
                || !TryReadBool(row, "AllowDiagonalTargeting", report, out var allowDiagonalTargeting)
                || !TryReadBool(row, "SkipMovePhase", report, out var skipMovePhase))
            {
                return null;
            }

            return new ActionRow
            {
                Id = id,
                DisplayName = displayName,
                Description = row.Get("Description"),
                ActionKind = actionKind,
                TargetMode = targetMode,
                TargetFilter = targetFilter,
                CanTargetEmptyCell = canTargetEmptyCell,
                MinRange = minRange,
                MaxRange = maxRange,
                UseActorAttackValue = useActorAttackValue,
                DamageAmount = damageAmount,
                HealAmount = healAmount,
                PushForce = pushForce,
                Radius = radius,
                MoveRange = moveRange,
                MovePattern = movePattern,
                MoveBeforeEffect = moveBeforeEffect,
                ExtraActionsGranted = extraActionsGranted,
                ApplyStatusId = row.Get("ApplyStatusId"),
                OverrideStatusDuration = overrideStatusDuration,
                SummonEntityId = row.Get("SummonEntityId"),
                ConsumeActorAction = consumeActorAction,
                AllowDiagonalTargeting = allowDiagonalTargeting,
                SkipMovePhase = skipMovePhase,
            };
        }

        private static MoveProfileRow ParseMoveProfileRow(RowAccessor row, TactiRogueExcelValidationReport report)
        {
            if (!TryReadRequiredString(row, "Id", report, out var id)
                || !TryReadBool(row, "UseSeparateMovePhase", report, out var useSeparateMovePhase)
                || !TryReadInt(row, "MoveRange", report, out var moveRange)
                || !TryReadEnum(row, "MoveType", report, out MoveType moveType)
                || !TryReadBool(row, "AllowStayInPlace", report, out var allowStayInPlace)
                || !TryReadBool(row, "AllowDiagonalMove", report, out var allowDiagonalMove)
                || !TryReadBool(row, "CanPassThroughUnits", report, out var canPassThroughUnits)
                || !TryReadBool(row, "CanPassThroughBuildings", report, out var canPassThroughBuildings)
                || !TryReadBool(row, "RequirePath", report, out var requirePath))
            {
                return null;
            }

            return new MoveProfileRow
            {
                Id = id,
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

        private static EntityRow ParseEntityRow(RowAccessor row, TactiRogueExcelValidationReport report)
        {
            if (!TryReadRequiredString(row, "Id", report, out var id)
                || !TryReadRequiredString(row, "DisplayName", report, out var displayName)
                || !TryReadRequiredString(row, "ShortLabel", report, out var shortLabel)
                || !TryReadEnum(row, "EntityKind", report, out EntityKind entityKind)
                || !TryReadEnum(row, "DefaultTeam", report, out TeamId defaultTeam)
                || !TryReadInt(row, "MaxHp", report, out var maxHp)
                || !TryReadInt(row, "Attack", report, out var attack)
                || !TryReadInt(row, "PushBonus", report, out var pushBonus)
                || !TryReadBool(row, "CanAct", report, out var canAct)
                || !TryReadBool(row, "BlocksMovement", report, out var blocksMovement)
                || !TryReadBool(row, "OccupiesCell", report, out var occupiesCell)
                || !TryReadBool(row, "Targetable", report, out var targetable)
                || !TryReadRequiredString(row, "MoveProfileId", report, out var moveProfileId)
                || !TryReadColor(row, "Tint", report, out var tint))
            {
                return null;
            }

            return new EntityRow
            {
                Id = id,
                DisplayName = displayName,
                ShortLabel = shortLabel,
                Description = row.Get("Description"),
                EntityKind = entityKind,
                DefaultTeam = defaultTeam,
                MaxHp = maxHp,
                Attack = attack,
                PushBonus = pushBonus,
                ActionId = row.Get("ActionId"),
                IntentDefinitionId = row.Get("IntentDefinitionId"),
                CanAct = canAct,
                BlocksMovement = blocksMovement,
                OccupiesCell = occupiesCell,
                Targetable = targetable,
                MoveProfileId = moveProfileId,
                Tint = tint,
            };
        }

        private static EntityStatusRow ParseEntityStatusRow(RowAccessor row, TactiRogueExcelValidationReport report)
        {
            if (!TryReadRequiredString(row, "EntityId", report, out var entityId)
                || !TryReadInt(row, "Order", report, out var order)
                || !TryReadRequiredString(row, "StatusId", report, out var statusId))
            {
                return null;
            }

            return new EntityStatusRow
            {
                EntityId = entityId,
                Order = order,
                StatusId = statusId,
            };
        }

        private static IntentRow ParseIntentRow(RowAccessor row, TactiRogueExcelValidationReport report)
        {
            if (!TryReadRequiredString(row, "Id", report, out var id)
                || !TryReadRequiredString(row, "DisplayName", report, out var displayName)
                || !TryReadEnum(row, "IntentKind", report, out IntentKind intentKind)
                || !TryReadRequiredString(row, "ActionId", report, out var actionId)
                || !TryReadInt(row, "AcquireRange", report, out var acquireRange)
                || !TryReadBool(row, "PreferCommander", report, out var preferCommander)
                || !TryReadEnum(row, "TargetingMode", report, out IntentTargetingMode targetingMode)
                || !TryReadEnum(row, "RevalidationPolicy", report, out IntentRevalidationPolicy revalidationPolicy)
                || !TryReadEnum(row, "FallbackMode", report, out IntentFallbackMode fallbackMode)
                || !TryReadColor(row, "Tint", report, out var tint))
            {
                return null;
            }

            return new IntentRow
            {
                Id = id,
                DisplayName = displayName,
                Description = row.Get("Description"),
                IntentKind = intentKind,
                ActionId = actionId,
                AcquireRange = acquireRange,
                PreferCommander = preferCommander,
                TargetingMode = targetingMode,
                RevalidationPolicy = revalidationPolicy,
                FallbackMode = fallbackMode,
                Tint = tint,
            };
        }

        private static CardRow ParseCardRow(RowAccessor row, TactiRogueExcelValidationReport report)
        {
            if (!TryReadRequiredString(row, "Id", report, out var id)
                || !TryReadRequiredString(row, "DisplayName", report, out var displayName)
                || !TryReadEnum(row, "CardKind", report, out CardKind cardKind)
                || !TryReadInt(row, "Cost", report, out var cost)
                || !TryReadInt(row, "SummonMinRange", report, out var summonMinRange)
                || !TryReadInt(row, "SummonMaxRange", report, out var summonMaxRange)
                || !TryReadColor(row, "Tint", report, out var tint))
            {
                return null;
            }

            return new CardRow
            {
                Id = id,
                DisplayName = displayName,
                Description = row.Get("Description"),
                CardKind = cardKind,
                Cost = cost,
                ActionId = row.Get("ActionId"),
                SummonEntityId = row.Get("SummonEntityId"),
                SummonMinRange = summonMinRange,
                SummonMaxRange = summonMaxRange,
                Tint = tint,
            };
        }

        private static CardPieceVisualRow ParseCardPieceVisualRow(RowAccessor row, TactiRogueExcelValidationReport report)
        {
            if (!TryReadRequiredString(row, "Id", report, out var id)
                || !TryReadRequiredString(row, "ModelKey", report, out var modelKey)
                || !TryReadRequiredString(row, "CardArtKey", report, out var cardArtKey)
                || !TryReadRequiredString(row, "BackArtKey", report, out var backArtKey)
                || !TryReadFloat(row, "IdleTiltAngle", report, out var idleTiltAngle)
                || !TryReadFloat(row, "DefaultScale", report, out var defaultScale)
                || !TryReadFloat(row, "YOffset", report, out var yOffset))
            {
                return null;
            }

            var defaultRotation = UnitPresentationView.DefaultRotationFromIdleTilt(idleTiltAngle);
            if (!TryReadOptionalFloat(row, "DefaultRotationX", defaultRotation.x, report, out var defaultRotationX)
                || !TryReadOptionalFloat(row, "DefaultRotationY", defaultRotation.y, report, out var defaultRotationY)
                || !TryReadOptionalFloat(row, "DefaultRotationZ", defaultRotation.z, report, out var defaultRotationZ))
            {
                return null;
            }

            return new CardPieceVisualRow
            {
                Id = id,
                ModelKey = modelKey,
                CardArtKey = cardArtKey,
                BackArtKey = backArtKey,
                IdleTiltAngle = idleTiltAngle,
                DefaultRotationX = defaultRotationX,
                DefaultRotationY = defaultRotationY,
                DefaultRotationZ = defaultRotationZ,
                DefaultScale = defaultScale,
                YOffset = yOffset,
                FrameModelKey = row.Get("FrameModelKey"),
                FrameMaterialKey = row.Get("FrameMaterialKey"),
                IdleMotionKey = row.Get("IdleMotionKey"),
                MoveMotionKey = row.Get("MoveMotionKey"),
                AttackMotionKey = row.Get("AttackMotionKey"),
                HitMotionKey = row.Get("HitMotionKey"),
                DeathMotionKey = row.Get("DeathMotionKey"),
                SpawnMotionKey = row.Get("SpawnMotionKey"),
            };
        }

        private static ScenarioRow ParseScenarioRow(RowAccessor row, TactiRogueExcelValidationReport report)
        {
            if (!TryReadRequiredString(row, "Id", report, out var id)
                || !TryReadRequiredString(row, "DisplayName", report, out var displayName)
                || !TryReadInt(row, "DisplayOrder", report, out var displayOrder)
                || !TryReadInt(row, "Width", report, out var width)
                || !TryReadInt(row, "Height", report, out var height)
                || !TryReadInt(row, "RandomSeed", report, out var randomSeed)
                || !TryReadInt(row, "StartingMana", report, out var startingMana)
                || !TryReadInt(row, "MaxMana", report, out var maxMana)
                || !TryReadInt(row, "CardsPerTurn", report, out var cardsPerTurn))
            {
                return null;
            }

            return new ScenarioRow
            {
                Id = id,
                DisplayName = displayName,
                Description = row.Get("Description"),
                DisplayOrder = displayOrder,
                Width = width,
                Height = height,
                RandomSeed = randomSeed,
                StartingMana = startingMana,
                MaxMana = maxMana,
                CardsPerTurn = cardsPerTurn,
            };
        }

        private static ScenarioSpawnRow ParseScenarioSpawnRow(RowAccessor row, TactiRogueExcelValidationReport report)
        {
            if (!TryReadRequiredString(row, "ScenarioId", report, out var scenarioId)
                || !TryReadInt(row, "Order", report, out var order)
                || !TryReadRequiredString(row, "TemplateId", report, out var templateId)
                || !TryReadEnum(row, "Team", report, out TeamId team)
                || !TryReadInt(row, "X", report, out var x)
                || !TryReadInt(row, "Y", report, out var y))
            {
                return null;
            }

            return new ScenarioSpawnRow
            {
                ScenarioId = scenarioId,
                Order = order,
                TemplateId = templateId,
                Team = team,
                X = x,
                Y = y,
            };
        }

        private static ScenarioDeckRow ParseScenarioDeckRow(RowAccessor row, TactiRogueExcelValidationReport report)
        {
            if (!TryReadRequiredString(row, "ScenarioId", report, out var scenarioId)
                || !TryReadInt(row, "Order", report, out var order)
                || !TryReadRequiredString(row, "CardId", report, out var cardId))
            {
                return null;
            }

            return new ScenarioDeckRow
            {
                ScenarioId = scenarioId,
                Order = order,
                CardId = cardId,
            };
        }

        private static ScenarioVoidCellRow ParseScenarioVoidCellRow(RowAccessor row, TactiRogueExcelValidationReport report)
        {
            if (!TryReadRequiredString(row, "ScenarioId", report, out var scenarioId)
                || !TryReadInt(row, "Order", report, out var order)
                || !TryReadInt(row, "X", report, out var x)
                || !TryReadInt(row, "Y", report, out var y))
            {
                return null;
            }

            return new ScenarioVoidCellRow
            {
                ScenarioId = scenarioId,
                Order = order,
                X = x,
                Y = y,
            };
        }

        private static bool TryReadRequiredString(RowAccessor row, string columnName, TactiRogueExcelValidationReport report, out string value)
        {
            value = row.Get(columnName)?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            report.AddError($"{row.Location}: '{columnName}' is required.");
            return false;
        }

        private static bool TryReadInt(RowAccessor row, string columnName, TactiRogueExcelValidationReport report, out int value)
        {
            var raw = row.Get(columnName)?.Trim();
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            report.AddError($"{row.Location}: '{columnName}' must be an integer, got '{raw}'.");
            return false;
        }

        private static bool TryReadFloat(RowAccessor row, string columnName, TactiRogueExcelValidationReport report, out float value)
        {
            var raw = row.Get(columnName)?.Trim();
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            report.AddError($"{row.Location}: '{columnName}' must be a number, got '{raw}'.");
            return false;
        }

        private static bool TryReadOptionalFloat(RowAccessor row, string columnName, float defaultValue, TactiRogueExcelValidationReport report, out float value)
        {
            var raw = row.Get(columnName)?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                value = defaultValue;
                return true;
            }

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            report.AddError($"{row.Location}: '{columnName}' must be a number, got '{raw}'.");
            return false;
        }

        private static bool TryReadBool(RowAccessor row, string columnName, TactiRogueExcelValidationReport report, out bool value)
        {
            var raw = row.Get(columnName)?.Trim();
            if (bool.TryParse(raw, out value))
            {
                return true;
            }

            report.AddError($"{row.Location}: '{columnName}' must be true/false, got '{raw}'.");
            return false;
        }

        private static bool TryReadColor(RowAccessor row, string columnName, TactiRogueExcelValidationReport report, out Color value)
        {
            var raw = row.Get(columnName)?.Trim();
            if (TactiRogueExcelShared.TryParseColor(raw, out value))
            {
                return true;
            }

            report.AddError($"{row.Location}: '{columnName}' must be #RRGGBB or #RRGGBBAA, got '{raw}'.");
            return false;
        }

        private static bool TryReadEnum<TEnum>(RowAccessor row, string columnName, TactiRogueExcelValidationReport report, out TEnum value)
            where TEnum : struct
        {
            var raw = row.Get(columnName)?.Trim();
            if (!string.IsNullOrWhiteSpace(raw) && Enum.TryParse(raw, false, out value))
            {
                return true;
            }

            report.AddError($"{row.Location}: '{columnName}' must be a valid {typeof(TEnum).Name} name, got '{raw}'.");
            value = default;
            return false;
        }

        private static void ValidateDuplicates<TRow>(IEnumerable<TRow> rows, Func<TRow, string> selector, string sheetName, TactiRogueExcelValidationReport report)
        {
            foreach (var duplicate in rows
                         .Where(row => !string.IsNullOrWhiteSpace(selector(row)))
                         .GroupBy(selector, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1)
                         .Select(group => group.Key))
            {
                report.AddError($"Sheet '{sheetName}' has duplicated id '{duplicate}'.");
            }
        }

        private static void ValidateOrderedChildren<TRow>(IEnumerable<TRow> rows, Func<TRow, string> selector, string sheetName, TactiRogueExcelValidationReport report)
        {
            foreach (var duplicate in rows
                         .Where(row => !string.IsNullOrWhiteSpace(selector(row)))
                         .GroupBy(selector, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1)
                         .Select(group => group.Key))
            {
                report.AddError($"Sheet '{sheetName}' has duplicated order key '{duplicate}'.");
            }
        }

        private static void ValidateReference(string sheetName, string rowId, string fieldName, string value, ISet<string> validIds, TactiRogueExcelValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!validIds.Contains(value))
            {
                report.AddError($"Sheet '{sheetName}' row '{rowId}' references missing {fieldName} '{value}'.");
            }
        }

        private static void ValidateRequiredReference(string sheetName, string rowId, string fieldName, string value, ISet<string> validIds, TactiRogueExcelValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                report.AddError($"Sheet '{sheetName}' row '{rowId}' requires {fieldName}.");
                return;
            }

            if (!validIds.Contains(value))
            {
                report.AddError($"Sheet '{sheetName}' row '{rowId}' references missing {fieldName} '{value}'.");
            }
        }

        private static BoardVisualRow ParseBoardVisualRow(RowAccessor row, TactiRogueExcelValidationReport report)
        {
            if (!TryReadRequiredString(row, "Id", report, out var id)
                || !TryReadFloat(row, "CellSize", report, out var cellSize)
                || !TryReadFloat(row, "CellGap", report, out var cellGap)
                || !TryReadFloat(row, "CellHeight", report, out var cellHeight))
            {
                return null;
            }

            return new BoardVisualRow
            {
                Id = id,
                CellSize = cellSize,
                CellGap = cellGap,
                CellHeight = cellHeight,
            };
        }

        private sealed class SheetAccessor
        {
            private readonly Dictionary<string, int> _headerMap = new Dictionary<string, int>(StringComparer.Ordinal);

            public SheetAccessor(TactiRogueExcelSheet sheet, IReadOnlyList<string> requiredHeaders, TactiRogueExcelValidationReport report)
            {
                Sheet = sheet;
                RequiredHeaders = requiredHeaders;

                if (sheet.Rows.Count == 0)
                {
                    report.AddError($"Sheet '{sheet.Name}' is missing a header row.");
                    return;
                }

                var headerRow = sheet.Rows[0];
                for (var columnIndex = 0; columnIndex < headerRow.Count; columnIndex++)
                {
                    var header = headerRow[columnIndex].Value?.Trim();
                    if (string.IsNullOrWhiteSpace(header))
                    {
                        continue;
                    }

                    if (_headerMap.ContainsKey(header))
                    {
                        report.AddError($"Sheet '{sheet.Name}' has duplicated header '{header}'.");
                        continue;
                    }

                    _headerMap[header] = columnIndex;
                }

                foreach (var requiredHeader in requiredHeaders)
                {
                    if (!_headerMap.ContainsKey(requiredHeader))
                    {
                        report.AddError($"Sheet '{sheet.Name}' is missing required column '{requiredHeader}'.");
                    }
                }
            }

            public TactiRogueExcelSheet Sheet { get; }
            public IReadOnlyList<string> RequiredHeaders { get; }

            public IEnumerable<RowAccessor> DataRows()
            {
                for (var rowIndex = 1; rowIndex < Sheet.Rows.Count; rowIndex++)
                {
                    yield return new RowAccessor(this, rowIndex);
                }
            }

            public void ValidateCells(TactiRogueExcelValidationReport report)
            {
                if (Sheet.HasMergedCells)
                {
                    report.AddError($"Sheet '{Sheet.Name}' contains merged cells, which are not supported.");
                }

                for (var rowIndex = 0; rowIndex < Sheet.Rows.Count; rowIndex++)
                {
                    for (var columnIndex = 0; columnIndex < Sheet.Rows[rowIndex].Count; columnIndex++)
                    {
                        if (Sheet.Rows[rowIndex][columnIndex].HasFormula)
                        {
                            report.AddError($"Sheet '{Sheet.Name}' cell {columnIndex + 1},{rowIndex + 1} contains a formula, which is not supported.");
                        }
                    }
                }
            }

            public string GetCellValue(int rowIndex, string columnName)
            {
                if (!_headerMap.TryGetValue(columnName, out var columnIndex))
                {
                    return string.Empty;
                }

                var row = Sheet.Rows[rowIndex];
                return columnIndex >= 0 && columnIndex < row.Count ? row[columnIndex].Value ?? string.Empty : string.Empty;
            }
        }

        private readonly struct RowAccessor
        {
            private readonly SheetAccessor _sheet;
            private readonly int _rowIndex;

            public RowAccessor(SheetAccessor sheet, int rowIndex)
            {
                _sheet = sheet;
                _rowIndex = rowIndex;
            }

            public string Location => $"Sheet '{_sheet.Sheet.Name}' row {_rowIndex + 1}";

            public bool IsEmpty
            {
                get
                {
                    foreach (var header in _sheet.RequiredHeaders)
                    {
                        if (!string.IsNullOrWhiteSpace(Get(header)))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public string Get(string columnName)
            {
                return _sheet.GetCellValue(_rowIndex, columnName);
            }
        }
    }

    public static class TactiRogueExcelImporter
    {
        public static TactiRogueExcelValidationReport ImportWorkbookToGameData(string workbookPath = null)
        {
            if (!TactiRogueExcelValidator.TryReadWorkbook(workbookPath, out var workbookData, out var report))
            {
                return report;
            }

            var bundle = BuildImportBundle(workbookData);
            WriteImportBundle(bundle);
            return report;
        }

        public static TactiRogueExcelImportBundle BuildImportBundle(TactiRogueExcelWorkbookData workbookData)
        {
            var moveProfiles = workbookData.MoveProfileRows.ToDictionary(
                row => row.Id,
                row => new MoveProfile
                {
                    UseSeparateMovePhase = row.UseSeparateMovePhase,
                    MoveRange = row.MoveRange,
                    MoveType = row.MoveType,
                    AllowStayInPlace = row.AllowStayInPlace,
                    AllowDiagonalMove = row.AllowDiagonalMove,
                    CanPassThroughUnits = row.CanPassThroughUnits,
                    CanPassThroughBuildings = row.CanPassThroughBuildings,
                    RequirePath = row.RequirePath,
                },
                StringComparer.Ordinal);

            var entityStatuses = workbookData.EntityStatusRows
                .GroupBy(row => row.EntityId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(item => item.Order).Select(item => item.StatusId).ToArray(),
                    StringComparer.Ordinal);

            var database = ScriptableObject.CreateInstance<TactiRogueContentDatabase>();
            database.name = "TactiRogueContentDatabase";
            var boardVisual = workbookData.BoardVisualRows.FirstOrDefault() ?? BoardVisualRow.CreateDefault();
            database.BoardCellSize = boardVisual.CellSize;
            database.BoardCellGap = boardVisual.CellGap;
            database.BoardCellHeight = boardVisual.CellHeight;
            database.StatusTemplates = workbookData.StatusRows.Select(CreateStatusAsset).ToArray();
            database.ActionDefinitions = workbookData.ActionRows.Select(CreateActionAsset).ToArray();
            database.EntityTemplates = workbookData.EntityRows.Select(row => CreateEntityAsset(row, moveProfiles, entityStatuses)).ToArray();
            database.CardTemplates = workbookData.CardRows.Select(CreateCardAsset).ToArray();
            database.IntentDefinitions = workbookData.IntentRows.Select(CreateIntentAsset).ToArray();
            database.CardPieceVisualDefinitions = workbookData.CardPieceVisualRows.Select(CreateCardPieceVisualAsset).ToArray();

            var bundle = new TactiRogueExcelImportBundle
            {
                Database = database,
                Scenarios = workbookData.ScenarioRows
                    .OrderBy(row => row.DisplayOrder)
                    .Select(row => CreateScenarioDefinition(row, workbookData))
                    .ToList(),
            };

            return bundle;
        }

        private static void WriteImportBundle(TactiRogueExcelImportBundle bundle)
        {
            TactiRogueExcelPaths.EnsureFolder("Assets/Resources");
            TactiRogueExcelPaths.EnsureFolder("Assets/Resources/TactiRogue");
            TactiRogueExcelPaths.EnsureFolder(TactiRogueExcelPaths.ContentRoot);
            TactiRogueExcelPaths.EnsureFolder(TactiRogueExcelPaths.GeneratedRoot);
            TactiRogueExcelPaths.EnsureFolder("Assets/TactiRogue");
            TactiRogueExcelPaths.EnsureFolder("Assets/TactiRogue/DataAuthoring");

            foreach (var assetPath in AssetDatabase.FindAssets(string.Empty, new[] { TactiRogueExcelPaths.GeneratedRoot })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(path => !AssetDatabase.IsValidFolder(path))
                         .ToList())
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            foreach (var jsonPath in Directory.Exists(TactiRogueExcelPaths.ScenarioRoot)
                         ? Directory.GetFiles(TactiRogueExcelPaths.ScenarioRoot, "*.json", SearchOption.TopDirectoryOnly)
                         : Array.Empty<string>())
            {
                File.Delete(jsonPath);
            }

            bundle.Database.StatusTemplates = bundle.Database.StatusTemplates.Select(template => PersistGeneratedAsset(template, $"Status_{template.Id}.asset")).ToArray();
            bundle.Database.ActionDefinitions = bundle.Database.ActionDefinitions.Select(definition => PersistGeneratedAsset(definition, $"Action_{definition.Id}.asset")).ToArray();
            bundle.Database.EntityTemplates = bundle.Database.EntityTemplates.Select(template => PersistGeneratedAsset(template, $"Entity_{template.Id}.asset")).ToArray();
            bundle.Database.CardTemplates = bundle.Database.CardTemplates.Select(template => PersistGeneratedAsset(template, $"Card_{template.Id}.asset")).ToArray();
            bundle.Database.IntentDefinitions = bundle.Database.IntentDefinitions.Select(definition => PersistGeneratedAsset(definition, $"Intent_{definition.Id}.asset")).ToArray();
            bundle.Database.CardPieceVisualDefinitions = bundle.Database.CardPieceVisualDefinitions.Select(definition => PersistGeneratedAsset(definition, $"CardPieceVisual_{definition.Id}.asset")).ToArray();

            if (AssetDatabase.LoadAssetAtPath<TactiRogueContentDatabase>(TactiRogueExcelPaths.DatabaseAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(TactiRogueExcelPaths.DatabaseAssetPath);
            }

            AssetDatabase.CreateAsset(bundle.Database, TactiRogueExcelPaths.DatabaseAssetPath);
            WriteScenarioJson(bundle.Scenarios);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TactiRogueContentProvider.ResetCache();
            TactiRogueScenarioRepository.ResetCache();
        }

        private static void WriteScenarioJson(IEnumerable<ScenarioDefinition> scenarios)
        {
            Directory.CreateDirectory(TactiRogueExcelPaths.ToAbsolutePath(TactiRogueExcelPaths.ScenarioRoot));
            foreach (var scenario in scenarios)
            {
                var filePath = TactiRogueExcelPaths.ToAbsolutePath($"{TactiRogueExcelPaths.ScenarioRoot}/{scenario.Id}.json");
                File.WriteAllText(filePath, JsonUtility.ToJson(scenario, true), new System.Text.UTF8Encoding(false));
            }
        }

        private static T PersistGeneratedAsset<T>(T asset, string fileName) where T : ScriptableObject
        {
            AssetDatabase.CreateAsset(asset, $"{TactiRogueExcelPaths.GeneratedRoot}/{fileName}");
            return asset;
        }

        private static StatusTemplate CreateStatusAsset(StatusRow row)
        {
            var asset = ScriptableObject.CreateInstance<StatusTemplate>();
            asset.name = $"Status_{row.Id}";
            asset.Id = row.Id;
            asset.DisplayName = row.DisplayName;
            asset.Description = row.Description;
            asset.GrantedKeyword = row.GrantedKeyword;
            asset.AttackModifier = row.AttackModifier;
            asset.PushModifier = row.PushModifier;
            asset.ActionsGrantedOnApply = row.ActionsGrantedOnApply;
            asset.DefaultDuration = row.DefaultDuration;
            asset.TickPhase = row.TickPhase;
            asset.Tint = row.Tint;
            return asset;
        }

        private static ActionDefinition CreateActionAsset(ActionRow row)
        {
            var asset = ScriptableObject.CreateInstance<ActionDefinition>();
            asset.name = $"Action_{row.Id}";
            asset.Id = row.Id;
            asset.DisplayName = row.DisplayName;
            asset.Description = row.Description;
            asset.ActionKind = row.ActionKind;
            asset.TargetMode = row.TargetMode;
            asset.TargetFilter = row.TargetFilter;
            asset.CanTargetEmptyCell = row.CanTargetEmptyCell;
            asset.MinRange = row.MinRange;
            asset.MaxRange = row.MaxRange;
            asset.UseActorAttackValue = row.UseActorAttackValue;
            asset.DamageAmount = row.DamageAmount;
            asset.HealAmount = row.HealAmount;
            asset.PushForce = row.PushForce;
            asset.Radius = row.Radius;
            asset.MoveRange = row.MoveRange;
            asset.MovePattern = row.MovePattern;
            asset.MoveBeforeEffect = row.MoveBeforeEffect;
            asset.ExtraActionsGranted = row.ExtraActionsGranted;
            asset.ApplyStatusId = row.ApplyStatusId;
            asset.OverrideStatusDuration = row.OverrideStatusDuration;
            asset.SummonEntityId = row.SummonEntityId;
            asset.ConsumeActorAction = row.ConsumeActorAction;
            asset.AllowDiagonalTargeting = row.AllowDiagonalTargeting;
            asset.SkipMovePhase = row.SkipMovePhase;
            asset.AuthoringVersion = 2;
            return asset;
        }

        private static EntityTemplate CreateEntityAsset(EntityRow row, IReadOnlyDictionary<string, MoveProfile> moveProfiles, IReadOnlyDictionary<string, string[]> entityStatuses)
        {
            var asset = ScriptableObject.CreateInstance<EntityTemplate>();
            asset.name = $"Entity_{row.Id}";
            asset.Id = row.Id;
            asset.DisplayName = row.DisplayName;
            asset.ShortLabel = row.ShortLabel;
            asset.Description = row.Description;
            asset.EntityKind = row.EntityKind;
            asset.DefaultTeam = row.DefaultTeam;
            asset.MaxHp = row.MaxHp;
            asset.Attack = row.Attack;
            asset.PushBonus = row.PushBonus;
            asset.ActionId = row.ActionId;
            asset.IntentDefinitionId = row.IntentDefinitionId;
            asset.CanAct = row.CanAct;
            asset.BlocksMovement = row.BlocksMovement;
            asset.OccupiesCell = row.OccupiesCell;
            asset.Targetable = row.Targetable;
            asset.MoveProfile = moveProfiles[row.MoveProfileId].Clone();
            asset.StartingStatusIds = entityStatuses.TryGetValue(row.Id, out var statusIds) ? statusIds : Array.Empty<string>();
            asset.Tint = row.Tint;
            asset.AuthoringVersion = 1;
            return asset;
        }

        private static IntentDefinition CreateIntentAsset(IntentRow row)
        {
            var asset = ScriptableObject.CreateInstance<IntentDefinition>();
            asset.name = $"Intent_{row.Id}";
            asset.Id = row.Id;
            asset.DisplayName = row.DisplayName;
            asset.Description = row.Description;
            asset.IntentKind = row.IntentKind;
            asset.ActionId = row.ActionId;
            asset.AcquireRange = row.AcquireRange;
            asset.PreferCommander = row.PreferCommander;
            asset.TargetingMode = row.TargetingMode;
            asset.RevalidationPolicy = row.RevalidationPolicy;
            asset.FallbackMode = row.FallbackMode;
            asset.Tint = row.Tint;
            return asset;
        }

        private static CardTemplate CreateCardAsset(CardRow row)
        {
            var asset = ScriptableObject.CreateInstance<CardTemplate>();
            asset.name = $"Card_{row.Id}";
            asset.Id = row.Id;
            asset.DisplayName = row.DisplayName;
            asset.Description = row.Description;
            asset.CardKind = row.CardKind;
            asset.Cost = row.Cost;
            asset.ActionId = row.ActionId;
            asset.SummonEntityId = row.SummonEntityId;
            asset.SummonMinRange = row.SummonMinRange;
            asset.SummonMaxRange = row.SummonMaxRange;
            asset.Tint = row.Tint;
            return asset;
        }

        private static CardPieceVisualDefinition CreateCardPieceVisualAsset(CardPieceVisualRow row)
        {
            var asset = ScriptableObject.CreateInstance<CardPieceVisualDefinition>();
            asset.name = $"CardPieceVisual_{row.Id}";
            asset.Id = row.Id;
            asset.ModelKey = row.ModelKey;
            asset.CardArtKey = row.CardArtKey;
            asset.BackArtKey = row.BackArtKey;
            asset.IdleTiltAngle = row.IdleTiltAngle;
            asset.DefaultRotationEuler = new Vector3(row.DefaultRotationX, row.DefaultRotationY, row.DefaultRotationZ);
            asset.DefaultScale = row.DefaultScale;
            asset.YOffset = row.YOffset;
            asset.FrameModelKey = row.FrameModelKey;
            asset.FrameMaterialKey = row.FrameMaterialKey;
            asset.IdleMotionKey = row.IdleMotionKey;
            asset.MoveMotionKey = row.MoveMotionKey;
            asset.AttackMotionKey = row.AttackMotionKey;
            asset.HitMotionKey = row.HitMotionKey;
            asset.DeathMotionKey = row.DeathMotionKey;
            asset.SpawnMotionKey = row.SpawnMotionKey;
            return asset;
        }

        private static ScenarioDefinition CreateScenarioDefinition(ScenarioRow row, TactiRogueExcelWorkbookData workbookData)
        {
            return new ScenarioDefinition
            {
                Id = row.Id,
                DisplayName = row.DisplayName,
                Description = row.Description,
                DisplayOrder = row.DisplayOrder,
                Width = row.Width,
                Height = row.Height,
                RandomSeed = row.RandomSeed,
                StartingMana = row.StartingMana,
                MaxMana = row.MaxMana,
                CardsPerTurn = row.CardsPerTurn,
                StartingDeck = workbookData.ScenarioDeckRows
                    .Where(item => item.ScenarioId == row.Id)
                    .OrderBy(item => item.Order)
                    .Select(item => item.CardId)
                    .ToArray(),
                VoidCells = workbookData.ScenarioVoidCellRows
                    .Where(item => item.ScenarioId == row.Id)
                    .OrderBy(item => item.Order)
                    .Select(item => new GridPosition(item.X, item.Y).ToString())
                    .ToArray(),
                Spawns = workbookData.ScenarioSpawnRows
                    .Where(item => item.ScenarioId == row.Id)
                    .OrderBy(item => item.Order)
                    .Select(item => new ScenarioEntitySpawn
                    {
                        TemplateId = item.TemplateId,
                        Team = item.Team.ToString(),
                        X = item.X,
                        Y = item.Y,
                    })
                    .ToArray(),
            };
        }
    }

    public static class TactiRogueExcelMenu
    {
        [MenuItem("Tools/TactiRogue/Excel/Export Current Data To Excel")]
        public static void ExportCurrentDataToExcel()
        {
            try
            {
                TactiRogueExcelExporter.ExportCurrentDataToWorkbook();
                EditorUtility.DisplayDialog("TactiRogue", $"Workbook exported to {TactiRogueExcelPaths.WorkbookAssetPath}.", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("TactiRogue", $"Export failed.\n{exception.Message}", "OK");
            }
        }

        [MenuItem("Tools/TactiRogue/Excel/Validate Excel Workbook")]
        public static void ValidateExcelWorkbook()
        {
            var report = TactiRogueExcelValidator.ValidateWorkbook();
            if (report.IsValid)
            {
                EditorUtility.DisplayDialog("TactiRogue", $"Workbook valid: {report.WorkbookPath}", "OK");
                return;
            }

            Debug.LogError(report.ToDisplayString(64));
            EditorUtility.DisplayDialog("TactiRogue", $"Workbook validation failed.\n{report.ToDisplayString()}", "OK");
        }

        [MenuItem("Tools/TactiRogue/Excel/Import Excel To Game Data")]
        public static void ImportExcelToGameData()
        {
            try
            {
                var report = TactiRogueExcelImporter.ImportWorkbookToGameData();
                if (report.IsValid)
                {
                    EditorUtility.DisplayDialog("TactiRogue", "Workbook imported and game data regenerated.", "OK");
                    return;
                }

                Debug.LogError(report.ToDisplayString(64));
                EditorUtility.DisplayDialog("TactiRogue", $"Import failed.\n{report.ToDisplayString()}", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("TactiRogue", $"Import failed.\n{exception.Message}", "OK");
            }
        }
    }
}
