using System;
using UnityEngine;

namespace TactiRogue
{
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
}
