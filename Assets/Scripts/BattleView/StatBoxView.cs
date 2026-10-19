using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// One stat box, as a prefab: a border, a background, an optional fill and a number. A pilot slot has two of them
    /// (attack and defense). The slot only says what number to show, how full the box is and how to colour it; how the box
    /// looks is set on the prefab. The fill shows how much of the value is left, shrinking from the right (the defense box's
    /// fill is what the old wound bar was). Its full size and place are whatever the "StatFill" object has in the prefab.
    /// </summary>
    public class StatBoxView : MonoBehaviour
    {
        [Tooltip("The frame of the box. Its material is the lerp-colour shader; the slot sets the colours.")]
        [SerializeField] private SpriteRenderer _border;
        [Tooltip("Shows how much of the value is left. Its size in the prefab is the full box.")]
        [SerializeField] private SpriteRenderer _fill;
        [SerializeField] private TextMeshPro _text;

        [Header("Temporary looks")]
        [Tooltip("The number's colour while an ability or effect raises the stat (for example Surge).")]
        [SerializeField] private Color _boostColor = new Color(1f, 0.85f, 0.25f);
        [Tooltip("The number's colour while an ability or effect lowers the stat (for example a snipe or the Surge crash).")]
        [SerializeField] private Color _dropColor = new Color(1f, 0.35f, 0.35f);
        [Tooltip("A used stat at 0: the whole box blinks between these two colours.")]
        [SerializeField] private Color _zeroBlinkColor = new Color(0.55f, 0.05f, 0.05f);
        [SerializeField] private Color _zeroRestColor = new Color(0.02f, 0.02f, 0.02f);
        [SerializeField] private float _zeroBlinkSeconds = 0.45f;

        private bool _fillMeasured;
        private Tween _zeroBlink;
        private float _fullWidth;
        private Vector3 _fullCenter;

        public void SetValue(string value, Color color, StatChange change = StatChange.None)
        {
            _text.text = value;
            _text.color = change == StatChange.Boost ? _boostColor : change == StatChange.Drop ? _dropColor : color;
            _text.fontStyle = change == StatChange.None ? FontStyles.Normal : FontStyles.Bold;
        }

        /// <param name="fadeSeconds">Fade from the current colours over this time; 0 sets them at once.</param>
        public void SetBorder(ColorPair colors, float fadeSeconds = 0f)
        {
            LerpColorTint.Apply(_border, colors, fadeSeconds);
        }

        /// <summary>Fills the given fraction (0 to 1) of the box, from the left, in the given colour.</summary>
        public void SetFill(float fraction, Color color)
        {
            if (_fill == null)
            {
                return;
            }

            StopZeroBlink();
            MeasureFill();
            fraction = Mathf.Clamp01(fraction);
            _fill.enabled = true;
            _fill.color = color;
            _fill.transform.localScale = new Vector3(_fullWidth * fraction, _fill.transform.localScale.y, 1f);
            _fill.transform.localPosition = _fullCenter + new Vector3(-_fullWidth * 0.5f + _fullWidth * fraction * 0.5f, 0f, 0f);
        }

        /// <summary>For a box whose stat is not used by the slot's chains.</summary>
        public void HideFill()
        {
            StopZeroBlink();
            if (_fill != null)
            {
                _fill.enabled = false;
            }
        }

        /// <summary>A used stat that is at 0: the whole box blinks dark red and black so it cannot be missed.</summary>
        public void ShowZero()
        {
            if (_fill == null || _zeroBlink != null)
            {
                return;
            }

            MeasureFill();
            _fill.enabled = true;
            _fill.transform.localScale = new Vector3(_fullWidth, _fill.transform.localScale.y, 1f);
            _fill.transform.localPosition = _fullCenter;
            _fill.color = _zeroRestColor;
            _zeroBlink = _fill.DOColor(_zeroBlinkColor, _zeroBlinkSeconds).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(gameObject);
        }

        private void StopZeroBlink()
        {
            _zeroBlink?.Kill();
            _zeroBlink = null;
        }

        private void OnDisable()
        {
            StopZeroBlink();
        }

        public void Clear()
        {
            _text.text = string.Empty;
        }

        // the prefab's fill, before it is first changed, is the full box
        private void MeasureFill()
        {
            if (_fillMeasured)
            {
                return;
            }

            _fullWidth = _fill.transform.localScale.x;
            _fullCenter = _fill.transform.localPosition;
            _fillMeasured = true;
        }
    }
}
