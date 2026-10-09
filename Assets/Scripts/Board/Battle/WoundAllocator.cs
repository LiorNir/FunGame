using System;
using System.Collections.Generic;
using FunGame.Board;

namespace FunGame.Battle
{
    /// <summary>
    /// Spreads damage over a defense chain as wounds on its pilots. Every contributing pilot loses the same fraction
    /// of its defense, chosen so the chain loses exactly the damage taken. Pilots whose reduction would raise the chain
    /// value (the operand of a subtract or divide) take no wounds.
    /// </summary>
    public static class WoundAllocator
    {
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// Lowers the chain's value by <paramref name="damage"/> by wounding its pilots. Returns false if the chain
        /// cannot be lowered that far (it is destroyed instead, and the caller should make its pilots fall).
        /// </summary>
        public static bool TryApplyDamage(
            BoardGraph graph,
            ChainDefinition chain,
            float currentValue,
            float damage,
            Func<string, SlotContent> contentOf,
            IReadOnlyDictionary<string, PilotUnit> pilotBySlot)
        {
            return TryApplyDamage(graph, chain, currentValue, damage, contentOf, pilotBySlot, out _);
        }

        /// <param name="woundedSlots">The slots whose pilots were wounded (empty if the chain could not take the damage).</param>
        public static bool TryApplyDamage(
            BoardGraph graph,
            ChainDefinition chain,
            float currentValue,
            float damage,
            Func<string, SlotContent> contentOf,
            IReadOnlyDictionary<string, PilotUnit> pilotBySlot,
            out List<string> woundedSlots)
        {
            woundedSlots = new List<string>();
            var result = ChainEvaluator.Evaluate(graph, chain, contentOf);
            var contributors = FindContributors(graph, result);
            var target = currentValue - damage;

            // value(0): everything that contributes is wounded completely. If that is still above the target, the
            // chain cannot take this much damage, so it is destroyed.
            if (EvaluateScaled(graph, chain, contentOf, contributors, 0f) > target + Epsilon)
            {
                return false;
            }

            // a hit equal to the chain's whole value wounds every contributor completely (exactly, not by bisection)
            var low = 0f;
            var high = target <= Epsilon ? 0f : 1f;
            for (var i = 0; i < 40 && high > 0f; i++)
            {
                var middle = (low + high) * 0.5f;
                if (EvaluateScaled(graph, chain, contentOf, contributors, middle) > target)
                {
                    high = middle;
                }
                else
                {
                    low = middle;
                }
            }

            var factor = low;
            foreach (var slotId in contributors)
            {
                var pilot = pilotBySlot[slotId];
                pilot.DefenseWound = 1f - (1f - pilot.DefenseWound) * factor;
                woundedSlots.Add(slotId);
            }

            return true;
        }

        private static HashSet<string> FindContributors(BoardGraph graph, ChainResult result)
        {
            var contributors = new HashSet<string>();
            for (var index = 0; index < result.Steps.Count; index++)
            {
                var step = result.Steps[index];
                if (step.Status != SlotStatus.Active)
                {
                    break;
                }

                graph.TryGetNode(step.NodeId, out var node);
                var isToll = node.ConnectionOperation == Operation.Subtract || node.ConnectionOperation == Operation.Divide;
                if (index == 0 || !isToll)
                {
                    contributors.Add(step.NodeId);
                }
            }

            return contributors;
        }

        private static float EvaluateScaled(
            BoardGraph graph,
            ChainDefinition chain,
            Func<string, SlotContent> contentOf,
            HashSet<string> contributors,
            float factor)
        {
            SlotContent Scaled(string slotId)
            {
                var content = contentOf(slotId);
                return contributors.Contains(slotId) && content.IsFilled
                    ? new SlotContent(content.Attack, content.Defense * factor)
                    : content;
            }

            return ChainEvaluator.Evaluate(graph, chain, Scaled).Value;
        }
    }
}
