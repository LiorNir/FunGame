using System.Text.RegularExpressions;

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

        /// <summary>The SP (skill point) icon on its own.</summary>
        public const string SpIcon = "<sprite=\"SP\" index=0>";

        /// <summary>The AP icon on its own (for example before "3/3").</summary>
        public const string ApIcon = "<sprite=\"AP\" index=0>";

        /// <summary>An AP amount: the number followed by the AP icon, for example a cost.</summary>
        public static string Ap(int amount)
        {
            return $"{amount}{ApIcon}";
        }

        /// <summary>An SP amount: the number followed by the SP icon, for example an upgrade's price.</summary>
        public static string Sp(int amount)
        {
            return $"{amount}{SpIcon}";
        }

        /// <summary>
        /// The text with every "AP" written as the AP icon ("costs 1 AP" becomes "costs 1" and the icon), for texts written
        /// in words: tutorial lines and the rules' reasons ("not enough AP").
        /// </summary>
        public static string WithApIcons(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // one pass, and never the "AP" of an icon tag ("AP" in quotes), so a text is not changed twice
            return Regex.Replace(text, @"(?<=\d) AP\b(?!"")|(?<!"")\bAP\b(?!"")", ApIcon);
        }
    }
}
