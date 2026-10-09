using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FunGame.Battle;
using FunGame.Board;

namespace FunGame.Sim
{
    /// <summary>A kind of player to test with.</summary>
    public sealed class PlayerSpec
    {
        public string Name { get; }
        public Func<IPlayer> Create { get; }

        public PlayerSpec(string name, Func<IPlayer> create)
        {
            Name = name;
            Create = create;
        }

        public static PlayerSpec Greedy => new PlayerSpec("Greedy", () => new GreedyPlayer());
        public static PlayerSpec Casual => new PlayerSpec("Casual", () => new SearchPlayer(BotProfile.Casual));
        public static PlayerSpec Careful => new PlayerSpec("Careful", () => new SearchPlayer(BotProfile.Careful));
        public static PlayerSpec CarefulIgnoringLocks => new PlayerSpec("Careful, ignoring locks", () => new SearchPlayer(BotProfile.CarefulIgnoringLocks));
        public static PlayerSpec Gambler => new PlayerSpec("Gambler", () => new SearchPlayer(BotProfile.Gambler));
        public static PlayerSpec Reference => new PlayerSpec("Reference", () => new SearchPlayer(BotProfile.Reference));
    }

    /// <summary>One encounter to test: the battle setup (board, lanes, enemies, rules) and the pilots that come with it.</summary>
    public sealed class EncounterCase
    {
        public string Name { get; }

        /// <summary>Where in the game it is meant to sit, which decides how forgiving it should be (see <see cref="TargetFeel"/>).</summary>
        public string Tier { get; }

        /// <summary>The setup; its deck is the base roster that the power ladder scales.</summary>
        public BattleSetup Setup { get; }

        public string Note { get; }

        /// <summary>The world (a short run of encounters that introduces one topic) this encounter belongs to, or null for a loose one.</summary>
        public string World { get; }

        /// <summary>Its place in the world, 1 = first. Only meaningful with a world.</summary>
        public int Position { get; }

        /// <summary>What the encounter is meant to teach, in a few words. The lab checks the encounter against this.</summary>
        public string Teaches { get; }

        public EncounterCase(string name, string tier, BattleSetup setup, string note = null, string world = null, int position = 0, string teaches = null)
        {
            Name = name;
            Tier = tier;
            Setup = setup;
            Note = note;
            World = world;
            Position = position;
            Teaches = teaches;
        }

        public BattleSetup WithDeck(IReadOnlyList<PilotStats> deck, int seed)
        {
            return new BattleSetup(Setup.Board, Setup.SlotRules, Setup.Lanes, Setup.Rules, deck, seed);
        }

        /// <summary>The same encounter with other rules, for checking what happens when a mechanic is taken away.</summary>
        public EncounterCase WithRules(BattleRules rules)
        {
            var setup = new BattleSetup(Setup.Board, Setup.SlotRules, Setup.Lanes, rules, Setup.Deck, Setup.Seed);
            return new EncounterCase(Name, Tier, setup, Note, World, Position, Teaches);
        }

        /// <summary>The same encounter in which slots that start locked never open (no turn, no wave, no AP).</summary>
        public EncounterCase WithoutUnlocks()
        {
            var rules = Setup.SlotRules.Select(rule => new SlotRule(rule.SlotId, rule.Locked, 0, 0, 0, rule.UnlockLaneId)).ToList();
            var setup = new BattleSetup(Setup.Board, rules, Setup.Lanes, Setup.Rules, Setup.Deck, Setup.Seed);
            return new EncounterCase(Name, Tier, setup, Note, World, Position, Teaches);
        }

        public bool HasLockedSlots => Setup.SlotRules.Any(rule => rule.Locked);

        /// <summary>True if any enemy action of the encounter can lock a pilot.</summary>
        public bool HasEnemyLocks => Setup.Lanes.SelectMany(lane => lane.Enemies).SelectMany(enemy => enemy.Actions)
            .Any(action => action.Effects.Any(effect => effect is LockEffect));

        /// <summary>The same encounter placed in a world (or moved to another tier).</summary>
        public EncounterCase Placed(string tier, string world, int position, string teaches)
        {
            return new EncounterCase(Name, tier ?? Tier, Setup, Note, world ?? World, position > 0 ? position : Position, teaches ?? Teaches);
        }
    }

    /// <summary>
    /// Standard "stronger" rosters, in place of spending SP. Rung 0 is the base roster; every rung adds a tenth of the
    /// base values to every stat of every pilot, and every rung below 0 takes a tenth away (down to a fifth of the
    /// base). An encounter's difficulty is then the lowest rung that beats it.
    /// </summary>
    public static class PowerLadder
    {
        public const float StepPerRung = 0.1f;

        /// <summary>The weakest rung: a fifth of the base stats.</summary>
        public const int MinRung = -8;

