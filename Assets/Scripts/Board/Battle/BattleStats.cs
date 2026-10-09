namespace FunGame.Battle
{
    /// <summary>
    /// Counters of what happened in a battle, kept by the engine itself (so they work with events and the log switched
    /// off). They are what a balance test learns from besides the outcome.
    /// </summary>
    public sealed class BattleStats
    {
        public int Places { get; set; }
        public int Replaces { get; set; }
        public int Ejects { get; set; }
        public int Unlocks { get; set; }

        /// <summary>AP spent on the player's actions.</summary>
        public int ApSpent { get; set; }

        /// <summary>AP left over when a turn was resolved. It does not carry over.</summary>
        public int ApWasted { get; set; }

        /// <summary>Damage the player dealt to enemies (after their defense), including damage past an enemy's last health.</summary>
        public float DamageDealt { get; set; }

        /// <summary>Damage enemies dealt to the player.</summary>
        public float DamageTaken { get; set; }

        /// <summary>Enemy damage that the defense chains absorbed.</summary>
        public float DamageAbsorbed { get; set; }

        public int PilotsFallen { get; set; }
        public int ChainsDestroyed { get; set; }
        public int EnemiesDefeated { get; set; }
        public int DivideByZeros { get; set; }

        /// <summary>Pilots an enemy locked (a target that had left the board is not counted).</summary>
        public int PilotsLocked { get; set; }

        /// <summary>A separate copy with the same counts (all counters, including ones added later).</summary>
        public BattleStats Copy()
        {
            return (BattleStats)MemberwiseClone();
        }
    }
}
