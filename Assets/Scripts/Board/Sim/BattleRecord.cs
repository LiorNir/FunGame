using System;
using System.Collections.Generic;
using System.Linq;
using FunGame.Battle;

namespace FunGame.Sim
{
    /// <summary>Everything worth knowing about one played battle.</summary>
    public sealed class BattleRecord
    {
        public int Seed { get; set; }
        public BattleOutcome Outcome { get; set; }
        public int Turns { get; set; }
        public float MaxHealth { get; set; }
        public float FinalHealth { get; set; }

        /// <summary>The lowest the player's health was at the end of any turn.</summary>
        public float LowestHealth { get; set; }

        public BattleStats Stats { get; set; }
        public IReadOnlyList<float> HealthByTurn { get; set; } = new float[0];

        /// <summary>The text log of the battle. Only filled when asked for.</summary>
        public IReadOnlyList<string> Log { get; set; } = new string[0];

        /// <summary>What a search player planned each turn. Only filled when asked for.</summary>
        public IReadOnlyList<PlannedTurn> Plans { get; set; } = new PlannedTurn[0];

        public bool Won => Outcome == BattleOutcome.Victory;
    }

    /// <summary>Plays whole battles and records them.</summary>
    public static class BotRunner
    {
        public const int DefaultTurnLimit = 40;

        /// <summary>A battle in which no health changes for this many turns in a row is called a stalemate at once.</summary>
        public const int StalledTurns = 6;

        private static string StateOf(BattleEngine engine)
        {
            var enemies = engine.Lanes.Sum(lane => (lane.Current != null ? lane.Current.Health : 0f) + lane.EnemiesSpawned * 1000f);
            return $"{engine.PlayerHealth}|{enemies}";
        }

        public static BattleRecord Run(BattleSetup setup, IPlayer player, int turnLimit = DefaultTurnLimit, bool keepNarrative = false)
        {
            var engine = new BattleEngine(setup) { Logging = keepNarrative, RecordEvents = false };
            engine.Start();
            var healthByTurn = new List<float>();
            var plans = new List<PlannedTurn>();
            var lowest = engine.PlayerHealth;
            var search = player as SearchPlayer;
            var quietTurns = 0;
            var lastState = StateOf(engine);

            while (engine.Outcome == BattleOutcome.InProgress)
            {
                if (engine.Turn > turnLimit || quietTurns >= StalledTurns)
                {
                    engine.MarkStalemate();
                    break;
                }

                player.PlayPlanningPhase(engine);
                if (keepNarrative && search != null && search.LastPlan != null)
                {
                    plans.Add(search.LastPlan);
                }

                engine.ResolveTurn();
                healthByTurn.Add(engine.PlayerHealth);
                lowest = Math.Min(lowest, engine.PlayerHealth);

                // nobody's health has moved for several turns: neither side can hurt the other, so it will never end
                var state = StateOf(engine);
                quietTurns = state == lastState ? quietTurns + 1 : 0;
                lastState = state;
            }

            return new BattleRecord
            {
                Seed = setup.Seed,
                Outcome = engine.Outcome,
                Turns = engine.Turn,
                MaxHealth = setup.Rules.PlayerMaxHealth,
                FinalHealth = engine.PlayerHealth,
                LowestHealth = lowest,
                Stats = engine.Stats,
                HealthByTurn = healthByTurn,
                Log = keepNarrative ? engine.Log : new string[0],
                Plans = plans
            };
        }
    }

    /// <summary>The results of many battles of one kind, boiled down.</summary>
    public sealed class BatchSummary
    {
        public const int TurnBuckets = 16;
        public const int HealthBuckets = 5;

        public int Runs { get; private set; }
        public int Wins { get; private set; }
        public int Defeats { get; private set; }
        public int Stalemates { get; private set; }

        public double WinRate => Runs == 0 ? 0d : Wins / (double)Runs;

        /// <summary>The 95% confidence interval of the win rate (Wilson), so a small batch does not pretend to be certain.</summary>
        public (double Low, double High) WinInterval { get; private set; }

