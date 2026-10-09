using System;
using UnityEngine;

namespace BattleView
{
    /// <summary>What a pilot slot looks like it is doing, for the colours of its ring and stat borders.</summary>
    public enum SlotVisualState
    {
        /// <summary>No pilot stands in the slot.</summary>
        Empty,

        /// <summary>The slot cannot hold a pilot yet.</summary>
        Locked,

        /// <summary>A pilot stands here but does not count: the chain is broken closer to the root, or the pilot fell.</summary>
        Inactive,

        /// <summary>A pilot stands here and counts.</summary>
        Active
    }

    /// <summary>
    /// The colours of a slot's ring and stat borders for each state. Locked and inactive have colours of their own. Active
    /// takes the colour of the chain the slot belongs to (attack, defense or both, from the Battle Visuals), and empty is
    /// the same colour dimmed, as a hint of which chain the slot would feed. In both the dark side is derived from the
    /// bright one. Lives on the slot prefab, so changing it touches that prefab only.
    /// </summary>
    [Serializable]
    public class SlotTints
    {
        [SerializeField] private ColorPair _locked = new ColorPair(new Color(0.05f, 0.05f, 0.07f), new Color(0.2f, 0.2f, 0.25f));
        [SerializeField] private ColorPair _inactive = new ColorPair(new Color(0.15f, 0.15f, 0.15f), new Color(0.5f, 0.5f, 0.5f));

        [Header("Active (chain colour)")]
        [Tooltip("How bright the dark colour is compared with the bright one (0 = black).")]
        [Range(0f, 1f)] [SerializeField] private float _activeDarkShade = 0.3f;

        [Header("Empty (dimmed chain colour)")]
        [Tooltip("How bright the bright colour is compared with the chain's colour (0 = black, 1 = as bright as active).")]
        [Range(0f, 1f)] [SerializeField] private float _emptyBrightness = 0.4f;
        [Tooltip("How bright the dark colour is compared with the bright one (0 = black).")]
        [Range(0f, 1f)] [SerializeField] private float _emptyDarkShade = 0.3f;

        [Header("Defense fill")]
        [Tooltip("How bright the fill inside the defense box is compared with the defense colour. Kept dark so the number stays readable on it.")]
        [Range(0f, 1f)] [SerializeField] private float _fillBrightness = 0.4f;

        [Header("Unused stat")]
        [Tooltip("How bright the border and number of a stat are when the slot is in no chain that uses it (for example the defense of a pilot that is only in an attack chain).")]
        [Range(0f, 1f)] [SerializeField] private float _unusedBrightness = 0.35f;

        public Color Unused(Color color)
        {
            return Scaled(color, _unusedBrightness);
        }

        public ColorPair Unused(ColorPair colors)
        {
            return new ColorPair(Scaled(colors.Dark, _unusedBrightness), Scaled(colors.Bright, _unusedBrightness));
        }

        /// <summary>The colour of the fill inside a stat box: the box's colour toned down, or the inactive grey.</summary>
        public Color FillFor(SlotVisualState state, Color boxColor)
        {
            return state == SlotVisualState.Inactive ? _inactive.Dark : Scaled(boxColor, _fillBrightness);
        }

        /// <param name="chainColor">The colour of the chain this ring or border belongs to; used when empty or active.</param>
        public ColorPair For(SlotVisualState state, Color chainColor)
        {
            switch (state)
            {
                case SlotVisualState.Empty:
                    return ColorPair.FromBright(Scaled(chainColor, _emptyBrightness), _emptyDarkShade);
                case SlotVisualState.Locked:
                    return _locked;
                case SlotVisualState.Inactive:
                    return _inactive;
                default:
                    return ColorPair.FromBright(chainColor, _activeDarkShade);
            }
        }

        private static Color Scaled(Color color, float brightness)
        {
            return new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
        }
    }
}
