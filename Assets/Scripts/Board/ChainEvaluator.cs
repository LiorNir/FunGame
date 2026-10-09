using System;
using System.Collections.Generic;

namespace FunGame.Board
{
    /// <summary>
    /// Evaluates chains from the root outward: <c>acc = acc OP pilotStat</c>, then the slot's bonus. Evaluation stops
    /// at the first empty slot; everything behind it is dormant. Subtraction never goes below zero, and dividing by a
    /// zero stat zeroes the chain and reports where it happened.
    /// </summary>
    public static class ChainEvaluator
    {
        public static ChainResult Evaluate(BoardGraph graph, ChainDefinition chain, Func<string, SlotContent> getContent)
        {
            var path = graph.GetPathFromRoot(chain.TerminalId);
            if (path == null)
            {
                throw new InvalidOperationException($"Chain '{chain.Id}' has no valid path to terminal '{chain.TerminalId}'.");
            }

            var steps = new List<ChainStep>(path.Count);
            var accumulated = 0f;
            var isBroken = false;
            string divideByZeroNodeId = null;

            for (var index = 0; index < path.Count; index++)
            {
                var node = path[index];
                var content = getContent(node.Id);

                if (isBroken)
                {
                    steps.Add(new ChainStep(node.Id, content.IsFilled ? SlotStatus.Dormant : SlotStatus.Empty, accumulated));
                    continue;
                }

                if (!content.IsFilled)
                {
                    isBroken = true;
                    steps.Add(new ChainStep(node.Id, SlotStatus.Empty, accumulated));
                    continue;
                }

                var stat = content.GetStat(chain.Role);
                if (index == 0)
                {
                    accumulated = stat;
                }
                else
                {
                    var operation = node.ConnectionOperation
                                    ?? throw new InvalidOperationException($"Node '{node.Id}' has no connection operator.");
                    if (operation == Operation.Divide && stat == 0f)
                    {
                        divideByZeroNodeId = node.Id;
                        accumulated = 0f;
                        isBroken = true;
                        steps.Add(new ChainStep(node.Id, SlotStatus.Active, accumulated));
                        continue;
                    }

                    accumulated = Apply(operation, accumulated, stat);
                }

                accumulated = node.Bonus.Apply(accumulated);
                steps.Add(new ChainStep(node.Id, SlotStatus.Active, accumulated));
            }

            return new ChainResult(chain, accumulated, steps, divideByZeroNodeId);
        }

        public static Dictionary<string, ChainResult> EvaluateAll(BoardGraph graph, Func<string, SlotContent> getContent)
        {
            var results = new Dictionary<string, ChainResult>();
            foreach (var chain in graph.Chains)
            {
                results[chain.Id] = Evaluate(graph, chain, getContent);
            }

            return results;
        }

        public static float Apply(Operation operation, float accumulated, float stat)
        {
            switch (operation)
            {
                case Operation.Add:
                    return accumulated + stat;
                case Operation.Subtract:
                    return Math.Max(0f, accumulated - stat);
                case Operation.Multiply:
                    return accumulated * stat;
                case Operation.Divide:
                    return accumulated / stat;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }
    }
}
