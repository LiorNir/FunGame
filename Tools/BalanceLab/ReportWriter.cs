using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using FunGame.Battle;
using FunGame.Sim;

namespace BalanceLab
{
    /// <summary>Turns the measurements into one self-contained HTML page for reading and discussing.</summary>
    public static class ReportWriter
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        private static readonly Dictionary<string, string> BotColors = new Dictionary<string, string>
        {
            ["Greedy"] = "#8a8f98",
            ["Casual"] = "#e8832a",
            ["Careful"] = "#2d6cdf",
            ["Gambler"] = "#8e55c7"
        };

        private const double TargetTolerance = 0.08;

        public static string Write(IReadOnlyList<EncounterStudy> studies, StudySettings settings, string source)
        {
            var page = new StringBuilder();
            page.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            page.Append("<title>Encounter Balance Report</title><style>").Append(Css).Append("</style></head><body><main>");

            page.Append("<h1>Encounter balance report</h1>");
            page.Append($"<p class=\"sub\">{Html.Esc(source)} &middot; {studies.Count} encounter(s) &middot; {settings.RunsPerRung} battles per power step, {settings.RunsAtIntended} at the intended power &middot; generated {DateTime.Now:yyyy-MM-dd HH:mm}</p>");

            page.Append(ReadThisFirst());
            page.Append("<h2>What we learned</h2>").Append(FindingsList(studies));
            page.Append("<h2>Encounters, easiest to hardest</h2>").Append(RankingTable(studies));
            page.Append(WorldSections(studies));
            page.Append("<h2>Each encounter</h2>");
            foreach (var study in Ranked(studies))
            {
                page.Append(Card(study, study.Index + 1));
            }

            page.Append("<h2>The players compared</h2>").Append(BotTable(studies, settings));
            page.Append("<h2>How this was measured, and what to distrust</h2>").Append(Method());
            page.Append("</main></body></html>");
            return page.ToString();
        }

        // ---------- small formatting helpers ----------

        private static string Mult(double? value)
        {
            return value.HasValue ? "&times;" + value.Value.ToString("0.0", Culture) : "never";
        }

        private static string Signed(double value)
        {
            return (value >= 0 ? "+" : "-") + Math.Abs(value).ToString("0.0", Culture);
        }

        private static string Pct(double value)
        {
            return (value * 100).ToString("0", Culture) + "%";
        }

        private static string Num(double value, string format = "0.0")
        {
            return value.ToString(format, Culture);
        }

        private static string Color(string bot)
        {
            return BotColors.TryGetValue(bot, out var color) ? color : "#555";
        }

        private static IEnumerable<EncounterStudy> Ranked(IReadOnlyList<EncounterStudy> studies)
        {
            return studies.OrderBy(study => study.Required("Careful", 0.9) ?? 99d).ThenBy(study => study.Required("Careful", 0.5) ?? 99d);
        }

        private static (string Text, string Class) Verdict(EncounterStudy study)
        {
            if (!study.AtIntended.TryGetValue("Careful", out var careful))
            {
                return ("not measured", "neutral");
            }

            var target = study.Tier.CarefulWinRate;
            if (careful.WinRate > target + TargetTolerance)
            {
                return ("Easier than target", "easy");
            }

            if (careful.WinRate < target - TargetTolerance)
            {
                return ("Harder than target", "hard");
            }

            return ("On target", "ok");
        }

        // ---------- sections ----------

        private static string ReadThisFirst()
        {
            return "<div class=\"box\"><b>How to read this.</b> Pilots never get stronger in this test. Instead, each encounter is played with the same " +
                   "pilots at different <b>power</b>: &times;1.0 is the starter roster as it is, &times;1.5 means every stat of every pilot is 50% higher. " +
                   "An encounter's difficulty is the power at which a player starts to win. Four simulated players try each one: " +
                   "<span class=\"dot\" style=\"background:#2d6cdf\"></span><b>Careful</b> reads the enemy's announced action and plans ahead, " +
                   "<span class=\"dot\" style=\"background:#e8832a\"></span><b>Casual</b> only thinks about this turn and never takes a pilot back, " +
                   "<span class=\"dot\" style=\"background:#8a8f98\"></span><b>Greedy</b> is the old script that ignores the enemy, and <span class=\"dot\" style=\"background:#8e55c7\"></span><b>Gambler</b> plays like Careful but is happy to lose pilots for damage. " +
                   "Each encounter also has an <b>intended power</b> and a <b>tier</b> (Tutorial, Early, Mid, Late); the tier sets the win rates we want there.</div>";
        }

