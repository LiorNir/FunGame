using System.Collections.Generic;

namespace FunGame.Battle
{
    /// <summary>The kinds of ability a pilot asset can have (one entry per ability class).</summary>
    public enum PilotAbilityKind
    {
        None,
        Surge
    }

    public static class PilotAbilities
    {
        /// <summary>The ability of a kind at an AP cost, or null for <see cref="PilotAbilityKind.None"/>.</summary>
        public static IPilotAbility Create(PilotAbilityKind kind, int apCost)
        {
            switch (kind)
            {
                case PilotAbilityKind.Surge:
                    return new SurgeAbility(apCost);
                default:
                    return null;
            }
        }

        public static PilotAbilityKind KindOf(IPilotAbility ability)
        {
            return ability is SurgeAbility ? PilotAbilityKind.Surge : PilotAbilityKind.None;
        }
    }

    /// <summary>
    /// Something a pilot can do once per encounter for its own AP cost. Each kind of ability is a small class, so a new one
    /// does not change the engine. The first kind works while the pilot is on the board.
    /// </summary>
    public interface IPilotAbility
    {
        /// <summary>Short name for menus and logs, for example "Surge".</summary>
        string Name { get; }

        /// <summary>The AP it costs to use. Every ability has its own.</summary>
        int ApCost { get; }

        /// <summary>What it does, in a few words, for menus and cards.</summary>
        string Describe();

        /// <summary>Applies the ability to its pilot (the engine has already checked and paid for it).</summary>
        void Activate(PilotUnit pilot);

        /// <summary>The same ability with another AP cost (for comparing costs in the lab).</summary>
        IPilotAbility WithCost(int apCost);
    }

    /// <summary>
    /// "Surge": the pilot's attack is multiplied this turn and drops to nothing next turn (harsh, but it scales with
    /// upgrades and multiply slots, and the crash pushes the player to swap or eject on the attack side).
    /// </summary>
    public sealed class SurgeAbility : IPilotAbility
    {
        public float Multiplier { get; }
        public int ApCost { get; }
        public string Name => "Surge";

        public SurgeAbility(int apCost, float multiplier = 2f)
        {
            ApCost = apCost;
            Multiplier = multiplier;
        }

        public string Describe()
        {
            return $"ATK x{DisplayFormat.Value(Multiplier)} this turn, ATK 0 next turn";
        }

        public void Activate(PilotUnit pilot)
        {
            pilot.AttackModifiers.Add(new StatModifier(Multiplier, 0, 1));
            pilot.AttackModifiers.Add(new StatModifier(0f, 1, 1));
        }

        public IPilotAbility WithCost(int apCost)
        {
            return new SurgeAbility(apCost, Multiplier);
        }
    }

    /// <summary>
    /// A temporary change to one of a pilot's stats (attack or defense): a multiplier that starts in some turns and lasts some turns. Lives on the
    /// pilot, so it travels with it (to the hand and back).
    /// </summary>
    public sealed class StatModifier
    {
        public float Multiplier { get; }

        /// <summary>Turns until it starts: 0 = it applies this turn.</summary>
        public int StartsIn { get; private set; }

        /// <summary>How many turns it applies once started.</summary>
        public int TurnsLeft { get; private set; }

        public bool IsActive => StartsIn == 0 && TurnsLeft > 0;
        public bool IsOver => TurnsLeft <= 0;

        public StatModifier(float multiplier, int startsIn, int turns)
        {
            Multiplier = multiplier;
            StartsIn = startsIn;
            TurnsLeft = turns;
        }

        /// <summary>Moves on by one turn: a waiting modifier comes closer, an active one uses up a turn.</summary>
        public void Advance()
        {
            if (StartsIn > 0)
            {
                StartsIn--;
            }
            else
            {
                TurnsLeft--;
            }
        }

        public StatModifier Copy()
        {
            return new StatModifier(Multiplier, StartsIn, TurnsLeft);
        }

        /// <summary>The product of the active modifiers' multipliers (1 when none is active).</summary>
        public static float Product(IEnumerable<StatModifier> modifiers, bool nextTurn = false)
        {
            var product = 1f;
            foreach (var modifier in modifiers)
            {
                var active = nextTurn
                    ? (modifier.StartsIn == 1 && modifier.TurnsLeft > 0) || (modifier.StartsIn == 0 && modifier.TurnsLeft > 1)
                    : modifier.IsActive;
                if (active)
                {
                    product *= modifier.Multiplier;
                }
            }

            return product;
        }
    }
}
