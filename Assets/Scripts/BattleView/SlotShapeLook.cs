using UnityEngine;

namespace BattleView
{
    /// <summary>The shape pilot slots are drawn with (set on the Battle Visuals asset, to try both).</summary>
    public enum PilotSlotShape
    {
        Circle,
        Rectangle
    }

    /// <summary>
    /// One look of a pilot slot (the circle or the rectangle): its background, which shows the slot's state colours
    /// (highlight, locked), its frame, which shows the chain colour through the tint material, and the objects that belong
    /// to it. The slot shows one look and hides the other.
    /// </summary>
    public class SlotShapeLook : MonoBehaviour
    {
        [SerializeField] private PilotSlotShape _shape;

        [Tooltip("Tinted with the slot's state colours (highlight while placing, locked). Its size is the slot's click size.")]
        [SerializeField] private SpriteRenderer _background;

        [Tooltip("Coloured by the chain(s) the slot is in, through its tint material (_DarkColor / _BrightColor).")]
        [SerializeField] private SpriteRenderer _frame;

        [Tooltip("Everything shown with this look and hidden with the other one.")]
        [SerializeField] private GameObject[] _parts = new GameObject[0];

        public PilotSlotShape Shape => _shape;
        public SpriteRenderer Background => _background;
        public SpriteRenderer Frame => _frame;

        public void Show(bool shown)
        {
            foreach (var part in _parts)
            {
                if (part != null)
                {
                    part.SetActive(shown);
                }
            }
        }
    }
}
