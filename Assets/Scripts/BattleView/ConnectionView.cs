using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// The look of one connection between two board positions, as a prefab: a strip picture that lies on the board and is
    /// stretched from one end to the other. The strip is tinted with the role colour (attack, defense, both), so a white
    /// picture shows the colours as they are. Widths and the faded look of an unpowered connection are set here.
    /// </summary>
    public class ConnectionView : MonoBehaviour
    {
        [Tooltip("The stretched picture. Its sprite is scaled to the connection's length and width.")]
        [SerializeField] private SpriteRenderer _strip;
        [SerializeField] private float _width = 0.12f;
        [Tooltip("Width while a pilot powers the connection.")]
        [SerializeField] private float _poweredWidth = 0.2f;
        [Tooltip("Opacity of a connection no pilot powers yet (1 = as bright as a powered one).")]
        [Range(0f, 1f)]
        [SerializeField] private float _unpoweredAlpha = 0.35f;

        private float _length;

        /// <summary>Lays the strip from one point to the other, both in the parent's space (the board's).</summary>
        public void Connect(Vector3 localFrom, Vector3 localTo)
        {
            var delta = localTo - localFrom;
            _length = new Vector2(delta.x, delta.y).magnitude;
            transform.localPosition = (localFrom + localTo) * 0.5f;
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        public void SetStyle(Color color, bool powered)
        {
            if (_strip == null)
            {
                return;
            }

            if (!powered)
            {
                color.a *= _unpoweredAlpha;
            }

            _strip.color = color;
            var size = _strip.sprite != null ? _strip.sprite.bounds.size : Vector3.one;
            var width = powered ? _poweredWidth : _width;
            _strip.transform.localScale = new Vector3(
                size.x > 0f ? _length / size.x : 1f,
                size.y > 0f ? width / size.y : 1f,
                1f);
        }
    }
}
