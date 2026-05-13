using UnityEngine;

namespace TactiRogue
{
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
}
