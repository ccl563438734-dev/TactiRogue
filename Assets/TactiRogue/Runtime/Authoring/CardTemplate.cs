using UnityEngine;

namespace TactiRogue
{
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
}