        public double AverageTurns { get; private set; }
        public double AverageTurnsOnWin { get; private set; }

        /// <summary>Share of full health left at the end of a won battle, averaged over wins.</summary>
        public double AverageHealthLeftOnWin { get; private set; }

        /// <summary>Lowest share of full health reached during the battle, averaged over all battles.</summary>
        public double AverageLowestHealth { get; private set; }

        public double AveragePilotsFallen { get; private set; }
        public double AverageDamageTaken { get; private set; }
        public double AverageDamageDealt { get; private set; }
        public double AverageDamageAbsorbed { get; private set; }
        public double AverageApWasted { get; private set; }
        public double AveragePlaces { get; private set; }
        public double AverageReplaces { get; private set; }
        public double AverageEjects { get; private set; }
        public double AverageUnlocks { get; private set; }
        public double AverageDivideByZeros { get; private set; }

        /// <summary>Won battles by share of health left: 0-20%, 20-40%, and so on.</summary>
        public int[] HealthLeftOnWins { get; } = new int[HealthBuckets];

        /// <summary>All battles by turns taken: index 0 is turn 1; the last bucket also holds everything longer.</summary>
        public int[] TurnsTaken { get; } = new int[TurnBuckets];

        public static BatchSummary From(IReadOnlyList<BattleRecord> records)
        {
            var summary = new BatchSummary { Runs = records.Count };
            if (records.Count == 0)
            {
                return summary;
            }

            var wins = records.Where(record => record.Won).ToList();
            summary.Wins = wins.Count;
            summary.Defeats = records.Count(record => record.Outcome == BattleOutcome.Defeat);
            summary.Stalemates = records.Count(record => record.Outcome == BattleOutcome.Stalemate);
            summary.WinInterval = Wilson(summary.Wins, summary.Runs);

            summary.AverageTurns = records.Average(record => record.Turns);
            summary.AverageTurnsOnWin = wins.Count == 0 ? 0d : wins.Average(record => record.Turns);
            summary.AverageHealthLeftOnWin = wins.Count == 0 ? 0d : wins.Average(record => record.FinalHealth / record.MaxHealth);
            summary.AverageLowestHealth = records.Average(record => record.LowestHealth / record.MaxHealth);
            summary.AveragePilotsFallen = records.Average(record => record.Stats.PilotsFallen);
            summary.AverageDamageTaken = records.Average(record => record.Stats.DamageTaken);
            summary.AverageDamageDealt = records.Average(record => record.Stats.DamageDealt);
            summary.AverageDamageAbsorbed = records.Average(record => record.Stats.DamageAbsorbed);
            summary.AverageApWasted = records.Average(record => record.Stats.ApWasted);
            summary.AveragePlaces = records.Average(record => record.Stats.Places);
            summary.AverageReplaces = records.Average(record => record.Stats.Replaces);
            summary.AverageEjects = records.Average(record => record.Stats.Ejects);
            summary.AverageUnlocks = records.Average(record => record.Stats.Unlocks);
            summary.AverageDivideByZeros = records.Average(record => record.Stats.DivideByZeros);

            foreach (var record in wins)
            {
                var bucket = Math.Min(HealthBuckets - 1, (int)(record.FinalHealth / record.MaxHealth * HealthBuckets));
                summary.HealthLeftOnWins[bucket]++;
            }

            foreach (var record in records)
            {
                summary.TurnsTaken[Math.Min(TurnBuckets - 1, Math.Max(0, record.Turns - 1))]++;
            }

            return summary;
        }

        /// <summary>Wilson score interval for a proportion, 95%.</summary>
        public static (double Low, double High) Wilson(int successes, int total)
        {
            if (total == 0)
            {
                return (0d, 1d);
            }

            const double z = 1.96d;
            var p = successes / (double)total;
            var denominator = 1d + z * z / total;
            var centre = (p + z * z / (2d * total)) / denominator;
            var margin = z * Math.Sqrt(p * (1d - p) / total + z * z / (4d * total * total)) / denominator;
            return (Math.Max(0d, centre - margin), Math.Min(1d, centre + margin));
        }
    }
}
