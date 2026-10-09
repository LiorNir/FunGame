using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FunGame.Board.Tests
{
    public class BoardSolverTests
    {
        // root feeds an attack slot "a" and a defense slot "d", both through Add.
        private static BoardGraph SplitBoard(Operation attackOperation = Operation.Add)
        {
            var nodes = new[]
            {
                new SlotNode("root"),
                new SlotNode("a", "root", attackOperation),
                new SlotNode("d", "root", Operation.Add)
            };
            var chains = new[]
            {
                new ChainDefinition("atk", "lane", ChainRole.Attack, "a"),
                new ChainDefinition("def", "lane", ChainRole.Defense, "d")
            };
            return new BoardGraph(nodes, chains);
        }

        [Test]
        public void CountArrangements_MatchesHandCalculatedValues()
        {
            Assert.AreEqual(6d, BoardSolver.CountArrangements(3, 3, false));
            Assert.AreEqual(34d, BoardSolver.CountArrangements(3, 3, true));
            Assert.AreEqual(6720d, BoardSolver.CountArrangements(8, 5, false));
        }

        [Test]
        public void Solve_EvaluatesEveryFullPlacement()
        {
            var roster = new[]
            {
                new PilotStats("striker", 5f, 0f),
                new PilotStats("wall", 0f, 5f),
                new PilotStats("balanced", 1f, 1f)
            };

            var result = BoardSolver.Solve(SplitBoard(), roster);

            Assert.AreEqual(6L, result.ArrangementsEvaluated);
        }

        [Test]
        public void Solve_AllowPartial_EvaluatesPartialPlacementsToo()
        {
            var roster = new[]
            {
                new PilotStats("striker", 5f, 0f),
                new PilotStats("wall", 0f, 5f),
                new PilotStats("balanced", 1f, 1f)
            };

            var result = BoardSolver.Solve(SplitBoard(), roster, new SolverOptions { AllowPartial = true });

            Assert.AreEqual(34L, result.ArrangementsEvaluated);
        }

        [Test]
        public void Solve_FindsTheDominatingArrangement()
        {
            var roster = new[]
            {
                new PilotStats("striker", 5f, 0f),
                new PilotStats("wall", 0f, 5f),
                new PilotStats("balanced", 1f, 1f)
            };

            var result = BoardSolver.Solve(SplitBoard(), roster);

            // balanced at the root, striker on attack, wall on defense: 1+5 attack and 1+5 defense.
            Assert.AreEqual(1, result.ParetoFrontier.Count);
            var best = result.ParetoFrontier[0];
            Assert.AreEqual(6f, best.TotalAttack);
            Assert.AreEqual(6f, best.TotalDefense);
            Assert.AreEqual("balanced", best.PilotBySlotId["root"]);
            Assert.AreEqual("striker", best.PilotBySlotId["a"]);
            Assert.AreEqual("wall", best.PilotBySlotId["d"]);
        }

        [Test]
        public void Solve_KeepsBothSidesOfATradeOff()
        {
            // One slot feeds both chains, so a striker or a wall must choose a side, and a balanced pilot is a third non-dominated option.
            var nodes = new[] { new SlotNode("root") };
            var chains = new[]
            {
                new ChainDefinition("atk", "lane", ChainRole.Attack, "root"),
                new ChainDefinition("def", "lane", ChainRole.Defense, "root")
            };
            var graph = new BoardGraph(nodes, chains);
            var roster = new[]
            {
                new PilotStats("striker", 5f, 0f),
                new PilotStats("wall", 0f, 5f),
                new PilotStats("weak", 1f, 1f)
            };

            var result = BoardSolver.Solve(graph, roster);

            Assert.AreEqual(3, result.ParetoFrontier.Count);
            Assert.AreEqual(5f, result.ParetoFrontier[0].TotalAttack);
            Assert.AreEqual(0f, result.ParetoFrontier[0].TotalDefense);
            Assert.AreEqual(1f, result.ParetoFrontier[1].TotalAttack);
            Assert.AreEqual(1f, result.ParetoFrontier[1].TotalDefense);
            Assert.AreEqual(5f, result.ParetoFrontier[2].TotalDefense);
        }

        [Test]
        public void Solve_ReportsWhenTheBestArrangementCrossesAToll()
        {
            var nodes = new[]
            {
                new SlotNode("root"),
                new SlotNode("a", "root", Operation.Subtract)
            };
            var chains = new[]
            {
                new ChainDefinition("atk", "lane", ChainRole.Attack, "a"),
                new ChainDefinition("def", "lane", ChainRole.Defense, "root")
            };
            var graph = new BoardGraph(nodes, chains);
            var roster = new[] { new PilotStats("striker", 5f, 0f), new PilotStats("nothing", 0f, 0f) };

            var result = BoardSolver.Solve(graph, roster);

            Assert.AreEqual(1, result.ParetoFrontier.Count);
            Assert.AreEqual(5f, result.ParetoFrontier[0].TotalAttack);
            Assert.IsTrue(result.ParetoFrontier[0].CrossesToll);
            Assert.AreEqual(1f, result.TollCrossingRate);
        }

        [Test]
        public void Solve_LockedSlotsStayEmptyAndBreakTheChain()
        {
            var roster = new[]
            {
                new PilotStats("striker", 5f, 0f),
                new PilotStats("wall", 0f, 5f),
                new PilotStats("balanced", 1f, 1f)
            };
            var options = new SolverOptions { LockedSlotIds = new HashSet<string> { "a" } };

            var result = BoardSolver.Solve(SplitBoard(), roster, options);

            Assert.IsTrue(result.ParetoFrontier.All(arrangement => !arrangement.PilotBySlotId.ContainsKey("a")));
            Assert.AreEqual(5f, result.BestValuePerChain["atk"]);
        }

        [Test]
        public void Solve_RefusesToRunAboveTheArrangementLimit()
        {
            var roster = new[] { new PilotStats("a", 1f, 1f), new PilotStats("b", 1f, 1f), new PilotStats("c", 1f, 1f) };
            var options = new SolverOptions { MaxArrangements = 3 };

            Assert.Throws<System.InvalidOperationException>(() => BoardSolver.Solve(SplitBoard(), roster, options));
        }
    }
}
