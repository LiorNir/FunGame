using System.Collections.Generic;

namespace FunGame.Board
{
    /// <summary>
    /// The topology of a board: a forest of slots plus the chains that read it. The constructor never throws so that
    /// <see cref="BoardValidator"/> can report problems in an invalid board.
    /// </summary>
    public sealed class BoardGraph
    {
        private readonly Dictionary<string, SlotNode> _nodesById = new Dictionary<string, SlotNode>();

        public IReadOnlyList<SlotNode> Nodes { get; }
        public IReadOnlyList<ChainDefinition> Chains { get; }

        public BoardGraph(IEnumerable<SlotNode> nodes, IEnumerable<ChainDefinition> chains)
        {
            Nodes = new List<SlotNode>(nodes);
            Chains = new List<ChainDefinition>(chains);
            foreach (var node in Nodes)
            {
                if (node.Id != null && !_nodesById.ContainsKey(node.Id))
                {
                    _nodesById.Add(node.Id, node);
                }
            }
        }

        public bool TryGetNode(string id, out SlotNode node)
        {
            node = null;
            return id != null && _nodesById.TryGetValue(id, out node);
        }

        /// <summary>
        /// The nodes from the root down to <paramref name="nodeId"/>, or null if the node is unknown, an ancestor is
        /// missing, or the parent links form a cycle.
        /// </summary>
        public List<SlotNode> GetPathFromRoot(string nodeId)
        {
            var path = new List<SlotNode>();
            var visited = new HashSet<string>();
            var currentId = nodeId;
            while (currentId != null)
            {
                if (!visited.Add(currentId) || !TryGetNode(currentId, out var node))
                {
                    return null;
                }

                path.Add(node);
                currentId = node.ParentId;
            }

            path.Reverse();
            return path;
        }
    }
}
