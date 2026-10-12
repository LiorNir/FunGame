using System.Collections.Generic;

namespace FunGame.Battle
{
    public enum BattleEventType
    {
        /// <summary>A = turn number.</summary>
        TurnStarted,

        /// <summary>The planning phase begins (after spawns, intents, unlocks and the draw). A = player health, B = AP.</summary>
        PlanningStarted,

        /// <summary>Subject = pilot name, Slots = the slot, A = AP cost, B = AP left, Detail = the replaced pilot's name (or null).</summary>
        PilotPlaced,

        /// <summary>Subject = pilot name, Slots = the slot, A = AP cost, B = AP left.</summary>
        PilotEjected,

        /// <summary>Subject = slot id, A = AP cost, B = AP left. The player paid to open the slot (see <see cref="SlotUnlocked"/> for slots that open by themselves).</summary>
        SlotBought,

        /// <summary>Subject = pilot name, Slots = the slot.</summary>
        FallenCleared,

        /// <summary>Subject = pilot name, Detail = ability name, Slots = the pilot's slot, A = AP cost, B = AP left.</summary>
        AbilityActivated,

        /// <summary>A = turn number. Resolution begins; the board is as the player left it.</summary>
        TurnResolving,

        /// <summary>LaneId, Subject = enemy name, A = health, B = max health, C = the enemy's number in the lane (1 = first).</summary>
        EnemySpawned,

        /// <summary>LaneId, Subject = action name, A = attack, B = defense, Slots = the slots of the pilots its effects target.</summary>
        IntentRevealed,

        /// <summary>Subject = slot id, Detail = why it opened. Raised for slots that open by themselves (turn or wave).</summary>
        SlotUnlocked,

        /// <summary>LaneId, Subject = chain id, Slots = the slot where the division by zero happened.</summary>
        DivideByZero,

        /// <summary>LaneId, A = the player's attack value (may be 0), B = the enemy's defense, C = damage dealt.</summary>
        PlayerAttacked,

        /// <summary>LaneId, A = enemy health after, B = max health. Only raised when damage was dealt.</summary>
        EnemyDamaged,

        /// <summary>LaneId, Subject = enemy name.</summary>
        EnemyDefeated,

        /// <summary>LaneId, Subject = action name. The enemy's turn begins; its effects follow.</summary>
        EnemyActed,

        /// <summary>LaneId, Subject = action name, A = attack.</summary>
        EnemyAttacked,

        /// <summary>LaneId, Subject = chain id, A = damage absorbed, B = chain value before, C = value after, Slots/SlotValues = wounded slots and their new wound fraction.</summary>
        ChainAbsorbed,

        /// <summary>LaneId, Subject = chain id, A = damage absorbed, B = chain value before, Slots = the slots whose pilots fell.</summary>
        ChainDestroyed,

        /// <summary>LaneId, A = damage, B = player health after.</summary>
        PlayerDamaged,

        /// <summary>LaneId, Subject = pilot name, Slots = its slot, A = the turns it is locked for.</summary>
        PilotLocked,

        /// <summary>LaneId, Subject = pilot name, Slots = its slot, A = the share taken off both stats (0-1), B = for how many turns.</summary>
        PilotSniped,

        /// <summary>LaneId, Subject = pilot name, Slots = its slot: a snipe found the pilot spent and it fell.</summary>
        PilotAssassinated,

        /// <summary>LaneId, Subject = the effect (for example "LOCK 2"), Detail = why it did nothing (its target left the board, or there was none).</summary>
        EffectFizzled,

        Victory,
        Defeat,

        /// <summary>The battle was stopped at the turn limit.</summary>
        Stalemate
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

        /// <summary>A second name where an event needs one (see the event type).</summary>
        public string Detail { get; }

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
            IReadOnlyList<float> slotValues = null,
            string detail = null)
        {
            Detail = detail;
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
