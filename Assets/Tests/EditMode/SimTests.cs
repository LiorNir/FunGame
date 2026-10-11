using System.Collections.Generic;
using System.Linq;
using FunGame.Battle;
using FunGame.Board;
using FunGame.Sim;
using NUnit.Framework;

namespace FunGame.Board.Tests
{
    public class SimTests
    {
        private static EncounterCase Sample(string name)
        {
            return BuiltInEncounters.All().First(encounter => encounter.Name.Contains(name));
        }

        private static string Fingerprint(BattleEngine engine)
        {
            return $"T{engine.Turn} AP{engine.ActionPoints} HP{engine.PlayerHealth} {engine.Outcome} " +
                   string.Join(",", engine.PilotBySlot.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value.Name}:{pair.Value.DefenseWound}:{pair.Value.Fallen}")) + " " +
                   string.Join(",", engine.Hand.Select(pilot => pilot.Name)) + " " +
                   string.Join(",", engine.Lanes.Select(lane => $"{lane.EnemiesSpawned}:{(lane.Current == null ? "-" : lane.Current.Health + "/" + lane.Current.Intent.Name)}"));
        }

        // ---------- clone ----------

        [Test]
        public void ACloneWithTheSameDice_PlaysOutExactlyLikeTheOriginal()
        {
            var setup = Sample("Waves").WithDeck(BuiltInEncounters.StarterRoster, 3);
            var engine = new BattleEngine(setup);
            engine.Start();
            engine.TryPlace(0, "R");
            var clone = engine.Clone();

            engine.ResolveTurn();
            clone.ResolveTurn();

            Assert.AreEqual(Fingerprint(engine), Fingerprint(clone));
        }

        [Test]
        public void ChangingTheClone_DoesNotChangeTheOriginal()
        {
            var setup = Sample("Waves").WithDeck(BuiltInEncounters.StarterRoster, 3);
            var engine = new BattleEngine(setup);
            engine.Start();
            var before = Fingerprint(engine);
            var clone = engine.Clone();

            clone.TryPlace(0, "R");
            clone.ResolveTurn();

            Assert.AreEqual(before, Fingerprint(engine));
        }

        [Test]
        public void ACloneWithFreshDice_DrawsADifferentFuture_ButTheSameNow()
        {
            var setup = Sample("Waves").WithDeck(BuiltInEncounters.StarterRoster, 3);
            var engine = new BattleEngine(setup);
            engine.Start();
            engine.TryPlace(0, "R");
            var fresh = engine.Clone(12345);
            var exact = engine.Clone();

            fresh.ResolveTurn();
            exact.ResolveTurn();
            engine.ResolveTurn();

            // what is already decided (the board, the enemy's current action) matches; the next hand may differ
            Assert.AreEqual(Fingerprint(engine), Fingerprint(exact));
            Assert.AreEqual(engine.PlayerHealth, fresh.PlayerHealth, 0.001f);
        }

        [Test]
        public void ResolveCombat_StopsBeforeTheNextTurnBegins()
        {
            var setup = Sample("Waves").WithDeck(BuiltInEncounters.StarterRoster, 3);
            var engine = new BattleEngine(setup);
            engine.Start();
            engine.TryPlace(0, "R");
            var handBefore = engine.Hand.Count;

            engine.ResolveCombat();

            Assert.AreEqual(1, engine.Turn);
            Assert.AreEqual(handBefore, engine.Hand.Count); // nothing was drawn yet
        }

