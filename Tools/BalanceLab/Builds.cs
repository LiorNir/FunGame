using System;
using System.Collections.Generic;
using System.Linq;
using FunGame.Board;
using FunGame.Progress;
using FunGame.Sim;

namespace BalanceLab
{
    /// <summary>
    /// "builds N [filter]": plays each World 2+ encounter with the pilots a campaign player would have there, upgraded
    /// along different paths with the SP they would have (World N comes after N-1 bosses: N-1 SP). The test decks' own
    /// upgrades are taken off first. Paths: defense first (as the test decks assumed), attack with a flat cost (every SP
    /// a +1), attack with the rising cost (each further +1 on the same stat costs 1 more).
    /// </summary>
    public static class Builds
    {
        private static readonly (string Pilot, PilotStat Stat)[] DefenseFirst =
        {
            ("Quill", PilotStat.Defense), ("Yawn", PilotStat.Defense), ("Osis", PilotStat.Defense), ("Sil", PilotStat.Defense), ("Pion", PilotStat.Defense)
        };

        private static readonly (string Pilot, PilotStat Stat)[] AttackFirst =
        {
            ("Bolt", PilotStat.Attack), ("Spark", PilotStat.Attack), ("Osis", PilotStat.Attack), ("Sil", PilotStat.Attack)
        };

        public static int Run(string[] args)
        {
            var runs = args.Length > 1 ? int.Parse(args[1]) : 100;
            var filter = args.Length > 2 ? args[2] : "World";
            foreach (var encounter in BuiltInEncounters.All().Where(candidate => candidate.Name.Contains(filter) && candidate.World != null && WorldNumber(candidate) >= 2))
            {
                var skillPoints = WorldNumber(encounter) - 1;
                var baseDeck = encounter.Setup.Deck.Select(Unupgraded).ToList();
                var builds = new[]
                {
                    ("defense", Upgrade(baseDeck, DefenseFirst, skillPoints, rising: true)),
                    ("attack flat", Upgrade(baseDeck, AttackFirst, skillPoints, rising: false)),
                    ("attack rising", Upgrade(baseDeck, AttackFirst, skillPoints, rising: true))
                };

                Console.WriteLine($"== {encounter.Name} ({skillPoints} SP)");
                foreach (var (name, deck) in builds)
                {
                    var upgraded = string.Join(", ", deck.Zip(baseDeck, (now, before) => (now, before))
                        .Where(pair => pair.now.Attack != pair.before.Attack || pair.now.Defense != pair.before.Defense)
                        .Select(pair => $"{pair.now.Name} {pair.now.Attack:0.##}/{pair.now.Defense:0.##}"));
                    var line = string.Join("   ", new[] { PlayerSpec.Casual, PlayerSpec.Careful }.Select(bot =>
                    {
                        var summary = BatchSummary.From(Lab.PlayBatch(encounter, deck, bot, 5000, runs));
                        return $"{bot.Name} {summary.WinRate * 100,3:0}% hp {summary.AverageHealthLeftOnWin * 100,3:0}% turns {summary.AverageTurns:0.0}";
                    }));
                    Console.WriteLine($"  {name,-14} {line}   [{upgraded}]");
                }
            }

            return 0;
        }

        private static int WorldNumber(EncounterCase encounter)
        {
            return int.TryParse(encounter.World?.Replace("World ", string.Empty), out var number) ? number : 0;
        }

        // the starter pilots as they are before any upgrade (the test decks raised Quill's and Yawn's defense)
        private static PilotStats Unupgraded(PilotStats pilot)
        {
            switch (pilot.Name)
            {
                case "Quill":
                    return pilot.WithStats(1f, 2f);
                case "Yawn":
                    return pilot.WithStats(1f, 1f);
                default:
                    return pilot;
            }
        }

        // spends the SP along the priority list, one +1 at a time, cheapest first among the listed pilots the player has
        private static List<PilotStats> Upgrade(List<PilotStats> deck, (string Pilot, PilotStat Stat)[] priority, int skillPoints, bool rising)
        {
            var bought = new Dictionary<(string, PilotStat), int>();
            var left = skillPoints;
            while (true)
            {
                var options = priority.Where(option => deck.Any(pilot => pilot.Name == option.Pilot))
                    .Select(option => (option, cost: rising ? Progression.CostAfter(bought.TryGetValue(option, out var count) ? count : 0) : Progression.BaseUpgradeCost))
                    .Where(choice => choice.cost <= left)
                    .ToList();
                if (options.Count == 0)
                {
                    break;
                }

                // the first listed option that can be afforded: a player with a plan buys along it
                var (pick, price) = options[0];
                bought[pick] = (bought.TryGetValue(pick, out var already) ? already : 0) + 1;
                left -= price;
            }

            return deck.Select(pilot =>
            {
                var attack = bought.TryGetValue((pilot.Name, PilotStat.Attack), out var a) ? a : 0;
                var defense = bought.TryGetValue((pilot.Name, PilotStat.Defense), out var d) ? d : 0;
                return attack + defense == 0 ? pilot : pilot.WithStats(pilot.Attack + attack, pilot.Defense + defense);
            }).ToList();
        }
    }
}
