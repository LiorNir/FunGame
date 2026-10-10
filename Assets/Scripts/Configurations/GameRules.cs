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

        [Tooltip("How much of its defense a wounded pilot gets back each turn it rests in the hand (after being replaced or ejected), as a share of its full defense. 0.5 heals it in two turns; 0 keeps wounds.")]
        [Range(0f, 1f)] public float HandRecoveryPerTurn = 0.5f;

        [Header("Action costs (AP)")]
        [Min(0)] public int PlacePilotCost = 1;
        [Min(0)] public int ReplacePilotCost = 1;

        [Tooltip("Taking a living pilot off the board without placing another. Placeholder: the cost is not decided yet.")]
        [Min(0)] public int EjectPilotCost = 1;
        [Min(0)] public int ActivateAbilityCost = 1;
    }
}
