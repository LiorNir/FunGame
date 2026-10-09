using System;
using System.Linq;
using FunGame.Battle;
using FunGame.Sim;

namespace BalanceLab
{
    /// <summary>Win rates of the careful and casual players at rung 0 with and without each mechanic: ablate runs filter.</summary>
    public static class Ablate
    {
        public static int Run(string[] args)
        {
            var runs = int.Parse(args[1]);
            var filter = args[2];
            foreach (var encounter in BuiltInEncounters.All().Where(candidate => candidate.Name.Contains(filter)))
            {
                Console.WriteLine($"== {encounter.Name}");
                foreach (var bot in new[] { PlayerSpec.Casual, PlayerSpec.Careful })
                {
                    var deck = encounter.Setup.Deck;
                    string Rate(EncounterCase variant)
                    {
                        var summary = BatchSummary.From(Lab.PlayBatch(variant, deck, bot, 5000, runs));
                        return $"{summary.WinRate * 100:0}% (hp {summary.AverageHealthLeftOnWin * 100:0}%, lost {summary.AveragePilotsFallen:0.0})";
                    }

                    var text = $"  {bot.Name,-8} normal {Rate(encounter)}";
                    if (encounter.HasLockedSlots)
                    {
                        text += $"   locked slots stay shut {Rate(encounter.WithoutUnlocks())}";
                    }

                    Console.WriteLine(text);
                }
            }

            return 0;
        }
    }
}