        private static string FindingsList(IReadOnlyList<EncounterStudy> studies)
        {
            var items = Findings.Build(studies);
            return "<ul class=\"findings\">" + string.Join(string.Empty, items.Select(item => "<li>" + item + "</li>")) + "</ul>";
        }

        private static string RankingTable(IReadOnlyList<EncounterStudy> studies)
        {
            var table = new StringBuilder();
            table.Append("<div class=\"scroll\"><table><thead><tr><th>#</th><th>Encounter</th><th>Tier</th>" +
                         "<th title=\"Power at which a careful player wins half of its battles\">Careful<br>wins 50%</th>" +
                         "<th title=\"Power at which a careful player wins 9 of 10\">Careful<br>wins 90%</th>" +
                         "<th>Casual<br>wins 90%</th><th>Greedy<br>wins 90%</th><th title=\"A player that sacrifices pilots for damage\">Gambler<br>wins 90%</th>" +
                         "<th title=\"Extra power a casual player needs compared with a careful one\">Skill<br>gap</th>" +
                         "<th title=\"Power steps between winning 10% and winning 90%\">Step<br>width</th>" +
                         "<th>Intended<br>power</th><th>Careful wins<br>at intended</th><th>Casual wins<br>at intended</th><th>Verdict</th></tr></thead><tbody>");
            var rank = 0;
            foreach (var study in Ranked(studies))
            {
                rank++;
                var careful = study.Required("Careful", 0.9);
                var casual = study.Required("Casual", 0.9);
                var gap = careful.HasValue && casual.HasValue ? Signed(casual.Value - careful.Value) : "-";
                var width = study.TransitionWidth("Careful");
                var verdict = Verdict(study);
                study.AtIntended.TryGetValue("Careful", out var atCareful);
                study.AtIntended.TryGetValue("Casual", out var atCasual);
                table.Append($"<tr><td>{rank}</td><td><a href=\"#enc-{study.Index}\">{Html.Esc(study.Encounter.Name)}</a></td><td>{Html.Esc(study.Encounter.Tier)}</td>" +
                             $"<td>{Mult(study.Required("Careful", 0.5))}</td><td>{Mult(careful)}</td><td>{Mult(casual)}</td><td>{Mult(study.Required("Greedy", 0.9))}</td><td>{Mult(study.Required("Gambler", 0.9))}</td>" +
                             $"<td>{gap}</td><td>{(width.HasValue ? width.Value.ToString(Culture) : "-")}</td>" +
                             $"<td>&times;{Num(study.Intended)}</td><td>{(atCareful != null ? Pct(atCareful.WinRate) : "-")}</td><td>{(atCasual != null ? Pct(atCasual.WinRate) : "-")}</td>" +
                             $"<td><span class=\"pill {verdict.Class}\">{verdict.Text}</span></td></tr>");
            }

            table.Append("</tbody></table></div>");
            table.Append("<p class=\"hint\">&ldquo;never&rdquo; means not within the tested range of power. Step width counts power steps of 0.1: a width of 0 or 1 means the encounter goes from hopeless to easy within a single step.</p>");
            return table.ToString();
        }

