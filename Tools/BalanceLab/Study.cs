using System;
using System.Collections.Generic;
using System.Linq;
using FunGame.Battle;
using FunGame.Board;
using FunGame.Sim;

namespace BalanceLab
{
    public sealed class StudySettings
    {
        public int RunsPerRung = 100;
        public int RunsAtIntended = 300;
        public int MaxRung = 40;
        public string[] Bots = { "Greedy", "Casual", "Careful", "Gambler" };
    }

    /// <summary>Everything measured about one encounter.</summary>
    public sealed class EncounterStudy
    {
        public EncounterCase Encounter { get; set; }

        /// <summary>Position in the list of encounters, for links inside the report.</summary>
        public int Index { get; set; }

        /// <summary>The roster power (a multiplier of the base roster) the encounter is meant to be played at.</summary>
        public double Intended { get; set; } = 1.0;

        public int IntendedRung { get; set; }
        public Dictionary<string, Ladder> Ladders { get; } = new Dictionary<string, Ladder>();
        public Dictionary<string, BatchSummary> AtIntended { get; } = new Dictionary<string, BatchSummary>();
        public BattleRecord TypicalWin { get; set; }
        public BattleRecord TypicalLoss { get; set; }

        /// <summary>
        /// The careful player at the intended power with one mechanic taken away, by what was taken away (same battles as the
        /// normal batch, so the difference is the mechanic). Only mechanics the encounter actually allows are tested.
        /// </summary>
        public Dictionary<string, BatchSummary> WithoutMechanic { get; } = new Dictionary<string, BatchSummary>();

        public TargetTier Tier => TargetFeel.Find(Encounter.Tier);

        /// <summary>The roster power at which the bot first wins at least this share of battles, or null if it never does.</summary>
        public double? Required(string bot, double winRate)
        {
            var rung = Ladders[bot].RungFor(winRate);
            return rung.HasValue ? PowerLadder.Multiplier(rung.Value) : (double?)null;
        }

        /// <summary>
        /// Rungs between the first at which the bot wins 10% and the first at which it wins 90%: how sharp the step from
        /// "cannot" to "can" is. 1 means the encounter flips from hopeless to easy within one small step.
        /// </summary>
        public int? TransitionWidth(string bot)
        {
            var low = Ladders[bot].RungFor(0.10);
            var high = Ladders[bot].RungFor(0.90);
            return low.HasValue && high.HasValue ? high.Value - low.Value : (int?)null;
        }

        /// <summary>Drops in win rate between a rung and a later one of at least this many points, as (from multiplier, to multiplier, points).</summary>
        public List<(double From, double To, double Drop)> Dips(string bot, double minimumDrop)
        {
            var dips = new List<(double, double, double)>();
            var points = Ladders[bot].Points;
            for (var later = 1; later < points.Count; later++)
            {
                var best = points.Take(later).OrderByDescending(point => point.Summary.WinRate).First();
                var drop = best.Summary.WinRate - points[later].Summary.WinRate;
                if (drop >= minimumDrop)
                {
                    dips.Add((best.Multiplier, points[later].Multiplier, drop * 100));
                }
            }

            return dips.GroupBy(dip => dip.Item1).Select(group => group.OrderByDescending(dip => dip.Item3).First()).ToList();
        }
    }

    public static class Study
    {
        public static int RungOf(double multiplier)
        {
            return (int)Math.Round((multiplier - 1d) / PowerLadder.StepPerRung);
        }

