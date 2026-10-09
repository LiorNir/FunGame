using System.Collections.Generic;

namespace FunGame.Battle
{
    public enum BattleEventType
    {
        /// <summary>A = turn number.</summary>
        TurnStarted,

        /// <summary>LaneId, Subject = enemy name, A = health, B = max health, C = the enemy's number in the lane (1 = first).</summary>
        EnemySpawned,

        /// <summary>LaneId, Subject = action name, A = attack, B = defense.</summary>
        IntentRevealed,

        /// <summary>Subject = slot id.</summary>
        SlotUnlocked,

        /// <summary>LaneId, Subject = chain id, Slots = the slot where the division by zero happened.</summary>
        DivideByZero,

        /// <summary>LaneId, A = the player's attack value, B = the enemy's defense, C = damage dealt. Only raised when the attack value is above zero.</summary>
        PlayerAttacked,

        /// <summary>LaneId, A = enemy health after, B = max health. Only raised when damage was dealt.</summary>
        EnemyDamaged,

        /// <summary>LaneId, Subject = enemy name.</summary>
        EnemyDefeated,

        /// <summary>LaneId, Subject = action name, A = attack.</summary>
        EnemyAttacked,

        /// <summary>LaneId, Subject = chain id, A = damage absorbed, B = chain value before, C = value after, Slots/SlotValues = wounded slots and their new wound fraction.</summary>
        ChainAbsorbed,

        /// <summary>LaneId, Subject = chain id, A = damage absorbed, B = chain value before, Slots = the slots whose pilots fell.</summary>
        ChainDestroyed,

        /// <summary>A = damage, B = player health after.</summary>
        PlayerDamaged,

        Victory,
        Defeat
    }

    /// <summary>
    /// A step of a turn's resolution, in the order it happened. The engine's state is already final when the events are
    /// read, so each event carries the numbers a view needs to show that step.
    /// </summary>
    public sealed class BattleEvent
    {
        public BattleEventType Type { get; }
        public string LaneId { get; }
        public string Subject { get; }
        public float A { get; }
        public float B { get; }
        public float C { get; }
        public IReadOnlyList<string> Slots { get; }
        public IReadOnlyList<float> SlotValues { get; }

        public BattleEvent(
            BattleEventType type,
            string laneId = null,
            string subject = null,
            float a = 0f,
            float b = 0f,
            float c = 0f,
            IReadOnlyList<string> slots = null,
            IReadOnlyList<float> slotValues = null)
        {
            Type = type;
            LaneId = laneId;
            Subject = subject;
            A = a;
            B = b;
            C = c;
            Slots = slots ?? new string[0];
            SlotValues = slotValues ?? new float[0];
        }

        public override string ToString()
        {
            return $"{Type} lane={LaneId} subject={Subject} a={A} b={B} c={C} slots=[{string.Join(",", Slots)}]";
        }
    }
}
