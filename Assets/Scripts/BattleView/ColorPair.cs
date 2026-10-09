using System;
using UnityEngine;

namespace BattleView
{
    /// <summary>The two colours the lerp-colour shader blends between, by the black-and-white picture it is given.</summary>
    [Serializable]
    public struct ColorPair
    {
        public Color Dark;
        public Color Bright;

        public ColorPair(Color dark, Color bright)
        {
            Dark = dark;
            Bright = bright;
        }

        /// <summary>A pair built from one colour: the bright colour as given, the dark one the same hue at a fraction of the brightness.</summary>
        public static ColorPair FromBright(Color bright, float darkShade)
        {
            var dark = new Color(bright.r * darkShade, bright.g * darkShade, bright.b * darkShade, bright.a);
            return new ColorPair(dark, bright);
        }
    }
}
