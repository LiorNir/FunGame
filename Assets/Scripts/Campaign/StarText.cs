using System.Collections.Generic;
using System.Text;
using FunGame.Progress;

namespace Campaign
{
    /// <summary>
    /// How stars are written in texts (temporary look: the font has no star glyph, so a gold dot is an earned star and a dark
    /// one a star still to earn). The end screen and the map both use it, so the look changes in one place.
    /// </summary>
    public static class StarText
    {
        private const string Earned = "<color=#FFD24A>●</color>";
        private const string Missing = "<color=#4A4F5C>●</color>";

        /// <summary>One dot per star, for example "●●○" for two of three.</summary>
        public static string Row(int earnedMask, int count)
        {
            // drawn larger than the small text it usually sits in (the map buttons' footer)
            var text = new StringBuilder("<size=220%>");
            for (var index = 0; index < count; index++)
            {
                text.Append(StarRating.Has(earnedMask, index) ? Earned : Missing);
            }

            return text.Append("</size>").ToString();
        }

        /// <summary>One line per star with its condition, marking the ones met this battle and the ones earned for the first time.</summary>
        public static string Lines(IReadOnlyList<StarCondition> stars, int metMask, int newMask, float maxHealth)
        {
            var text = new StringBuilder();
            for (var index = 0; index < stars.Count; index++)
            {
                var met = StarRating.Has(metMask, index);
                text.Append(met ? Earned : Missing).Append(' ');
                text.Append(met ? stars[index].Describe(maxHealth) : $"<color=#8A8F9C>{stars[index].Describe(maxHealth)}</color>");
                if (StarRating.Has(newMask, index))
                {
                    text.Append(" <color=#FFD24A><b>new!</b></color>");
                }

                if (index < stars.Count - 1)
                {
                    text.Append('\n');
                }
            }

            return text.ToString();
        }
    }
}
