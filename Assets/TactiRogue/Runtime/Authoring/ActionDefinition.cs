using UnityEngine;

namespace TactiRogue
{
    [CreateAssetMenu(fileName = "ActionDefinition", menuName = "TactiRogue/Action Definition")]
    public sealed class ActionDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public ActionKind ActionKind;
        public ActionTargetMode TargetMode;
        public ActionTargetFilter TargetFilter;
        public bool CanTargetEmptyCell;
        public int MinRange;
        public int MaxRange = 1;
        public bool UseActorAttackValue = true;
        public int DamageAmount;
        public int HealAmount;
        public int PushForce;
        public int Radius;
        public int MoveRange;
        public MovementPattern MovePattern;
        public bool MoveBeforeEffect = true;
        public int ExtraActionsGranted;
        public string ApplyStatusId;
        public int OverrideStatusDuration = -1;
        public string SummonEntityId;
        public bool ConsumeActorAction = true;
        public bool AllowDiagonalTargeting = true;
        public bool SkipMovePhase;
        [HideInInspector] public int AuthoringVersion = 2;
    }
}
