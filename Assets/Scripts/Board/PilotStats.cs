using FunGame.Battle;

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

        /// <summary>The pilot's ability, or null if it has none.</summary>
        public IPilotAbility Ability { get; }

        public PilotStats(string name, float attack, float defense, object source = null, IPilotAbility ability = null)
        {
            Source = source;
            Ability = ability;
            Name = name;
            Attack = attack;
            Defense = defense;
        }

        /// <summary>The same pilot (name, source, ability) with other stats, for example a stronger roster in the lab.</summary>
        public PilotStats WithStats(float attack, float defense)
        {
            return new PilotStats(Name, attack, defense, Source, Ability);
        }

        public SlotContent ToSlotContent()
        {
            return new SlotContent(Attack, Defense);
        }
    }
}
