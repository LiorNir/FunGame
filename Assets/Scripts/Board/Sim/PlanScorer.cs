using System;
using System.Linq;
using FunGame.Battle;
using FunGame.Board;

namespace FunGame.Sim
{
    /// <summary>How much each thing matters when a player judges the board at the end of its turn.</summary>
    public sealed class ScoreWeights
    {
        /// <summary>Per full health bar of the player left.</summary>
        public float Hp = 100f;

        /// <summary>For the share of all enemy health already taken off.</summary>
        public float Progress = 60f;

        /// <summary>For how much of the next enemy this board would kill next turn (a guess from the enemy's average action).</summary>
        public float KillNext = 25f;

        /// <summary>For how much of the next enemy attack this board would absorb next turn (same guess).</summary>
        public float CoverNext = 25f;

        /// <summary>Per pilot that fell this turn. A fallen pilot is gone for the whole battle, so this is high.</summary>
        public float PilotLost = 30f;

        /// <summary>Per full pilot's worth of defense lost to wounds.</summary>
        public float Wound = 2f;

        /// <summary>Per AP spent, so that a plan that gains nothing is not preferred over doing nothing.</summary>
        public float ApSpent = 0.05f;

        /// <summary>Looks at the turn and its result only.</summary>
        public static ScoreWeights Short => new ScoreWeights { KillNext = 0f, CoverNext = 0f, PilotLost = 3f, Wound = 0.5f };

        /// <summary>Also judges how ready the board is for the next turn.</summary>
        public static ScoreWeights Full => new ScoreWeights();
    }

    /// <summary>What a plan is expected to do this turn, from playing the turn out on a copy of the battle.</summary>
    public readonly struct Prediction
    {
        public float Score { get; }
        public BattleOutcome Outcome { get; }
        public float HealthAfter { get; }
        public float DamageDealt { get; }
        public float DamageTaken { get; }
        public int PilotsLost { get; }

        public Prediction(float score, BattleOutcome outcome, float healthAfter, float damageDealt, float damageTaken, int pilotsLost)
        {
            Score = score;
            Outcome = outcome;
            HealthAfter = healthAfter;
            DamageDealt = damageDealt;
            DamageTaken = damageTaken;
            PilotsLost = pilotsLost;
        }

        public override string ToString()
        {
            if (Outcome == BattleOutcome.Victory)
            {
                return "wins this turn";
            }

            if (Outcome == BattleOutcome.Defeat)
            {
                return "loses this turn";
            }

            return $"deals {DisplayFormat.Value(DamageDealt)}, takes {DisplayFormat.Value(DamageTaken)} (HP {DisplayFormat.Value(HealthAfter)}), " +
                   $"{PilotsLost} pilot{(PilotsLost == 1 ? string.Empty : "s")} lost";
        }
    }

    /// <summary>
    /// Scores a battle that has just resolved its combat (see <see cref="BattleEngine.ResolveCombat"/>): how a player judges
    /// the end of a turn. Nothing here reads anything the player could not know: the state after this turn's combat, plus
    /// a guess for the next turn from the average action of the enemy that will be there.
    /// </summary>
    public static class PlanScorer
    {
        private const float WinScore = 10000f;

        /// <param name="afterCombat">The battle after the plan was carried out and the combat resolved.</param>
        /// <param name="baseline">The counters before the plan, to tell what this turn did.</param>
        public static Prediction Score(BattleEngine afterCombat, BattleStats baseline, ScoreWeights weights, int apSpent)
        {
            var stats = afterCombat.Stats;
            var maxHealth = (float)afterCombat.Setup.Rules.PlayerMaxHealth;
            var healthFraction = afterCombat.PlayerHealth / maxHealth;
            var dealt = stats.DamageDealt - baseline.DamageDealt;
            var taken = stats.DamageTaken - baseline.DamageTaken;
            var lost = stats.PilotsFallen - baseline.PilotsFallen;

            var totalEnemyHealth = 0f;
            var remainingEnemyHealth = 0f;
            foreach (var lane in afterCombat.Lanes)
            {
                foreach (var enemy in lane.Spec.Enemies)
                {
                    totalEnemyHealth += enemy.MaxHealth;
                }

                remainingEnemyHealth += lane.Current != null ? lane.Current.Health : 0f;
                for (var index = lane.EnemiesSpawned; index < lane.Spec.Enemies.Count; index++)
                {
                    remainingEnemyHealth += lane.Spec.Enemies[index].MaxHealth;
                }
            }

            var progress = totalEnemyHealth > 0f ? 1f - remainingEnemyHealth / totalEnemyHealth : 1f;

            if (afterCombat.Outcome == BattleOutcome.Victory)
            {
                return new Prediction(WinScore + healthFraction * weights.Hp - weights.PilotLost * lost, afterCombat.Outcome,
                    afterCombat.PlayerHealth, dealt, taken, lost);
            }

            if (afterCombat.Outcome == BattleOutcome.Defeat)
            {
                return new Prediction(-WinScore + progress * weights.Progress, afterCombat.Outcome, afterCombat.PlayerHealth, dealt, taken, lost);
            }

            var score = weights.Hp * healthFraction + weights.Progress * progress - weights.PilotLost * lost - weights.ApSpent * apSpent;
            score -= weights.Wound * afterCombat.PilotBySlot.Values.Where(pilot => !pilot.Fallen).Sum(pilot => pilot.DefenseWound);

            if (weights.KillNext > 0f || weights.CoverNext > 0f)
            {
                var results = afterCombat.EvaluateChains().Values.ToList();
                var kill = 0f;
                var cover = 0f;
                var lanes = 0;
                foreach (var lane in afterCombat.Lanes)
                {
                    var next = lane.Current ?? (lane.HasMoreEnemies ? new EnemyUnit(lane.Spec.Enemies[lane.EnemiesSpawned]) : null);
                    if (next == null)
                    {
                        continue;
                    }

                    lanes++;
                    var averageAttack = next.Spec.Actions.Count == 0 ? 0f : (float)next.Spec.Actions.Average(action => action.Attack);
                    var averageDefense = next.Spec.Actions.Count == 0 ? 0f : (float)next.Spec.Actions.Average(action => action.Defense);
                    var attackValue = results.Where(result => result.Chain.LaneId == lane.Spec.Id && result.Chain.Role == ChainRole.Attack).Sum(result => result.Value);
                    var defenseValue = results.Where(result => result.Chain.LaneId == lane.Spec.Id && result.Chain.Role == ChainRole.Defense).Sum(result => result.Value);

                    kill += Math.Min(1f, Math.Max(0f, attackValue - averageDefense) / Math.Max(next.Health, 0.01f));
                    cover += averageAttack > 0f ? Math.Min(1f, defenseValue / averageAttack) : 1f;
                }

                if (lanes > 0)
                {
                    score += weights.KillNext * kill / lanes + weights.CoverNext * cover / lanes;
                }
            }

            return new Prediction(score, afterCombat.Outcome, afterCombat.PlayerHealth, dealt, taken, lost);
        }
    }
}
