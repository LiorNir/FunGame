using System;
using System.Diagnostics;
using System.Linq;
using FunGame.Sim;

namespace BalanceLab
{
    /// <summary>Compares beam widths of the search player with the exhaustive one at the hard spots.</summary>
    public static class BotCheck
    {
        public static int Run(int runs)
        {
            var spots = new[] { ("Boss", 2), ("Divide trap", -3), ("Two lanes", -5), ("The gate", 2), ("Bonus road", -4) };
            var widths = new[] { 12, 24, 48, 96 };
            foreach (var (name, rung) in spots)
            {
                var encounter = BuiltInEncounters.All().First(candidate => candidate.Name.Contains(name));
                var deck = PowerLadder.Scale(encounter.Setup.Deck, rung);
                Console.WriteLine($"== {encounter.Name} at x{PowerLadder.Multiplier(rung):0.0}");
                foreach (var width in widths)
                {
                    var spec = new PlayerSpec("beam " + width, () => new SearchPlayer(new BotProfile("beam", width, true, ScoreWeights.Full)));
                    var clock = Stopwatch.StartNew();
                    var summary = BatchSummary.From(Lab.PlayBatch(encounter, deck, spec, 1000, runs));
                    Console.WriteLine($"  beam {width,3}: {summary.WinRate * 100:0}%  hp {summary.AverageHealthLeftOnWin * 100:0}%  ({clock.ElapsedMilliseconds} ms)");
                }

                var clock2 = Stopwatch.StartNew();
                var reference = BatchSummary.From(Lab.PlayBatch(encounter, deck, PlayerSpec.Reference, 1000, runs));
                Console.WriteLine($"  all    : {reference.WinRate * 100:0}%  hp {reference.AverageHealthLeftOnWin * 100:0}%  ({clock2.ElapsedMilliseconds} ms)");
            }

            return 0;
        }
    }
}
