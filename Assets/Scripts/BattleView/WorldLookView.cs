using Encounters;
using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// Shows the current world's look in the battle scene: the board art and the background. Their size, place and draw
    /// order are set on the two renderers in the scene; only the pictures change per world.
    /// </summary>
    public class WorldLookView : MonoBehaviour
    {
        [Tooltip("The board picture, facing the camera, behind the slots.")]
        [SerializeField] private SpriteRenderer _board;

        [Tooltip("The full-screen background, behind everything.")]
        [SerializeField] private SpriteRenderer _background;

        /// <summary>Shows the look's pictures; any it leaves empty come from <paramref name="fallback"/>.</summary>
        public void Show(WorldLook look, WorldLook fallback)
        {
            Set(_board, look != null && look.Board != null ? look.Board : fallback != null ? fallback.Board : null);
            Set(_background, look != null && look.Background != null ? look.Background : fallback != null ? fallback.Background : null);
        }

        private static void Set(SpriteRenderer renderer, Sprite sprite)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.sprite = sprite;
            renderer.enabled = sprite != null;
        }
    }
}
