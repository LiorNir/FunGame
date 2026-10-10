using System.Collections.Generic;
using System.Linq;
using FunGame.Battle;
using FunGame.Board;

namespace FunGame.Sim
{
    /// <summary>
    /// Made-up encounters and the starter roster, so the balance lab works before the Unity encounters are exported and so
    /// the tests have something to play. They are not the game's real encounters; the report marks them as built-in.
    /// </summary>
    public static class BuiltInEncounters
    {
        /// <summary>
        /// The six pilots of the Unity starter deck (Assets/Rules/StarterDeck.asset), in its order, with their display names and
        /// current stats. A copy for the lab: keep it in step with the deck (exported encounters carry the real deck).
        /// </summary>
        public static IReadOnlyList<PilotStats> StarterRoster => new[]
        {
            new PilotStats("Osis", 2f, 1f),
            new PilotStats("Sil", 2f, 1f),
            new PilotStats("Quill", 1f, 2f),
            new PilotStats("Yawn", 1f, 1f),
            new PilotStats("Haze", 1.5f, 0.5f),
            new PilotStats("Pion", 1f, 1f)
        };

        /// <summary>World 1 followed by the loose samples.</summary>
        public static IReadOnlyList<EncounterCase> All()
        {
            return WorldOne().Concat(new[]
            {
                TutorialStraight(),
                EarlySplitPair(),
                EarlyWaves(),
                MidBonusRoad(),
                MidTwoLanes(),
                MidDivideTrap(),
                LateGate(),
                LateBoss(),
                LockPinnedDown()
            }).ToList();
        }

        // ---------- helpers ----------

        private static EnemyActionData Act(string name, int attack, int defense)
        {
            return new EnemyActionData(name, attack, defense);
        }

        private static EnemySpec Enemy(string name, int health, bool random, params EnemyActionData[] actions)
        {
            return new EnemySpec(name, health, actions, random);
        }

        private static EncounterCase Make(
            string name,
            string tier,
            BoardGraph board,
            IReadOnlyList<LaneSpec> lanes,
            IReadOnlyList<SlotRule> slotRules = null,
            string note = null)
        {
            var setup = new BattleSetup(board, slotRules ?? new List<SlotRule>(), lanes, new BattleRules(), StarterRoster, 1);
            return new EncounterCase(name, tier, setup, note);
        }

        private static ChainDefinition Chain(string id, string lane, ChainRole role, string terminal)
        {
            return new ChainDefinition(id, lane, role, terminal);
        }


        // ---------- World 1: placing pilots and reading operators ----------

        /// <summary>
        /// The first world, as agreed: placing pilots in a chain and reading what the operators do. Replacing and ejecting
        /// pilots are not available yet (they are taught in world 2). The last encounter is the world's boss.
        /// </summary>
        public static IReadOnlyList<EncounterCase> WorldOne()
        {
            var rules = new BattleRules { AllowReplace = false, AllowEject = false };

            // 1: two slots in a line, a harmless enemy
            var firstBoard = new BoardGraph(
                new[] { new SlotNode("R"), new SlotNode("S2", "R", Operation.Add) },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "S2"), Chain("def", "lane1", ChainRole.Defense, "S2") });
            var first = new EncounterCase("World 1-1: First steps", "Tutorial",
                new BattleSetup(firstBoard, new List<SlotRule>(),
                    new[] { new LaneSpec("lane1", new[] { Enemy("Training dummy", 8, false, Act("Poke", 1, 0)) }) }, rules, StarterRoster, 1),
                "Place pilots and win.", "World 1", 1, "placing pilots; a chain adds up");

