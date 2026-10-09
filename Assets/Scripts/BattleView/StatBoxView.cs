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

        private bool _fillMeasured;
        private float _fullWidth;
        private Vector3 _fullCenter;

        public void SetValue(string value, Color color)
        {
            _text.text = value;
            _text.color = color;
        }

        public void SetBorder(ColorPair colors)
        {
            LerpColorTint.Apply(_border, colors);
        }

        /// <summary>Fills the given fraction (0 to 1) of the box, from the left, in the given colour.</summary>
        public void SetFill(float fraction, Color color)
        {
            if (_fill == null)
            {
                return;
            }

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
            if (_fill != null)
            {
                _fill.enabled = false;
            }
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
