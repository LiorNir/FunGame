using System.Linq;
using FunGame.Battle;

namespace BattleView
{
    /// <summary>Whether a stat shown on a slot or card is raised or lowered right now by an ability or an enemy effect.</summary>
    public enum StatChange
    {
        None,
        Boost,
        Drop
    }

    public static class StatChanges
    {
        public static StatChange Of(float multiplier)
        {
            return multiplier > 1.001f ? StatChange.Boost : multiplier < 0.999f ? StatChange.Drop : StatChange.None;
        }

        public static StatChange OfAttack(PilotUnit pilot)
        {
            return Of(StatModifier.Product(pilot.AttackModifiers));
        }

        public static StatChange OfDefense(PilotUnit pilot)
        {
            return Of(StatModifier.Product(pilot.DefenseModifiers));
        }
    }

    /// <summary>How much of a pilot's stats is left (0 to 1, for bars), now and after a turn resting in the hand.</summary>
    public static class StatShares
    {
        public static float Attack(PilotUnit pilot)
        {
            return pilot.BaseAttack > 0f ? Clamp01(pilot.EffectiveAttack / pilot.BaseAttack) : 0f;
        }

        /// <summary>The share of its defense left after wounds and effects (snipes).</summary>
        public static float Defense(PilotUnit pilot)
        {
            return (1f - pilot.DefenseWound) * DefenseEffects(pilot);
        }

        /// <summary>The share of its defense left by effects alone (wounds aside).</summary>
        public static float DefenseEffects(PilotUnit pilot)
        {
            return Clamp01(StatModifier.Product(pilot.DefenseModifiers));
        }

        /// <param name="attackAfterRest">The attack the pilot will have after the next turn's recovery (<see cref="BattleEngine.AttackAfterRest"/>).</param>
        public static float AttackAfterRest(PilotUnit pilot, float attackAfterRest)
        {
            return pilot.BaseAttack > 0f ? Clamp01(attackAfterRest / pilot.BaseAttack) : 0f;
        }

        /// <param name="woundAfterRest">The wound the pilot will have after the next turn's recovery.</param>
        public static float DefenseAfterRest(PilotUnit pilot, float woundAfterRest)
        {
            var effects = StatModifier.Product(pilot.DefenseModifiers.Where(modifier => !modifier.LastsUntilRested), nextTurn: true);
            return (1f - woundAfterRest) * Clamp01(effects);
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }
}