        private static string Card(EncounterStudy study, int index)
        {
            var encounter = study.Encounter;
            var card = new StringBuilder();
            var verdict = Verdict(study);
            card.Append($"<section class=\"card\" id=\"enc-{index - 1}\"><h3>{Html.Esc(encounter.Name)} <span class=\"tier\">{Html.Esc(encounter.Tier)}</span> <span class=\"pill {verdict.Class}\">{verdict.Text}</span></h3>");
            if (!string.IsNullOrEmpty(encounter.Note))
            {
                card.Append($"<p class=\"note\">{Html.Esc(encounter.Note)}</p>");
            }

            card.Append("<p class=\"meta\">").Append(Describe(encounter)).Append("</p>");

            // chart and outcome split
            var series = new List<Html.Series>();
            foreach (var pair in study.Ladders)
            {
                var line = new Html.Series { Name = pair.Key, Color = Color(pair.Key) };
                foreach (var point in pair.Value.Points)
                {
                    line.Points.Add((point.Multiplier, point.Summary.WinRate));
                }

                series.Add(line);
            }

            var minX = study.Ladders.Values.SelectMany(ladder => ladder.Points).Min(point => (double)point.Multiplier);
            var maxX = study.Ladders.Values.SelectMany(ladder => ladder.Points).Max(point => (double)point.Multiplier);
            minX = Math.Min(minX, Math.Max(0.2, study.Intended - 0.2));
            maxX = Math.Max(maxX, study.Intended + 0.3);
            var tier = study.Tier;
            var chart = Html.LineChart(series, Math.Floor(minX * 10) / 10, Math.Ceiling(maxX * 10) / 10, "roster power", "win rate",
                new[] { (tier.CarefulWinRate, $"{tier.Name} target (careful)", "#2d6cdf"), (tier.CasualWinRate, $"{tier.Name} target (casual)", "#e8832a") },
                study.Intended, $"intended &times;{Num(study.Intended)}".Replace("&times;", "x"));
            card.Append("<div class=\"cols\"><div><h4>Win rate by power</h4>").Append(chart);
            card.Append("<p class=\"legend\">").Append(string.Join(" ", series.Select(line => $"<span class=\"dot\" style=\"background:{line.Color}\"></span>{line.Name}"))).Append("</p></div>");

            card.Append($"<div><h4>At the intended power (&times;{Num(study.Intended)})</h4>");
            foreach (var bot in new[] { "Careful", "Casual" })
            {
                if (!study.AtIntended.TryGetValue(bot, out var summary))
                {
                    continue;
                }

                var wins = summary.Wins / (double)summary.Runs;
                card.Append($"<div class=\"botrow\"><span class=\"dot\" style=\"background:{Color(bot)}\"></span><b>{bot}</b> " +
                            $"{Pct(summary.WinRate)} <span class=\"ci\">({Pct(summary.WinInterval.Low)}&ndash;{Pct(summary.WinInterval.High)})</span></div>");
                card.Append(Html.Stack(new[]
                {
                    ("won", wins, "#3d9a5b"),
                    ("lost", summary.Defeats / (double)summary.Runs, "#c24b4b"),
                    ("stalled", summary.Stalemates / (double)summary.Runs, "#9a8f3d")
                }));
            }

            card.Append(StatsTable(study)).Append(Reliance(study)).Append("</div></div>");

            // histograms for the careful player
            if (study.AtIntended.TryGetValue("Careful", out var careful) && careful.Wins > 0)
            {
                card.Append("<div class=\"cols\"><div><h4>Health left after a win (careful)</h4>")
                    .Append(Html.Bars(careful.HealthLeftOnWins, new[] { "0-20%", "20-40%", "40-60%", "60-80%", "80-100%" }, "#3d9a5b")).Append("</div>");
                var turns = careful.TurnsTaken;
                var last = Math.Max(5, Array.FindLastIndex(turns, count => count > 0) + 1);
                card.Append("<div><h4>Turns taken (careful)</h4>")
                    .Append(Html.Bars(turns.Take(last).ToList(), Enumerable.Range(1, last).Select(turn => turn == BatchSummary.TurnBuckets ? turn + "+" : turn.ToString(Culture)).ToList(), "#6c7fa8")).Append("</div></div>");
            }

            card.Append(Game("A typical win (careful player)", study.TypicalWin));
            card.Append(Game("A typical loss (careful player)", study.TypicalLoss));
            card.Append("</section>");
            return card.ToString();
        }

        /// <summary>How far a win rate must fall without a mechanic for the encounter to count as needing it.</summary>
        private const double NeededDrop = 0.15;

        /// <summary>The mechanics (of those tested) whose removal drops the careful player's win rate by at least <see cref="NeededDrop"/>.</summary>
        private static List<string> Needs(EncounterStudy study)
        {
            if (!study.AtIntended.TryGetValue("Careful", out var careful))
            {
                return new List<string>();
            }

            return study.WithoutMechanic.Where(pair => careful.WinRate - pair.Value.WinRate >= NeededDrop).Select(pair => pair.Key).ToList();
        }