        public static float Multiplier(int rung)
        {
            return 1f + StepPerRung * rung;
        }

        public static IReadOnlyList<PilotStats> Scale(IReadOnlyList<PilotStats> roster, int rung)
        {
            var factor = Multiplier(rung);
            return roster.Select(pilot => new PilotStats(pilot.Name, pilot.Attack * factor, pilot.Defense * factor, pilot.Source)).ToList();
        }
    }

    /// <summary>How an encounter did at one rung of the power ladder.</summary>
    public sealed class LadderPoint
    {
        public int Rung { get; }
        public float Multiplier { get; }
        public BatchSummary Summary { get; }

        public LadderPoint(int rung, float multiplier, BatchSummary summary)
        {
            Rung = rung;
            Multiplier = multiplier;
            Summary = summary;
        }
    }

    /// <summary>One encounter played by one kind of player at every rung of the ladder (up to where it is always won).</summary>
    public sealed class Ladder
    {
        public EncounterCase Encounter { get; }
        public string Player { get; }
        public IReadOnlyList<LadderPoint> Points { get; }

        public Ladder(EncounterCase encounter, string player, IReadOnlyList<LadderPoint> points)
        {
            Encounter = encounter;
            Player = player;
            Points = points;
        }

        /// <summary>The lowest rung at which the win rate reaches the given rate, or null if it never does within the ladder.</summary>
        public int? RungFor(double winRate)
        {
            foreach (var point in Points)
            {
                if (point.Summary.WinRate >= winRate)
                {
                    return point.Rung;
                }
            }

            return null;
        }
    }

    public static class Lab
    {
        /// <summary>Plays the same encounter many times with consecutive seeds, in parallel.</summary>
        public static List<BattleRecord> PlayBatch(
            EncounterCase encounter,
            IReadOnlyList<PilotStats> deck,
            PlayerSpec player,
            int firstSeed,
            int runs,
            int turnLimit = BotRunner.DefaultTurnLimit)
        {
            var records = new BattleRecord[runs];
            Parallel.For(0, runs, index =>
            {
                var setup = encounter.WithDeck(deck, firstSeed + index);
                records[index] = BotRunner.Run(setup, player.Create(), turnLimit);
            });
            return records.ToList();
        }

        /// <summary>
        /// Plays the encounter at rung 0, 1, 2 ... and stops once the player wins nearly every time (or at the last rung).
        /// </summary>
        public static Ladder RunLadder(
            EncounterCase encounter,
            PlayerSpec player,
            int maxRung,
            int runsPerRung,
            int firstSeed = 1,
            double stopAtWinRate = 0.99,
            int minRung = PowerLadder.MinRung)
        {
            var points = new List<LadderPoint>();
            var inARow = 0;
            for (var rung = minRung; rung <= maxRung; rung++)
            {
                var deck = PowerLadder.Scale(encounter.Setup.Deck, rung);
                var records = PlayBatch(encounter, deck, player, firstSeed, runsPerRung);
                var summary = BatchSummary.From(records);
                points.Add(new LadderPoint(rung, PowerLadder.Multiplier(rung), summary));

                // stop once the encounter is won nearly every time at two rungs in a row (a curve is not always smooth)
                inARow = summary.WinRate >= stopAtWinRate ? inARow + 1 : 0;
                if (inARow >= 2)
                {
                    break;
                }
            }

            return new Ladder(encounter, player.Name, points);
        }
    }

    /// <summary>How forgiving an encounter should be, by where it sits in the game. These are starting guesses to tune.</summary>
    public sealed class TargetTier
    {
        public string Name { get; }

        /// <summary>The win rate a careful player should have at the intended power.</summary>
        public double CarefulWinRate { get; }

        /// <summary>The win rate a casual player should have at the intended power.</summary>
        public double CasualWinRate { get; }

        /// <summary>The share of health a careful player should typically have left after a win.</summary>
        public double HealthLeft { get; }

        public TargetTier(string name, double carefulWinRate, double casualWinRate, double healthLeft)
        {
            Name = name;
            CarefulWinRate = carefulWinRate;
            CasualWinRate = casualWinRate;
            HealthLeft = healthLeft;
        }
    }

    public static class TargetFeel
    {
        public static readonly IReadOnlyList<TargetTier> Tiers = new[]
        {
            new TargetTier("Tutorial", 0.97, 0.85, 0.80),
            new TargetTier("Early", 0.92, 0.75, 0.65),
            new TargetTier("Mid", 0.70, 0.45, 0.50),
            new TargetTier("Late", 0.55, 0.30, 0.35)
        };

        public static TargetTier Find(string name)
        {
            return Tiers.FirstOrDefault(tier => string.Equals(tier.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Tiers[0];
        }
    }
}
