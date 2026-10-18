using System.Collections.Generic;
using System.Linq;
using FunGame.Battle;
using FunGame.Board;
using NUnit.Framework;

namespace FunGame.Board.Tests
{
    public class BattleTests
    {
        private const float Tolerance = 0.001f;

        // ---------- helpers ----------

        private static BoardGraph StraightDefenseChain(Operation ab, Operation bc, NodeBonus bonusOnC = default)
        {
            var nodes = new[]
            {
                new SlotNode("a"),
                new SlotNode("b", "a", ab),
                new SlotNode("c", "b", bc, bonusOnC)
            };
            return new BoardGraph(nodes, new[] { new ChainDefinition("def", "lane", ChainRole.Defense, "c") });
        }

        private static Dictionary<string, PilotUnit> Pilots(float a, float b, float c)
        {
            return new Dictionary<string, PilotUnit>
            {
                ["a"] = new PilotUnit("A", 0f, a),
                ["b"] = new PilotUnit("B", 0f, b),
                ["c"] = new PilotUnit("C", 0f, c)
            };
        }

        private static System.Func<string, SlotContent> ContentOf(Dictionary<string, PilotUnit> pilots)
        {
            return id => pilots.TryGetValue(id, out var pilot) && !pilot.Fallen
                ? new SlotContent(pilot.EffectiveAttack, pilot.EffectiveDefense)
                : SlotContent.Empty;
        }

        // A one-slot board whose single slot ends both an attack and a defense chain of one lane.
        private static BoardGraph OneSlotBoard(params SlotNode[] extraNodes)
        {
            var nodes = new List<SlotNode> { new SlotNode("R") };
            nodes.AddRange(extraNodes);
            var chains = new[]
            {
                new ChainDefinition("atk", "lane1", ChainRole.Attack, "R"),
                new ChainDefinition("def", "lane1", ChainRole.Defense, "R")
            };
            return new BoardGraph(nodes, chains);
        }

        private static EnemySpec Enemy(string name, int health, params EnemyActionData[] actions)
        {
            return new EnemySpec(name, health, actions, false);
        }

        private static BattleSetup Setup(
            BoardGraph board,
            IReadOnlyList<EnemySpec> enemies,
            IReadOnlyList<PilotStats> deck,
            IReadOnlyList<SlotRule> slotRules = null,
            BattleRules rules = null,
            int seed = 1)
        {
            return new BattleSetup(
                board,
                slotRules ?? new List<SlotRule>(),
                new[] { new LaneSpec("lane1", enemies) },
                rules ?? new BattleRules(),
                deck,
                seed);
        }

        private static PilotStats[] Strong(int count)
        {
            return Enumerable.Range(0, count).Select(i => new PilotStats("P" + i, 5f, 5f)).ToArray();
        }

        // ---------- wounds ----------

        [Test]
        public void Wounds_OnAnAddChain_AreProportional()
        {
            var graph = StraightDefenseChain(Operation.Add, Operation.Add);
            var pilots = Pilots(3f, 5f, 2f);

            var applied = WoundAllocator.TryApplyDamage(graph, graph.Chains[0], 10f, 5f, ContentOf(pilots), pilots);

            Assert.IsTrue(applied);
            foreach (var pilot in pilots.Values)
            {
                Assert.AreEqual(0.5f, pilot.DefenseWound, Tolerance);
            }

            Assert.AreEqual(5f, ChainEvaluator.Evaluate(graph, graph.Chains[0], ContentOf(pilots)).Value, Tolerance);
            Assert.AreEqual(1.5f, pilots["a"].EffectiveDefense, Tolerance);
            Assert.AreEqual(2.5f, pilots["b"].EffectiveDefense, Tolerance);
            Assert.AreEqual(1f, pilots["c"].EffectiveDefense, Tolerance);
        }

        [Test]
        public void Wounds_OnAMultiplyChain_RemoveExactlyTheDamage()
        {
            var graph = StraightDefenseChain(Operation.Multiply, Operation.Multiply);
            var pilots = Pilots(3f, 5f, 2f);

            var applied = WoundAllocator.TryApplyDamage(graph, graph.Chains[0], 30f, 15f, ContentOf(pilots), pilots);

            Assert.IsTrue(applied);
            Assert.AreEqual(15f, ChainEvaluator.Evaluate(graph, graph.Chains[0], ContentOf(pilots)).Value, 0.01f);
            Assert.AreEqual(0.2063f, pilots["a"].DefenseWound, 0.001f);
        }

        [Test]
        public void Wounds_SkipTheOperandOfASubtract()
        {
            var nodes = new[] { new SlotNode("a"), new SlotNode("b", "a", Operation.Subtract) };
            var graph = new BoardGraph(nodes, new[] { new ChainDefinition("def", "lane", ChainRole.Defense, "b") });
            var pilots = new Dictionary<string, PilotUnit>
            {
                ["a"] = new PilotUnit("A", 0f, 5f),
                ["b"] = new PilotUnit("B", 0f, 1f)
            };

            var applied = WoundAllocator.TryApplyDamage(graph, graph.Chains[0], 4f, 2f, ContentOf(pilots), pilots);

            Assert.IsTrue(applied);
            Assert.AreEqual(0f, pilots["b"].DefenseWound, Tolerance);
            Assert.AreEqual(0.4f, pilots["a"].DefenseWound, Tolerance); // 5 x 0.6 - 1 = 2
            Assert.AreEqual(2f, ChainEvaluator.Evaluate(graph, graph.Chains[0], ContentOf(pilots)).Value, 0.01f);
        }

        [Test]
        public void Wounds_CannotLowerAChainBelowItsBonus_SoTheChainIsDestroyed()
        {
            var graph = StraightDefenseChain(Operation.Add, Operation.Add, new NodeBonus(BonusKind.Add, 10f));
            var pilots = Pilots(1f, 1f, 1f);
            var value = ChainEvaluator.Evaluate(graph, graph.Chains[0], ContentOf(pilots)).Value; // 13

            var applied = WoundAllocator.TryApplyDamage(graph, graph.Chains[0], value, 5f, ContentOf(pilots), pilots);

            Assert.IsFalse(applied);
        }

        // ---------- battle flow ----------

        [Test]
        public void Start_DealsTheHand_AndSetsHealthAndAp()
        {
            var setup = Setup(OneSlotBoard(), new[] { Enemy("E", 10, new EnemyActionData("Hit", 1, 0)) }, Strong(6));
            var engine = new BattleEngine(setup);
            engine.Start();

            Assert.AreEqual(4, engine.Hand.Count);
            Assert.AreEqual(2, engine.Bank.Count);
            Assert.AreEqual(20f, engine.PlayerHealth, Tolerance);
            Assert.AreEqual(3, engine.ActionPoints);
            Assert.AreEqual(1, engine.Turn);
        }

