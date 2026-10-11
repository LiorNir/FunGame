using System;
using System.Collections.Generic;
using BoardAuthoring;
using Combat;
using Configurations;
using UnityEngine;

namespace Encounters
{
    /// <summary>
    /// Everything needed to run one battle: the board, which enemies queue up in each lane, and the reward. Global
    /// rules (starting AP, AP per turn, hand size, action costs) live in the GameRules asset, and the player's pilot
    /// deck is separate.
    /// </summary>
    [CreateAssetMenu(fileName = "Encounter", menuName = "ScriptableObjects/Encounter")]
    public class EncounterDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;

        [Tooltip("A board prefab: a prefab with a Board Layout Authoring component on its root.")]
        [SerializeField] private BoardLayoutAuthoring _board;

        [Tooltip("One entry per lane of the board. Lane ids must match the lane names on the board's chain markers.")]
        [SerializeField] private List<LaneEnemies> _lanes = new List<LaneEnemies>();

        [Min(0)] [SerializeField] private int _rewardSkillPoints;

        [Header("Mechanics the encounter allows")]
        [Tooltip("Off until the game has taught replacing a pilot that is already on the board. Early encounters switch it off.")]
        [SerializeField] private bool _allowReplace = true;

        [Tooltip("Off until the game has taught taking a pilot back into the hand. Early encounters switch it off.")]
        [SerializeField] private bool _allowEject = true;

        [Header("Testing (until progression exists)")]
        [Tooltip("Optional. Plays this encounter with this deck instead of the starter deck: the pilots the player would have by this point (upgrades and recruited pilots), until SP spending and rewards exist.")]
        [SerializeField] private PilotDeck _testDeck;

        [Tooltip("Optional: pilots drawn into the first hand before the shuffled rest (for a step that must start with a pilot).")]
        [SerializeField] private List<PilotTokenConfiguration> _openingHand = new List<PilotTokenConfiguration>();

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public BoardLayoutAuthoring Board => _board;
        public IReadOnlyList<LaneEnemies> Lanes => _lanes;
        public int RewardSkillPoints => _rewardSkillPoints;
        public bool AllowReplace => _allowReplace;
        public bool AllowEject => _allowEject;

        /// <summary>The deck this encounter is played with instead of the starter deck, or null for the starter deck.</summary>
        public PilotDeck TestDeck => _testDeck;

        public IReadOnlyList<PilotTokenConfiguration> OpeningHand => _openingHand;

        public void SetOpeningHand(List<PilotTokenConfiguration> pilots)
        {
            _openingHand = pilots ?? new List<PilotTokenConfiguration>();
        }

        public void SetData(string displayName, BoardLayoutAuthoring board, List<LaneEnemies> lanes, int rewardSkillPoints)
        {
            _displayName = displayName;
            _board = board;
            _lanes = lanes;
            _rewardSkillPoints = rewardSkillPoints;
        }

        /// <summary>Which pilot tools this encounter allows (off until the game has taught them).</summary>
        public void SetAllowedTools(bool allowReplace, bool allowEject)
        {
            _allowReplace = allowReplace;
            _allowEject = allowEject;
        }

        public void SetTestDeck(PilotDeck deck)
        {
            _testDeck = deck;
        }
    }

    /// <summary>The enemies of one lane, in the order they appear: the first at the start, each next after the previous dies.</summary>
    [Serializable]
    public class LaneEnemies
    {
        [LaneId] public string LaneId;
        public List<EnemyDefinition> Enemies = new List<EnemyDefinition>();

        public LaneEnemies(string laneId = "", List<EnemyDefinition> enemies = null)
        {
            LaneId = laneId;
            Enemies = enemies ?? new List<EnemyDefinition>();
        }
    }
}
