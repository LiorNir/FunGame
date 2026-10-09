namespace FunGame.Board
{
    /// <summary>The stats of one pilot as the solver sees it.</summary>
    public readonly struct PilotStats
    {
        public string Name { get; }
        public float Attack { get; }
        public float Defense { get; }

        /// <summary>Whatever the caller wants to get back with the pilot, for example the pilot asset it came from.</summary>
        public object Source { get; }

        public PilotStats(string name, float attack, float defense, object source = null)
        {
            Source = source;
            Name = name;
            Attack = attack;
            Defense = defense;
        }

        public SlotContent ToSlotContent()
        {
            return new SlotContent(Attack, Defense);
        }
    }
}
