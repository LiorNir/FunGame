using System.Linq;
using NUnit.Framework;

namespace FunGame.Board.Tests
{
    public class BoardValidatorTests
    {
        private static ChainDefinition[] BothChains(string attackTerminal, string defenseTerminal)
        {
            return new[]
            {
                new ChainDefinition("atk", "lane", ChainRole.Attack, attackTerminal),
                new ChainDefinition("def", "lane", ChainRole.Defense, defenseTerminal)
            };
        }

        private static BoardGraph ValidBoard()
        {
            var nodes = new[]
            {
                new SlotNode("root"),
                new SlotNode("a", "root", Operation.Add),
                new SlotNode("d", "root", Operation.Multiply)
            };
            return new BoardGraph(nodes, BothChains("a", "d"));
        }

        [Test]
        public void ValidBoard_HasNoIssues()
        {
            var issues = BoardValidator.Validate(ValidBoard());

            Assert.IsEmpty(issues);
        }

        [Test]
        public void DuplicateSlotId_IsAnError()
        {
            var nodes = new[] { new SlotNode("root"), new SlotNode("root") };
            var graph = new BoardGraph(nodes, BothChains("root", "root"));

            var issues = BoardValidator.Validate(graph);

            Assert.IsTrue(issues.Any(issue => issue.Severity == IssueSeverity.Error && issue.Message.Contains("Duplicate slot")));
        }

        [Test]
        public void UnknownParent_IsAnError()
        {
            var nodes = new[] { new SlotNode("root"), new SlotNode("a", "ghost", Operation.Add) };
            var graph = new BoardGraph(nodes, BothChains("root", "root"));

            var issues = BoardValidator.Validate(graph);

            Assert.IsTrue(issues.Any(issue => issue.Message.Contains("unknown parent")));
        }

        [Test]
        public void Cycle_IsAnError()
        {
            var nodes = new[]
            {
                new SlotNode("root"),
                new SlotNode("x", "y", Operation.Add),
                new SlotNode("y", "x", Operation.Add)
            };
            var graph = new BoardGraph(nodes, BothChains("root", "root"));

            var issues = BoardValidator.Validate(graph);

            Assert.IsTrue(issues.Any(issue => issue.Message.Contains("cycle")));
        }

        [Test]
        public void ChildWithoutOperator_IsAnError()
        {
            var nodes = new[] { new SlotNode("root"), new SlotNode("a", "root") };
            var graph = new BoardGraph(nodes, BothChains("a", "a"));

            var issues = BoardValidator.Validate(graph);

            Assert.IsTrue(issues.Any(issue => issue.Message.Contains("no operator")));
        }

        [Test]
        public void RootWithOperator_IsAnError()
        {
            var nodes = new[] { new SlotNode("root", null, Operation.Add) };
            var graph = new BoardGraph(nodes, BothChains("root", "root"));

            var issues = BoardValidator.Validate(graph);

            Assert.IsTrue(issues.Any(issue => issue.Message.Contains("must not have a connection operator")));
        }

        [Test]
        public void LaneWithoutDefenseChain_IsAnError()
        {
            var nodes = new[] { new SlotNode("root") };
            var chains = new[] { new ChainDefinition("atk", "lane", ChainRole.Attack, "root") };
            var graph = new BoardGraph(nodes, chains);

            var issues = BoardValidator.Validate(graph);

            Assert.IsTrue(issues.Any(issue => issue.Message.Contains("no defense chain")));
        }

        [Test]
        public void ChainWithUnknownTerminal_IsAnError()
        {
            var nodes = new[] { new SlotNode("root") };
            var graph = new BoardGraph(nodes, BothChains("root", "ghost"));

            var issues = BoardValidator.Validate(graph);

            Assert.IsTrue(BoardValidator.HasErrors(issues));
        }

        [Test]
        public void SlotNotOnAnyChain_IsAWarningOnly()
        {
            var nodes = new[]
            {
                new SlotNode("root"),
                new SlotNode("a", "root", Operation.Add),
                new SlotNode("orphan", "root", Operation.Add)
            };
            var graph = new BoardGraph(nodes, BothChains("a", "a"));

            var issues = BoardValidator.Validate(graph);

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(IssueSeverity.Warning, issues[0].Severity);
            Assert.IsFalse(BoardValidator.HasErrors(issues));
        }

        [Test]
        public void BoardWithNoChains_IsAnError()
        {
            var graph = new BoardGraph(new[] { new SlotNode("root") }, new ChainDefinition[0]);

            var issues = BoardValidator.Validate(graph);

            Assert.IsTrue(BoardValidator.HasErrors(issues));
        }
    }
}