        [Test]
        public void Placing_CostsAp_AndIsRefusedWithoutEnough()
        {
            var rules = new BattleRules { StartingActionPoints = 1, ActionPointsPerTurn = 1 };
            var nodes = new[] { new SlotNode("R"), new SlotNode("S", "R", Operation.Add) };
            var board = new BoardGraph(nodes, new[]
            {
                new ChainDefinition("atk", "lane1", ChainRole.Attack, "S"),
                new ChainDefinition("def", "lane1", ChainRole.Defense, "S")
            });
            var engine = new BattleEngine(Setup(board, new[] { Enemy("E", 10, new EnemyActionData("Hit", 1, 0)) }, Strong(4), rules: rules));
            engine.Start();

            Assert.IsTrue(engine.TryPlace(0, "R"));
            Assert.AreEqual(0, engine.ActionPoints);
            Assert.IsFalse(engine.TryPlace(0, "S"));
        }

        [Test]
        public void Ap_IsRefreshedToTheFixedValue_NotAccumulated()
        {
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { Enemy("E", 100, new EnemyActionData("Hit", 1, 0)) }, Strong(6)));
            engine.Start();
            engine.ResolveTurn(); // nothing was placed or spent, so 3 AP are left over

            Assert.AreEqual(2, engine.Turn);
            Assert.AreEqual(3, engine.ActionPoints);
        }

        [Test]
        public void Hand_IsRefilledAtTheStartOfEachTurn()
        {
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { Enemy("E", 100, new EnemyActionData("Hit", 1, 0)) }, Strong(6)));
            engine.Start();
            engine.TryPlace(0, "R");
            Assert.AreEqual(3, engine.Hand.Count);

            engine.ResolveTurn();

            Assert.AreEqual(4, engine.Hand.Count);
        }

        [Test]
        public void Slot_UnlocksByTurn_AtTheStartOfThatTurn()
        {
            var nodes = new[] { new SlotNode("R"), new SlotNode("S", "R", Operation.Add) };
            var board = new BoardGraph(nodes, new[]
            {
                new ChainDefinition("atk", "lane1", ChainRole.Attack, "S"),
                new ChainDefinition("def", "lane1", ChainRole.Defense, "S")
            });
            var slotRules = new[] { new SlotRule("S", true, 0, 0, 2) };
            var engine = new BattleEngine(Setup(board, new[] { Enemy("E", 100, new EnemyActionData("Hit", 1, 0)) }, Strong(6), slotRules));
            engine.Start();
            Assert.IsFalse(engine.IsUnlocked("S"));

            engine.ResolveTurn();

            Assert.IsTrue(engine.IsUnlocked("S"));
        }

        [Test]
        public void Slot_UnlocksForAp()
        {
            var slotRules = new[] { new SlotRule("S", true, 2, 0, 0) };
            var nodes = new[] { new SlotNode("R"), new SlotNode("S", "R", Operation.Add) };
            var board = new BoardGraph(nodes, new[]
            {
                new ChainDefinition("atk", "lane1", ChainRole.Attack, "S"),
                new ChainDefinition("def", "lane1", ChainRole.Defense, "S")
            });
            var engine = new BattleEngine(Setup(board, new[] { Enemy("E", 100, new EnemyActionData("Hit", 1, 0)) }, Strong(6), slotRules));
            engine.Start();

            Assert.IsTrue(engine.TryUnlock("S"));
            Assert.AreEqual(1, engine.ActionPoints);
            Assert.IsTrue(engine.IsUnlocked("S"));
        }

        [Test]
        public void NextEnemy_AppearsAtTheStartOfTheNextTurn_AndVictoryNeedsTheQueueCleared()
        {
            var weak = Enemy("Weak", 1, new EnemyActionData("Hit", 0, 0));
            var second = Enemy("Second", 1, new EnemyActionData("Hit", 0, 0));
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { weak, second }, Strong(6)));
            engine.Start();
            engine.TryPlace(0, "R");

            engine.ResolveTurn();

            Assert.AreEqual(BattleOutcome.InProgress, engine.Outcome);
            Assert.AreEqual("Second", engine.Lanes[0].Current.Spec.Name);
            Assert.AreEqual(2, engine.Wave);

            engine.ResolveTurn();

            Assert.AreEqual(BattleOutcome.Victory, engine.Outcome);
        }

        [Test]
        public void WaveUnlock_FollowsTheNamedLane_NotAnyLane()
        {
            var nodes = new[]
            {
                new SlotNode("R1"), new SlotNode("R2"),
                new SlotNode("OnLane1", "R1", Operation.Add), new SlotNode("OnLane2", "R2", Operation.Add)
            };
            var chains = new[]
            {
                new ChainDefinition("a1", "lane1", ChainRole.Attack, "R1"), new ChainDefinition("d1", "lane1", ChainRole.Defense, "R1"),
                new ChainDefinition("a2", "lane2", ChainRole.Attack, "R2"), new ChainDefinition("d2", "lane2", ChainRole.Defense, "R2")
            };
            var slotRules = new[]
            {
                new SlotRule("OnLane1", true, 0, 2, 0, "lane1"),
                new SlotRule("OnLane2", true, 0, 2, 0, "lane2")
            };
            var lanes = new[]
            {
                new LaneSpec("lane1", new[] { Enemy("Quick", 1, new EnemyActionData("Idle", 0, 0)), Enemy("Next", 50, new EnemyActionData("Idle", 0, 0)) }),
                new LaneSpec("lane2", new[] { Enemy("Tank", 500, new EnemyActionData("Idle", 0, 0)), Enemy("Never", 50, new EnemyActionData("Idle", 0, 0)) })
            };
            var setup = new BattleSetup(new BoardGraph(nodes, chains), slotRules, lanes, new BattleRules(), Strong(6), 1);
            var engine = new BattleEngine(setup);
            engine.Start();
            engine.TryPlace(0, "R1");

            engine.ResolveTurn(); // lane1's first enemy dies; its second appears at the start of turn 2

            Assert.IsTrue(engine.IsUnlocked("OnLane1"));
            Assert.IsFalse(engine.IsUnlocked("OnLane2"));
        }

        [Test]
        public void Eject_ReturnsALivingPilotToTheHand_ForAp()
        {
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { Enemy("E", 100, new EnemyActionData("Hit", 0, 0)) }, Strong(6)));
            engine.Start();
            engine.TryPlace(0, "R");
            Assert.AreEqual(3, engine.Hand.Count);

            Assert.IsTrue(engine.TryEject("R"));

            Assert.AreEqual(4, engine.Hand.Count);
            Assert.AreEqual(1, engine.ActionPoints);
            Assert.IsFalse(engine.PilotBySlot.ContainsKey("R"));
        }

        [Test]
        public void Eject_IsRefusedWhenTheHandIsFull()
        {
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { Enemy("E", 100, new EnemyActionData("Hit", 0, 0)) }, Strong(6)));
            engine.Start();
            engine.TryPlace(0, "R");
            // refill the hand to the limit by hand-feeding the bank, as a later turn would
            engine.Hand.Add(engine.Bank[0]);
            engine.Bank.RemoveAt(0);
            Assert.AreEqual(4, engine.Hand.Count);

            Assert.IsFalse(engine.TryEject("R"));
        }

        [Test]
        public void EnemyDefense_AbsorbsThePlayersAttackFirst()
        {
            var enemy = Enemy("E", 10, new EnemyActionData("Guard", 0, 3));
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { enemy }, new[] { new PilotStats("P", 5f, 0f) }));
            engine.Start();
            engine.TryPlace(0, "R");

            engine.ResolveTurn();

            Assert.AreEqual(8f, engine.Lanes[0].Current.Health, Tolerance); // attack 5 - defense 3 = 2 damage
        }

        [Test]
        public void ChainDestroyed_MakesItsPilotsFall_AndRemainderHurtsThePlayer()
        {
            var enemy = Enemy("E", 100, new EnemyActionData("Smash", 10, 0));
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { enemy }, new[] { new PilotStats("P", 0f, 4f) }));
            engine.Start();
            engine.TryPlace(0, "R");

            engine.ResolveTurn();

            Assert.IsTrue(engine.PilotBySlot["R"].Fallen);
            Assert.AreEqual(14f, engine.PlayerHealth, Tolerance); // 10 attack - 4 absorbed = 6
        }

        // ---------- chains worth zero ----------

        private static void PlaceByName(BattleEngine engine, string pilotName, string slotId)
        {
            var index = engine.Hand.FindIndex(pilot => pilot.Name == pilotName);
            Assert.IsTrue(index >= 0);
            Assert.IsTrue(engine.TryPlace(index, slotId));
        }

        // Two separate root slots, each ending a defense chain of the lane (the first is hit first).
        private static BoardGraph TwoDefenseChains()
        {
            var nodes = new[] { new SlotNode("R1"), new SlotNode("R2") };
            return new BoardGraph(nodes, new[]
            {
                new ChainDefinition("atk", "lane1", ChainRole.Attack, "R1"),
                new ChainDefinition("def1", "lane1", ChainRole.Defense, "R1"),
                new ChainDefinition("def2", "lane1", ChainRole.Defense, "R2")
            });
        }

        [Test]
        public void ZeroValueDefenseChain_IsDestroyedByAHit_AndTheAttackPassesOnInFull()
        {
            var enemy = Enemy("E", 100, new EnemyActionData("Hit", 5, 0));
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { enemy }, new[] { new PilotStats("P", 3f, 0f) }));
            engine.Start();
            engine.TryPlace(0, "R");

            engine.ResolveTurn();

            Assert.IsTrue(engine.PilotBySlot["R"].Fallen);
            Assert.AreEqual(15f, engine.PlayerHealth, Tolerance);
        }

        [Test]
        public void ZeroValueChain_InFrontOfAnother_FallsAndTheNextChainAbsorbs()
        {
            var enemy = Enemy("E", 100, new EnemyActionData("Hit", 3, 0));
            var deck = new[] { new PilotStats("Zero", 0f, 0f), new PilotStats("Wall", 0f, 5f) };
            var engine = new BattleEngine(Setup(TwoDefenseChains(), new[] { enemy }, deck));
            engine.Start();
            PlaceByName(engine, "Zero", "R1");
            PlaceByName(engine, "Wall", "R2");

            engine.ResolveTurn();

            Assert.IsTrue(engine.PilotBySlot["R1"].Fallen);
            Assert.IsFalse(engine.PilotBySlot["R2"].Fallen);
            Assert.AreEqual(0.6f, engine.PilotBySlot["R2"].DefenseWound, Tolerance);
            Assert.AreEqual(20f, engine.PlayerHealth, Tolerance);
        }

        [Test]
        public void ZeroValueChain_BehindOneThatAbsorbsEverything_IsNotReached()
        {
            var enemy = Enemy("E", 100, new EnemyActionData("Hit", 3, 0));
            var deck = new[] { new PilotStats("Zero", 0f, 0f), new PilotStats("Wall", 0f, 5f) };
            var engine = new BattleEngine(Setup(TwoDefenseChains(), new[] { enemy }, deck));
            engine.Start();
            PlaceByName(engine, "Wall", "R1");
            PlaceByName(engine, "Zero", "R2");

            engine.ResolveTurn();

            Assert.IsFalse(engine.PilotBySlot["R1"].Fallen);
            Assert.IsFalse(engine.PilotBySlot["R2"].Fallen);
            Assert.AreEqual(20f, engine.PlayerHealth, Tolerance);
        }

        // ---------- a hit equal to a chain's whole value ----------

        // R -> S, both ends of the lane's chains: attack and defense read the same two slots.
        private static BoardGraph TwoSlotChain(Operation operation)
        {
            var nodes = new[] { new SlotNode("R"), new SlotNode("S", "R", operation) };
            return new BoardGraph(nodes, new[]
            {
                new ChainDefinition("atk", "lane1", ChainRole.Attack, "S"),
                new ChainDefinition("def", "lane1", ChainRole.Defense, "S")
            });
        }

        private static BattleEngine ChainHitForItsWholeValue(Operation operation, int hit)
        {
            var enemy = Enemy("E", 100, new EnemyActionData("Hit", hit, 0));
            var deck = new[] { new PilotStats("A", 0f, 2f), new PilotStats("B", 0f, 3f), new PilotStats("Fresh", 0f, 4f) };
            var engine = new BattleEngine(Setup(TwoSlotChain(operation), new[] { enemy }, deck));
            engine.Start();
            PlaceByName(engine, "A", "R");
            PlaceByName(engine, "B", "S");
            engine.ResolveTurn();
            return engine;
        }

        [Test]
        public void HitEqualToTheChainsValue_LeavesItStandingAtZero()
        {
            var engine = ChainHitForItsWholeValue(Operation.Add, 5); // 2 + 3 = 5

            Assert.IsFalse(engine.PilotBySlot["R"].Fallen);
            Assert.IsFalse(engine.PilotBySlot["S"].Fallen);
            Assert.AreEqual(1f, engine.PilotBySlot["R"].DefenseWound);
            Assert.AreEqual(1f, engine.PilotBySlot["S"].DefenseWound);
            Assert.AreEqual(0f, engine.SumChains(ChainRole.Defense));
            Assert.AreEqual(20f, engine.PlayerHealth, Tolerance);
        }

        [Test]
        public void ChainLeftAtZero_IsDestroyedByTheNextHit_WhichPassesOnInFull()
        {
            var engine = ChainHitForItsWholeValue(Operation.Add, 5);

            engine.ResolveTurn();

            Assert.IsTrue(engine.PilotBySlot["R"].Fallen);
            Assert.IsTrue(engine.PilotBySlot["S"].Fallen);
            Assert.AreEqual(15f, engine.PlayerHealth, Tolerance);
        }

        [Test]
        public void ChainLeftAtZero_ComesBackWhenOnePilotIsReplaced_IfItsOperatorAdds()
        {
            var engine = ChainHitForItsWholeValue(Operation.Add, 5);

            PlaceByName(engine, "Fresh", "S");

            Assert.AreEqual(4f, engine.SumChains(ChainRole.Defense), Tolerance); // 0 + 4
        }

        [Test]
        public void ChainLeftAtZero_StaysAtZeroWhenOnePilotIsReplaced_IfItsOperatorMultiplies()
        {
            var engine = ChainHitForItsWholeValue(Operation.Multiply, 6); // 2 x 3 = 6

            PlaceByName(engine, "Fresh", "S");

            Assert.AreEqual(0f, engine.SumChains(ChainRole.Defense), Tolerance); // 0 x 4
        }

        [Test]
        public void HitAboveTheChainsValue_StillDestroysIt()
        {
            var engine = ChainHitForItsWholeValue(Operation.Add, 6);

            Assert.IsTrue(engine.PilotBySlot["R"].Fallen);
            Assert.AreEqual(19f, engine.PlayerHealth, Tolerance);
        }

        // ---------- lock ----------

        private static EnemyActionData Pin(int turns, PilotTargetRule rule = PilotTargetRule.MostWounded)
        {
            return new EnemyActionData("Pin", new IEnemyEffect[] { new LockEffect(turns, rule) });
        }

        // R -> S with a subtract toll: Wall (R) takes the wounds, Toll (S) none, so Wall is the most wounded pilot.
        // The enemy pokes on turn 1, reveals Pin on turn 2 (resolving it at the end of turn 2), then does what follows.
        private static BattleEngine PinnedBoard(int turns, params EnemyActionData[] afterPin)
        {
            var actions = new List<EnemyActionData> { new EnemyActionData("Poke", 1, 0), Pin(turns) };
            actions.AddRange(afterPin.Length > 0 ? afterPin : new[] { new EnemyActionData("Idle", 0, 0) });
            var deck = new[] { new PilotStats("Wall", 0f, 5f), new PilotStats("Toll", 0f, 1f), new PilotStats("Fresh", 0f, 3f) };
            var engine = new BattleEngine(Setup(TwoSlotChain(Operation.Subtract), new[] { new EnemySpec("E", 100, actions, false) }, deck));
            engine.Start();
            PlaceByName(engine, "Wall", "R");
            PlaceByName(engine, "Toll", "S");
            engine.ResolveTurn();
            return engine;
        }

        [Test]
        public void Lock_ChoosesItsTargetWhenRevealed_SoThePlayerSeesIt()
        {
            var engine = PinnedBoard(1);

            var targets = engine.IntentTargets().ToList();
            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual("R", targets[0].SlotId);
            Assert.AreEqual("Wall", targets[0].Pilot.Name);
            Assert.IsFalse(engine.PilotBySlot["R"].IsLocked); // not yet: it resolves on the enemy's turn
        }

        [Test]
        public void LockedPilot_CanBeNeitherEjectedNorReplaced()
        {
            var engine = PinnedBoard(2);
            engine.ResolveTurn();

            Assert.IsTrue(engine.PilotBySlot["R"].IsLocked);
            Assert.IsFalse(engine.CanEject("R"));
            Assert.IsTrue(engine.EjectBlocker("R").Contains("locked"));
            Assert.IsFalse(engine.CanPlace(engine.Hand.FindIndex(pilot => pilot.Name == "Fresh"), "R"));
            Assert.IsTrue(engine.CanEject("S")); // the other pilot is free
        }

        [Test]
        public void Lock_LastsItsNumberOfPlayerTurns()
        {
            var engine = PinnedBoard(2);
            engine.ResolveTurn(); // Pin resolves at the end of turn 2

            Assert.IsFalse(engine.CanEject("R")); // turn 3
            engine.ResolveTurn();
            Assert.IsFalse(engine.CanEject("R")); // turn 4
            engine.ResolveTurn();
            Assert.IsTrue(engine.CanEject("R")); // turn 5
        }

        [Test]
        public void Lock_Fizzles_WhenItsTargetLeftTheBoardBeforeItResolves()
        {
            var engine = PinnedBoard(2);
            Assert.IsTrue(engine.TryEject("R"));

            engine.ResolveTurn();

            Assert.AreEqual(0, engine.Stats.PilotsLocked);
            Assert.IsTrue(engine.PilotBySlot.Values.All(pilot => !pilot.IsLocked));
        }

        [Test]
        public void Lock_TargetsOnlyActivePilots()
        {
            var actions = new[] { new EnemyActionData("Idle", 0, 0), Pin(1, PilotTargetRule.HighestDefense) };
            var deck = new[] { new PilotStats("Behind", 0f, 9f) };
            var engine = new BattleEngine(Setup(TwoSlotChain(Operation.Add), new[] { new EnemySpec("E", 100, actions, false) }, deck));
            engine.Start();
            PlaceByName(engine, "Behind", "S"); // R is empty, so S is cut off
            engine.ResolveTurn();

            Assert.AreEqual(0, engine.IntentTargets().Count());
            engine.ResolveTurn();
            Assert.IsFalse(engine.PilotBySlot["S"].IsLocked);
        }

        [Test]
        public void LockedPilotThatFalls_CanStillBeCleared()
        {
            var engine = PinnedBoard(3, new EnemyActionData("Smash", 10, 0));
            engine.ResolveTurn(); // locked
            engine.ResolveTurn(); // Smash destroys the chain

            Assert.IsTrue(engine.PilotBySlot["R"].Fallen);
            Assert.IsTrue(engine.ClearFallen("R"));
        }

        // ---------- abilities ----------

        private static BattleEngine SurgeBattle(int apCost = 2)
        {
            var enemy = Enemy("Dummy", 100, new EnemyActionData("Idle", 0, 0));
            var deck = new[] { new PilotStats("Bolt", 2.5f, 1.5f, null, new SurgeAbility(apCost)), new PilotStats("Plain", 1f, 1f) };
            // the game's recovery: half per turn in the hand, twice that for a pilot ejected the turn before
            var rules = new BattleRules { HandRecoveryPerTurn = 0.5f, EjectedRecoveryMultiplier = 2f };
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { enemy }, deck, rules: rules));
            engine.Start();
            PlaceByName(engine, "Bolt", "R");
            return engine;
        }

        [Test]
        public void Surge_DoublesAttackThisTurn_ThenAttackStaysZeroOnTheBoard()
        {
            var engine = SurgeBattle();
            Assert.IsTrue(engine.TryActivate("R"));
            Assert.AreEqual(5f, engine.PilotBySlot["R"].EffectiveAttack, Tolerance);
            Assert.AreEqual(0f, engine.PilotBySlot["R"].AttackNextTurn, Tolerance);

            engine.ResolveTurn();
            Assert.AreEqual(95f, engine.Lanes[0].Current.Health, Tolerance);
            Assert.AreEqual(0f, engine.PilotBySlot["R"].EffectiveAttack, Tolerance);

            engine.ResolveTurn();
            engine.ResolveTurn();
            Assert.AreEqual(95f, engine.Lanes[0].Current.Health, Tolerance); // the crash lasts while Bolt stays on the board
            Assert.AreEqual(0f, engine.PilotBySlot["R"].EffectiveAttack, Tolerance);
        }

        [Test]
        public void SurgeCrash_EjectedPilot_RecoversFullyInOneTurn()
        {
            var engine = SurgeBattle();
            Assert.IsTrue(engine.TryActivate("R"));
            engine.ResolveTurn();

            Assert.IsTrue(engine.TryEject("R"));
            var bolt = engine.Hand.Single(pilot => pilot.Name == "Bolt");
            Assert.AreEqual(0f, bolt.EffectiveAttack, Tolerance); // still crashed until the turn starts in the hand
            Assert.AreEqual(2.5f, engine.AttackAfterRest(bolt), Tolerance); // ejected: 50% x 2

            engine.ResolveTurn();
            Assert.AreEqual(2.5f, bolt.EffectiveAttack, Tolerance);
            Assert.IsFalse(bolt.AttackModifiers.Any(modifier => modifier.LastsUntilRested));
        }

        [Test]
        public void SurgeCrash_SwappedPilot_RecoversAtTheHandRate()
        {
            var engine = SurgeBattle();
            Assert.IsTrue(engine.TryActivate("R"));
            engine.ResolveTurn();

            PlaceByName(engine, "Plain", "R"); // pushes Bolt back to the hand: not ejected, so the normal rate
            var bolt = engine.Hand.Single(pilot => pilot.Name == "Bolt");
            Assert.AreEqual(1.25f, engine.AttackAfterRest(bolt), Tolerance);

            engine.ResolveTurn();
            Assert.AreEqual(1.25f, bolt.EffectiveAttack, Tolerance); // half its attack back after one turn
            Assert.AreEqual(2.5f, engine.AttackAfterRest(bolt), Tolerance);

            engine.ResolveTurn();
            Assert.AreEqual(2.5f, bolt.EffectiveAttack, Tolerance);
            Assert.IsFalse(bolt.AttackModifiers.Any(modifier => modifier.LastsUntilRested));
        }

        [Test]
        public void Ability_CostsItsOwnAp_AndCanBeUsedOncePerEncounter()
        {
            var engine = SurgeBattle(apCost: 2);
            Assert.AreEqual(2, engine.ActionPoints); // 3 minus 1 for placing
            Assert.IsTrue(engine.TryActivate("R"));
            Assert.AreEqual(0, engine.ActionPoints);
            Assert.AreEqual(1, engine.Stats.AbilitiesUsed);

            engine.ResolveTurn();
            Assert.IsFalse(engine.CanActivate("R"));
            Assert.AreEqual("already used", engine.ActivateBlocker("R"));
        }

        [Test]
        public void Ability_NeedsEnoughAp_AndAPilotThatHasOne()
        {
            var engine = SurgeBattle(apCost: 3);
            Assert.AreEqual("not enough AP", engine.ActivateBlocker("R")); // 2 AP left after placing

            engine.ResolveTurn();
            Assert.IsTrue(engine.CanActivate("R"));
            Assert.IsTrue(engine.TryPlace(engine.Hand.FindIndex(pilot => pilot.Name == "Plain"), "R")); // Bolt goes back to the hand
            Assert.AreEqual("no ability", engine.ActivateBlocker("R"));
        }

        [Test]
        public void AbilityState_IsCopiedWithTheBattle()
        {
            var engine = SurgeBattle();
            engine.TryActivate("R");

            var copy = engine.Clone();

            Assert.IsTrue(copy.PilotBySlot["R"].AbilityUsed);
            Assert.AreEqual(5f, copy.PilotBySlot["R"].EffectiveAttack, Tolerance);
            copy.ResolveTurn();
            Assert.AreEqual(0f, copy.PilotBySlot["R"].EffectiveAttack, Tolerance);
            Assert.AreEqual(5f, engine.PilotBySlot["R"].EffectiveAttack, Tolerance); // the original is untouched
        }

        // ---------- snipe ----------

        // turn 1 the enemy idles; on turn 2 it reveals a snipe at the highest attacker, which lands for the next turn
        private static BattleEngine SnipedBoard(float share, int turns)
        {
            var actions = new[]
            {
                new EnemyActionData("Idle", 0, 0),
                new EnemyActionData("Snipe", new IEnemyEffect[] { new SnipeEffect(share, turns, PilotTargetRule.HighestAttack) }),
                new EnemyActionData("Idle", 0, 0), new EnemyActionData("Idle", 0, 0)
            };
            var deck = new[] { new PilotStats("Striker", 4f, 2f), new PilotStats("Wall", 1f, 4f) };
            var engine = new BattleEngine(Setup(TwoSlotChain(Operation.Add), new[] { new EnemySpec("E", 100, actions, false) }, deck));
            engine.Start();
            PlaceByName(engine, "Wall", "R");
            PlaceByName(engine, "Striker", "S");
            engine.ResolveTurn();
            return engine;
        }

        [Test]
        public void Snipe_LowersBothStatsOfItsTarget_FromTheNextTurn_ForItsTurns()
        {
            var engine = SnipedBoard(0.5f, 1);
            Assert.AreEqual("S", engine.IntentTargets().Single().SlotId); // the highest attacker, shown before it lands

            engine.ResolveTurn(); // the snipe lands at the end of turn 2
            var striker = engine.PilotBySlot["S"];
            Assert.AreEqual(2f, striker.EffectiveAttack, Tolerance);
            Assert.AreEqual(1f, striker.EffectiveDefense, Tolerance);

            engine.ResolveTurn(); // one turn only
            Assert.AreEqual(4f, striker.EffectiveAttack, Tolerance);
            Assert.AreEqual(2f, striker.EffectiveDefense, Tolerance);
        }

        [Test]
        public void Snipe_Misses_WhenItsTargetLeftTheBoard()
        {
            var engine = SnipedBoard(0.5f, 1);
            Assert.IsTrue(engine.TryEject("S"));

            engine.ResolveTurn();

            Assert.AreEqual(0, engine.Stats.PilotsSniped);
        }

        [Test]
        public void Snipe_OnASpentPilot_Assassinates_ItFallsAndCutsItsChains()
        {
            // the root has nothing left in either chain it serves; the snipe picks it (the most wounded, ties by slot order)
            var actions = new[] { new EnemyActionData("Snipe", new IEnemyEffect[] { new SnipeEffect(0.5f, 1, PilotTargetRule.MostWounded) }) };
            var deck = new[] { new PilotStats("Husk", 0f, 0f), new PilotStats("Striker", 4f, 2f) };
            var engine = new BattleEngine(Setup(TwoSlotChain(Operation.Add), new[] { new EnemySpec("E", 100, actions, false) }, deck));
            engine.Start();
            PlaceByName(engine, "Husk", "R");
            PlaceByName(engine, "Striker", "S");
            engine.ResolveTurn();
            Assert.AreEqual("R", engine.IntentTargets().Single().SlotId);
            Assert.IsTrue(engine.IsSpent("R"));
            Assert.IsFalse(engine.IsSpent("S"));
            Assert.AreEqual(SnipeEffect.AssassinateText, engine.DescribeIntent(engine.Lanes[0]).Single());

            engine.ResolveCombat();

            Assert.IsTrue(engine.PilotBySlot["R"].Fallen);
            Assert.AreEqual(1, engine.Stats.PilotsAssassinated);
            Assert.AreEqual(0, engine.Stats.PilotsSniped);
            Assert.AreEqual(0f, engine.SumChains(ChainRole.Attack), Tolerance); // the striker is cut off behind the fallen root
        }

        [Test]
        public void Snipe_OnAPilotWithAStatLeft_IsAnOrdinarySnipe()
        {
            var engine = SnipedBoard(0.5f, 1);
            Assert.IsFalse(engine.IsSpent("S"));
            Assert.AreEqual("SNIPE -50%", engine.DescribeIntent(engine.Lanes[0]).Single());
        }

        // ---------- charge ----------

        // the enemy charges (guard 5) then unleashes 9; a striker of the given attack stands in a one-slot board
        private static BattleEngine ChargeBattle(float strikerAttack)
        {
            var actions = new[]
            {
                new EnemyActionData("Charge", new IEnemyEffect[] { new ChargeEffect(5) }),
                new EnemyActionData("Unleash", new IEnemyEffect[] { new UnleashEffect(9) })
            };
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { new EnemySpec("Ram", 100, actions, false) },
                new[] { new PilotStats("Striker", strikerAttack, 2f), new PilotStats("Spare", 1f, 1f) }, rules: new BattleRules { PlayerMaxHealth = 30 }));
            engine.Start();
            PlaceByName(engine, "Striker", "R");
            return engine;
        }

        [Test]
        public void BreakingAChargesWholeGuard_StunsTheUnleash()
        {
            var engine = ChargeBattle(6f);
            engine.ResolveTurn(); // 6 against a guard of 5: broken

            Assert.AreEqual(1, engine.Stats.ChargesBroken);
            Assert.AreEqual(UnleashEffect.StunnedText, engine.DescribeIntent(engine.Lanes[0]).Single());
            engine.ResolveTurn();
            Assert.AreEqual(30f, engine.PlayerHealth, Tolerance);
        }

        [Test]
        public void AChargeThatHolds_Unleashes()
        {
            var engine = ChargeBattle(5f);
            engine.ResolveTurn(); // 5 against 5: the guard holds

            Assert.AreEqual(0, engine.Stats.ChargesBroken);
            engine.ResolveTurn(); // 9 against a defense of 2: 7 reach the player
            Assert.AreEqual(23f, engine.PlayerHealth, Tolerance);
        }

        [Test]
        public void TheNextThreat_IsTheKnownNextAction_AndABrokenChargeThreatensNothing()
        {
            var engine = ChargeBattle(6f);
            engine.ResolveCombat();
            Assert.AreEqual(0f, engine.Lanes[0].Current.NextThreat().Attack, Tolerance);

            var held = ChargeBattle(5f);
            held.ResolveCombat();
            Assert.AreEqual(9f, held.Lanes[0].Current.NextThreat().Attack, Tolerance);
        }

        [Test]
        public void ALoweredGuard_IsShownWithItsOriginalValue()
        {
            var actions = new[] { new EnemyActionData("Charge", new IEnemyEffect[] { new ChargeEffect(8) }) };
            var deck = new[] { new PilotStats("Brawler", 5f, 4f, null, new NoGuardAbility(1)), new PilotStats("Spare", 1f, 1f) };
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { new EnemySpec("Ram", 100, actions, false) }, deck));
            engine.Start();
            PlaceByName(engine, "Brawler", "R");
            Assert.IsTrue(engine.TryActivate("R"));
            Assert.AreEqual("CHARGE DEF 4 (was 8)", engine.DescribeIntent(engine.Lanes[0]).Single());
        }

        // ---------- No Guard ----------

        [Test]
        public void NoGuard_HalvesTheEnemysGuardAndThePlayersDefense_ThisTurnOnly()
        {
            var enemy = Enemy("Wall", 100, new EnemyActionData("Brace", 2, 6));
            var deck = new[] { new PilotStats("Brawler", 5f, 4f, null, new NoGuardAbility(1)), new PilotStats("Spare", 1f, 1f) };
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { enemy }, deck));
            engine.Start();
            PlaceByName(engine, "Brawler", "R");
            Assert.IsTrue(engine.TryActivate("R"));
            Assert.AreEqual(2f, engine.SumChains(ChainRole.Defense), Tolerance);

            engine.ResolveTurn(); // 5 against a guard of 3: 2 damage; the hit of 2 takes all of the halved defense (2)

            Assert.AreEqual(98f, engine.Lanes[0].Current.Health, Tolerance);
            Assert.AreEqual(0.5f, engine.PilotBySlot["R"].DefenseWound, Tolerance); // 2 of the real 4
            Assert.AreEqual(1f, engine.GuardFactor("lane1"), Tolerance); // back to normal next turn
            Assert.AreEqual(2f, engine.SumChains(ChainRole.Defense), Tolerance);
        }

        [Test]
        public void NoGuard_OnlyLowersTheLanesItsPilotFightsIn()
        {
            // left lane: L -> LA (attack) / LD (defense); right lane: R -> RA / RD; the brawler stands in the left root
            var nodes = new[]
            {
                new SlotNode("L"), new SlotNode("LA", "L", Operation.Add), new SlotNode("LD", "L", Operation.Add),
                new SlotNode("R"), new SlotNode("RA", "R", Operation.Add), new SlotNode("RD", "R", Operation.Add)
            };
            var board = new BoardGraph(nodes, new[]
            {
                new ChainDefinition("left-atk", "left", ChainRole.Attack, "LA"), new ChainDefinition("left-def", "left", ChainRole.Defense, "LD"),
                new ChainDefinition("right-atk", "right", ChainRole.Attack, "RA"), new ChainDefinition("right-def", "right", ChainRole.Defense, "RD")
            });
            var lanes = new[]
            {
                new LaneSpec("left", new[] { Enemy("Wall", 100, new EnemyActionData("Brace", 0, 6)) }),
                new LaneSpec("right", new[] { Enemy("Shield", 100, new EnemyActionData("Brace", 0, 6)) })
            };
            var deck = new[] { new PilotStats("Brawler", 5f, 4f, null, new NoGuardAbility(1)), new PilotStats("Other", 1f, 2f) };
            var engine = new BattleEngine(new BattleSetup(board, new List<SlotRule>(), lanes, new BattleRules(), deck, 1));
            engine.Start();
            PlaceByName(engine, "Brawler", "L");
            PlaceByName(engine, "Other", "R");
            Assert.IsTrue(engine.TryActivate("L"));

            Assert.AreEqual(0.5f, engine.GuardFactor("left"), Tolerance);
            Assert.AreEqual(1f, engine.GuardFactor("right"), Tolerance);
            Assert.AreEqual(2f, engine.SumChains(ChainRole.Defense, "left"), Tolerance);
            Assert.AreEqual(2f, engine.SumChains(ChainRole.Defense, "right"), Tolerance);
        }

        // ---------- defeat ----------

        [Test]
        public void TheBattleIsLost_WhenNoPilotIsLeft()
        {
            // one pilot, no bank: the hit destroys its chain and the player still has health
            var enemy = Enemy("E", 100, new EnemyActionData("Hit", 3, 0));
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { enemy }, new[] { new PilotStats("Only", 0f, 1f) },
                rules: new BattleRules { PlayerMaxHealth = 50 }));
            engine.Start();
            PlaceByName(engine, "Only", "R");

            engine.ResolveTurn();

            Assert.AreEqual(BattleOutcome.Defeat, engine.Outcome);
            Assert.AreEqual(BattleEngine.DefeatNoPilots, engine.DefeatReason);
            Assert.Greater(engine.PlayerHealth, 0f);
        }

        [Test]
        public void GivingUp_LosesTheBattle()
        {
            var engine = SurgeBattle();
            engine.GiveUp();
            Assert.AreEqual(BattleOutcome.Defeat, engine.Outcome);
            Assert.AreEqual(BattleEngine.DefeatGaveUp, engine.DefeatReason);
        }

        // ---------- opening hand ----------

        [Test]
        public void OpeningHand_DrawsTheNamedPilotsFirst()
        {
            var deck = Enumerable.Range(0, 8).Select(i => new PilotStats("P" + i, 1f, 1f)).ToArray();
            for (var seed = 1; seed <= 20; seed++)
            {
                var setup = Setup(OneSlotBoard(), new[] { Enemy("E", 10, new EnemyActionData("Idle", 0, 0)) }, deck, seed: seed);
                setup.OpeningHand = new[] { "P7", "P5" };
                var engine = new BattleEngine(setup);
                engine.Start();

                Assert.IsTrue(engine.Hand.Any(pilot => pilot.Name == "P7"));
                Assert.IsTrue(engine.Hand.Any(pilot => pilot.Name == "P5"));
            }
        }

        // ---------- placing on a fallen pilot ----------

        [Test]
        public void PlacingOnAFallenPilot_ClearsIt_AtThePlaceCost_EvenWithoutReplacing()
        {
            var enemy = Enemy("E", 100, new EnemyActionData("Smash", 10, 0));
            var rules = new BattleRules { AllowReplace = false, AllowEject = false, PlacePilotCost = 1, ReplacePilotCost = 2 };
            var deck = new[] { new PilotStats("Victim", 0f, 4f), new PilotStats("Fresh", 0f, 3f) };
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { enemy }, deck, rules: rules));
            engine.Start();
            PlaceByName(engine, "Victim", "R");
            engine.ResolveTurn();
            Assert.IsTrue(engine.PilotBySlot["R"].Fallen);

            PlaceByName(engine, "Fresh", "R");

            Assert.AreEqual("Fresh", engine.PilotBySlot["R"].Name);
            Assert.AreEqual(2, engine.ActionPoints); // the place cost, not the replace cost
            Assert.IsFalse(engine.Hand.Any(pilot => pilot.Name == "Victim")); // gone from the battle, not back in the hand
            Assert.AreEqual(2, engine.Stats.Places); // Victim, then Fresh: a place, not a replace
            Assert.AreEqual(0, engine.Stats.Replaces);
        }

        [Test]
        public void WoundedPilotInTheHand_RecoversEachTurn()
        {
            var enemy = Enemy("E", 100, new EnemyActionData("Hit", 3, 0), new EnemyActionData("Idle", 0, 0));
            var rules = new BattleRules { HandRecoveryPerTurn = 0.5f };
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { enemy }, new[] { new PilotStats("Worn", 0f, 4f) }, rules: rules));
            engine.Start();
            PlaceByName(engine, "Worn", "R");
            engine.ResolveTurn(); // 3 of 4 absorbed: wound 0.75
            Assert.IsTrue(engine.TryEject("R"));
            Assert.AreEqual(0.75f, engine.Hand[0].DefenseWound, Tolerance);

            engine.ResolveTurn();
            Assert.AreEqual(0.25f, engine.Hand[0].DefenseWound, Tolerance);
            engine.ResolveTurn();
            Assert.AreEqual(0f, engine.Hand[0].DefenseWound, Tolerance);
        }

        // a three-slot add chain that is lane1's defense; the root also starts an attack chain so the lane has one
        private static BoardGraph ThreeDefenders()
        {
            var nodes = new[] { new SlotNode("a"), new SlotNode("b", "a", Operation.Add), new SlotNode("c", "b", Operation.Add) };
            return new BoardGraph(nodes, new[]
            {
                new ChainDefinition("atk", "lane1", ChainRole.Attack, "a"),
                new ChainDefinition("def", "lane1", ChainRole.Defense, "c")
            });
        }

        [Test]
        public void AfterAHit_RemainingDefenseIsRoundedUpToTheDefenseStep()
        {
            // a hit of 4 on 3 + 3 + 3 leaves exactly 1.667 each; rounded up to 1.75
            var enemy = Enemy("E", 100, new EnemyActionData("Hit", 4, 0));
            var deck = new[] { new PilotStats("A", 0f, 3f), new PilotStats("B", 0f, 3f), new PilotStats("C", 0f, 3f) };
            var engine = new BattleEngine(Setup(ThreeDefenders(), new[] { enemy }, deck));
            engine.Start();
            PlaceByName(engine, "A", "a");
            PlaceByName(engine, "B", "b");
            PlaceByName(engine, "C", "c");

            engine.ResolveTurn();

            foreach (var slotId in new[] { "a", "b", "c" })
            {
                Assert.AreEqual(1.75f, engine.PilotBySlot[slotId].EffectiveDefense, Tolerance);
            }

            Assert.AreEqual(engine.Setup.Rules.PlayerMaxHealth, engine.PlayerHealth, Tolerance); // nothing got through
        }

        [Test]
        public void AMultiplyChainHit_LandsOnTheDefenseStep()
        {
            // (a + b) x c = (1 + 1) x 3 = 6, hit for 3: pilots snapped alone give 1.5 x 2.25 = 3.375, between steps
            var nodes = new[] { new SlotNode("a"), new SlotNode("b", "a", Operation.Add), new SlotNode("c", "b", Operation.Multiply) };
            var board = new BoardGraph(nodes, new[]
            {
                new ChainDefinition("atk", "lane1", ChainRole.Attack, "a"),
                new ChainDefinition("def", "lane1", ChainRole.Defense, "c")
            });
            var enemy = Enemy("E", 100, new EnemyActionData("Hit", 3, 0));
            var deck = new[] { new PilotStats("A", 0f, 1f), new PilotStats("B", 0f, 1f), new PilotStats("C", 0f, 3f) };
            var engine = new BattleEngine(Setup(board, new[] { enemy }, deck));
            engine.Start();
            PlaceByName(engine, "A", "a");
            PlaceByName(engine, "B", "b");
            PlaceByName(engine, "C", "c");

            engine.ResolveTurn();

            var value = engine.SumChains(ChainRole.Defense);
            Assert.AreEqual(0f, value % 0.25f, 0.002f);
            Assert.IsTrue(value >= 3f - Tolerance); // never below the exact remainder
        }

        [Test]
        public void WithADefenseStepOfZero_WoundsAreExact()
        {
            var enemy = Enemy("E", 100, new EnemyActionData("Hit", 4, 0));
            var deck = new[] { new PilotStats("A", 0f, 3f), new PilotStats("B", 0f, 3f), new PilotStats("C", 0f, 3f) };
            var engine = new BattleEngine(Setup(ThreeDefenders(), new[] { enemy }, deck,
                rules: new BattleRules { DefenseStep = 0f }));
            engine.Start();
            PlaceByName(engine, "A", "a");
            PlaceByName(engine, "B", "b");
            PlaceByName(engine, "C", "c");

            engine.ResolveTurn();

            Assert.AreEqual(5f / 3f, engine.PilotBySlot["a"].EffectiveDefense, Tolerance);
        }

        // R and S both end the defense chain; the enemy hits 3 of their 4 on turn 1 (wound 0.75 each), then idles
        private static BattleEngine TwoWornPilots(float ejectedMultiplier)
        {
            var enemy = Enemy("E", 100, new EnemyActionData("Hit", 3, 0), new EnemyActionData("Idle", 0, 0));
            var rules = new BattleRules { HandRecoveryPerTurn = 0.25f, EjectedRecoveryMultiplier = ejectedMultiplier };
            var deck = new[] { new PilotStats("Ejected", 0f, 2f), new PilotStats("Replaced", 0f, 2f), new PilotStats("Fresh", 0f, 1f) };
            var engine = new BattleEngine(Setup(TwoSlotChain(Operation.Add), new[] { enemy }, deck, rules: rules));
            engine.Start();
            PlaceByName(engine, "Ejected", "R");
            PlaceByName(engine, "Replaced", "S");
            engine.ResolveTurn();
            Assert.IsTrue(engine.TryEject("R"));
            PlaceByName(engine, "Fresh", "S"); // pushes Replaced out to the hand
            return engine;
        }

        [Test]
        public void EjectedPilot_RecoversFasterOnItsFirstTurnOfRest_ThanAReplacedOne()
        {
            var engine = TwoWornPilots(2f);

            engine.ResolveTurn();

            Assert.AreEqual(0.25f, engine.Hand.First(pilot => pilot.Name == "Ejected").DefenseWound, Tolerance); // 0.75 - 2 x 0.25
            Assert.AreEqual(0.5f, engine.Hand.First(pilot => pilot.Name == "Replaced").DefenseWound, Tolerance); // 0.75 - 0.25
            engine.ResolveTurn();
            Assert.AreEqual(0f, engine.Hand.First(pilot => pilot.Name == "Ejected").DefenseWound, Tolerance); // only the first turn is faster
        }

        [Test]
        public void EjectedPilot_PlacedAgainTheSameTurn_GetsNoBonus()
        {
            var engine = TwoWornPilots(2f);
            PlaceByName(engine, "Ejected", "R"); // moved straight back onto the board: it does not rest

            engine.ResolveTurn(); // the enemy idles

            Assert.AreEqual(0.75f, engine.PilotBySlot["R"].DefenseWound, Tolerance);
        }

        [Test]
        public void LivingPilot_StillCannotBeReplaced_WhenReplacingIsOff()
        {
            var rules = new BattleRules { AllowReplace = false };
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { Enemy("E", 100, new EnemyActionData("Idle", 0, 0)) }, Strong(4), rules: rules));
            engine.Start();
            Assert.IsTrue(engine.TryPlace(0, "R"));

            Assert.IsFalse(engine.CanPlace(0, "R"));
        }

        [Test]
        public void ChainZeroedByDivideByZero_IsStillLeftAlone()
        {
            var enemy = Enemy("E", 100, new EnemyActionData("Hit", 5, 0));
            var nodes = new[] { new SlotNode("R"), new SlotNode("S", "R", Operation.Divide) };
            var board = new BoardGraph(nodes, new[]
            {
                new ChainDefinition("atk", "lane1", ChainRole.Attack, "S"),
                new ChainDefinition("def", "lane1", ChainRole.Defense, "S")
            });
            var deck = new[] { new PilotStats("Top", 0f, 4f), new PilotStats("Zero", 0f, 0f) };
            var engine = new BattleEngine(Setup(board, new[] { enemy }, deck));
            engine.Start();
            PlaceByName(engine, "Top", "R");
            PlaceByName(engine, "Zero", "S");

            engine.ResolveTurn();

            Assert.IsFalse(engine.PilotBySlot["R"].Fallen);
            Assert.IsFalse(engine.PilotBySlot["S"].Fallen);
            Assert.AreEqual(15f, engine.PlayerHealth, Tolerance);
        }

        [Test]
        public void Defeat_WhenTheEnemyOutDamagesThePlayer()
        {
            var enemy = Enemy("E", 100, new EnemyActionData("Smash", 25, 0));
            var engine = new BattleEngine(Setup(OneSlotBoard(), new[] { enemy }, Strong(4)));
            engine.Start();

            engine.ResolveTurn();

            Assert.AreEqual(BattleOutcome.Defeat, engine.Outcome);
        }

        [Test]
        public void ScriptedPlayer_ConcludesABattle()
        {
            var enemies = new[]
            {
                Enemy("Slime", 8, new EnemyActionData("Hit", 2, 0), new EnemyActionData("Guard", 0, 1)),
                Enemy("Golem", 12, new EnemyActionData("Smash", 3, 0))
            };
            var result = BattleRunner.Run(Setup(OneSlotBoard(), enemies, Strong(6)));

            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            Assert.Greater(result.PlayerHealth, 0f);
        }

        [Test]
        public void ScriptedPlayer_BattleThatCannotBeWon_EndsInStalemateAtTheTurnLimit()
        {
            var enemy = Enemy("Wall", 1000, new EnemyActionData("Idle", 0, 99));
            var result = BattleRunner.Run(Setup(OneSlotBoard(), new[] { enemy }, Strong(4)), turnLimit: 5);

            Assert.AreEqual(BattleOutcome.Stalemate, result.Outcome);
        }

        [Test]
        public void SameSeed_GivesTheSameBattle()
        {
            var enemy = new EnemySpec("E", 15, new[] { new EnemyActionData("A", 1, 0), new EnemyActionData("B", 2, 0), new EnemyActionData("C", 0, 1) }, true);
            var first = BattleRunner.Run(Setup(OneSlotBoard(), new[] { enemy }, Strong(6), seed: 7));
            var second = BattleRunner.Run(Setup(OneSlotBoard(), new[] { enemy }, Strong(6), seed: 7));

            CollectionAssert.AreEqual(first.Log, second.Log);
        }
    }
}
