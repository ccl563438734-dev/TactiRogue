using UnityEngine;

namespace TactiRogue
{
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
}
