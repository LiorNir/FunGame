using System.Collections.Generic;

namespace FunGame.Board
{
    /// <summary>Hand-built boards for trying out the solver before layout assets exist.</summary>
    public static class SampleBoards
    {
        public static readonly IReadOnlyList<string> Names = new[] { "Split pair", "Two-lane hub (sketch)" };

        public static BoardGraph Create(int index)
        {
            return index == 0 ? SplitPair() : TwoLaneHub();
        }

        /// <summary>One root feeding a two-slot attack branch and a two-slot defense branch.</summary>
        private static BoardGraph SplitPair()
        {
            var nodes = new[]
            {
                new SlotNode("root"),
                new SlotNode("atk1", "root", Operation.Add),
                new SlotNode("atk2", "atk1", Operation.Multiply),
                new SlotNode("def1", "root", Operation.Add),
                new SlotNode("def2", "def1", Operation.Subtract)
            };
            var chains = new[]
            {
                new ChainDefinition("atk", "lane", ChainRole.Attack, "atk2"),
                new ChainDefinition("def", "lane", ChainRole.Defense, "def2")
            };
            return new BoardGraph(nodes, chains);
        }

        /// <summary>
        /// The branching two-lane sketch: three roots along the bottom, a hub (F) that feeds both lanes, and four
        /// chains. Operators and bonuses are arbitrary sample values.
        /// </summary>
        private static BoardGraph TwoLaneHub()
        {
            var nodes = new[]
            {
                new SlotNode("C"),
                new SlotNode("B", "C", Operation.Add),
                new SlotNode("A", "B", Operation.Multiply),
                new SlotNode("H"),
                new SlotNode("F", "H", Operation.Add),
                new SlotNode("E", "F", Operation.Subtract),
                new SlotNode("D", "E", Operation.Add),
                new SlotNode("G", "F", Operation.Multiply, new NodeBonus(BonusKind.Add, 2f)),
                new SlotNode("K"),
                new SlotNode("J", "K", Operation.Add),
                new SlotNode("I", "J", Operation.Divide)
            };
            var chains = new[]
            {
                new ChainDefinition("left-def", "left", ChainRole.Defense, "A"),
                new ChainDefinition("left-atk", "left", ChainRole.Attack, "D"),
                new ChainDefinition("right-atk", "right", ChainRole.Attack, "G"),
                new ChainDefinition("right-def", "right", ChainRole.Defense, "I")
            };
            return new BoardGraph(nodes, chains);
        }
    }
}
