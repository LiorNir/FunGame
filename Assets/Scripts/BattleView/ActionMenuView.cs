using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleView
{
    /// <summary>
    /// A small pop-up list of the actions available for whatever the player clicked (a child of the HUD prefab). The
    /// blocker is a transparent full-screen button that catches clicks outside the menu; the panel holds the option
    /// buttons, which are copies of the option prefab.
    /// </summary>
    public class ActionMenuView : MonoBehaviour
    {
        public readonly struct Option
        {
            public string Label { get; }
            public bool Enabled { get; }
            public Action OnChosen { get; }

            public Option(string label, bool enabled, Action onChosen)
            {
                Label = label;
                Enabled = enabled;
                OnChosen = onChosen;
            }
        }

        [SerializeField] private Button _blocker;
        [SerializeField] private RectTransform _panel;
        [Tooltip("One of these is created per option. Its height and width set the size of the options.")]
        [SerializeField] private Button _optionPrefab;
        [SerializeField] private float _spacing = 8f;

        private RectTransform _canvasRect;

        /// <summary>Raised when the player clicks outside the menu without choosing anything.</summary>
        public event Action Dismissed;

        public bool IsOpen => _blocker != null && _blocker.gameObject.activeSelf;

        private void Awake()
        {
            _canvasRect = GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
            _blocker.onClick.AddListener(() =>
            {
                Close();
                Dismissed?.Invoke();
            });
            _blocker.gameObject.SetActive(false);
        }

        /// <param name="above">True: the menu's bottom edge sits at <paramref name="screenPosition"/> (it opens upward, for
        /// example above a hand card); false: the menu is centred there.</param>
        public void Show(Vector2 screenPosition, IReadOnlyList<Option> options, bool above = false)
        {
            foreach (Transform child in _panel)
            {
                Destroy(child.gameObject);
            }

            var optionSize = ((RectTransform)_optionPrefab.transform).sizeDelta;
            var height = options.Count * optionSize.y + (options.Count + 1) * _spacing;
            _panel.sizeDelta = new Vector2(optionSize.x + 2f * _spacing, height);

            for (var index = 0; index < options.Count; index++)
            {
                var option = options[index];
                var button = Instantiate(_optionPrefab, _panel);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, height * 0.5f - _spacing - optionSize.y * 0.5f - index * (optionSize.y + _spacing));
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = option.Label;
                }

                button.interactable = option.Enabled;
                var onChosen = option.OnChosen;
                button.onClick.AddListener(() =>
                {
                    Close();
                    onChosen?.Invoke();
                });
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPosition, null, out var local);
            var halfCanvas = _canvasRect.rect.size * 0.5f;
            var halfPanel = _panel.sizeDelta * 0.5f;
            if (above)
            {
                local.y += halfPanel.y;
            }

            local.x = Mathf.Clamp(local.x, -halfCanvas.x + halfPanel.x, halfCanvas.x - halfPanel.x);
            local.y = Mathf.Clamp(local.y, -halfCanvas.y + halfPanel.y, halfCanvas.y - halfPanel.y);
            _panel.anchoredPosition = local;

            _blocker.gameObject.SetActive(true);
            _blocker.transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (_blocker != null)
            {
                _blocker.gameObject.SetActive(false);
            }
        }
    }
}
