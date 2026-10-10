using System;
using System.Linq;
using FunGame.Battle;
using FunGame.Sim;

namespace BalanceLab
{
    /// <summary>
    /// A quick design check of built-in encounters at rung 0: win rate, health left, pilots lost, turns and unused AP per turn,
    /// as designed and with each mechanic taken away (replacing, ejecting, slots that open by themselves): ablate runs filter.
    /// </summary>
    public static class Ablate
    {
        public static int Run(string[] args)
        {
            var runs = int.Parse(args[1]);
            var filter = args[2];
            foreach (var encounter in BuiltInEncounters.All().Where(candidate => candidate.Name.Contains(filter)))
            {
                Console.WriteLine($"== {encounter.Name}");
                foreach (var bot in new[] { PlayerSpec.Casual, PlayerSpec.Careful, PlayerSpec.Gambler })
                {
                    var deck = encounter.Setup.Deck;
                    string Rate(EncounterCase variant)
                    {
                        var summary = BatchSummary.From(Lab.PlayBatch(variant, deck, bot, 5000, runs));
                        var apPerTurn = summary.AverageTurns > 0 ? summary.AverageApWasted / summary.AverageTurns : 0;
                        return $"{summary.WinRate * 100,3:0}% hp {summary.AverageHealthLeftOnWin * 100,3:0}% lost {summary.AveragePilotsFallen:0.0} " +
                               $"turns {summary.AverageTurns:0.0} idleAP {apPerTurn:0.0} rep {summary.AverageReplaces:0.0} ej {summary.AverageEjects:0.0}";
                    }

                    Console.WriteLine($"  {bot.Name,-8} normal          {Rate(encounter)}");
                    var rules = encounter.Setup.Rules;
                    if (rules.AllowReplace)
                    {
                        var without = rules.Copy();
                        without.AllowReplace = false;
                        Console.WriteLine($"  {"",-8} no replacing    {Rate(encounter.WithRules(without))}");
                    }

                    if (rules.AllowEject)
                    {
                        var without = rules.Copy();
                        without.AllowEject = false;
                        Console.WriteLine($"  {"",-8} no ejecting     {Rate(encounter.WithRules(without))}");
                    }

                    if (encounter.HasLockedSlots)
                    {
                        Console.WriteLine($"  {"",-8} slots stay shut {Rate(encounter.WithoutUnlocks())}");
                    }
                }
            }

            return 0;
        }
    }
}
