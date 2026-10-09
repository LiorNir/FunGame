using UnityEngine;

namespace Configurations
{
    /// <summary>Global rules that apply to every battle (not per encounter).</summary>
    [CreateAssetMenu(fileName = "GameRules", menuName = "ScriptableObjects/GameRules")]
    public class GameRules : ScriptableObject
    {
        [Header("Player")]
        [Min(1)] public int PlayerMaxHealth = 20;

        [Header("Action points")]
        [Tooltip("AP at the start of the battle.")]
        [Min(0)] public int StartingActionPoints = 3;

        [Tooltip("AP at the start of every turn. AP is refreshed to this value: unspent AP does not carry over.")]
        [Min(0)] public int ActionPointsPerTurn = 3;

        [Header("Hand")]
        [Min(1)] public int HandSize = 4;

        [Header("Action costs (AP)")]
        [Min(0)] public int PlacePilotCost = 1;
        [Min(0)] public int ReplacePilotCost = 1;

        [Tooltip("Taking a living pilot off the board without placing another. Placeholder: the cost is not decided yet.")]
        [Min(0)] public int EjectPilotCost = 1;
        [Min(0)] public int ActivateAbilityCost = 1;
    }
}
