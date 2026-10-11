using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FunGame.Sim;

namespace BalanceLab
{
    /// <summary>
    /// The balance lab: plays encounters with simulated players at different roster power and writes a report.
    ///   report [--runs N] [--builtin] [--only text] [--out file.html] [--player-hp N]   the full report (encounters from Tools/BalanceLab/encounters, or the built-in samples);
    ///                                                      --player-hp replays every encounter with that starting player health, to compare values
    ///   quick [N]                                          a short table in the console
    ///   botcheck [N]                                       compares the search player's beam widths with exhaustive search
    /// Tools/BalanceLab/tiers.txt (optional) sets each encounter's tier and intended power: lines of "name|Tier|power".
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            var command = args.Length > 0 ? args[0] : "report";
            switch (command)
            {
                case "report":
                    return Report(args);
                case "botcheck":
                    return BotCheck.Run(args.Length > 1 ? int.Parse(args[1]) : 30);
                case "inspect":
                    return Inspect.Run(args);
                case "ablate":
                    return Ablate.Run(args);
                case "quick":
                    return Quick(args.Length > 1 ? int.Parse(args[1]) : 100, args.Length > 2 ? args[2] : null);
                default:
                    Console.WriteLine("unknown command: " + command);
                    return 1;
            }
        }

        private static string Option(string[] args, string name, string fallback)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        private static int Report(string[] args)
        {
            var settings = new StudySettings { RunsPerRung = int.Parse(Option(args, "--runs", "100")) };
            settings.RunsAtIntended = Math.Max(settings.RunsPerRung, int.Parse(Option(args, "--runs-intended", "300")));
            var output = Option(args, "--out", Path.Combine("Tools", "BalanceLab", "reports", "balance-report.html"));

            var folder = Path.Combine("Tools", "BalanceLab", "encounters");
            var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.txt").OrderBy(file => file).ToList() : new List<string>();
            var builtin = args.Contains("--builtin") || files.Count == 0;
            List<EncounterCase> encounters;
            string source;
            var intended = new Dictionary<string, double>();
            if (builtin)
            {
                encounters = BuiltInEncounters.All().ToList();
                source = "Built-in sample encounters (made up to test the lab, not your real ones)";
                foreach (var encounter in encounters)
                {
                    intended[encounter.Name] = encounter.World != null ? 1.0 : encounter.Tier == "Tutorial" ? 0.8 : encounter.Tier == "Early" ? 1.0 : encounter.Tier == "Mid" ? 1.1 : 1.5;
                }
            }
            else
            {
                encounters = files.Select(file => SetupText.Read(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file))).ToList();
                source = $"{encounters.Count} encounter(s) exported from the Unity project";
            }

            encounters = ApplyTiers(encounters, intended);
            var only = Option(args, "--only", null);
            if (only != null)
            {
                encounters = encounters.Where(encounter => encounter.Name.Contains(only) || (encounter.World != null && encounter.World.Contains(only))).ToList();
                source += $" (only: {only})";
            }

            var ejectRest = Option(args, "--eject-rest", null);
            if (ejectRest != null)
            {
                encounters = encounters.Select(encounter =>
                {
                    var rules = encounter.Setup.Rules.Copy();
                    rules.EjectedRecoveryMultiplier = float.Parse(ejectRest, System.Globalization.CultureInfo.InvariantCulture);
                    return encounter.WithRules(rules);
                }).ToList();
                source += $" (ejected pilots recover x{ejectRest})";
            }

            var playerHealth = Option(args, "--player-hp", null);
            if (playerHealth != null)
            {
                encounters = encounters.Select(encounter =>
                {
                    var rules = encounter.Setup.Rules.Copy();
                    rules.PlayerMaxHealth = int.Parse(playerHealth);
                    return encounter.WithRules(rules);
                }).ToList();
                source += $" (player health {playerHealth})";
            }

            var clock = Stopwatch.StartNew();
            var studies = Study.Run(encounters, intended, settings, message => Console.WriteLine($"[{clock.Elapsed:mm\\:ss}] {message}"));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, ReportWriter.Write(studies, settings, source));
            File.WriteAllText(Path.ChangeExtension(output, ".csv"), Csv(studies));
            Console.WriteLine($"Report written to {output} ({clock.Elapsed:mm\\:ss}).");
            return 0;
        }

        /// <summary>Applies tiers.txt: lines of "encounter name|Tier|intended power". Both of the last fields are optional.</summary>
        private static List<EncounterCase> ApplyTiers(List<EncounterCase> encounters, Dictionary<string, double> intended)
        {
            var path = Path.Combine("Tools", "BalanceLab", "tiers.txt");
            if (!File.Exists(path))
            {
                return encounters;
            }

            var overrides = new Dictionary<string, (string Tier, double? Power, string World, int Position, string Teaches)>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(path).Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith("#")))
            {
                var fields = line.Split('|');
                string Field(int index) => fields.Length > index && fields[index].Trim().Length > 0 ? fields[index].Trim() : null;
                overrides[fields[0].Trim()] = (Field(1),
                    double.TryParse(Field(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var power) ? power : (double?)null,
                    Field(3), int.TryParse(Field(4), out var position) ? position : 0, Field(5));
            }

            return encounters.Select(encounter =>
            {
                if (!overrides.TryGetValue(encounter.Name, out var entry))
                {
                    return encounter;
                }

                if (entry.Power.HasValue)
                {
                    intended[encounter.Name] = entry.Power.Value;
                }

                return encounter.Placed(entry.Tier, entry.World, entry.Position, entry.Teaches);
            }).ToList();
        }

        private static string Csv(IReadOnlyList<EncounterStudy> studies)
        {
            var text = new StringBuilder("encounter,tier,player,rung,power,battles,wins,win_rate,win_low,win_high,avg_turns,avg_health_left_on_win,avg_lowest_health,avg_pilots_lost,avg_damage_taken,avg_damage_dealt,avg_ap_unused,avg_places,avg_replaces,avg_ejects,avg_unlocks\n");
            var culture = CultureInfo.InvariantCulture;
            foreach (var study in studies)
            {
                foreach (var ladder in study.Ladders.Values)
                {
                    foreach (var point in ladder.Points)
                    {
                        var s = point.Summary;
                        text.AppendLine(string.Join(",",
                            "\"" + study.Encounter.Name + "\"", study.Encounter.Tier, ladder.Player, point.Rung.ToString(culture), point.Multiplier.ToString("0.00", culture),
                            s.Runs.ToString(culture), s.Wins.ToString(culture), s.WinRate.ToString("0.000", culture), s.WinInterval.Low.ToString("0.000", culture), s.WinInterval.High.ToString("0.000", culture),
                            s.AverageTurns.ToString("0.00", culture), s.AverageHealthLeftOnWin.ToString("0.000", culture), s.AverageLowestHealth.ToString("0.000", culture),
                            s.AveragePilotsFallen.ToString("0.00", culture), s.AverageDamageTaken.ToString("0.00", culture), s.AverageDamageDealt.ToString("0.00", culture),
                            s.AverageApWasted.ToString("0.00", culture), s.AveragePlaces.ToString("0.00", culture), s.AverageReplaces.ToString("0.00", culture),
                            s.AverageEjects.ToString("0.00", culture), s.AverageUnlocks.ToString("0.00", culture)));
                    }
                }
            }

            return text.ToString();
        }

        private static int Quick(int runs, string filter)
        {
            var players = new[] { PlayerSpec.Greedy, PlayerSpec.Casual, PlayerSpec.Careful };
            foreach (var encounter in BuiltInEncounters.All().Where(candidate => filter == null || candidate.Name.Contains(filter)))
            {
                Console.WriteLine($"== {encounter.Name} [{encounter.Tier}]");
                foreach (var player in players)
                {
                    var clock = Stopwatch.StartNew();
                    var ladder = Lab.RunLadder(encounter, player, 30, runs);
                    string At(double rate)
                    {
                        var rung = ladder.RungFor(rate);
                        return rung.HasValue ? $"x{PowerLadder.Multiplier(rung.Value):0.0}" : "-";
                    }

                    Console.WriteLine($"  {player.Name,-8} 50% {At(0.5),-5} 90% {At(0.9),-5} 97% {At(0.97),-5} ({clock.ElapsedMilliseconds} ms)");
                }
            }

            return 0;
        }
    }
}
