using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FunGame.Board.Tests
{
    public class ChainEvaluatorTests
    {
        // A straight chain a -> b -> c with the given connection operators, read as a defense chain.
        private static BoardGraph StraightChain(Operation ab, Operation bc, NodeBonus bonusOnC = default)
        {
            var nodes = new[]
            {
                new SlotNode("a"),
                new SlotNode("b", "a", ab),
                new SlotNode("c", "b", bc, bonusOnC)
            };
            var chains = new[] { new ChainDefinition("def", "lane", ChainRole.Defense, "c") };
            return new BoardGraph(nodes, chains);
        }

        private static System.Func<string, SlotContent> Contents(Dictionary<string, SlotContent> contents)
        {
            return id => contents.TryGetValue(id, out var content) ? content : SlotContent.Empty;
        }

        private static SlotContent Def(float defense)
        {
            return new SlotContent(0f, defense);
        }

        [Test]
        public void AddChain_SumsThePilotsDefense()
        {
            var graph = StraightChain(Operation.Add, Operation.Add);
            var contents = new Dictionary<string, SlotContent> { ["a"] = Def(3), ["b"] = Def(5), ["c"] = Def(2) };

            var result = ChainEvaluator.Evaluate(graph, graph.Chains[0], Contents(contents));

            Assert.AreEqual(10f, result.Value);
            Assert.IsTrue(result.Steps.All(step => step.Status == SlotStatus.Active));
        }

        [Test]
        public void Chain_StopsAtFirstGap_AndLaterPilotsAreDormant()
        {
            var graph = StraightChain(Operation.Add, Operation.Add);
            var contents = new Dictionary<string, SlotContent> { ["a"] = Def(3), ["c"] = Def(2) };

            var result = ChainEvaluator.Evaluate(graph, graph.Chains[0], Contents(contents));

            Assert.AreEqual(3f, result.Value);
            Assert.AreEqual(SlotStatus.Active, result.Steps[0].Status);
            Assert.AreEqual(SlotStatus.Empty, result.Steps[1].Status);
            Assert.AreEqual(SlotStatus.Dormant, result.Steps[2].Status);
        }

        [Test]
        public void EmptyRoot_MakesTheWholeChainWorthZero()
        {
            var graph = StraightChain(Operation.Add, Operation.Add);
            var contents = new Dictionary<string, SlotContent> { ["b"] = Def(5), ["c"] = Def(2) };

            var result = ChainEvaluator.Evaluate(graph, graph.Chains[0], Contents(contents));

            Assert.AreEqual(0f, result.Value);
            Assert.AreEqual(SlotStatus.Empty, result.Steps[0].Status);
            Assert.AreEqual(SlotStatus.Dormant, result.Steps[1].Status);
            Assert.AreEqual(SlotStatus.Dormant, result.Steps[2].Status);
        }

        [Test]
        public void Subtract_UsesTheAccumulatedValueAsTheLeftOperand_AndNeverGoesBelowZero()
        {
            var graph = StraightChain(Operation.Subtract, Operation.Add);

            var fromHigh = ChainEvaluator.Evaluate(graph, graph.Chains[0],
                Contents(new Dictionary<string, SlotContent> { ["a"] = Def(8), ["b"] = Def(2) }));
            var fromLow = ChainEvaluator.Evaluate(graph, graph.Chains[0],
                Contents(new Dictionary<string, SlotContent> { ["a"] = Def(2), ["b"] = Def(8) }));

            Assert.AreEqual(6f, fromHigh.Value);
            Assert.AreEqual(0f, fromLow.Value);
        }

        [Test]
        public void Divide_UsesTheAccumulatedValueAsTheDividend()
        {
            var graph = StraightChain(Operation.Divide, Operation.Add);
            var contents = new Dictionary<string, SlotContent> { ["a"] = Def(8), ["b"] = Def(2) };

            var result = ChainEvaluator.Evaluate(graph, graph.Chains[0], Contents(contents));

            Assert.AreEqual(4f, result.Value);
        }

        [Test]
        public void Multiply_MultipliesTheAccumulatedValue()
        {
            var graph = StraightChain(Operation.Multiply, Operation.Multiply);
            var contents = new Dictionary<string, SlotContent> { ["a"] = Def(3), ["b"] = Def(5), ["c"] = Def(2) };

            var result = ChainEvaluator.Evaluate(graph, graph.Chains[0], Contents(contents));

            Assert.AreEqual(30f, result.Value);
        }

        [Test]
        public void DivideByZero_ZeroesTheChainAndReportsTheNode()
        {
            var graph = StraightChain(Operation.Add, Operation.Divide);
            var contents = new Dictionary<string, SlotContent> { ["a"] = Def(3), ["b"] = Def(5), ["c"] = Def(0) };

            var result = ChainEvaluator.Evaluate(graph, graph.Chains[0], Contents(contents));

            Assert.AreEqual(0f, result.Value);
            Assert.IsTrue(result.DividedByZero);
            Assert.AreEqual("c", result.DivideByZeroNodeId);
        }

        [Test]
        public void SubtractingZero_IsAFreePass()
        {
            var graph = StraightChain(Operation.Subtract, Operation.Add);
            var contents = new Dictionary<string, SlotContent> { ["a"] = Def(7), ["b"] = Def(0) };

            var result = ChainEvaluator.Evaluate(graph, graph.Chains[0], Contents(contents));

            Assert.AreEqual(7f, result.Value);
            Assert.IsFalse(result.DividedByZero);
        }

        [Test]
        public void Bonus_IsAppliedAfterTheSlotsOperator()
        {
            var graph = StraightChain(Operation.Add, Operation.Multiply, new NodeBonus(BonusKind.Add, 1f));
            var contents = new Dictionary<string, SlotContent> { ["a"] = Def(1), ["b"] = Def(1), ["c"] = Def(3) };

            var result = ChainEvaluator.Evaluate(graph, graph.Chains[0], Contents(contents));

            // (1 + 1) * 3 = 6, then +1 from the bonus.
            Assert.AreEqual(7f, result.Value);
        }

        [Test]
        public void Bonus_OnAnEmptySlot_DoesNotApply()
        {
            var graph = StraightChain(Operation.Add, Operation.Add, new NodeBonus(BonusKind.Multiply, 10f));
            var contents = new Dictionary<string, SlotContent> { ["a"] = Def(1), ["b"] = Def(1) };

            var result = ChainEvaluator.Evaluate(graph, graph.Chains[0], Contents(contents));

            Assert.AreEqual(2f, result.Value);
        }

        [Test]
        public void SharedTrunk_FeedsAttackAndDefenseChainsOfDifferentBranches_EachReadingItsOwnStat()
        {
            var nodes = new[]
            {
                new SlotNode("root"),
                new SlotNode("attackSlot", "root", Operation.Add),
                new SlotNode("defenseSlot", "root", Operation.Add)
            };
            var chains = new[]
            {
                new ChainDefinition("atk", "lane", ChainRole.Attack, "attackSlot"),
                new ChainDefinition("def", "lane", ChainRole.Defense, "defenseSlot")
            };
            var graph = new BoardGraph(nodes, chains);
            var contents = new Dictionary<string, SlotContent>
            {
                ["root"] = new SlotContent(2f, 1f),
                ["attackSlot"] = new SlotContent(4f, 9f),
                ["defenseSlot"] = new SlotContent(9f, 3f)
            };

            var results = ChainEvaluator.EvaluateAll(graph, Contents(contents));

            Assert.AreEqual(6f, results["atk"].Value);
            Assert.AreEqual(4f, results["def"].Value);
        }

        [Test]
        public void GapInOneBranch_DoesNotAffectTheOtherBranch()
        {
            var nodes = new[]
            {
                new SlotNode("root"),
                new SlotNode("left", "root", Operation.Add),
                new SlotNode("right", "root", Operation.Add)
            };
            var chains = new[]
            {
                new ChainDefinition("leftChain", "lane1", ChainRole.Attack, "left"),
                new ChainDefinition("rightChain", "lane2", ChainRole.Attack, "right")
            };
            var graph = new BoardGraph(nodes, chains);
            var contents = new Dictionary<string, SlotContent>
            {
                ["root"] = new SlotContent(2f, 0f),
                ["right"] = new SlotContent(5f, 0f)
            };

            var results = ChainEvaluator.EvaluateAll(graph, Contents(contents));

            Assert.AreEqual(2f, results["leftChain"].Value);
            Assert.AreEqual(7f, results["rightChain"].Value);
        }

        [Test]
        public void ChainWithUnknownTerminal_Throws()
        {
            var graph = new BoardGraph(new[] { new SlotNode("a") },
                new[] { new ChainDefinition("c", "lane", ChainRole.Attack, "missing") });

            Assert.Throws<System.InvalidOperationException>(() =>
                ChainEvaluator.Evaluate(graph, graph.Chains[0], _ => SlotContent.Empty));
        }
    }
}
