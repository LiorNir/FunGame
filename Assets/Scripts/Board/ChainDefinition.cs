namespace FunGame.Board
{
    /// <summary>The path from a root to <see cref="TerminalId"/>, read as an attack or defense chain of one lane.</summary>
    public sealed class ChainDefinition
    {
        public string Id { get; }
        public string LaneId { get; }
        public ChainRole Role { get; }
        public string TerminalId { get; }

        public ChainDefinition(string id, string laneId, ChainRole role, string terminalId)
        {
            Id = id;
            LaneId = laneId;
            Role = role;
            TerminalId = terminalId;
        }
    }
}
