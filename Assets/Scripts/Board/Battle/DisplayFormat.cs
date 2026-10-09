using System;
using System.Globalization;

namespace FunGame.Battle
{
    /// <summary>
    /// How numbers are shown to the player. Values are kept exact; they are only cut (not rounded) to two decimals for
    /// display, so 10.5783 shows as 10.57 and a wounded 0.6667 as 0.66. Trailing zeros are dropped. A value that is still
    /// above zero never shows as 0. The same format is used for every number, attack and defense alike.
    /// </summary>
    public static class DisplayFormat
    {
        private const double Scale = 100d;

        // Float arithmetic can leave a value a hair under what it should be (0.99999994 instead of 1), which would be cut
        // to 0.99. The tolerance is in units of the last shown digit.
        private const double Tolerance = 0.001d;

        public static string Value(float value)
        {
            var truncated = Math.Truncate(value * Scale + Tolerance) / Scale;
            if (value > 0.0001f && truncated < 1d / Scale)
            {
                truncated = 1d / Scale;
            }

            return truncated.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