        public static List<EncounterStudy> Run(
            IReadOnlyList<EncounterCase> encounters,
            IReadOnlyDictionary<string, double> intended,
            StudySettings settings,
            Action<string> progress)
        {
            var studies = new List<EncounterStudy>();
            foreach (var encounter in encounters)
            {
                var study = new EncounterStudy { Encounter = encounter, Index = studies.Count };
                if (intended.TryGetValue(encounter.Name, out var multiplier))
                {
                    study.Intended = multiplier;
                }

                study.IntendedRung = Math.Max(PowerLadder.MinRung, RungOf(study.Intended));
                foreach (var bot in settings.Bots)
                {
                    progress($"{encounter.Name}: {bot} ladder");
                    study.Ladders[bot] = Lab.RunLadder(encounter, Spec(bot), settings.MaxRung, settings.RunsPerRung);
                }

                // a larger batch at the power the encounter is meant for, and one typical game of each outcome to read
                var deck = PowerLadder.Scale(encounter.Setup.Deck, study.IntendedRung);
                foreach (var bot in settings.Bots.Where(bot => bot == "Casual" || bot == "Careful"))
                {
                    progress($"{encounter.Name}: {bot} at intended power");
                    var records = Lab.PlayBatch(encounter, deck, Spec(bot), 5000, settings.RunsAtIntended);
                    study.AtIntended[bot] = BatchSummary.From(records);
                    if (bot == "Careful")
                    {
                        study.TypicalWin = Narrate(encounter, deck, Typical(records.Where(record => record.Won).ToList()));
                        study.TypicalLoss = Narrate(encounter, deck, Typical(records.Where(record => !record.Won).ToList()));
                    }
                }

                RunWithoutMechanics(study, deck, settings, progress);
                studies.Add(study);
            }

            return studies;
        }

        /// <summary>
        /// Plays the careful player at the intended power with replacing, ejecting, self-opening slots or seeing locks coming taken away, one at a
        /// time. A big drop in win rate means the encounter needs that mechanic; no drop means it does not teach it.
        /// </summary>
        private static void RunWithoutMechanics(EncounterStudy study, IReadOnlyList<PilotStats> deck, StudySettings settings, Action<string> progress)
        {
            var encounter = study.Encounter;
            var runs = Math.Max(100, settings.RunsAtIntended / 2);
            void Test(string name, EncounterCase variant, PlayerSpec player = null)
            {
                progress($"{encounter.Name}: without {name}");
                study.WithoutMechanic[name] = BatchSummary.From(Lab.PlayBatch(variant, deck, player ?? PlayerSpec.Careful, 5000, runs));
            }

            if (encounter.Setup.Rules.AllowReplace)
            {
                var rules = encounter.Setup.Rules.Copy();
                rules.AllowReplace = false;
                Test("replacing pilots", encounter.WithRules(rules));
            }

            if (encounter.Setup.Rules.AllowEject)
            {
                var rules = encounter.Setup.Rules.Copy();
                rules.AllowEject = false;
                Test("ejecting pilots", encounter.WithRules(rules));
            }

            if (encounter.HasLockedSlots)
            {
                Test("slots that open by themselves", encounter.WithoutUnlocks());
            }

            if (encounter.HasEnemyLocks)
            {
                // the same encounter, played by a careful player that does not see locks coming
                Test("reacting to locks", encounter, PlayerSpec.CarefulIgnoringLocks);
            }
        }

        public static PlayerSpec Spec(string bot)
        {
            switch (bot)
            {
                case "Greedy":
                    return PlayerSpec.Greedy;
                case "Casual":
                    return PlayerSpec.Casual;
                case "Gambler":
                    return PlayerSpec.Gambler;
                case "Blind":
                    return PlayerSpec.CarefulIgnoringLocks;
                default:
                    return PlayerSpec.Careful;
            }
        }

        private static BattleRecord Typical(List<BattleRecord> records)
        {
            if (records.Count == 0)
            {
                return null;
            }

            var ordered = records.OrderBy(record => record.Turns).ThenBy(record => record.FinalHealth).ToList();
            return ordered[ordered.Count / 2];
        }

        private static BattleRecord Narrate(EncounterCase encounter, IReadOnlyList<PilotStats> deck, BattleRecord typical)
        {
            return typical == null
                ? null
                : BotRunner.Run(encounter.WithDeck(deck, typical.Seed), new SearchPlayer(BotProfile.Careful), BotRunner.DefaultTurnLimit, true);
        }
    }
}
