using System.Collections.Generic;
using System.Linq;

namespace FunGame.Board
{
    public enum IssueSeverity
    {
        Warning,
        Error
    }

    public readonly struct ValidationIssue
    {
        public IssueSeverity Severity { get; }
        public string Message { get; }

        public ValidationIssue(IssueSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }

        public override string ToString()
        {
            return $"{Severity}: {Message}";
        }
    }

    public static class BoardValidator
    {
        public static List<ValidationIssue> Validate(BoardGraph graph)
        {
            var issues = new List<ValidationIssue>();
            ValidateNodes(graph, issues);
            ValidateChains(graph, issues);
            ValidateLanes(graph, issues);
            return issues;
        }

        public static bool HasErrors(IEnumerable<ValidationIssue> issues)
        {
            return issues.Any(issue => issue.Severity == IssueSeverity.Error);
        }

        private static void ValidateNodes(BoardGraph graph, List<ValidationIssue> issues)
        {
            var seenIds = new HashSet<string>();
            foreach (var node in graph.Nodes)
            {
                if (string.IsNullOrEmpty(node.Id))
                {
                    issues.Add(Error("A slot has no id."));
                    continue;
                }

                if (!seenIds.Add(node.Id))
                {
                    issues.Add(Error($"Duplicate slot id '{node.Id}'."));
                }

                if (node.IsRoot)
                {
                    if (node.ConnectionOperation.HasValue)
                    {
                        issues.Add(Error($"Root slot '{node.Id}' must not have a connection operator."));
                    }

                    continue;
                }

                if (!graph.TryGetNode(node.ParentId, out _))
                {
                    issues.Add(Error($"Slot '{node.Id}' has unknown parent '{node.ParentId}'."));
                }
                else if (graph.GetPathFromRoot(node.Id) == null)
                {
                    issues.Add(Error($"Slot '{node.Id}' is part of a cycle."));
                }

                if (!node.ConnectionOperation.HasValue)
                {
                    issues.Add(Error($"Slot '{node.Id}' has no operator on its connection."));
                }
            }
        }

        private static void ValidateChains(BoardGraph graph, List<ValidationIssue> issues)
        {
            var slotsOnChains = new HashSet<string>();
            var chainIds = new HashSet<string>();
            foreach (var chain in graph.Chains)
            {
                if (!chainIds.Add(chain.Id))
                {
                    issues.Add(Error($"Duplicate chain id '{chain.Id}'."));
                }

                var path = graph.GetPathFromRoot(chain.TerminalId);
                if (path == null)
                {
                    issues.Add(Error($"Chain '{chain.Id}' has no valid path to terminal '{chain.TerminalId}'."));
                    continue;
                }

                foreach (var node in path)
                {
                    slotsOnChains.Add(node.Id);
                }
            }

            foreach (var node in graph.Nodes.Where(node => node.Id != null && !slotsOnChains.Contains(node.Id)))
            {
                issues.Add(Warning($"Slot '{node.Id}' is not on any chain, so it can never contribute."));
            }
        }

        private static void ValidateLanes(BoardGraph graph, List<ValidationIssue> issues)
        {
            var laneIds = graph.Chains.Select(chain => chain.LaneId).Distinct().ToList();
            if (laneIds.Count == 0)
            {
                issues.Add(Error("The board has no chains."));
                return;
            }

            if (laneIds.Count > 2)
            {
                issues.Add(Warning($"The board has {laneIds.Count} lanes; the design allows at most two."));
            }

            foreach (var laneId in laneIds)
            {
                var laneChains = graph.Chains.Where(chain => chain.LaneId == laneId).ToList();
                if (laneChains.All(chain => chain.Role != ChainRole.Attack))
                {
                    issues.Add(Error($"Lane '{laneId}' has no attack chain."));
                }

                if (laneChains.All(chain => chain.Role != ChainRole.Defense))
                {
                    issues.Add(Error($"Lane '{laneId}' has no defense chain."));
                }
            }
        }

        private static ValidationIssue Error(string message)
        {
            return new ValidationIssue(IssueSeverity.Error, message);
        }

        private static ValidationIssue Warning(string message)
        {
            return new ValidationIssue(IssueSeverity.Warning, message);
        }
    }
}