        [Test]
        public void TheEngine_CountsWhatHappened()
        {
            var enemy = new EnemySpec("E", 100, new[] { new EnemyActionData("Hit", 5, 0) }, false);
            var board = new BoardGraph(new[] { new SlotNode("R") }, new[]
            {
                new ChainDefinition("atk", "lane1", ChainRole.Attack, "R"),
                new ChainDefinition("def", "lane1", ChainRole.Defense, "R")
            });
            var setup = new BattleSetup(board, new List<SlotRule>(), new[] { new LaneSpec("lane1", new[] { enemy }) }, new BattleRules(),
                new[] { new PilotStats("P", 3f, 2f) }, 1);
            var engine = new BattleEngine(setup);
            engine.Start();
            engine.TryPlace(0, "R");

            engine.ResolveTurn();

            Assert.AreEqual(1, engine.Stats.Places);
            Assert.AreEqual(1, engine.Stats.ApSpent);
            Assert.AreEqual(2, engine.Stats.ApWasted);
            Assert.AreEqual(3f, engine.Stats.DamageDealt, 0.001f);
            Assert.AreEqual(3f, engine.Stats.DamageTaken, 0.001f); // 5 - 2 absorbed
            Assert.AreEqual(2f, engine.Stats.DamageAbsorbed, 0.001f);
            Assert.AreEqual(1, engine.Stats.PilotsFallen);
        }

        // ---------- players ----------

        [Test]
        public void ASearchPlayer_GivesTheSameResultEveryTime()
        {
            var encounter = Sample("Split pair");
            var deck = PowerLadder.Scale(encounter.Setup.Deck, 2);

            var first = BotRunner.Run(encounter.WithDeck(deck, 5), new SearchPlayer(BotProfile.Careful));
            var second = BotRunner.Run(encounter.WithDeck(deck, 5), new SearchPlayer(BotProfile.Careful));

            Assert.AreEqual(first.Outcome, second.Outcome);
            Assert.AreEqual(first.Turns, second.Turns);
            Assert.AreEqual(first.FinalHealth, second.FinalHealth, 0.0001f);
        }

        [Test]
        public void ASearchPlayer_DoesNotUseUpTheRealDice()
        {
            // the ogre's actions are random and the next hand is drawn from the dice: if the search consumed any of the
            // real dice, the battle that searched would see a different next turn than a control that did not
            var encounter = Sample("Bonus road");
            var deck = PowerLadder.Scale(encounter.Setup.Deck, 0);
            var engine = new BattleEngine(encounter.WithDeck(deck, 1));
            engine.Start();
            var control = engine.Clone(); // an exact copy, dice included
            var player = new SearchPlayer(BotProfile.Careful);

            player.PlayPlanningPhase(engine);
            foreach (var command in player.LastPlan.Commands)
            {
                BattleCommands.Apply(control, command);
            }

            engine.ResolveTurn();
            control.ResolveTurn();

            Assert.IsTrue(player.LastPlan.Commands.Count > 0);
            Assert.AreEqual(Fingerprint(engine), Fingerprint(control));
        }

        [Test]
        public void ASearchPlayer_ReadsTheEnemyIntent()
        {
            // the enemy is about to hit for 4: a careful player puts the 5 defense on the board (absorbing 4 leaves the pilot alive)
            var enemy = new EnemySpec("E", 50, new[] { new EnemyActionData("Hit", 4, 0) }, false);
            var board = new BoardGraph(new[] { new SlotNode("R"), new SlotNode("S", "R", Operation.Add) }, new[]
            {
                new ChainDefinition("atk", "lane1", ChainRole.Attack, "S"),
                new ChainDefinition("def", "lane1", ChainRole.Defense, "S")
            });
            var deck = new[] { new PilotStats("Wall", 0f, 5f), new PilotStats("Spear", 5f, 0f), new PilotStats("A", 0f, 0f), new PilotStats("B", 0f, 0f) };
            var setup = new BattleSetup(board, new List<SlotRule>(), new[] { new LaneSpec("lane1", new[] { enemy }) }, new BattleRules(), deck, 1);
            var engine = new BattleEngine(setup);
            engine.Start();

            var player = new SearchPlayer(BotProfile.Careful);
            player.PlayPlanningPhase(engine);
            engine.ResolveTurn();

            Assert.AreEqual(20f, engine.PlayerHealth, 0.001f); // nothing reached the player
        }

        [Test]
        public void TheSearchPlayer_IsNeverWorseThanItsOwnDoingNothing()
        {
            var encounter = Sample("Waves");
            var deck = PowerLadder.Scale(encounter.Setup.Deck, 0);
            var engine = new BattleEngine(encounter.WithDeck(deck, 2));
            engine.Start();
            var player = new SearchPlayer(BotProfile.Careful);

            player.PlayPlanningPhase(engine);

            Assert.IsTrue(player.LastPlan.Prediction.Score >= player.LastPlan.DoingNothing.Score - 0.001f);
        }

