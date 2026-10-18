using System;
using System.Collections.Generic;
using FunGame.Battle;

namespace FunGame.Progress
{
    /// <summary>What a star asks for.</summary>
    public enum StarConditionKind
    {
        /// <summary>Win the encounter.</summary>
        Clear,

        /// <summary>Win with at least <see cref="StarCondition.Value"/> of the player's max health left (0.6 = 60%).</summary>
        WinWithHealth,

        /// <summary>Win without losing a pilot.</summary>
        WinWithoutLosses,

        /// <summary>Win on turn <see cref="StarCondition.Value"/> or earlier.</summary>
        WinWithinTurns
    }

    /// <summary>
    /// One star of an encounter: a condition on how the battle ended. Every star stands on its own (none is required for
    /// another); the conditions other than <see cref="StarConditionKind.Clear"/> are about how the encounter was won, so a
    /// lost battle earns none of them. Plain data with public fields so encounter assets can hold it.
    /// </summary>
    [Serializable]
    public sealed class StarCondition
    {
        public StarConditionKind Kind;

        /// <summary>The share of max health for <see cref="StarConditionKind.WinWithHealth"/>, the last turn for <see cref="StarConditionKind.WinWithinTurns"/>.</summary>
        public float Value;

        public StarCondition()
        {
        }

        public StarCondition(StarConditionKind kind, float value = 0f)
        {
            Kind = kind;
            Value = value;
        }

        public bool IsMet(BattleEnd battle)
        {
            if (!battle.Won)
            {
                return false;
            }

            switch (Kind)
            {
                case StarConditionKind.Clear:
                    return true;
                case StarConditionKind.WinWithHealth:
                    return battle.HealthLeft >= RequiredHealth(battle.MaxHealth) - 0.0001f;
                case StarConditionKind.WinWithoutLosses:
                    return battle.PilotsLost == 0;
                case StarConditionKind.WinWithinTurns:
                    return battle.Turns <= Value + 0.0001f;
                default:
                    return false;
            }
        }

        /// <summary>The health a <see cref="StarConditionKind.WinWithHealth"/> star needs, in HP (rounded up to the 0.25 step).</summary>
        public float RequiredHealth(float maxHealth)
        {
            return (float)Math.Ceiling(Value * maxHealth * 4f - 0.0001f) / 4f;
        }

        /// <summary>The condition for the player, for example "Win with 6+ HP".</summary>
        public string Describe(float maxHealth)
        {
            switch (Kind)
            {
                case StarConditionKind.Clear:
                    return "Clear the encounter";
                case StarConditionKind.WinWithHealth:
                    return $"Win with {DisplayFormat.Value(RequiredHealth(maxHealth))}+ HP";
                case StarConditionKind.WinWithoutLosses:
                    return "Win without losing a pilot";
                case StarConditionKind.WinWithinTurns:
                    return $"Win by turn {Math.Round(Value)}";
                default:
                    return Kind.ToString();
            }
        }
    }

    /// <summary>How a battle ended, as the star conditions read it.</summary>
    public sealed class BattleEnd
    {
        public bool Won { get; }
        public float HealthLeft { get; }
        public float MaxHealth { get; }
        public int PilotsLost { get; }

        /// <summary>The turn the battle ended on (1 = won during the first turn).</summary>
        public int Turns { get; }

        public BattleEnd(bool won, float healthLeft, float maxHealth, int pilotsLost, int turns)
        {
            Won = won;
            HealthLeft = healthLeft;
            MaxHealth = maxHealth;
            PilotsLost = pilotsLost;
            Turns = turns;
        }

        public static BattleEnd Of(BattleEngine engine)
        {
            return new BattleEnd(engine.Outcome == BattleOutcome.Victory, engine.PlayerHealth,
                engine.Setup.Rules.PlayerMaxHealth, engine.Stats.PilotsFallen, engine.Turn);
        }
    }

    /// <summary>The stars of one battle, and how earned stars are kept (each one stays once earned).</summary>
    public static class StarRating
    {
        /// <summary>The usual three stars, until an encounter sets its own: clear, win with 60% health, win without losses.</summary>
        public static IReadOnlyList<StarCondition> Default => new[]
        {
            new StarCondition(StarConditionKind.Clear),
            new StarCondition(StarConditionKind.WinWithHealth, 0.6f),
            new StarCondition(StarConditionKind.WinWithoutLosses)
        };

        /// <summary>Which of the stars this battle met, as bits (star 1 = bit 0).</summary>
        public static int Evaluate(IReadOnlyList<StarCondition> stars, BattleEnd battle)
        {
            var mask = 0;
            for (var index = 0; index < stars.Count && index < 31; index++)
            {
                if (stars[index] != null && stars[index].IsMet(battle))
                {
                    mask |= 1 << index;
                }
            }

            return mask;
        }

        public static bool Has(int mask, int index)
        {
            return (mask & (1 << index)) != 0;
        }

        public static int Count(int mask)
        {
            var count = 0;
            for (; mask != 0; mask &= mask - 1)
            {
                count++;
            }

            return count;
        }
    }
}
