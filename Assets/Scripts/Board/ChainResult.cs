using System.Collections.Generic;

namespace FunGame.Board
{
    public enum SlotStatus
    {
        /// <summary>Filled and connected to the root through filled slots; contributes to the chain.</summary>
        Active,
        /// <summary>Not filled.</summary>
        Empty,
        /// <summary>Filled but cut off from the root by a gap, so it contributes nothing.</summary>
        Dormant
    }

    public readonly struct ChainStep
    {
        public string NodeId { get; }
        public SlotStatus Status { get; }

        /// <summary>The accumulated value after this node (unchanged for non-active nodes).</summary>
        public float AccumulatedValue { get; }

        public ChainStep(string nodeId, SlotStatus status, float accumulatedValue)
        {
            NodeId = nodeId;
            Status = status;
            AccumulatedValue = accumulatedValue;
        }
    }

    public sealed class ChainResult
    {
        public ChainDefinition Chain { get; }
        public float Value { get; }
        public IReadOnlyList<ChainStep> Steps { get; }
        public bool DividedByZero { get; }
        public string DivideByZeroNodeId { get; }

        public ChainResult(ChainDefinition chain, float value, IReadOnlyList<ChainStep> steps, string divideByZeroNodeId)
        {
            Chain = chain;
            Value = value;
            Steps = steps;
            DivideByZeroNodeId = divideByZeroNodeId;
            DividedByZero = divideByZeroNodeId != null;
        }
    }
}
