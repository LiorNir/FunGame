using System.Collections.Generic;
using System.Linq;
using FunGame.Battle;
using FunGame.Board;

namespace BattleView
{
    /// <summary>
    /// The explanations shown when something is hovered (temporary texts): operators, bonus slots and enemy effects. The
    /// slot texts that need the board's state are built by <see cref="BoardView"/> from these pieces.
    /// </summary>
    public static class HoverTexts
    {
        public static string Operator(Operation operation)
        {
            switch (operation)
            {
                case Operation.Add:
                    return "<b>+ Add</b>: adds this pilot's stat to the chain so far.";
                case Operation.Subtract:
                    return "<b>- Subtract (toll)</b>: takes this pilot's stat away from the chain so far (never below 0). A toll pilot is never wounded.";
                case Operation.Multiply:
                    return "<b>x Multiply</b>: multiplies the chain so far by this pilot's stat.";
                default:
                    return "<b>÷ Divide (toll)</b>: divides the chain so far by this pilot's stat. Below 1 it multiplies; 0 is an ERROR. A toll pilot is never wounded.";
            }
        }

        public static string Bonus(NodeBonus bonus)
        {
            switch (bonus.Kind)
            {
                case BonusKind.Add:
                    return $"<b>Bonus +{DisplayFormat.Value(bonus.Amount)}</b>: adds {DisplayFormat.Value(bonus.Amount)} to the chain at this slot while it holds a pilot.";
                case BonusKind.Multiply:
                    return $"<b>Bonus x{DisplayFormat.Value(bonus.Amount)}</b>: multiplies the chain at this slot by {DisplayFormat.Value(bonus.Amount)} while it holds a pilot.";
                default:
                    return null;
            }
        }

        /// <summary>An enemy: health, its next action effect by effect, and who follows it.</summary>
        public static string Enemy(BattleEngine engine, LaneState lane)
        {
            var enemy = lane.Current;
            if (enemy == null)
            {
                return null;
            }

            var lines = new List<string> { $"<b>{enemy.Spec.Name}</b>  HP {DisplayFormat.Value(enemy.Health)}/{enemy.Spec.MaxHealth}" };
            if (enemy.Intent != null)
            {
                lines.Add($"Next: <b>{enemy.Intent.Name}</b>");
                var texts = engine.DescribeIntent(lane);
                for (var index = 0; index < enemy.Intent.Effects.Count; index++)
                {
                    lines.Add("- " + Effect(enemy.Intent.Effects[index], index < texts.Count ? texts[index] : null, engine.GuardFactor(lane.Spec.Id)));
                }
            }

            var left = lane.Spec.Enemies.Count - lane.EnemiesSpawned;
            if (left > 0)
            {
                lines.Add($"Then: {string.Join(", ", lane.Spec.Enemies.Skip(lane.EnemiesSpawned).Select(next => next.Name))}");
            }

            return string.Join("\n", lines);
        }

        private static string Effect(IEnemyEffect effect, string shown, float guardFactor)
        {
            switch (effect)
            {
                case AttackEffect attack:
                    return $"Attack {attack.Amount}: hits this side's defense chains; what they cannot hold hits you.";
                case DefendEffect defend:
                    return $"Guard {DisplayFormat.Value(defend.Amount * guardFactor)}: your attack on this enemy must pass it first.";
                case ChargeEffect charge:
                    return $"Charge, guard {DisplayFormat.Value(charge.Amount * guardFactor)}: break through the whole guard this turn and its Unleash next turn does nothing.";
                case UnleashEffect unleash:
                    return shown == UnleashEffect.StunnedText
                        ? "Stunned: the charge was broken, this turn it does nothing."
                        : $"Unleash {unleash.Amount}: the big hit the charge was for.";
                case SnipeEffect snipe:
                    return shown == SnipeEffect.AssassinateText
                        ? "Assassinate: the marked pilot has nothing left, so it falls."
                        : $"Snipe: the marked pilot loses {DisplayFormat.Value(snipe.Share * 100f)}% of both stats for {snipe.Turns} turn(s), from next turn. Swap it out to dodge.";
                case LockEffect lockEffect:
                    return $"Lock {lockEffect.Turns}: the marked pilot cannot be replaced or ejected for {lockEffect.Turns} turn(s). Swap it out before it lands to dodge.";
                default:
                    return effect.Describe();
            }
        }
    }
}
