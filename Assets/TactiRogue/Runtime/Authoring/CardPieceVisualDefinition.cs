using UnityEngine;

namespace TactiRogue
{
    [CreateAssetMenu(fileName = "CardPieceVisualDefinition", menuName = "TactiRogue/Card Piece Visual Definition")]
    public sealed class CardPieceVisualDefinition : ScriptableObject
    {
        public string Id;
        public string ModelKey = "Assert/Model/sample";
        public string CardArtKey;
        public string BackArtKey = "Assert/Picture/鍗¤儗";
        public float IdleTiltAngle = 45f;
        public Vector3 DefaultRotationEuler = new Vector3(-45f, 0f, 0f);
        public float DefaultScale = 1f;
        public float YOffset;
        public string FrameModelKey;
        public string FrameMaterialKey;
        public string IdleMotionKey;
        public string MoveMotionKey;
        public string AttackMotionKey;
        public string HitMotionKey;
        public string DeathMotionKey;
        public string SpawnMotionKey;
    }
}
