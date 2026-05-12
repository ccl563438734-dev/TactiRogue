using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TactiRogue
{
    public static class TactiRogueScenarioRepository
    {
        private const string ScenarioResourcePath = "TactiRogue/Scenarios";
        private static IReadOnlyList<ScenarioDefinition> _cachedScenarios;

        public static void ResetCache()
        {
            _cachedScenarios = null;
        }

        public static IReadOnlyList<ScenarioDefinition> LoadAll()
        {
            if (_cachedScenarios != null)
            {
                return _cachedScenarios;
            }

            var scenarios = Resources.LoadAll<TextAsset>(ScenarioResourcePath)
                .Select(asset => JsonUtility.FromJson<ScenarioDefinition>(asset.text))
                .Where(scenario => scenario != null && !string.IsNullOrWhiteSpace(scenario.Id))
                .OrderBy(scenario => scenario.DisplayOrder)
                .ToList();

            if (scenarios.Count == 0)
            {
                scenarios = CreateBuiltInFallbacks().OrderBy(scenario => scenario.DisplayOrder).ToList();
            }

            _cachedScenarios = scenarios;
            return _cachedScenarios;
        }

        private static IEnumerable<ScenarioDefinition> CreateBuiltInFallbacks()
        {
            yield return new ScenarioDefinition
            {
                Id = "collision_tutorial",
                DisplayName = "Collision Tutorial",
                Description = "Practice slamming enemies into structures and walls.",
                DisplayOrder = 0,
                Width = 7,
                Height = 6,
                StartingDeck = DefaultDeck(),
                Spawns = new[]
                {
                    Spawn("commander_core", TeamId.Player, 1, 2),
                    Spawn("stone_pillar", TeamId.Neutral, 4, 2),
                    Spawn("hunter", TeamId.Enemy, 5, 2),
                    Spawn("bombardier", TeamId.Enemy, 5, 4),
                    Spawn("charger", TeamId.Enemy, 5, 1),
                },
            };

            yield return new ScenarioDefinition
            {
                Id = "frontline_defense",
                DisplayName = "Frontline Defense",
                Description = "Protect the commander and test taunt coverage.",
                DisplayOrder = 1,
                Width = 8,
                Height = 6,
                StartingDeck = DefaultDeck(),
                Spawns = new[]
                {
                    Spawn("commander_core", TeamId.Player, 1, 2),
                    Spawn("stone_pillar", TeamId.Neutral, 3, 2),
                    Spawn("stone_pillar", TeamId.Neutral, 4, 4),
                    Spawn("hunter", TeamId.Enemy, 6, 2),
                    Spawn("hunter", TeamId.Enemy, 6, 1),
                    Spawn("bombardier", TeamId.Enemy, 6, 4),
                    Spawn("anchor_warden", TeamId.Enemy, 6, 5),
                },
            };

            yield return new ScenarioDefinition
            {
                Id = "mixed_intents",
                DisplayName = "Mixed Intents",
                Description = "Handle directional, area, and lock-on threats together.",
                DisplayOrder = 2,
                Width = 9,
                Height = 7,
                StartingDeck = DefaultDeck(),
                Spawns = new[]
                {
                    Spawn("commander_core", TeamId.Player, 1, 3),
                    Spawn("stone_pillar", TeamId.Neutral, 4, 3),
                    Spawn("stone_pillar", TeamId.Neutral, 4, 5),
                    Spawn("charger", TeamId.Enemy, 7, 3),
                    Spawn("bombardier", TeamId.Enemy, 7, 5),
                    Spawn("hunter", TeamId.Enemy, 7, 1),
                    Spawn("anchor_warden", TeamId.Enemy, 7, 6),
                },
            };
        }

        private static string[] DefaultDeck()
        {
            return new[]
            {
                "call_guardian",
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
            };
        }

        private static ScenarioEntitySpawn Spawn(string templateId, TeamId team, int x, int y)
        {
            return new ScenarioEntitySpawn
            {
                TemplateId = templateId,
                Team = team.ToString(),
                X = x,
                Y = y,
            };
        }
    }
}
