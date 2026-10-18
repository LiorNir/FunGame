using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// The look of one connection between two board positions, as a prefab: a strip picture that lies on the board and is
    /// stretched from one end to the other. Its material gets the role colour (attack, defense, both) and, while the line
    /// is active (a pilot powers it), the noise switched on; both per line, through a property block, so every line shares
    /// one material. Widths and the faded look of an unpowered connection are set here.
    /// </summary>
    public class ConnectionView : MonoBehaviour
    {
        [Tooltip("The stretched picture. Its sprite is scaled to the connection's length and width.")]
        [SerializeField] private SpriteRenderer _strip;
        [SerializeField] private float _width = 0.12f;
        [Tooltip("Width while a pilot powers the connection.")]
        [SerializeField] private float _poweredWidth = 0.2f;
        [Tooltip("Opacity of a connection no pilot powers yet (1 = as opaque as a powered one).")]
        [Range(0f, 1f)]
        [SerializeField] private float _unpoweredAlpha = 0.35f;
        [Tooltip("Brightness of a connection no pilot powers yet: its role colour is darkened by this (1 = as bright as a powered one), so idle lines stay visible but quieter.")]
        [Range(0f, 1f)]
        [SerializeField] private float _unpoweredBrightness = 0.45f;

        [Header("Material")]
        [Tooltip("The material's colour property; it gets the role colour.")]
        [SerializeField] private string _colorProperty = "_MainColor";
        [Tooltip("The material's noise property: 1 while the line is active, 0 otherwise.")]
        [SerializeField] private string _noiseProperty = "_NoiseAmount";

        private float _length;
        private MaterialPropertyBlock _properties;

        /// <summary>Lays the strip from one point to the other, both in the parent's space (the board's).</summary>
        public void Connect(Vector3 localFrom, Vector3 localTo)
        {
            var delta = localTo - localFrom;
            _length = new Vector2(delta.x, delta.y).magnitude;
            transform.localPosition = (localFrom + localTo) * 0.5f;
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        /// <param name="powered">The line is active: it connects pilots that count (noise on, wider).</param>
        public void SetStyle(Color color, bool powered)
        {
            if (_strip == null)
            {
                return;
            }

            if (!powered)
            {
                color = new Color(color.r * _unpoweredBrightness, color.g * _unpoweredBrightness, color.b * _unpoweredBrightness,
                    color.a * _unpoweredAlpha);
            }

            // a material that reads the sprite's own colour still gets it this way too
            _strip.color = color;
            _properties ??= new MaterialPropertyBlock();
            _strip.GetPropertyBlock(_properties);
            if (!string.IsNullOrEmpty(_colorProperty))
            {
                _properties.SetColor(_colorProperty, color);
            }

            if (!string.IsNullOrEmpty(_noiseProperty))
            {
                _properties.SetFloat(_noiseProperty, powered ? 1f : 0f);
            }

            _strip.SetPropertyBlock(_properties);

            var size = _strip.sprite != null ? _strip.sprite.bounds.size : Vector3.one;
            var width = powered ? _poweredWidth : _width;
            _strip.transform.localScale = new Vector3(
                size.x > 0f ? _length / size.x : 1f,
                size.y > 0f ? width / size.y : 1f,
                1f);
        }
    }
}
