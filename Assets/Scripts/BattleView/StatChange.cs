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
}
