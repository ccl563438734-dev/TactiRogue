using System;
using UnityEngine;
using UnityEngine.UI;

namespace TactiRogue
{
    public sealed class CardButtonView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _bodyText;

        public int CardInstanceId { get; private set; }
        public Button Button => _button;
        public Image Background => _background;
        public Text TitleText => _titleText;
        public Text BodyText => _bodyText;

        public void Initialize(int cardInstanceId, Font font, Action<int> onClicked)
        {
            CardInstanceId = cardInstanceId;
            EnsureReferences(font);
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onClicked?.Invoke(CardInstanceId));
        }

        public void SetVisual(string title, string body, Color color, bool selected, bool interactable)
        {
            _titleText.text = title;
            _bodyText.text = body;
            _background.color = selected ? Color.Lerp(color, Color.white, 0.25f) : color;
            _button.interactable = interactable;
        }

        public bool HasRequiredReferences()
        {
            return _button != null && _background != null && _titleText != null && _bodyText != null;
        }

        public void BindPrefabReferences(Button button, Image background, Text titleText, Text bodyText)
        {
            _button = button;
            _background = background;
            _titleText = titleText;
            _bodyText = bodyText;
        }

        private void EnsureReferences(Font font)
        {
            var rect = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            if (rect.sizeDelta == Vector2.zero)
            {
                rect.sizeDelta = new Vector2(170f, 144f);
            }

            _background ??= GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            _button ??= GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            _titleText ??= FindChildText("Title") ?? CreateCardText("Title", font, 16, TextAnchor.UpperCenter, Color.white, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.95f));
            _bodyText ??= FindChildText("Body") ?? CreateCardText("Body", font, 12, TextAnchor.UpperLeft, new Color(0.95f, 0.95f, 0.95f, 1f), new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.58f));

            if (_titleText.font == null)
            {
                _titleText.font = font;
            }

            if (_bodyText.font == null)
            {
                _bodyText.font = font;
            }
        }

        private Text FindChildText(string childName)
        {
            var child = transform.Find(childName);
            return child == null ? null : child.GetComponent<Text>();
        }

        private Text CreateCardText(string objectName, Font font, int fontSize, TextAnchor alignment, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var textGo = new GameObject(objectName, typeof(RectTransform));
            textGo.transform.SetParent(transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = objectName == "Body" ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            var textRect = text.rectTransform;
            textRect.anchorMin = anchorMin;
            textRect.anchorMax = anchorMax;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return text;
        }
    }
}