        private static string Reliance(EncounterStudy study)
        {
            var rules = study.Encounter.Setup.Rules;
            var html = new StringBuilder("<h4>Does it need&hellip;? (careful player, same battles)</h4>");
            var banned = new List<string>();
            if (!rules.AllowReplace)
            {
                banned.Add("replacing pilots");
            }

            if (!rules.AllowEject)
            {
                banned.Add("ejecting pilots");
            }

            if (banned.Count > 0)
            {
                html.Append($"<p class=\"hint\">Not available in this encounter: {string.Join(" and ", banned)}.</p>");
            }

            if (study.WithoutMechanic.Count == 0 || !study.AtIntended.TryGetValue("Careful", out var careful))
            {
                return html.ToString();
            }

            html.Append("<table class=\"stats\"><thead><tr><th>Taken away</th><th>Wins</th><th>Change</th><th>So</th></tr></thead><tbody>");
            foreach (var pair in study.WithoutMechanic)
            {
                var change = (pair.Value.WinRate - careful.WinRate) * 100;
                var verdict = -change >= NeededDrop * 100 ? "needed" : -change >= 5 ? "helps" : "not needed";
                html.Append($"<tr><td>{Html.Esc(pair.Key)}</td><td>{Pct(pair.Value.WinRate)}</td><td>{(change >= 0 ? "+" : "-")}{Math.Abs(change).ToString("0", Culture)}</td><td>{verdict}</td></tr>");
            }

            html.Append("</tbody></table>");
            return html.ToString();
        }

        private static string WorldSections(IReadOnlyList<EncounterStudy> studies)
        {
            var html = new StringBuilder();
            foreach (var world in studies.Where(study => study.Encounter.World != null).GroupBy(study => study.Encounter.World))
            {
                var steps = world.OrderBy(study => study.Encounter.Position).ToList();
                html.Append($"<h2>{Html.Esc(world.Key)}: how it ramps</h2>");
                html.Append("<div class=\"scroll\"><table><thead><tr><th>Step</th><th>Encounter</th><th>It teaches</th><th>Power needed<br>(careful, 90%)</th>" +
                            "<th>Careful wins<br>at intended</th><th>Health left<br>on a win</th><th>Pilots lost<br>per battle</th><th>Casual wins<br>at intended</th><th>Skill<br>gap</th><th>Needs</th></tr></thead><tbody>");
                foreach (var step in steps)
                {
                    step.AtIntended.TryGetValue("Careful", out var careful);
                    step.AtIntended.TryGetValue("Casual", out var casual);
                    var required = step.Required("Careful", 0.9);
                    var casualRequired = step.Required("Casual", 0.9);
                    var needs = Needs(step);
                    html.Append($"<tr><td>{step.Encounter.Position}</td><td><a href=\"#enc-{step.Index}\">{Html.Esc(step.Encounter.Name)}</a></td><td>{Html.Esc(step.Encounter.Teaches ?? string.Empty)}</td>" +
                                $"<td>{Mult(required)}</td><td>{(careful != null ? Pct(careful.WinRate) : "-")}</td><td>{(careful != null && careful.Wins > 0 ? Pct(careful.AverageHealthLeftOnWin) : "-")}</td>" +
                                $"<td>{(careful != null ? Num(careful.AveragePilotsFallen) : "-")}</td><td>{(casual != null ? Pct(casual.WinRate) : "-")}</td>" +
                                $"<td>{(required.HasValue && casualRequired.HasValue ? Signed(casualRequired.Value - required.Value) : "-")}</td><td>{(needs.Count > 0 ? Html.Esc(string.Join(", ", needs)) : "nothing special")}</td></tr>");
                }

                html.Append("</tbody></table></div>");

                var labels = steps.Select(step => step.Encounter.Position.ToString(Culture)).ToList();
                html.Append("<div class=\"cols\"><div><h4>Power needed by step</h4>")
                    .Append(Html.ValueBars(steps.Select(step => step.Required("Careful", 0.9) ?? 0d).ToList(), labels, "#2d6cdf", "0.0")).Append("</div>")
                    .Append("<div><h4>Health left after a win, by step</h4>")
                    .Append(Html.ValueBars(steps.Select(step => step.AtIntended.TryGetValue("Careful", out var s) && s.Wins > 0 ? s.AverageHealthLeftOnWin * 100 : 0d).ToList(), labels, "#3d9a5b", "0")).Append("</div></div>");

                // checks, each stated with its numbers
                var checks = new List<string>();
                for (var index = 1; index < steps.Count; index++)
                {
                    var before = steps[index - 1].Required("Careful", 0.9);
                    var now = steps[index].Required("Careful", 0.9);
                    if (before.HasValue && now.HasValue && now.Value < before.Value - 1e-9)
                    {
                        checks.Add($"<b>Not a ramp:</b> step {steps[index].Encounter.Position} needs less power ({Mult(now)}) than step {steps[index - 1].Encounter.Position} ({Mult(before)}).");
                    }
                }

                foreach (var step in steps)
                {
                    var tier = step.Tier;
                    var casualRequired = step.Required("Casual", 0.9);
                    var required = step.Required("Careful", 0.9);
                    if (tier.Name == "Tutorial" && required.HasValue && casualRequired.HasValue && casualRequired.Value - required.Value > 0.15)
                    {
                        checks.Add($"<b>Needs more thinking than a tutorial should:</b> step {step.Encounter.Position} ({Html.Esc(step.Encounter.Name)}) asks a casual player for {Num(casualRequired.Value - required.Value)} more power than a careful one.");
                    }

                    var verdict = Verdict(step);
                    if (verdict.Class != "ok" && step.AtIntended.ContainsKey("Careful"))
                    {
                        checks.Add($"<b>{verdict.Text}:</b> step {step.Encounter.Position} ({Html.Esc(step.Encounter.Name)}), careful player wins {Pct(step.AtIntended["Careful"].WinRate)} where its tier ({Html.Esc(step.Encounter.Tier)}) asks for {Pct(step.Tier.CarefulWinRate)}.");
                    }

                    var needs = Needs(step);
                    var rules = step.Encounter.Setup.Rules;
                    if ((needs.Contains("replacing pilots") && !rules.AllowReplace) || (needs.Contains("ejecting pilots") && !rules.AllowEject))
                    {
                        checks.Add($"<b>Needs a mechanic it does not allow:</b> step {step.Encounter.Position}.");
                    }
                }

                html.Append(checks.Count == 0
                    ? "<p class=\"hint\">Checks (rising power needed, tutorial steps that need little thinking, win rates against the tier): all fine.</p>"
                    : "<ul class=\"findings\">" + string.Join(string.Empty, checks.Select(check => "<li>" + check + "</li>")) + "</ul>");
            }

            return html.ToString();
        }

