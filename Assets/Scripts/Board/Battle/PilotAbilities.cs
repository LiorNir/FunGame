using System.Collections.Generic;

namespace FunGame.Battle
{
    /// <summary>The kinds of ability a pilot asset can have (one entry per ability class).</summary>
    public enum PilotAbilityKind
    {
        None,
        Surge,
        NoGuard
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
                case PilotAbilityKind.NoGuard:
                    return new NoGuardAbility(apCost);
                default:
                    return null;
            }
        }

        public static PilotAbilityKind KindOf(IPilotAbility ability)
        {
            return ability is SurgeAbility ? PilotAbilityKind.Surge
                : ability is NoGuardAbility ? PilotAbilityKind.NoGuard
                : PilotAbilityKind.None;
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

        /// <summary>Applies the ability (the engine has already checked and paid for it).</summary>
        void Activate(PilotUnit pilot, BattleEngine engine);

        /// <summary>The same ability with another AP cost (for comparing costs in the lab).</summary>
        IPilotAbility WithCost(int apCost);
    }

    /// <summary>
    /// "Surge": the pilot's attack is multiplied this turn and drops to nothing from next turn on, until the pilot rests
    /// in the hand (ejected or replaced, then a turn start in the hand). Harsh, but it scales with upgrades and multiply
    /// slots, and the crash makes leaving the attack side unmanaged a real cost.
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
            return $"ATK x{DisplayFormat.Value(Multiplier)} this turn, then ATK 0 until rested in the hand";
        }

        public void Activate(PilotUnit pilot, BattleEngine engine)
        {
            pilot.AttackModifiers.Add(new StatModifier(Multiplier, 0, 1));
            pilot.AttackModifiers.Add(StatModifier.UntilRested(0f, 1));
        }

        public IPilotAbility WithCost(int apCost)
        {
            return new SurgeAbility(apCost, Multiplier);
        }
    }

    /// <summary>
    /// "No Guard": this turn both the player's defense and every enemy's guard are halved. Risky (wounds taken at half
    /// defense cut deep) and a matter of timing: best on a turn the enemy guards and barely attacks, such as a charge.
    /// </summary>
    public sealed class NoGuardAbility : IPilotAbility
    {
        public float Factor { get; }
        public int ApCost { get; }
        public string Name => "No Guard";

        public NoGuardAbility(int apCost, float factor = 0.5f)
        {
            ApCost = apCost;
            Factor = factor;
        }

        public string Describe()
        {
            return $"your defense and the enemy's guard x{DisplayFormat.Value(Factor)} this turn";
        }

        public void Activate(PilotUnit pilot, BattleEngine engine)
        {
            engine.LowerGuards(Factor);
        }

        public IPilotAbility WithCost(int apCost)
        {
            return new NoGuardAbility(apCost, Factor);
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

        /// <summary>True if it lasts, once started, until the pilot rests in the hand instead of for a number of turns.</summary>
        public bool LastsUntilRested { get; }

        public bool IsActive => StartsIn == 0 && TurnsLeft > 0;
        public bool IsOver => TurnsLeft <= 0;

        public StatModifier(float multiplier, int startsIn, int turns, bool lastsUntilRested = false)
        {
            Multiplier = multiplier;
            StartsIn = startsIn;
            TurnsLeft = turns;
            LastsUntilRested = lastsUntilRested;
        }

        /// <summary>A modifier that starts in some turns and lasts until the pilot rests in the hand (see <see cref="PilotUnit.Rest"/>).</summary>
        public static StatModifier UntilRested(float multiplier, int startsIn)
        {
            return new StatModifier(multiplier, startsIn, int.MaxValue, true);
        }

        /// <summary>Moves on by one turn: a waiting modifier comes closer, an active one uses up a turn.</summary>
        public void Advance()
        {
            if (StartsIn > 0)
            {
                StartsIn--;
            }
            else if (!LastsUntilRested)
            {
                TurnsLeft--;
            }
        }

        public StatModifier Copy()
        {
            return new StatModifier(Multiplier, StartsIn, TurnsLeft, LastsUntilRested);
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