            // 2: the same line with a third slot that opens on turn 2
            var secondBoard = new BoardGraph(
                new[] { new SlotNode("R"), new SlotNode("S2", "R", Operation.Add), new SlotNode("S3", "S2", Operation.Add) },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "S3"), Chain("def", "lane1", ChainRole.Defense, "S3") });
            var second = new EncounterCase("World 1-2: A door opens", "Tutorial",
                new BattleSetup(secondBoard, new[] { new SlotRule("S3", true, 0, 0, 2) },
                    new[] { new LaneSpec("lane1", new[] { Enemy("Scrapper", 14, false, Act("Shell", 1, 1)) }) }, rules, StarterRoster, 1),
                "A third slot opens on turn 2, and the enemy is too sturdy to beat without it.", "World 1", 2, "a slot that opens on a turn");

            // 3 (boss): attack and defense branch off one root, with multiply on the attack and a subtract toll on the defense
            var bossBoard = new BoardGraph(
                new[]
                {
                    new SlotNode("R"),
                    new SlotNode("A1", "R", Operation.Add), new SlotNode("A2", "A1", Operation.Multiply),
                    new SlotNode("D1", "R", Operation.Add), new SlotNode("D2", "D1", Operation.Subtract)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A2"), Chain("def", "lane1", ChainRole.Defense, "D2") });
            var boss = new EncounterCase("World 1-3: The gatekeeper (boss)", "Early",
                new BattleSetup(bossBoard, new List<SlotRule>(),
                    new[] { new LaneSpec("lane1", new[] { Enemy("Gatekeeper", 14, false, Act("Slam", 3, 0), Act("Brace", 0, 2), Act("Slam", 3, 0)) }) }, rules, StarterRoster, 1),
                "Multiply on the attack branch, a subtract toll on the defense branch.", "World 1", 3, "operators: add, subtract and multiply");

            return new[] { first, second, boss };
        }

        // ---------- enemy mechanics ----------

        /// <summary>
        /// Teaches reacting to Lock. The root slot is in both the attack and the defense chain, so the natural play is to put
        /// a strong pilot there. The enemy telegraphs a lock on that trunk pilot, then crushes the defense with more than it
        /// can hold. Normally a pilot can be ejected before a hit like that; a locked one cannot, so the trunk pilot falls with
        /// the chain. The reaction is to swap the marked pilot out (the lock then misses) before it lands.
        /// </summary>
        public static EncounterCase LockPinnedDown()
        {
            var board = new BoardGraph(
                new[]
                {
                    new SlotNode("R"),
                    new SlotNode("A1", "R", Operation.Add),
                    new SlotNode("D1", "R", Operation.Add), new SlotNode("D2", "D1", Operation.Add)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A1"), Chain("def", "lane1", ChainRole.Defense, "D2") });
            var pin = new EnemyActionData("Pin", new IEnemyEffect[] { new AttackEffect(1), new LockEffect(2, PilotTargetRule.Trunk) });
            var warden = Enemy("Warden", 18, false, Act("Jab", 2, 0), pin, Act("Crush", 6, 0));
            return new EncounterCase("Lock sample: Pinned down", "Early",
                new BattleSetup(board, new List<SlotRule>(), new[] { new LaneSpec("lane1", new[] { warden }) }, new BattleRules(), StarterRoster, 1),
                "Pin locks the trunk pilot for 2 turns, then Crush breaks the defense; the root is in both chains.", null, 0, "reacting to Lock");
        }

        // ---------- encounters ----------

        /// <summary>Three slots in a line; the same slots are the attack and the defense. One weak enemy.</summary>
        private static EncounterCase TutorialStraight()
        {
            var board = new BoardGraph(
                new[] { new SlotNode("R"), new SlotNode("S2", "R", Operation.Add), new SlotNode("S3", "S2", Operation.Add) },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "S3"), Chain("def", "lane1", ChainRole.Defense, "S3") });
            var lanes = new[] { new LaneSpec("lane1", new[] { Enemy("Slime", 12, false, Act("Poke", 2, 0), Act("Poke", 2, 0), Act("Wobble", 0, 1)) }) };
            return Make("Tutorial: Straight line", "Tutorial", board, lanes, null, "One lane, one enemy, everything adds up.");
        }

        private static EncounterCase EarlySplitPair()
        {
            var board = SampleBoards.Create(0);
            var lanes = new[] { new LaneSpec("lane", new[] { Enemy("Goblin", 14, false, Act("Smack", 3, 0), Act("Block", 0, 2), Act("Smack", 3, 0)) }) };
            return Make("Early: Split pair", "Early", board, lanes, null, "An attack branch with a multiply and a defense branch with a subtract toll.");
        }

        private static EncounterCase EarlyWaves()
        {
            var board = new BoardGraph(
                new[]
                {
                    new SlotNode("R"), new SlotNode("S2", "R", Operation.Add),
                    new SlotNode("S3", "S2", Operation.Add), new SlotNode("S4", "S3", Operation.Add)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "S4"), Chain("def", "lane1", ChainRole.Defense, "S4") });
            var rules = new[]
            {
                new SlotRule("S3", true, 0, 0, 2),
                new SlotRule("S4", true, 1, 0, 0)
            };
            var lanes = new[]
            {
                new LaneSpec("lane1", new[]
                {
                    Enemy("Rat", 7, false, Act("Nibble", 2, 0)),
                    Enemy("Rat", 7, false, Act("Nibble", 2, 0), Act("Squeak", 0, 1)),
                    Enemy("Boar", 14, false, Act("Gore", 3, 0), Act("Brace", 0, 2))
                })
            };
            return Make("Early: Waves", "Early", board, lanes, rules, "Three enemies in a row; one slot opens on turn 2, one can be bought for 1 AP.");
        }

        private static EncounterCase MidBonusRoad()
        {
            var board = new BoardGraph(
                new[]
                {
                    new SlotNode("R"),
                    new SlotNode("A1", "R", Operation.Add),
                    new SlotNode("A2", "A1", Operation.Add, new NodeBonus(BonusKind.Multiply, 2f)),
                    new SlotNode("D1", "R", Operation.Add),
                    new SlotNode("D2", "D1", Operation.Add)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A2"), Chain("def", "lane1", ChainRole.Defense, "D2") });
            var lanes = new[] { new LaneSpec("lane1", new[] { Enemy("Ogre", 26, true, Act("Smash", 6, 0), Act("Guard", 0, 4), Act("Jab", 3, 1)) }) };
            return Make("Mid: Bonus road", "Mid", board, lanes, null, "Reaching the x2 bonus doubles the attack; the enemy picks its action at random.");
        }

        private static EncounterCase MidTwoLanes()
        {
            var board = SampleBoards.Create(1);
            var lanes = new[]
            {
                new LaneSpec("left", new[] { Enemy("Archer", 12, false, Act("Shoot", 3, 0), Act("Duck", 0, 1)) }),
                new LaneSpec("right", new[] { Enemy("Knight", 14, true, Act("Slash", 4, 0), Act("Shield", 0, 3)) })
            };
            return Make("Mid: Two lanes", "Mid", board, lanes, null, "The two-lane hub; one hub slot feeds both lanes.");
        }

        private static EncounterCase MidDivideTrap()
        {
            var board = new BoardGraph(
                new[]
                {
                    new SlotNode("R"),
                    new SlotNode("A1", "R", Operation.Add),
                    new SlotNode("A2", "A1", Operation.Multiply),
                    new SlotNode("D1", "R", Operation.Add),
                    new SlotNode("D2", "D1", Operation.Divide)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A2"), Chain("def", "lane1", ChainRole.Defense, "D2") });
            var lanes = new[] { new LaneSpec("lane1", new[] { Enemy("Golem", 20, false, Act("Slam", 5, 0), Act("Harden", 0, 3)) }) };
            return Make("Mid: Divide trap", "Mid", board, lanes, null, "The defense chain divides by the last pilot's defense.");
        }

        private static EncounterCase LateGate()
        {
            var board = SampleBoards.Create(1);
            var rules = new[]
            {
                new SlotRule("G", true, 0, 2, 0, "left"),
                new SlotRule("I", true, 2, 0, 0)
            };
            var lanes = new[]
            {
                new LaneSpec("left", new[]
                {
                    Enemy("Archer", 10, false, Act("Shoot", 3, 0), Act("Duck", 0, 2)),
                    Enemy("Sniper", 14, false, Act("Aim", 0, 2), Act("Shoot", 6, 0))
                }),
                new LaneSpec("right", new[]
                {
                    Enemy("Knight", 14, true, Act("Slash", 4, 0), Act("Shield", 0, 3)),
                    Enemy("Captain", 18, true, Act("Charge", 6, 0), Act("Rally", 0, 3), Act("Slash", 4, 1))
                })
            };
            return Make("Late: The gate", "Late", board, lanes, rules, "Two lanes with two enemies each; the bonus slot opens when the left lane's second enemy arrives.");
        }

        private static EncounterCase LateBoss()
        {
            var board = new BoardGraph(
                new[]
                {
                    new SlotNode("R1"), new SlotNode("R2"),
                    new SlotNode("A1", "R1", Operation.Add), new SlotNode("A2", "A1", Operation.Multiply),
                    new SlotNode("D1", "R2", Operation.Add), new SlotNode("D2", "D1", Operation.Add),
                    new SlotNode("D3", "D2", Operation.Subtract)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A2"), Chain("def", "lane1", ChainRole.Defense, "D3") });
            var lanes = new[]
            {
                new LaneSpec("lane1", new[]
                {
                    Enemy("Boss: Warden", 40, false, Act("Hammer", 7, 0), Act("Bulwark", 0, 5), Act("Cleave", 4, 3), Act("Hammer", 7, 0))
                })
            };
            return Make("Late: Boss", "Late", board, lanes, null, "One big enemy with a heavy attack on a long cycle.");
        }
    }
}
