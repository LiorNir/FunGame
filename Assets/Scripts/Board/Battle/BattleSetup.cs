using System.Collections.Generic;
using FunGame.Board;

namespace FunGame.Battle
{
    /// <summary>Everything the engine needs to run one battle. Contains no Unity types.</summary>
    public sealed class BattleSetup
    {
        public BoardGraph Board { get; }
        public IReadOnlyList<SlotRule> SlotRules { get; }
        public IReadOnlyList<LaneSpec> Lanes { get; }
        public BattleRules Rules { get; }
        public IReadOnlyList<PilotStats> Deck { get; }
        public int Seed { get; }

        /// <summary>
        /// Pilots (by name) drawn into the first hand before the shuffled rest, for encounters that must start with a pilot
        /// (for example a teaching step). Empty for a normal shuffled draw.
        /// </summary>
        public IReadOnlyList<string> OpeningHand { get; set; } = new string[0];

        public BattleSetup(
            BoardGraph board,
            IReadOnlyList<SlotRule> slotRules,
            IReadOnlyList<LaneSpec> lanes,
            BattleRules rules,
            IReadOnlyList<PilotStats> deck,
            int seed)
        {
            Board = board;
            SlotRules = slotRules;
            Lanes = lanes;
            Rules = rules;
            Deck = deck;
            Seed = seed;
        }
    }
}
