using System;
using System.Collections.Generic;
using UnityEngine;

namespace Encounters
{
    /// <summary>When a tutorial line is shown during a battle.</summary>
    public enum TutorialTrigger
    {
        /// <summary>When the battle starts (before the first turn is planned).</summary>
        BattleStart,

        /// <summary>When the planning of the given turn starts.</summary>
        TurnStart,

        /// <summary>The first time a defense chain is hit down to exactly 0 (still standing).</summary>
        ChainAtZero,

        /// <summary>The first time a chain is broken (its pilots fall).</summary>
        ChainBroken,

        /// <summary>When a second (or later) enemy of a lane appears.</summary>
        NextEnemy,

        /// <summary>When an enemy reveals a Lock.</summary>
        EnemyLocks,

        /// <summary>When an enemy reveals a Snipe.</summary>
        EnemySnipes,

        /// <summary>When an enemy reveals a snipe that will assassinate (its target has nothing left).</summary>
        EnemyAssassinates,

        /// <summary>When an enemy reveals a Charge.</summary>
        EnemyCharges,

        /// <summary>When the player breaks a charge.</summary>
        ChargeBroken,

        /// <summary>The first time the player uses a pilot ability.</summary>
        AbilityUsed
    }

    [Serializable]
    public class TutorialLine
    {
        public EncounterDefinition Encounter;
        public TutorialTrigger Trigger;

        [Tooltip("For TurnStart only: which turn.")]
        [Min(1)] public int Turn = 1;

        [TextArea(2, 5)] public string Text;

        /// <summary>Identifies the line in the save (shown once per save), stable while its encounter, trigger and text stay.</summary>
        public string Id => $"{(Encounter != null ? Encounter.AssetId : "?")}|{Trigger}|{Turn}|{StableHash(Text ?? string.Empty)}";

        private static uint StableHash(string text)
        {
            var hash = 2166136261u;
            foreach (var character in text)
            {
                hash = (hash ^ character) * 16777619u;
            }

            return hash;
        }
    }

    /// <summary>
    /// Every tutorial line of the game in one asset, so they can be read and edited together: which encounter, when it is
    /// shown, and the text. Shown in the battle's tutorial panel, once per save (always on the test bench).
    /// </summary>
    [CreateAssetMenu(fileName = "Tutorial", menuName = "ScriptableObjects/Tutorial Script")]
    public class TutorialScript : ScriptableObject
    {
        [SerializeField] private List<TutorialLine> _lines = new List<TutorialLine>();

        public IReadOnlyList<TutorialLine> Lines => _lines;

        public void SetLines(List<TutorialLine> lines)
        {
            _lines = lines;
        }
    }
}
