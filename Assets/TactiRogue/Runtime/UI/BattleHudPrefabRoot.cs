using UnityEngine;
using UnityEngine.UI;

namespace TactiRogue
{
    public sealed class BattleHudPrefabRoot : MonoBehaviour
    {
        public Canvas Canvas;
        public CanvasScaler CanvasScaler;
        public GraphicRaycaster GraphicRaycaster;

        public RectTransform ScenarioButtonsContainer;
        public RectTransform HandContent;
        public RectTransform BoardViewport;

        public Text TurnText;
        public Text ManaText;
        public Text StateText;

        public Button DrawPileButton;
        public Text DrawPileButtonText;
        public Button DiscardPileButton;
        public Text DiscardPileButtonText;
        public Button EndTurnButton;
        public Button ResetButton;
        public Button CancelButton;

        public Text DetailTitleText;
        public Text DetailBodyText;
        public Button ActionButton;
        public Text ActionButtonText;
        public Text IntentText;
        public Text PreviewText;
        public Text LogText;
        public Text SnapshotText;

        public RectTransform PileViewerPanel;
        public Text PileViewerTitleText;
        public Text PileViewerBodyText;
        public Button PileViewerCloseButton;

        public Button ScenarioButtonPrefab;
        public CardButtonView HandCardPrefab;

        public bool HasRequiredReferences()
        {
            return Canvas != null
                   && CanvasScaler != null
                   && GraphicRaycaster != null
                   && ScenarioButtonsContainer != null
                   && ScenarioButtonsContainer.GetComponent<HorizontalLayoutGroup>() != null
                   && HandContent != null
                   && HandContent.GetComponent<HorizontalLayoutGroup>() != null
                   && BoardViewport != null
                   && TurnText != null
                   && ManaText != null
                   && StateText != null
                   && DrawPileButton != null
                   && DrawPileButtonText != null
                   && DiscardPileButton != null
                   && DiscardPileButtonText != null
                   && EndTurnButton != null
                   && ResetButton != null
                   && CancelButton != null
                   && DetailTitleText != null
                   && DetailBodyText != null
                   && ActionButton != null
                   && ActionButtonText != null
                   && IntentText != null
                   && PreviewText != null
                   && LogText != null
                   && SnapshotText != null
                   && PileViewerPanel != null
                   && PileViewerTitleText != null
                   && PileViewerBodyText != null
                   && PileViewerCloseButton != null
                   && ScenarioButtonPrefab != null
                   && HandCardPrefab != null
                   && HandCardPrefab.HasRequiredReferences();
        }

        private void Reset()
        {
            Canvas = GetComponent<Canvas>();
            CanvasScaler = GetComponent<CanvasScaler>();
            GraphicRaycaster = GetComponent<GraphicRaycaster>();
        }
    }
}
