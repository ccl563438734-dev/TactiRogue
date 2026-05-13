using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TactiRogue
{
    [CreateAssetMenu(fileName = "TactiRogueContentDatabase", menuName = "TactiRogue/Content Database")]
    public sealed class TactiRogueContentDatabase : ScriptableObject
    {
        public const float DefaultBoardCellSize = 1f;
        public const float DefaultBoardCellGap = 0.08f;
        public const float DefaultBoardCellHeight = 0.04f;
        private const float MinimumBoardCellVisualSize = 0.01f;

        public float BoardCellSize = DefaultBoardCellSize;
        public float BoardCellGap = DefaultBoardCellGap;
        public float BoardCellHeight = DefaultBoardCellHeight;
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

        public float GetBoardCellSize()
        {
            return BoardCellSize > 0f ? BoardCellSize : DefaultBoardCellSize;
        }

        public float GetBoardCellGap()
        {
            var cellSize = GetBoardCellSize();
            var maxGap = Mathf.Max(0f, cellSize - MinimumBoardCellVisualSize);
            return Mathf.Clamp(BoardCellGap, 0f, maxGap);
        }

        public float GetBoardCellVisualSize()
        {
            return Mathf.Max(MinimumBoardCellVisualSize, GetBoardCellSize() - GetBoardCellGap());
        }

        public float GetBoardCellHeight()
        {
            return BoardCellHeight > 0f ? BoardCellHeight : DefaultBoardCellHeight;
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
