using UnityEngine;

namespace BoardAuthoring
{
    /// <summary>
    /// The reference screen the board is designed for: 1920x1080 with an orthographic camera of size 5.4, so one world
    /// unit is exactly 100 pixels (matching the sprites' pixels per unit). The zones follow the proportions of the
    /// mock screens: enemies on top, the board in the middle band, the hand at the bottom. Sizes of slots are not here:
    /// they come from the slot prefabs (see <see cref="ISlotFootprint"/> and <see cref="BoardLayoutMetrics"/>).
    /// </summary>
    public static class BoardScreenMetrics
    {
        public const float CameraOrthographicSize = 5.4f;

        public static readonly Vector2 ScreenSize = new Vector2(19.2f, 10.8f);

        // Board band: roughly 31% to 74% of the screen height, 85% of its width.
        public static readonly Vector2 BoardCenter = new Vector2(0f, -0.27f);
        public static readonly Vector2 BoardSize = new Vector2(16.4f, 4.64f);
    }
}
