namespace FunGame.Board
{
    /// <summary>
    /// A pilot slot on the board. Roots have no parent. Every other node is joined to its parent by a connection
    /// that carries an operator.
    /// </summary>
    public sealed class SlotNode
    {
        public string Id { get; }
        public string ParentId { get; }
        public Operation? ConnectionOperation { get; }
        public NodeBonus Bonus { get; }

        public bool IsRoot => ParentId == null;

        public SlotNode(string id, string parentId = null, Operation? connectionOperation = null, NodeBonus bonus = default)
        {
            Id = id;
            ParentId = parentId;
            ConnectionOperation = connectionOperation;
            Bonus = bonus;
        }
    }
}
