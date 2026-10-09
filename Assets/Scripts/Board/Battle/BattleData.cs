using System.Collections.Generic;

namespace FunGame.Battle
{
    /// <summary>The global rules of a battle.</summary>
    public sealed class BattleRules
    {
        public int PlayerMaxHealth { get; set; } = 20;
        public int StartingActionPoints { get; set; } = 3;

        /// <summary>AP at the start of every turn. AP is refreshed to this value, not added to.</summary>
        public int ActionPointsPerTurn { get; set; } = 3;

        public int HandSize { get; set; } = 4;
        public int PlacePilotCost { get; set; } = 1;
        public int ReplacePilotCost { get; set; } = 1;

        /// <summary>Taking a living pilot off the board and back into the hand without placing another (placeholder cost, to be decided).</summary>
        public int EjectPilotCost { get; set; } = 1;
        public int ActivateAbilityCost { get; set; } = 1;

        /// <summary>
        /// Whether a pilot may be put on top of another pilot. Encounters switch this off until the game has taught it, so the
        /// early encounters can be played (and balanced) without it.
        /// </summary>
        public bool AllowReplace { get; set; } = true;

        /// <summary>Whether a living pilot may be taken off the board. Switched off like <see cref="AllowReplace"/> until taught.</summary>
        public bool AllowEject { get; set; } = true;

        public BattleRules Copy()
        {
            return (BattleRules)MemberwiseClone();
        }
    }

    public sealed class EnemyActionData
    {
        public string Name { get; }
        public int Attack { get; }
        public int Defense { get; }

        public EnemyActionData(string name, int attack, int defense)
        {
            Name = name;
            Attack = attack;
            Defense = defense;
        }

        public override string ToString()
        {
            return $"{Name} (ATK {Attack} / DEF {Defense})";
        }
    }

    public sealed class EnemySpec
    {
        public string Name { get; }
        public int MaxHealth { get; }
        public IReadOnlyList<EnemyActionData> Actions { get; }

        /// <summary>True: the next action is picked at random each turn. False: actions repeat in order.</summary>
        public bool RandomSelection { get; }

        /// <summary>The caller's data for this enemy (for example the enemy asset), for views.</summary>
        public object Source { get; }

        public EnemySpec(string name, int maxHealth, IReadOnlyList<EnemyActionData> actions, bool randomSelection, object source = null)
        {
            Source = source;
            Name = name;
            MaxHealth = maxHealth;
            Actions = actions;
            RandomSelection = randomSelection;
        }
    }

    /// <summary>Locking data of one pilot slot.</summary>
    public sealed class SlotRule
    {
        public string SlotId { get; }
        public bool Locked { get; }
        public int UnlockApCost { get; }
        public int UnlockAtWave { get; }
        public int UnlockAtTurn { get; }

        /// <summary>The lane whose enemy number counts for a wave unlock. Empty means the only lane of the board.</summary>
        public string UnlockLaneId { get; }

        public SlotRule(string slotId, bool locked, int unlockApCost, int unlockAtWave, int unlockAtTurn, string unlockLaneId = null)
        {
            UnlockLaneId = unlockLaneId;
            SlotId = slotId;
            Locked = locked;
            UnlockApCost = unlockApCost;
            UnlockAtWave = unlockAtWave;
            UnlockAtTurn = unlockAtTurn;
        }
    }

    public sealed class LaneSpec
    {
        public string Id { get; }

        /// <summary>Enemies in order: the first at the start, each next one after the previous dies.</summary>
        public IReadOnlyList<EnemySpec> Enemies { get; }

        public LaneSpec(string id, IReadOnlyList<EnemySpec> enemies)
        {
            Id = id;
            Enemies = enemies;
        }
    }
}
