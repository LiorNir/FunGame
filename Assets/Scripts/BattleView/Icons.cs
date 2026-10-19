namespace BattleView
{
    /// <summary>
    /// Inline icons for texts (TextMeshPro sprite tags). Each icon is a picture in Assets/Art/TextIcons/Resources/Sprite Assets
    /// whose sprite asset is rebuilt whenever the picture changes, so swapping the picture file changes the icon in every
    /// text. Use these instead of writing "AP" after a number.
    /// </summary>
    public static class Icons
    {
        /// <summary>The HP icon on its own (before the player's health, "10/10").</summary>
        public const string HpIcon = "<sprite=\"HP\" index=0>";

        /// <summary>The AP icon on its own (for example before "3/3").</summary>
        public const string ApIcon = "<sprite=\"AP\" index=0>";

        /// <summary>An AP amount: the number followed by the AP icon, for example a cost.</summary>
        public static string Ap(int amount)
        {
            return $"{amount}{ApIcon}";
        }
    }
}
