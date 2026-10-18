using UnityEngine;

namespace Encounters
{
    /// <summary>
    /// How a world looks in battle: the board art under the slots and the background behind everything. Each encounter
    /// points to its world's look; anything left empty uses the default look of the Battle Visuals asset. The art is
    /// shown at the size and place set on the battle scene's World Look object, so a world's pictures should share the
    /// default pictures' framing (board surface and horizon in the same places).
    /// </summary>
    [CreateAssetMenu(fileName = "World Look", menuName = "ScriptableObjects/World Look")]
    public class WorldLook : ScriptableObject
    {
        [Tooltip("The board picture (seen from the front, its top surface under the slots). Empty: the default look's.")]
        public Sprite Board;

        [Tooltip("The full-screen background behind the enemies and the board. Empty: the default look's.")]
        public Sprite Background;
    }
}
