using System;
using System.Linq;
using FunGame.Battle;
using FunGame.Sim;

namespace BalanceLab
{
    /// <summary>Prints the log of single battles: inspect "name part" rung bot firstSeed count</summary>
    public static class Inspect
    {
        public static int Run(string[] args)
        {
            var encounter = BuiltInEncounters.All().First(candidate => candidate.Name.Contains(args[1]));
            var rung = int.Parse(args[2]);
            var bot = Study.Spec(args[3]);
            var seed = int.Parse(args[4]);
            var count = args.Length > 5 ? int.Parse(args[5]) : 1;
            var deck = PowerLadder.Scale(encounter.Setup.Deck, rung);
            for (var index = 0; index < count; index++)
            {
                var record = BotRunner.Run(encounter.WithDeck(deck, seed + index), bot.Create(), BotRunner.DefaultTurnLimit, true);
                Console.WriteLine($"##### seed {seed + index}: {record.Outcome} in {record.Turns} turns, HP {record.FinalHealth}");
                var plans = record.Plans.ToDictionary(plan => plan.Turn);
                foreach (var line in record.Log)
                {
                    if (line.StartsWith("--- Turn ") && line.EndsWith("resolves ---") && plans.TryGetValue(int.Parse(line.Split(' ')[2]), out var plan))
                    {
                        Console.WriteLine($"   PLAN: {(plan.Actions.Count == 0 ? "nothing" : string.Join("; ", plan.Actions))} | expects {plan.Prediction}");
                    }

                    Console.WriteLine(line);
                }
            }

            return 0;
        }
    }
}