        private static string StatsTable(EncounterStudy study)
        {
            var table = new StringBuilder("<table class=\"stats\"><thead><tr><th></th>");
            var bots = new[] { "Careful", "Casual" }.Where(bot => study.AtIntended.ContainsKey(bot)).ToList();
            foreach (var bot in bots)
            {
                table.Append($"<th>{bot}</th>");
            }

            table.Append("</tr></thead><tbody>");
            void Row(string label, Func<BatchSummary, string> value)
            {
                table.Append($"<tr><td>{label}</td>");
                foreach (var bot in bots)
                {
                    table.Append($"<td>{value(study.AtIntended[bot])}</td>");
                }

                table.Append("</tr>");
            }

            Row("Turns (average)", summary => Num(summary.AverageTurns));
            Row("Health left on a win", summary => summary.Wins > 0 ? Pct(summary.AverageHealthLeftOnWin) : "-");
            Row("Lowest health reached", summary => Pct(summary.AverageLowestHealth));
            Row("Pilots lost per battle", summary => Num(summary.AveragePilotsFallen));
            Row("Damage taken / dealt", summary => $"{Num(summary.AverageDamageTaken)} / {Num(summary.AverageDamageDealt)}");
            Row("AP unused per battle", summary => Num(summary.AverageApWasted));
            Row("Placed / replaced / ejected", summary => $"{Num(summary.AveragePlaces)} / {Num(summary.AverageReplaces)} / {Num(summary.AverageEjects)}");
            Row("Slots unlocked with AP", summary => Num(summary.AverageUnlocks));
            if (bots.Any(bot => study.AtIntended[bot].AverageDivideByZeros > 0.01))
            {
                Row("Divide-by-zero events", summary => Num(summary.AverageDivideByZeros));
            }

            table.Append("</tbody></table>");
            return table.ToString();
        }

        private static string Describe(EncounterCase encounter)
        {
            var setup = encounter.Setup;
            var text = new StringBuilder();
            var locked = setup.SlotRules.Count(rule => rule.Locked);
            text.Append($"{setup.Board.Nodes.Count} slots ({locked} locked), {setup.Board.Chains.Count} chains, {setup.Lanes.Count} lane{(setup.Lanes.Count == 1 ? string.Empty : "s")}. ");
            foreach (var lane in setup.Lanes)
            {
                text.Append($"<br><b>{Html.Esc(lane.Id)}:</b> ");
                text.Append(string.Join(" &rarr; ", lane.Enemies.Select(enemy =>
                    $"{Html.Esc(enemy.Name)} {enemy.MaxHealth} HP ({string.Join(", ", enemy.Actions.Select(action => $"{Html.Esc(action.Name)} {action.Attack}/{action.Defense}"))}{(enemy.RandomSelection ? ", random" : string.Empty)})")));
            }

            return text.ToString();
        }

