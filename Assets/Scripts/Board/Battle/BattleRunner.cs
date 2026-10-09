using System.Collections.Generic;
using System.Linq;

namespace FunGame.Battle
{
    public sealed class BattleResult
    {
        public BattleOutcome Outcome { get; }
        public int Turns { get; }
        public float PlayerHealth { get; }
        public IReadOnlyList<string> Log { get; }

        public BattleResult(BattleOutcome outcome, int turns, float playerHealth, IReadOnlyList<string> log)
        {
            Outcome = outcome;
            Turns = turns;
            PlayerHealth = playerHealth;
            Log = log;
        }
    }

    public sealed class BattleSummary
    {
        public int Battles { get; }
        public int Victories { get; }
        public int Defeats { get; }
        public int Stalemates { get; }
        public double AverageTurns { get; }
        public double AverageHealthOnVictory { get; }

        public BattleSummary(int battles, int victories, int defeats, int stalemates, double averageTurns, double averageHealthOnVictory)
        {
            Battles = battles;
            Victories = victories;
            Defeats = defeats;
            Stalemates = stalemates;
            AverageTurns = averageTurns;
            AverageHealthOnVictory = averageHealthOnVictory;
        }

        public override string ToString()
        {
            return $"{Battles} battles: {Victories} victories, {Defeats} defeats, {Stalemates} stalemates; " +
                   $"avg {AverageTurns:0.#} turns; avg HP left on victory {DisplayFormat.Value((float)AverageHealthOnVictory)}";
        }
    }

    /// <summary>Plays whole battles with the planning player.</summary>
    public static class BattleRunner
    {
        public const int DefaultTurnLimit = 60;

        public static BattleResult Run(BattleSetup setup, int turnLimit = DefaultTurnLimit)
        {
            var engine = new BattleEngine(setup);
            engine.Start();
            while (engine.Outcome == BattleOutcome.InProgress)
            {
                if (engine.Turn > turnLimit)
                {
                    engine.MarkStalemate();
                    break;
                }

                PlannerPlayer.PlayPlanningPhase(engine);
                engine.ResolveTurn();
            }

            return new BattleResult(engine.Outcome, engine.Turn, engine.PlayerHealth, engine.Log);
        }

        /// <summary>Runs the same setup with consecutive seeds, which shuffle the deck and drive random enemy choices.</summary>
        public static BattleSummary RunMany(System.Func<int, BattleSetup> createSetup, int firstSeed, int count, int turnLimit = DefaultTurnLimit)
        {
            var results = Enumerable.Range(firstSeed, count).Select(seed => Run(createSetup(seed), turnLimit)).ToList();
            var victories = results.Where(result => result.Outcome == BattleOutcome.Victory).ToList();
            return new BattleSummary(
                results.Count,
                victories.Count,
                results.Count(result => result.Outcome == BattleOutcome.Defeat),
                results.Count(result => result.Outcome == BattleOutcome.Stalemate),
                results.Average(result => result.Turns),
                victories.Count == 0 ? 0 : victories.Average(result => result.PlayerHealth));
        }
    }
}
