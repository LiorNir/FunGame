using System;
using System.Collections.Generic;
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
        public float PilotLost = 15f;

        /// <summary>Per full pilot's worth of defense lost to wounds.</summary>
        public float Wound = 2f;

        /// <summary>Per AP spent, so that a plan that gains nothing is not preferred over doing nothing.</summary>
        public float ApSpent = 0.05f;

        /// <summary>Per pilot locked by an enemy at the end of the turn: it can no longer be swapped out.</summary>
        public float Locked = 0f;

        /// <summary>Per full wound on a locked pilot: a wounded pilot that cannot be replaced keeps its chain weak until the lock ends.</summary>
        public float LockedWound = 0f;

        /// <summary>
        /// Per locked pilot standing in a defense chain weaker than the lane enemy's biggest attack: it cannot be pulled out
        /// before that attack breaks the chain, so it may fall. A player reacting to a telegraphed lock moves such a target
        /// out first.
        /// </summary>
        public float LockedAtRisk = 15f;

        /// <summary>Looks at the turn and its result only.</summary>
        public static ScoreWeights Short => new ScoreWeights { KillNext = 0f, CoverNext = 0f, PilotLost = 3f, Wound = 0.5f, Locked = 0f, LockedWound = 0f, LockedAtRisk = 0f };

        /// <summary>Also judges how ready the board is for the next turn.</summary>
        public static ScoreWeights Full => new ScoreWeights();

        /// <summary>The same weights, blind to enemy locks (for checking whether an encounter rewards reacting to them).</summary>
        public ScoreWeights IgnoringLocks()
        {
            var copy = (ScoreWeights)MemberwiseClone();
            copy.Locked = 0f;
            copy.LockedWound = 0f;
            copy.LockedAtRisk = 0f;
            return copy;
        }
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
            // a wounded pilot resting in the hand counts with the wound it will still have when the next turn starts (the
            // recovery rules are known, so this is no guess about the future)
            score -= weights.Wound * afterCombat.Hand.Sum(pilot => afterCombat.WoundAfterRest(pilot));
            if (afterCombat.PilotBySlot.Values.Any(pilot => !pilot.Fallen && pilot.IsLocked))
            {
                score -= LockPenalty(afterCombat, weights);
            }

            if (weights.KillNext > 0f || weights.CoverNext > 0f)
            {
                // next turn's board: attack after known temporary changes (a Surge's crash turn counts against it)
                var results = afterCombat.EvaluateChainsNextTurn().Values.ToList();
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

        private static float LockPenalty(BattleEngine afterCombat, ScoreWeights weights)
        {
            var penalty = 0f;
            foreach (var pilot in afterCombat.PilotBySlot.Values.Where(pilot => !pilot.Fallen && pilot.IsLocked))
            {
                penalty += weights.Locked + weights.LockedWound * pilot.DefenseWound;
            }

            if (weights.LockedAtRisk <= 0f)
            {
                return penalty;
            }

            // locked pilots in a defense chain that the lane's enemy can break with its biggest attack
            var atRisk = new HashSet<string>();
            foreach (var result in afterCombat.EvaluateChains().Values.Where(result => result.Chain.Role == ChainRole.Defense))
            {
                var lane = afterCombat.Lanes.FirstOrDefault(candidate => candidate.Spec.Id == result.Chain.LaneId);
                var enemy = lane?.Current?.Spec ?? (lane != null && lane.HasMoreEnemies ? lane.Spec.Enemies[lane.EnemiesSpawned] : null);
                var biggestAttack = enemy == null || enemy.Actions.Count == 0 ? 0 : enemy.Actions.Max(action => action.Attack);
                if (biggestAttack <= result.Value)
                {
                    continue;
                }

                foreach (var step in result.Steps.Where(step => step.Status == SlotStatus.Active))
                {
                    if (afterCombat.PilotBySlot.TryGetValue(step.NodeId, out var pilot) && pilot.IsLocked)
                    {
                        atRisk.Add(step.NodeId);
                    }
                }
            }

            return penalty + weights.LockedAtRisk * atRisk.Count;
        }
    }
}
