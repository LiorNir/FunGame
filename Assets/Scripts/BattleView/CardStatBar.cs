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

        [Tooltip("Optional: how far the stat will have come back by the next turn (a pilot recovering in the hand), drawn from the fill's right edge.")]
        [SerializeField] private Image _preview;
        [SerializeField] private Color _previewColor = new Color(0.4f, 1f, 0.4f, 0.35f);
        [Tooltip("The preview blinks down to this share of its alpha and back.")]
        [Range(0f, 1f)] [SerializeField] private float _previewBlinkAlpha = 0.3f;
        [SerializeField] private float _blinkSeconds = 0.45f;

        [Header("Temporary looks")]
        [Tooltip("The label's colour while an ability or effect raises the stat.")]
        [SerializeField] private Color _boostColor = new Color(1f, 0.85f, 0.25f);
        [Tooltip("The label's colour while an ability or effect lowers the stat.")]
        [SerializeField] private Color _dropColor = new Color(1f, 0.45f, 0.45f);
        [Tooltip("A stat at 0: the empty bar blinks between these two colours.")]
        [SerializeField] private Color _zeroBlinkColor = new Color(0.55f, 0.05f, 0.05f);
        [SerializeField] private Color _zeroRestColor = new Color(0.02f, 0.02f, 0.02f);

        [Tooltip("How bright the fill is compared with the stat's colour. Kept dark so the label stays readable on it.")]
        [Range(0f, 1f)] [SerializeField] private float _fillBrightness = 0.45f;

        [SerializeField] private float _animationSeconds = 0.4f;

        private float _shown = -1f;
        private float _target;
        private Tween _tween;
        private Tween _previewBlink;
        private Tween _zeroBlink;
        private Color? _labelColor;
        private Image _background;

        /// <param name="share">How much of the stat is left, 0 to 1.</param>
        /// <param name="animate">Slide the fill from what it showed before (the same pilot changed), instead of jumping.</param>
        /// <param name="change">Shown on the label: a stat raised or lowered right now by an ability or an effect.</param>
        public void Set(string text, float share, Color color, bool animate, StatChange change = StatChange.None)
        {
            _labelColor ??= _label.color;
            _label.text = text;
            _label.color = change == StatChange.Boost ? _boostColor : change == StatChange.Drop ? _dropColor : _labelColor.Value;
            _label.fontStyle = change == StatChange.None ? TMPro.FontStyles.Normal : TMPro.FontStyles.Bold;
            _fill.color = new Color(color.r * _fillBrightness, color.g * _fillBrightness, color.b * _fillBrightness, color.a);
            share = Mathf.Clamp01(share);
            _target = share;
            ShowZero(share <= 0.001f);

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

        /// <summary>
        /// Shows where the stat will be after it recovers (a share 0 to 1), as a blinking part from the end of the fill to
        /// that share; a negative share, or one no higher than the fill, hides the preview. Call after <see cref="Set"/>.
        /// </summary>
        public void SetPreview(float share)
        {
            if (_preview == null)
            {
                return;
            }

            _previewBlink?.Kill();
            _previewBlink = null;
            var shown = share >= 0f && share > _target + 0.001f;
            _preview.enabled = shown;
            if (!shown)
            {
                return;
            }

            _preview.color = _previewColor;
            _preview.transform.SetAsLastSibling(); // in front of the fill, under nothing else that matters
            _label.transform.SetAsLastSibling();
            var rect = _preview.rectTransform;
            rect.anchorMin = new Vector2(_target, rect.anchorMin.y);
            rect.anchorMax = new Vector2(Mathf.Clamp01(share), rect.anchorMax.y);
            _previewBlink = _preview.DOFade(_previewColor.a * _previewBlinkAlpha, _blinkSeconds).SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine).SetLink(gameObject);
        }

        /// <summary>A stat at 0: the bar's background blinks dark red and black (the bar's own Image is its background).</summary>
        private void ShowZero(bool zero)
        {
            _background ??= GetComponent<Image>();
            if (_background == null)
            {
                return;
            }

            if (!zero)
            {
                if (_zeroBlink != null)
                {
                    _zeroBlink.Kill(true);
                    _zeroBlink = null;
                }

                return;
            }

            if (_zeroBlink != null)
            {
                return;
            }

            var restColor = _background.color;
            _background.color = _zeroRestColor;
            _zeroBlink = _background.DOColor(_zeroBlinkColor, _blinkSeconds).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                .SetLink(gameObject).OnKill(() => _background.color = restColor);
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
            _previewBlink?.Kill();
            _zeroBlink?.Kill();
        }
    }
}