        private static string Game(string title, BattleRecord record)
        {
            if (record == null)
            {
                return $"<details><summary>{title}</summary><p class=\"hint\">None happened in this batch.</p></details>";
            }

            var plans = record.Plans.ToDictionary(plan => plan.Turn);
            var text = new StringBuilder();
            foreach (var line in record.Log)
            {
                if (line.StartsWith("--- Turn ") && line.EndsWith("resolves ---"))
                {
                    var turn = int.Parse(line.Split(' ')[2], Culture);
                    if (plans.TryGetValue(turn, out var plan))
                    {
                        var actions = plan.Actions.Count == 0 ? "does nothing" : string.Join("; ", plan.Actions);
                        text.Append($"<span class=\"plan\">  the player: {Html.Esc(actions)}\n  expects: {Html.Esc(plan.Prediction.ToString())}  (doing nothing: {Html.Esc(plan.DoingNothing.ToString())})</span>\n");
                    }
                }

                text.Append(Html.Esc(line)).Append('\n');
            }

            var outcome = record.Outcome == BattleOutcome.Victory ? "won" : record.Outcome == BattleOutcome.Defeat ? "lost" : "stalled";
            return $"<details><summary>{title} &middot; {outcome} in {record.Turns} turns, {Num(record.FinalHealth)} / {Num(record.MaxHealth, "0")} HP left</summary><pre class=\"log\">{text}</pre></details>";
        }

        private static string BotTable(IReadOnlyList<EncounterStudy> studies, StudySettings settings)
        {
            var table = new StringBuilder("<table class=\"stats\"><thead><tr><th>Player</th><th>Average power needed to win 90%</th><th>Never wins within the range</th><th>Average power needed vs Careful</th></tr></thead><tbody>");
            foreach (var bot in settings.Bots)
            {
                var needed = studies.Select(study => study.Required(bot, 0.9)).ToList();
                var reachable = needed.Where(value => value.HasValue).Select(value => value.Value).ToList();
                var relative = studies.Select(study => study.Required(bot, 0.9) - study.Required("Careful", 0.9)).Where(value => value.HasValue).Select(value => value.Value).ToList();
                table.Append($"<tr><td><span class=\"dot\" style=\"background:{Color(bot)}\"></span>{bot}</td><td>{(reachable.Count > 0 ? "&times;" + Num(reachable.Average()) : "-")}</td>" +
                             $"<td>{needed.Count(value => !value.HasValue)}</td><td>{(relative.Count > 0 ? (relative.Average() >= 0 ? "+" : string.Empty) + Num(relative.Average()) : "-")}</td></tr>");
            }

            table.Append("</tbody></table><p class=\"hint\">The last column is the extra power a player needs compared with the careful one, averaged over encounters: the price of not reading the enemy.</p>");
            return table.ToString();
        }

        private static string Method()
        {
            return "<ul class=\"method\">" +
                   "<li><b>Power.</b> Every stat of every pilot is multiplied by the same number. This stands in for spending skill points, which do not exist yet, and it only measures how <i>much</i> power an encounter needs, not <i>which</i> upgrades beat it. Abilities are not part of it yet.</li>" +
                   "<li><b>Win rate</b> is the share of battles won out of the battles played at that power (shown with a 95% confidence range at the intended power). The same seeds are used for every player and every power, so differences are not luck of the draw.</li>" +
                   "<li><b>The players are programs, not people.</b> Careful tries many plans each turn and judges each by playing the turn out on a copy of the battle, using what the enemy has announced. It never sees the future draw or the enemy's next random choice. Its judgement of a board is a hand-made scoring rule, so it can be wrong in ways a human would not be, and humans will find tricks it does not. Treat the numbers as a ranking and a sanity check, not as a promise of how a person will fare.</li>" +
                   "<li><b>Search check.</b> In a spot check of five hard cases (60 battles each), a search that keeps 48 plans matched trying every plan within the noise of the test in four of them and was 11 points lower in the fifth. A 12-plan search was clearly worse on one (the boss). Neither is a ground truth: how a player <i>values</i> a board (for example how much a lost pilot costs) moved some results far more than the width of the search did.</li>" +
                   "<li><b>Tiers and targets</b> (careful / casual win rate at the intended power, health left): Tutorial 97% / 85% / 80%, Early 92% / 75% / 65%, Mid 70% / 45% / 50%, Late 55% / 30% / 35%. These are starting guesses to tune. &ldquo;On target&rdquo; means within 8 points.</li>" +
                   "<li><b>Not simulated yet:</b> abilities, Snipe, Lock and Swap, discarding, deck choice, pilots growing between encounters, and the SP economy.</li>" +
                   "</ul>";
        }

