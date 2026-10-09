using System.Collections.Generic;
using System.Linq;
using FunGame.Board;

namespace BoardAuthoring
{
    /// <summary>Turns an authored board prefab into the pure board model used by the evaluator and solver.</summary>
    public static class BoardConverter
    {
        public static BoardGraph ToGraph(BoardLayoutAuthoring board)
        {
            var nodes = new List<SlotNode>();
            foreach (var slot in board.GetSlots())
            {
                var incoming = board.GetIncomingOperators(slot).FirstOrDefault(operatorSlot => operatorSlot.From != null);
                if (incoming == null)
                {
                    nodes.Add(new SlotNode(slot.Id, null, null, slot.Bonus));
                    continue;
                }

                nodes.Add(new SlotNode(slot.Id, incoming.From.Id, incoming.Operation, slot.Bonus));
            }

            var chains = new List<ChainDefinition>();
            var counters = new Dictionary<string, int>();
            foreach (var marker in board.GetChainMarkers())
            {
                var key = $"{marker.LaneId}-{marker.Role}";
                counters[key] = counters.TryGetValue(key, out var count) ? count + 1 : 1;
                chains.Add(new ChainDefinition($"{key}-{counters[key]}", marker.LaneId, marker.Role, marker.Slot.Id));
            }

            return new BoardGraph(nodes, chains);
        }

        public static HashSet<string> GetLockedSlotIds(BoardLayoutAuthoring board)
        {
            return new HashSet<string>(board.GetSlots().Where(slot => slot.IsLocked).Select(slot => slot.Id));
        }
    }
}
