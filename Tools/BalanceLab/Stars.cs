using System;
using System.Collections.Generic;
using System.Linq;
using FunGame.Sim;

namespace BalanceLab
{
    /// <summary>
    /// "stars N [filter]": how often each bot would meet the possible star conditions, to pick per-encounter thresholds
    /// (aim: a careful player meets a star sometimes, not always). For each encounter and bot, as a share of all battles:
    /// wins, wins with at least 50% to 100% health, wins without losing a pilot, and wins by turn T for the usual turns.
    /// </summary>
    public static class Stars
    {
        private static readonly float[] HealthShares = { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f };

        public static int Run(string[] args)
        {
            var runs = args.Length > 1 ? int.Parse(args[1]) : 100;
            var filter = args.Length > 2 ? args[2] : "World";
            Console.WriteLine("share of all battles that win and meet the condition");
            foreach (var encounter in BuiltInEncounters.All().Where(candidate => candidate.Name.Contains(filter)))
            {
                Console.WriteLine($"== {encounter.Name}");
                foreach (var bot in new[] { PlayerSpec.Casual, PlayerSpec.Careful })
                {
                    var records = Lab.PlayBatch(encounter, encounter.Setup.Deck, bot, 5000, runs);
                    var wins = records.Where(record => record.Won).ToList();
                    string Share(Func<BattleRecord, bool> condition) => $"{wins.Count(condition) * 100f / records.Count,3:0}%";

                    var health = string.Join(" ", HealthShares.Select(share =>
                        $"{share * 100:0}:{Share(record => record.FinalHealth >= share * record.MaxHealth - 0.0001f)}"));
                    var turnsSeen = wins.Select(record => record.Turns).OrderBy(turns => turns).ToList();
                    var turns = turnsSeen.Count == 0
                        ? "-"
                        : string.Join(" ", Enumerable.Range(turnsSeen.First(), Math.Min(5, turnsSeen.Last() - turnsSeen.First() + 1))
                            .Select(limit => $"<={limit}:{Share(record => record.Turns <= limit)}"));
                    Console.WriteLine($"  {bot.Name,-8} win {Share(record => true)}  hp {health}  no loss {Share(record => record.Stats.PilotsFallen == 0)}  turns {turns}");
                }
            }

            return 0;
        }
    }
}
