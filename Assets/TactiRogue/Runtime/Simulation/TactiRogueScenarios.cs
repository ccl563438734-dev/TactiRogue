using System;

namespace TactiRogue
{
    [Serializable]
    public sealed class ScenarioEntitySpawn
    {
        public string TemplateId;
        public string Team;
        public int X;
        public int Y;
    }

    [Serializable]
    public sealed class ScenarioDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public int DisplayOrder;
        public int Width;
        public int Height;
        public int RandomSeed = 12345;
        public int StartingMana = 3;
        public int MaxMana = 3;
        public int CardsPerTurn = 5;
        public string[] VoidCells = Array.Empty<string>();
        public string[] StartingDeck = Array.Empty<string>();
        public ScenarioEntitySpawn[] Spawns = Array.Empty<ScenarioEntitySpawn>();
    }
}
