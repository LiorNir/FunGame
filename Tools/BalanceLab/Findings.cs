using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BalanceLab
{
    /// <summary>Plain statements computed from the measurements. Each one states a number, so it can be checked against the tables.</summary>
    public static class Findings
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        private static string Mult(double value)
        {
            return "&times;" + value.ToString("0.0", Culture);
        }

        private static string Pct(double value)
        {
            return (value * 100).ToString("0", Culture) + "%";
        }

        public static List<string> Build(IReadOnlyList<EncounterStudy> studies)
        {
            var items = new List<string>();
            var withCareful = studies.Where(study => study.Required("Careful", 0.9).HasValue).OrderBy(study => study.Required("Careful", 0.9).Value).ToList();

            // ranking
            if (withCareful.Count >= 2)
            {
                var easiest = withCareful.First();
                var hardest = withCareful.Last();
                items.Add($"<b>The range.</b> For a careful player to win 9 battles in 10, the easiest encounter (<i>{Html.Esc(easiest.Encounter.Name)}</i>) needs " +
                          $"{Mult(easiest.Required("Careful", 0.9).Value)} the starter roster's power and the hardest (<i>{Html.Esc(hardest.Encounter.Name)}</i>) needs {Mult(hardest.Required("Careful", 0.9).Value)}.");
            }

            var unbeatable = studies.Where(study => !study.Required("Careful", 0.9).HasValue).ToList();
            if (unbeatable.Count > 0)
            {
                items.Add($"<b>Not beaten at all</b> within the tested power: {string.Join(", ", unbeatable.Select(study => "<i>" + Html.Esc(study.Encounter.Name) + "</i>"))}. " +
                          "Either they are far too hard or they cannot be won with these pilots; the encounter card shows how close the best attempts got.");
            }

            // sharpness
            var widths = studies.Select(study => study.TransitionWidth("Careful")).Where(width => width.HasValue).Select(width => width.Value).OrderBy(width => width).ToList();
            if (widths.Count > 0)
            {
                var median = Math.Max(1, widths[widths.Count / 2]);
                items.Add($"<b>Difficulty is a cliff, not a slope.</b> For a careful player the win rate climbs from 10% to 90% within {median} power step{(median == 1 ? string.Empty : "s")} " +
                          $"(of 0.1 each) in the typical encounter. With a small deck and a player who sees the enemy's next move, the result is mostly decided by whether the board's numbers are big enough, " +
                          "and chance changes little. So win rate alone separates encounters poorly; <b>health left after a win</b> and <b>pilots lost</b> are the numbers to compare them by. " +
                          "More chance (a larger deck, random enemy actions, random abilities) would soften the cliff.");
            }

            // skill gap
            var gaps = studies.Select(study => (Study: study, Casual: study.Required("Casual", 0.9), Careful: study.Required("Careful", 0.9), Greedy: study.Required("Greedy", 0.9)))
                .Where(row => row.Casual.HasValue && row.Careful.HasValue).ToList();
            if (gaps.Count > 0)
            {
                var average = gaps.Average(row => row.Casual.Value - row.Careful.Value);
                var widest = gaps.OrderByDescending(row => row.Casual.Value - row.Careful.Value).First();
                var narrowest = gaps.OrderBy(row => row.Casual.Value - row.Careful.Value).First();
                items.Add($"<b>Skill matters.</b> A casual player needs on average {average.ToString("0.0", Culture)} more power than a careful one to win 90% (the careful player's power is the baseline). " +
                          $"The gap is widest in <i>{Html.Esc(widest.Study.Encounter.Name)}</i> ({(widest.Casual.Value - widest.Careful.Value).ToString("0.0", Culture)}) and narrowest in " +
                          $"<i>{Html.Esc(narrowest.Study.Encounter.Name)}</i> ({(narrowest.Casual.Value - narrowest.Careful.Value).ToString("0.0", Culture)}). " +
                          "A wide gap means the encounter rewards reading the enemy and planning; a narrow one means raw power decides it.");
            }

            var greedy = gaps.Where(row => row.Greedy.HasValue).ToList();
            if (greedy.Count > 0)
            {
                items.Add($"<b>Ignoring the enemy is expensive.</b> The old script, which does not read what the enemy announced, needs on average {greedy.Average(row => row.Greedy.Value - row.Careful.Value).ToString("0.0", Culture)} more power than the careful player. " +
                          "That is the size of the advantage a human gets from using the telegraphed intents.");
            }

            // dips
            var dips = new List<string>();
            foreach (var study in studies)
            {
                var divides = study.Encounter.Setup.Board.Nodes.Any(node => node.ConnectionOperation == FunGame.Board.Operation.Divide);
                foreach (var bot in new[] { "Careful", "Gambler", "Casual", "Greedy" })
                {
                    foreach (var dip in study.Dips(bot, 0.25))
                    {
                        dips.Add($"<i>{Html.Esc(study.Encounter.Name)}</i> ({bot}: {dip.From.ToString("0.0", Culture)} to {dip.To.ToString("0.0", Culture)}, -{dip.Drop.ToString("0", Culture)} points{(divides ? ", the board divides" : string.Empty)})");
                    }
                }
            }

            if (dips.Count > 0)
            {
                items.Add($"<b>More power made an encounter harder</b> (a drop of 25 points or more in win rate when the roster got stronger) in: {string.Join("; ", dips.Take(6))}. " +
                          "The cause is not known from the numbers alone. It is either a rule that punishes bigger numbers (dividing by a stat below 1 multiplies the chain, and tolls behave differently as values grow) " +
                          "or a simulated player making a poor trade (this happened in an early version of the careful player, which gave up pilots too cheaply as damage grew). " +
                          "Read a typical loss on the encounter's card before concluding anything.");
            }

            // sacrificing pilots
            var sacrifice = studies.Select(study => (Study: study, Careful: study.Required("Careful", 0.9), Gambler: study.Required("Gambler", 0.9)))
                .Where(row => row.Careful.HasValue && row.Gambler.HasValue && row.Careful.Value - row.Gambler.Value >= 0.2 - 1e-9).ToList();
            var gamblerOnly = studies.Where(study => !study.Required("Careful", 0.9).HasValue && study.Required("Gambler", 0.9).HasValue).ToList();
            if (sacrifice.Count > 0 || gamblerOnly.Count > 0)
            {
                var parts = sacrifice.Select(row => $"<i>{Html.Esc(row.Study.Encounter.Name)}</i> ({Mult(row.Careful.Value)} careful vs {Mult(row.Gambler.Value)} gambling)")
                    .Concat(gamblerOnly.Select(study => $"<i>{Html.Esc(study.Encounter.Name)}</i> (only the gambler wins it)"));
                items.Add($"<b>Sacrificing pilots can be the way to win.</b> A player willing to lose pilots for damage needs at least 0.2 less power than one who protects them in: {string.Join("; ", parts)}. " +
                          "Pilots lost are gone for the rest of the battle, so this is a real choice between speed and staying power. If you want it to be a deliberate part of the game, those encounters are the ones that test it; " +
                          "if not, losing pilots may be too cheap or too rewarding. How the simulated players weigh a lost pilot changes some results a lot, which is why both styles are reported.");
            }

            // target feel
            var measured = studies.Where(study => study.AtIntended.ContainsKey("Careful")).ToList();
            if (measured.Count > 0)
            {
                var easy = measured.Where(study => study.AtIntended["Careful"].WinRate > study.Tier.CarefulWinRate + 0.08).ToList();
                var hard = measured.Where(study => study.AtIntended["Careful"].WinRate < study.Tier.CarefulWinRate - 0.08).ToList();
                var ok = measured.Count - easy.Count - hard.Count;
                items.Add($"<b>Against the target feel</b> at each encounter's intended power: {ok} on target, {easy.Count} easier than its tier asks, {hard.Count} harder." +
                          (hard.Count > 0 ? $" Harder: {string.Join(", ", hard.Select(study => $"<i>{Html.Esc(study.Encounter.Name)}</i> ({Pct(study.AtIntended["Careful"].WinRate)} vs {Pct(study.Tier.CarefulWinRate)} wanted)"))}." : string.Empty) +
                          (easy.Count > 0 ? $" Easier: {string.Join(", ", easy.Select(study => $"<i>{Html.Esc(study.Encounter.Name)}</i> ({Pct(study.AtIntended["Careful"].WinRate)} vs {Pct(study.Tier.CarefulWinRate)} wanted)"))}." : string.Empty) +
                          " The intended power is an input: change it in tiers.txt to ask the question for a different stage of the game.");
            }

            // mechanics usage
            var carefulAt = studies.Where(study => study.AtIntended.ContainsKey("Careful")).Select(study => study.AtIntended["Careful"]).ToList();
            if (carefulAt.Count > 0)
            {
                var ejects = carefulAt.Average(summary => summary.AverageEjects);
                var replaces = carefulAt.Average(summary => summary.AverageReplaces);
                var wasted = carefulAt.Average(summary => summary.AverageApWasted / Math.Max(1d, summary.AverageTurns));
                items.Add($"<b>How the careful player spends its turns.</b> Per battle it ejects {ejects.ToString("0.00", Culture)} pilots and replaces {replaces.ToString("0.0", Culture)}, and leaves " +
                          $"{wasted.ToString("0.0", Culture)} of its 3 AP unused per turn on average." +
                          (ejects < 0.05 ? " Ejecting is almost never worth its AP cost in these encounters." : string.Empty) +
                          (wasted > 1.2 ? " Much of the AP is going unused: either there are few useful things to do each turn or the board fills up quickly." : string.Empty));
            }

            var stalls = studies.Where(study => study.AtIntended.Values.Any(summary => summary.Stalemates > 0)).ToList();
            if (stalls.Count > 0)
            {
                items.Add($"<b>Battles that never end.</b> Some battles hit the turn limit without a result in: {string.Join(", ", stalls.Select(study => "<i>" + Html.Esc(study.Encounter.Name) + "</i>"))}. " +
                          "That usually means the enemy's defense is higher than anything the board can attack with.");
            }

            return items;
        }
    }
}
