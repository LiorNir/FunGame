using System.Collections.Generic;

namespace FunGame.Battle
{
    public enum BattleOutcome
    {
        InProgress,
        Victory,
        Defeat,
        Stalemate
    }

    /// <summary>A pilot taking part in the battle. Wounds live on the pilot and lower its effective defense.</summary>
    public sealed class PilotUnit
    {
        public string Name { get; }
        public float BaseAttack { get; }
        public float BaseDefense { get; }

        /// <summary>The caller's data for this pilot (for example the pilot asset), carried through the battle for views.</summary>
        public object Source { get; }

        /// <summary>Fraction of the pilot's defense lost to damage, from 0 (unhurt) to 1.</summary>
        public float DefenseWound { get; set; }

        /// <summary>True once the pilot's chain was destroyed. A fallen pilot is gone from the battle.</summary>
        public bool Fallen { get; set; }

        /// <summary>Identifies the pilot within its battle (its position in the deck), so an intent can name it even in a copy of the battle.</summary>
        public int Id { get; internal set; }

        /// <summary>How many more of the player's turns the pilot is locked for (cannot be ejected or replaced). 0 = free.</summary>
        public int LockedTurns { get; set; }

        public bool IsLocked => LockedTurns > 0;

        public float EffectiveAttack => BaseAttack;
        public float EffectiveDefense => BaseDefense * (1f - DefenseWound);

        public PilotUnit(string name, float baseAttack, float baseDefense, object source = null)
        {
            Source = source;
            Name = name;
            BaseAttack = baseAttack;
            BaseDefense = baseDefense;
        }

        /// <summary>A separate pilot with the same stats and the same wounds, for copying a battle.</summary>
        public PilotUnit Copy()
        {
            return new PilotUnit(Name, BaseAttack, BaseDefense, Source) { DefenseWound = DefenseWound, Fallen = Fallen, Id = Id, LockedTurns = LockedTurns };
        }

        public override string ToString()
        {
            return Fallen ? $"{Name} (fallen)"
                : $"{Name} {DisplayFormat.Value(BaseAttack)}/{DisplayFormat.Value(EffectiveDefense)}{(IsLocked ? $" [locked {LockedTurns}]" : string.Empty)}";
        }
    }

    public sealed class EnemyUnit
    {
        public EnemySpec Spec { get; }
        public float Health { get; set; }

        /// <summary>The action revealed for the current turn.</summary>
        public EnemyActionData Intent { get; set; }

        /// <summary>
        /// For each effect of <see cref="Intent"/> (same order), the ids of the pilots it targets, chosen when the intent was
        /// revealed. Empty lists for effects that target nobody.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<int>> IntentTargets { get; set; } = new IReadOnlyList<int>[0];

        public int NextSequentialAction { get; set; }

        public EnemyUnit(EnemySpec spec)
        {
            Spec = spec;
            Health = spec.MaxHealth;
        }

        public EnemyUnit Copy()
        {
            return new EnemyUnit(Spec) { Health = Health, Intent = Intent, IntentTargets = IntentTargets, NextSequentialAction = NextSequentialAction };
        }
    }

    public sealed class LaneState
    {
        public LaneSpec Spec { get; }

        /// <summary>The enemy currently in the lane, or null if the last one died and the next has not appeared yet.</summary>
        public EnemyUnit Current { get; set; }

        public int EnemiesSpawned { get; set; }

        public bool HasMoreEnemies => EnemiesSpawned < Spec.Enemies.Count;
        public bool IsCleared => Current == null && !HasMoreEnemies;

        public LaneState(LaneSpec spec)
        {
            Spec = spec;
        }
    }

    /// <summary>A pilot on the board that a revealed enemy intent is aimed at.</summary>
    public readonly struct IntentTarget
    {
        public string LaneId { get; }
        public IEnemyEffect Effect { get; }
        public string SlotId { get; }
        public PilotUnit Pilot { get; }

        public IntentTarget(string laneId, IEnemyEffect effect, string slotId, PilotUnit pilot)
        {
            LaneId = laneId;
            Effect = effect;
            SlotId = slotId;
            Pilot = pilot;
        }
    }
}