        // ---------- switched-off mechanics ----------

        private static BattleEngine StartedWorldOne(int step, int seed = 1)
        {
            var encounter = BuiltInEncounters.WorldOne()[step];
            var engine = new BattleEngine(encounter.WithDeck(encounter.Setup.Deck, seed));
            engine.Start();
            return engine;
        }

        [Test]
        public void WhenReplacingIsOff_AFilledSlotCannotBeReplaced()
        {
            var engine = StartedWorldOne(0);
            Assert.IsTrue(engine.TryPlace(0, "R"));

            Assert.IsFalse(engine.CanPlace(0, "R"));
            Assert.IsFalse(engine.TryPlace(0, "R"));
            Assert.IsTrue(engine.CanPlace(0, "S2")); // an empty slot is still fine
        }

        [Test]
        public void WhenEjectingIsOff_APilotCannotBeTakenBack()
        {
            var engine = StartedWorldOne(0);
            engine.TryPlace(0, "R");

            Assert.IsFalse(engine.CanEject("R"));
        }

        [Test]
        public void TheCommandList_LeavesOutWhatIsSwitchedOff()
        {
            var engine = StartedWorldOne(0);
            engine.TryPlace(0, "R");
            var commands = new List<BattleCommand>();

            BattleCommands.Legal(engine, commands);

            Assert.IsFalse(commands.Any(command => command.Type == BattleCommandType.Eject));
            Assert.IsFalse(commands.Any(command => command.Type == BattleCommandType.Place && command.SlotId == "R"));
        }

        [Test]
        public void TheOldPlayer_DoesNotTryToReplaceEither()
        {
            var encounter = BuiltInEncounters.WorldOne()[1];
            var record = BotRunner.Run(encounter.WithDeck(encounter.Setup.Deck, 3), new GreedyPlayer());

            Assert.AreEqual(0, record.Stats.Replaces);
            Assert.AreEqual(0, record.Stats.Ejects);
        }

        [Test]
        public void WithoutUnlocks_TheLockedSlotNeverOpens()
        {
            var encounter = BuiltInEncounters.WorldOne()[1].WithoutUnlocks(); // S3 normally opens on turn 2
            var engine = new BattleEngine(encounter.WithDeck(encounter.Setup.Deck, 1));
            engine.Start();

            for (var turn = 0; turn < 4 && engine.Outcome == BattleOutcome.InProgress; turn++)
            {
                engine.ResolveTurn();
            }

            Assert.IsFalse(engine.IsUnlocked("S3"));
        }

        [Test]
        public void TheSwitches_SurviveTheTextForm()
        {
            var encounter = BuiltInEncounters.WorldOne()[0];

            var read = SetupText.Read(SetupText.Write(encounter.Name, encounter.Tier, encounter.Setup));

            Assert.IsFalse(read.Setup.Rules.AllowReplace);
            Assert.IsFalse(read.Setup.Rules.AllowEject);
        }

        [Test]
        public void WorldOne_IsThreeOrderedSteps_WithNoReplacingOrEjecting()
        {
            var world = BuiltInEncounters.WorldOne();

            Assert.AreEqual(3, world.Count);
            for (var index = 0; index < world.Count; index++)
            {
                Assert.AreEqual(index + 1, world[index].Position);
                Assert.AreEqual("World 1", world[index].World);
                Assert.IsFalse(world[index].Setup.Rules.AllowReplace);
                Assert.IsFalse(world[index].Setup.Rules.AllowEject);
            }
        }

        [Test]
        public void WorldTwo_HasReplacingAndEjectingInEveryStep()
        {
            var world = BuiltInEncounters.WorldTwo();

            Assert.AreEqual(3, world.Count);
            for (var index = 0; index < world.Count; index++)
            {
                Assert.AreEqual(index + 1, world[index].Position);
                Assert.AreEqual("World 2", world[index].World);
                Assert.IsTrue(world[index].Setup.Rules.AllowReplace);
                Assert.IsTrue(world[index].Setup.Rules.AllowEject); // 2-1 teaches replacing, then ejecting at its wave change
                Assert.AreEqual(BuiltInEncounters.WorldTwoRoster.Count, world[index].Setup.Deck.Count);
            }

        }

