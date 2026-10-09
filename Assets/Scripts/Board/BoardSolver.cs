using System;
using System.Collections.Generic;

namespace FunGame.Board
{
    /// <summary>
    /// Brute-forces every way of placing a roster on a board and keeps the arrangements on the attack/defense
    /// Pareto frontier. Uses the same <see cref="ChainEvaluator"/> as the game.
    /// </summary>
    public static class BoardSolver
    {
        private const float Epsilon = 0.0001f;

        public static double CountArrangements(int slotCount, int pilotCount, bool allowPartial)
        {
            if (allowPartial)
            {
                double total = 0;
                for (var placed = 0; placed <= Math.Min(slotCount, pilotCount); placed++)
                {
                    total += Choose(pilotCount, placed) * Permutations(slotCount, placed);
                }

                return total;
            }

            var target = Math.Min(slotCount, pilotCount);
            return Choose(slotCount, target) * Permutations(pilotCount, target);
        }

        public static SolveResult Solve(BoardGraph graph, IReadOnlyList<PilotStats> roster, SolverOptions options = null)
        {
            options = options ?? new SolverOptions();
            var slots = new List<SlotNode>();
            foreach (var node in graph.Nodes)
            {
                if (!options.LockedSlotIds.Contains(node.Id))
                {
                    slots.Add(node);
                }
            }

            var count = CountArrangements(slots.Count, roster.Count, options.AllowPartial);
            if (count > options.MaxArrangements)
            {
                throw new InvalidOperationException(
                    $"{count:N0} arrangements exceeds the limit of {options.MaxArrangements:N0}.");
            }

            var search = new Search(graph, roster, slots, options.AllowPartial);
            search.Run();
            return search.BuildResult();
        }

        private static double Choose(int n, int k)
        {
            double result = 1;
            for (var i = 1; i <= k; i++)
            {
                result = result * (n - k + i) / i;
            }

            return result;
        }

        private static double Permutations(int n, int k)
        {
            double result = 1;
            for (var i = 0; i < k; i++)
            {
                result *= n - i;
            }

            return result;
        }

        private sealed class Search
        {
            private readonly BoardGraph _graph;
            private readonly IReadOnlyList<PilotStats> _roster;
            private readonly List<SlotNode> _slots;
            private readonly int _target;
            private readonly bool _allowPartial;

            private readonly SlotContent[] _contentBySlot;
            private readonly int[] _pilotBySlot;
            private readonly bool[] _pilotUsed;
            private readonly Dictionary<string, int> _slotIndexById = new Dictionary<string, int>();
            private readonly List<SolvedArrangement> _frontier = new List<SolvedArrangement>();
            private readonly Dictionary<string, float> _bestPerChain = new Dictionary<string, float>();
            private long _evaluated;

            public Search(BoardGraph graph, IReadOnlyList<PilotStats> roster, List<SlotNode> slots, bool allowPartial)
            {
                _graph = graph;
                _roster = roster;
                _slots = slots;
                _allowPartial = allowPartial;
                _target = allowPartial ? int.MaxValue : Math.Min(slots.Count, roster.Count);
                _contentBySlot = new SlotContent[slots.Count];
                _pilotBySlot = new int[slots.Count];
                _pilotUsed = new bool[roster.Count];
                for (var i = 0; i < slots.Count; i++)
                {
                    _slotIndexById[slots[i].Id] = i;
                }
            }

            public void Run()
            {
                Recurse(0, 0);
            }

            public SolveResult BuildResult()
            {
                _frontier.Sort((a, b) => b.TotalAttack.CompareTo(a.TotalAttack));
                return new SolveResult(_evaluated, _frontier, _bestPerChain);
            }

            private void Recurse(int slotIndex, int filled)
            {
                if (slotIndex == _slots.Count)
                {
                    if (_allowPartial || filled == _target)
                    {
                        Evaluate();
                    }

                    return;
                }

                var remainingAfterThis = _slots.Count - slotIndex - 1;
                if (_allowPartial || remainingAfterThis >= _target - filled)
                {
                    _contentBySlot[slotIndex] = SlotContent.Empty;
                    _pilotBySlot[slotIndex] = -1;
                    Recurse(slotIndex + 1, filled);
                }

                if (filled >= _target)
                {
                    return;
                }

                for (var pilot = 0; pilot < _roster.Count; pilot++)
                {
                    if (_pilotUsed[pilot])
                    {
                        continue;
                    }

                    _pilotUsed[pilot] = true;
                    _contentBySlot[slotIndex] = _roster[pilot].ToSlotContent();
                    _pilotBySlot[slotIndex] = pilot;
                    Recurse(slotIndex + 1, filled + 1);
                    _pilotUsed[pilot] = false;
                }
            }

            private SlotContent GetContent(string slotId)
            {
                return _slotIndexById.TryGetValue(slotId, out var index) ? _contentBySlot[index] : SlotContent.Empty;
            }

            private void Evaluate()
            {
                _evaluated++;
                var results = ChainEvaluator.EvaluateAll(_graph, GetContent);

                float totalAttack = 0f;
                float totalDefense = 0f;
                var crossesToll = false;
                var usesBonus = false;
                foreach (var result in results.Values)
                {
                    if (result.Chain.Role == ChainRole.Attack)
                    {
                        totalAttack += result.Value;
                    }
                    else
                    {
                        totalDefense += result.Value;
                    }

                    if (!_bestPerChain.TryGetValue(result.Chain.Id, out var best) || result.Value > best)
                    {
                        _bestPerChain[result.Chain.Id] = result.Value;
                    }

                    foreach (var step in result.Steps)
                    {
                        if (step.Status != SlotStatus.Active || !_graph.TryGetNode(step.NodeId, out var node))
                        {
                            continue;
                        }

                        if (node.ConnectionOperation == Operation.Subtract || node.ConnectionOperation == Operation.Divide)
                        {
                            crossesToll = true;
                        }

                        if (node.Bonus.Kind != BonusKind.None)
                        {
                            usesBonus = true;
                        }
                    }
                }

                TryAddToFrontier(results, totalAttack, totalDefense, crossesToll, usesBonus);
            }

            private void TryAddToFrontier(
                Dictionary<string, ChainResult> results,
                float totalAttack,
                float totalDefense,
                bool crossesToll,
                bool usesBonus)
            {
                foreach (var existing in _frontier)
                {
                    if (existing.TotalAttack >= totalAttack - Epsilon && existing.TotalDefense >= totalDefense - Epsilon)
                    {
                        return;
                    }
                }

                _frontier.RemoveAll(existing =>
                    totalAttack >= existing.TotalAttack - Epsilon && totalDefense >= existing.TotalDefense - Epsilon);

                var pilots = new Dictionary<string, string>();
                for (var i = 0; i < _slots.Count; i++)
                {
                    if (_pilotBySlot[i] >= 0)
                    {
                        pilots[_slots[i].Id] = _roster[_pilotBySlot[i]].Name;
                    }
                }

                var chainValues = new Dictionary<string, float>();
                foreach (var pair in results)
                {
                    chainValues[pair.Key] = pair.Value.Value;
                }

                _frontier.Add(new SolvedArrangement(pilots, chainValues, totalAttack, totalDefense, crossesToll, usesBonus));
            }
        }
    }
}
