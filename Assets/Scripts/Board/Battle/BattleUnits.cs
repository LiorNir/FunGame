using System.Collections.Generic;
using System.Linq;

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

        /// <summary>The pilot's ability (null if none); <see cref="AbilityUsed"/> once it has been used this encounter.</summary>
        public IPilotAbility Ability { get; }

        public bool AbilityUsed { get; set; }

        /// <summary>True if the pilot has an ability it has not used yet.</summary>
        public bool HasUnusedAbility => Ability != null && !AbilityUsed;

        /// <summary>Temporary changes to attack (from abilities, later from enemy effects); they move on at every turn start.</summary>
        public List<StatModifier> AttackModifiers { get; } = new List<StatModifier>();

        /// <summary>Temporary changes to defense (enemy snipes); they move on at every turn start like the attack ones.</summary>
        public List<StatModifier> DefenseModifiers { get; } = new List<StatModifier>();

        public float EffectiveAttack => BaseAttack * StatModifier.Product(AttackModifiers);

        /// <summary>The attack the pilot will have next turn, by the modifiers already on it (known, not guessed).</summary>
        public float AttackNextTurn => BaseAttack * StatModifier.Product(AttackModifiers, nextTurn: true);

        /// <summary>Moves the pilot's temporary modifiers on by one turn.</summary>
        public void AdvanceTurn()
        {
            foreach (var modifier in AttackModifiers.Concat(DefenseModifiers))
            {
                modifier.Advance();
            }

            AttackModifiers.RemoveAll(modifier => modifier.IsOver);
            DefenseModifiers.RemoveAll(modifier => modifier.IsOver);
        }

        /// <summary>
        /// Rounds the defense left after wounds up to a multiple of <paramref name="step"/> (see
        /// <see cref="BattleRules.DefenseStep"/>); nothing for a step of 0, an unhurt pilot or one with nothing left.
        /// </summary>
        public void SnapWound(float step)
        {
            if (step <= 0f || BaseDefense <= 0f || DefenseWound <= 0f || DefenseWound >= 1f)
            {
                return;
            }

            var left = BaseDefense * (1f - DefenseWound);
            var snapped = (float)System.Math.Ceiling(left / step - 0.0001f) * step;
            DefenseWound = System.Math.Max(0f, 1f - System.Math.Min(snapped, BaseDefense) / BaseDefense);
        }

        /// <summary>A turn start spent resting in the hand: modifiers that last until rested (the Surge crash) end.</summary>
        public void Rest()
        {
            AttackModifiers.RemoveAll(modifier => modifier.LastsUntilRested);
            DefenseModifiers.RemoveAll(modifier => modifier.LastsUntilRested);
        }

        /// <summary>The attack the pilot will have next turn if it stays in the hand (it rests at the turn start).</summary>
        public float AttackAfterRest => BaseAttack * StatModifier.Product(AttackModifiers.Where(modifier => !modifier.LastsUntilRested), nextTurn: true);
        public float EffectiveDefense => BaseDefense * (1f - DefenseWound) * StatModifier.Product(DefenseModifiers);

        public PilotUnit(string name, float baseAttack, float baseDefense, object source = null, IPilotAbility ability = null)
        {
            Source = source;
            Ability = ability;
            Name = name;
            BaseAttack = baseAttack;
            BaseDefense = baseDefense;
        }

        /// <summary>A separate pilot with the same stats and the same wounds, for copying a battle.</summary>
        public PilotUnit Copy()
        {
            var copy = new PilotUnit(Name, BaseAttack, BaseDefense, Source, Ability)
            {
                DefenseWound = DefenseWound, Fallen = Fallen, Id = Id, LockedTurns = LockedTurns, AbilityUsed = AbilityUsed
            };
            foreach (var modifier in AttackModifiers)
            {
                copy.AttackModifiers.Add(modifier.Copy());
            }

            foreach (var modifier in DefenseModifiers)
            {
                copy.DefenseModifiers.Add(modifier.Copy());
            }

            return copy;
        }

        public override string ToString()
        {
            return Fallen ? $"{Name} (fallen)"
                : $"{Name} {DisplayFormat.Value(EffectiveAttack)}/{DisplayFormat.Value(EffectiveDefense)}{(IsLocked ? $" [locked {LockedTurns}]" : string.Empty)}";
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