        // ---------- ladder and summaries ----------

        [Test]
        public void ThePowerLadder_ScalesEveryStat()
        {
            var roster = new[] { new PilotStats("P", 2f, 1f) };

            var scaled = PowerLadder.Scale(roster, 5);

            Assert.AreEqual(3f, scaled[0].Attack, 0.0001f); // x1.5
            Assert.AreEqual(1.5f, scaled[0].Defense, 0.0001f);
            Assert.AreEqual(1f, PowerLadder.Multiplier(0), 0.0001f);
        }

        [Test]
        public void TheWilsonInterval_ContainsTheRate_AndNarrowsWithMoreRuns()
        {
            var small = BatchSummary.Wilson(7, 10);
            var large = BatchSummary.Wilson(700, 1000);

            Assert.IsTrue(small.Low < 0.7 && small.High > 0.7);
            Assert.IsTrue(large.High - large.Low < small.High - small.Low);
        }

        [Test]
        public void ABatch_IsReproducible()
        {
            var encounter = Sample("Tutorial");
            var deck = PowerLadder.Scale(encounter.Setup.Deck, 0);

            var first = BatchSummary.From(Lab.PlayBatch(encounter, deck, PlayerSpec.Casual, 1, 20));
            var second = BatchSummary.From(Lab.PlayBatch(encounter, deck, PlayerSpec.Casual, 1, 20));

            Assert.AreEqual(first.Wins, second.Wins);
            Assert.AreEqual((float)first.AverageTurns, (float)second.AverageTurns, 0.0001f);
        }

        // ---------- text form ----------

        [Test]
        public void APilotsAbility_SurvivesBeingWrittenAndRead()
        {
            var encounter = BuiltInEncounters.WorldOne()[0];
            var deck = new[] { new PilotStats("Bolt", 2.5f, 1.5f, null, new SurgeAbility(2, 2f)), new PilotStats("Plain", 1f, 1f) };
            var text = SetupText.Write(encounter.Name, encounter.Tier, encounter.WithDeck(deck, 1), null);

            var read = SetupText.Read(text);

            var bolt = read.Setup.Deck.First(pilot => pilot.Name == "Bolt");
            Assert.IsTrue(bolt.Ability is SurgeAbility);
            Assert.AreEqual(2, bolt.Ability.ApCost);
            Assert.IsTrue(read.Setup.Deck.First(pilot => pilot.Name == "Plain").Ability == null);
        }

        [Test]
        public void AnEncounter_SurvivesBeingWrittenAndRead()
        {
            foreach (var encounter in BuiltInEncounters.All())
            {
                var text = SetupText.Write(encounter.Name, encounter.Tier, encounter.Setup, encounter.Note);
                var read = SetupText.Read(text);

                Assert.AreEqual(encounter.Name, read.Name);
                Assert.AreEqual(encounter.Tier, read.Tier);
                Assert.AreEqual(encounter.Setup.Board.Nodes.Count, read.Setup.Board.Nodes.Count);
                Assert.AreEqual(encounter.Setup.Board.Chains.Count, read.Setup.Board.Chains.Count);
                Assert.AreEqual(encounter.Setup.Deck.Count, read.Setup.Deck.Count);

                // and it plays the same
                var original = BotRunner.Run(encounter.WithDeck(encounter.Setup.Deck, 4), new GreedyPlayer());
                var copy = BotRunner.Run(read.WithDeck(read.Setup.Deck, 4), new GreedyPlayer());
                Assert.AreEqual(original.Outcome, copy.Outcome);
                Assert.AreEqual(original.Turns, copy.Turns);
                Assert.AreEqual(original.FinalHealth, copy.FinalHealth, 0.0001f);
            }
        }

        [Test]
        public void ABadLine_IsReportedWithItsNumber()
        {
            Assert.Throws<System.FormatException>(() => SetupText.Read("name|X\nrules|20|3\n"));
        }
    }
}