        private const string Css =
            "body{margin:0;background:#f6f7f9;color:#1d2330;font:15px/1.5 system-ui,-apple-system,Segoe UI,Roboto,sans-serif}" +
            "main{max-width:1060px;margin:0 auto;padding:28px 18px 80px}" +
            "h1{font-size:28px;margin:0 0 4px}h2{font-size:20px;margin:36px 0 10px;border-bottom:1px solid #d9dde4;padding-bottom:6px}" +
            "h3{font-size:18px;margin:0 0 6px}h4{font-size:13px;margin:12px 0 4px;color:#4a5262;text-transform:uppercase;letter-spacing:.04em}" +
            ".sub{color:#5b6475;margin:0 0 18px}.hint{color:#5b6475;font-size:13px}.note{margin:0 0 4px}.meta{color:#4a5262;font-size:13px;margin:4px 0 8px}" +
            ".box{background:#fff;border:1px solid #d9dde4;border-left:4px solid #2d6cdf;border-radius:8px;padding:12px 16px}" +
            ".findings li{margin:7px 0}.findings{padding-left:20px}" +
            ".scroll{overflow-x:auto}table{border-collapse:collapse;width:100%;background:#fff;border:1px solid #d9dde4;border-radius:8px;font-size:13.5px}" +
            "th,td{padding:7px 9px;border-bottom:1px solid #eceef2;text-align:left;vertical-align:top}th{background:#f0f2f6;font-weight:600}" +
            "table.stats{margin-top:10px;font-size:13px}table.stats td:not(:first-child),table.stats th:not(:first-child){text-align:right}" +
            ".card{background:#fff;border:1px solid #d9dde4;border-radius:10px;padding:16px 18px;margin:16px 0}" +
            ".cols{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:18px}" +
            ".chart{width:100%;height:auto}.bars{width:100%;height:auto;max-width:360px}" +
            ".chart .grid{stroke:#e5e8ee}.chart .axis{stroke:#9aa3b2}.chart .tick{font-size:10.5px;fill:#5b6475}.chart .label{font-size:11px;fill:#4a5262}.chart .mark{stroke:#3d9a5b;stroke-width:1.5;stroke-dasharray:2 3}" +
            ".bars .tick{font-size:10px;fill:#5b6475}" +
            ".tier{font-size:11px;font-weight:600;background:#e8ecf4;color:#3a4560;border-radius:10px;padding:2px 8px;vertical-align:middle}" +
            ".pill{font-size:11.5px;font-weight:600;border-radius:10px;padding:2px 9px;white-space:nowrap}" +
            ".pill.ok{background:#dff3e5;color:#1f6b3a}.pill.easy{background:#dde9fb;color:#1e4f9e}.pill.hard{background:#fbe0e0;color:#9a2a2a}.pill.neutral{background:#eceef2;color:#4a5262}" +
            ".dot{display:inline-block;width:10px;height:10px;border-radius:50%;margin:0 5px 0 8px;vertical-align:middle}.dot:first-child{margin-left:0}" +
            ".botrow{margin:8px 0 3px}.ci{color:#5b6475;font-size:12.5px}.legend{font-size:12.5px;margin:2px 0}" +
            ".stack{display:flex;height:22px;border-radius:5px;overflow:hidden;background:#eceef2;font-size:11.5px;color:#fff}.stack span{display:block;line-height:22px;text-align:center;white-space:nowrap;overflow:hidden}" +
            "details{margin-top:10px}summary{cursor:pointer;font-weight:600;color:#2d4f95}" +
            "pre.log{background:#10151f;color:#dbe2f0;padding:12px 14px;border-radius:8px;overflow-x:auto;font-size:12px;line-height:1.45;max-height:420px}" +
            ".plan{color:#7fd1a0}.method li{margin:7px 0}a{color:#2d6cdf}";
    }
}
