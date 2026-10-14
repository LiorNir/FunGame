using TMPro;
using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// The hover explanation box, part of the HUD prefab: a panel with a text that follows the mouse while something on
    /// the board or an enemy is hovered. It never takes clicks.
    /// </summary>
    public class TooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TextMeshProUGUI _text;
        [Tooltip("Distance from the mouse, in canvas pixels.")]
        [SerializeField] private Vector2 _offset = new Vector2(24f, -24f);

        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            _panel.gameObject.SetActive(false);
        }

        public void Show(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Hide();
                return;
            }

            _text.text = text;
            _panel.gameObject.SetActive(true);
            Follow();
        }

        public void Hide()
        {
            _panel.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_panel.gameObject.activeSelf)
            {
                Follow();
            }
        }

        // next to the mouse, flipped to the other side near the right or bottom edge of the screen
        private void Follow()
        {
            var scale = _canvas != null ? _canvas.scaleFactor : 1f;
            var mouse = (Vector2)Input.mousePosition;
            var size = _panel.rect.size * scale;
            var flipX = mouse.x + _offset.x * scale + size.x > Screen.width;
            var flipY = mouse.y + _offset.y * scale - size.y < 0f;
            _panel.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);
            var offset = new Vector2(flipX ? -_offset.x : _offset.x, flipY ? -_offset.y : _offset.y) * scale;
            _panel.position = mouse + offset;
        }
    }
}
