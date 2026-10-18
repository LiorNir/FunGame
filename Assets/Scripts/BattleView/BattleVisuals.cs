using BoardAuthoring;
using FunGame.Battle;
using FunGame.Board;
using TMPro;
using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// The art, colours and prefabs the playable battle uses. Create the default prefabs with
    /// Tools > Fun Game > Create Battle Prefabs, then edit them freely (scale, move texts, swap sprites). Sprites here are
    /// only used where a prefab does not carry its own.
    /// </summary>
    [CreateAssetMenu(fileName = "BattleVisuals", menuName = "ScriptableObjects/BattleVisuals")]
    public class BattleVisuals : ScriptableObject
    {
        [Header("Numbers")]
        [Tooltip("On: a wounded pilot's defense box and hand card show what is left of its defense (0.75). Off (on trial since 2026-10-11): they show its full defense (3) and the fill shows how much is left; the exact value is shown when the pilot is clicked. Chain totals are exact either way.")]
        public bool ShowRemainingDefense;

        [Header("Prefabs")]
        public EnemyView EnemyPrefab;
        public CardView CardPrefab;
        [Tooltip("The whole 2D UI: corner texts, Play Turn button, hand area, action menu, test panel and end screen.")]
        public HudView HudPrefab;
        [Tooltip("A world-space text shown at the end of each chain with its value.")]
        public TextMeshPro ChainLabelPrefab;
        [Tooltip("A world-space text that floats up and fades out (damage numbers and so on).")]
        public TextMeshPro PopupPrefab;
        [Tooltip("The connection drawn between slots and operators on the board.")]
        public ConnectionView ConnectionPrefab;

        [Header("World looks")]
        [Tooltip("The board and background art for a world whose look (or part of it) is not set.")]
        public Encounters.WorldLook DefaultLook;

        [Tooltip("Where the world's board art and background are shown (their size, place and draw order); placed in the battle scene unless it has its own.")]
        public WorldLookView WorldLookPrefab;

        [Header("Sprites used when a prefab has none")]
        public Sprite SlotBase;
        public Sprite BarSprite;

        [Header("Operators")]
        public Sprite AddSprite;
        public Sprite SubtractSprite;
        public Sprite MultiplySprite;
        public Sprite DivideSprite;

        [Header("Icons")]
        public Sprite AttackIcon;
        public Sprite DefenseIcon;

        // Only colours that mean something across several parts of the game live here (lines, chain labels, wound bars,
        // texts and the Scene view gizmos all use them). The look of a single prefab, such as the slot's base, ring,
        // bonus text and locked tint, is set on that prefab.
        [Header("Role colours")]
        public Color AttackColor = new Color(1f, 0.4f, 0.3f);
        public Color DefenseColor = new Color(0.35f, 0.65f, 1f);
        public Color BothColor = new Color(0.8f, 0.45f, 1f);
        public Color UnusedColor = new Color(0.55f, 0.55f, 0.6f);

        /// <summary>The one place that decides the colour for attack, defense or both. Lines, bars, labels and texts all use it.</summary>
        /// <summary>The defense number to show on a pilot's box or card (see <see cref="ShowRemainingDefense"/>).</summary>
        public string DefenseNumber(PilotUnit pilot)
        {
            return DisplayFormat.Value(ShowRemainingDefense ? pilot.EffectiveDefense : pilot.BaseDefense);
        }

        /// <summary>A pilot's exact stats in one line, for the menu shown when it is clicked: "Quill  ATK 1  DEF 0.75 of 3".</summary>
        public string ExactStats(PilotUnit pilot)
        {
            var defense = pilot.DefenseWound > 0.001f
                ? $"{DisplayFormat.Value(pilot.EffectiveDefense)} of {DisplayFormat.Value(pilot.BaseDefense)}"
                : DisplayFormat.Value(pilot.BaseDefense);
            return $"{pilot.Name}   ATK {DisplayFormat.Value(pilot.EffectiveAttack)}   DEF {defense}";
        }

        public Color ColorForRoles(BoardLayoutAuthoring.RoleFlags roles)
        {
            switch (roles)
            {
                case BoardLayoutAuthoring.RoleFlags.Attack:
                    return AttackColor;
                case BoardLayoutAuthoring.RoleFlags.Defense:
                    return DefenseColor;
                case BoardLayoutAuthoring.RoleFlags.Attack | BoardLayoutAuthoring.RoleFlags.Defense:
                    return BothColor;
                default:
                    return UnusedColor;
            }
        }

        public Color ColorForRole(ChainRole role)
        {
            return role == ChainRole.Attack ? AttackColor : DefenseColor;
        }

        /// <summary>Wraps text in a rich-text colour tag using the attack colour.</summary>
        public string AttackText(string text)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(AttackColor) + ">" + text + "</color>";
        }

        public string DefenseText(string text)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(DefenseColor) + ">" + text + "</color>";
        }

        public Sprite GetOperatorSprite(Operation operation)
        {
            switch (operation)
            {
                case Operation.Add:
                    return AddSprite;
                case Operation.Subtract:
                    return SubtractSprite;
                case Operation.Multiply:
                    return MultiplySprite;
                default:
                    return DivideSprite;
            }
        }

        public static string OperatorSymbol(Operation operation)
        {
            switch (operation)
            {
                case Operation.Add:
                    return "+";
                case Operation.Subtract:
                    return "-";
                case Operation.Multiply:
                    return "x";
                default:
                    return "/";
            }
        }
    }
}
