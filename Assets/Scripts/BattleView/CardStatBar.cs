using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleView
{
    /// <summary>
    /// One stat of a hand card as a bar: a background, a fill whose width is the share of the stat that is left (a
    /// recovering pilot's defense is part full) and a label with the value. Part of the CardView prefab; its look is set there.
    /// </summary>
    public class CardStatBar : MonoBehaviour
    {
        [Tooltip("Anchored to the left; its width follows the share of the stat that is left.")]
        [SerializeField] private Image _fill;
        [SerializeField] private TextMeshProUGUI _label;

        [Tooltip("Optional, behind the fill: how far the stat will have come back by the next turn (a pilot recovering in the hand).")]
        [SerializeField] private Image _preview;
        [SerializeField] private Color _previewColor = new Color(0.4f, 1f, 0.4f, 0.35f);

        [Tooltip("How bright the fill is compared with the stat's colour. Kept dark so the label stays readable on it.")]
        [Range(0f, 1f)] [SerializeField] private float _fillBrightness = 0.45f;

        [SerializeField] private float _animationSeconds = 0.4f;

        private float _shown = -1f;
        private Tween _tween;

        /// <param name="share">How much of the stat is left, 0 to 1.</param>
        /// <param name="animate">Slide the fill from what it showed before (the same pilot changed), instead of jumping.</param>
        public void Set(string text, float share, Color color, bool animate)
        {
            _label.text = text;
            _fill.color = new Color(color.r * _fillBrightness, color.g * _fillBrightness, color.b * _fillBrightness, color.a);
            share = Mathf.Clamp01(share);

            _tween?.Kill();
            if (animate && _shown >= 0f && Mathf.Abs(_shown - share) > 0.001f)
            {
                _tween = DOTween.To(() => _shown, ShowShare, share, _animationSeconds).SetEase(Ease.OutCubic);
            }
            else
            {
                ShowShare(share);
            }
        }

        /// <summary>Shows where the stat will be after it recovers (a share 0 to 1); a negative share hides the preview.</summary>
        public void SetPreview(float share)
        {
            if (_preview == null)
            {
                return;
            }

            _preview.enabled = share >= 0f;
            _preview.color = _previewColor;
            var rect = _preview.rectTransform;
            rect.anchorMax = new Vector2(Mathf.Clamp01(share), rect.anchorMax.y);
        }

        private void ShowShare(float share)
        {
            _shown = share;
            var rect = _fill.rectTransform;
            rect.anchorMax = new Vector2(share, rect.anchorMax.y);
        }

        private void OnDestroy()
        {
            _tween?.Kill();
        }
    }
}
