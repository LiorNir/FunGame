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
            new PilotStats("Brash", 1.5f, 0.5f),
            new PilotStats("Pion", 1f, 1f)
        };

        /// <summary>
        /// The game's global rules as set in the Unity GameRules asset (Assets/Rules/GameRules.asset): a copy for the lab, keep it
        /// in step. Player health 10 and half a pilot's defense recovered per turn in the hand since 2026-10-10; an ejected pilot
        /// recovers twice as fast on its first turn of rest since 2026-10-11.
        /// </summary>
        public static BattleRules GameRules => new BattleRules { PlayerMaxHealth = 10, HandRecoveryPerTurn = 0.5f, EjectedRecoveryMultiplier = 2f };

        /// <summary>World 1 and World 2 followed by the loose samples.</summary>
        public static IReadOnlyList<EncounterCase> All()
        {
            return WorldOne().Concat(WorldTwo()).Concat(WorldThree()).Concat(WorldFour()).Concat(WorldFive()).Concat(new[]
            {
                TutorialStraight(),
                EarlySplitPair(),
                EarlyWaves(),
                MidBonusRoad(),
                MidTwoLanes(),
                MidDivideTrap(),
                LateGate(),
                LateBoss(),
                LockPinnedDown(),
                AbilitySurgeSample()
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
            var setup = new BattleSetup(board, slotRules ?? new List<SlotRule>(), lanes, GameRules, StarterRoster, 1);
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
            var rules = GameRules;
            rules.AllowReplace = false;
            rules.AllowEject = false;

            // 1: two slots in a line, a harmless enemy
            var firstBoard = new BoardGraph(
                new[] { new SlotNode("R"), new SlotNode("S2", "R", Operation.Add) },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "S2"), Chain("def", "lane1", ChainRole.Defense, "S2") });
            var first = new EncounterCase("World 1-1: First steps", "Tutorial",
                new BattleSetup(firstBoard, new List<SlotRule>(),
                    new[] { new LaneSpec("lane1", new[] { Enemy("Training dummy", 4, false, Act("Poke", 1, 0)) }) }, rules, StarterRoster, 1),
                "Place pilots and win.", "World 1", 1, "placing pilots; a chain adds up");

            // 2: the same line with a third slot that opens on turn 2
            var secondBoard = new BoardGraph(
                new[] { new SlotNode("R"), new SlotNode("S2", "R", Operation.Add), new SlotNode("S3", "S2", Operation.Add) },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "S3"), Chain("def", "lane1", ChainRole.Defense, "S3") });
            var second = new EncounterCase("World 1-2: A door opens", "Tutorial",
                new BattleSetup(secondBoard, new[] { new SlotRule("S3", true, 0, 0, 2) },
                    new[] { new LaneSpec("lane1", new[] { Enemy("Scrapper", 7, false, Act("Shell", 1, 1)) }) }, rules, StarterRoster, 1),
                "A third slot opens on turn 2, and the enemy is too sturdy to beat without it.", "World 1", 2, "a slot that opens on a turn");

            // 3 (boss): attack and defense branch off one root, with multiply on the attack. The defense branch is short on
            // purpose (it had a subtract toll, which no pilot could use well; removed after the first playtest), so the defense
            // breaks during the battle and the player sees a broken chain.
            var bossBoard = new BoardGraph(
                new[]
                {
                    new SlotNode("R"),
                    new SlotNode("A1", "R", Operation.Add), new SlotNode("A2", "A1", Operation.Multiply),
                    new SlotNode("D1", "R", Operation.Add)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A2"), Chain("def", "lane1", ChainRole.Defense, "D1") });
            var boss = new EncounterCase("World 1-3: The gatekeeper (boss)", "Early",
                new BattleSetup(bossBoard, new List<SlotRule>(),
                    new[] { new LaneSpec("lane1", new[] { Enemy("Gatekeeper", 14, false, Act("Slam", 3, 0), Act("Brace", 0, 2), Act("Slam", 3, 0)) }) }, rules, StarterRoster, 1),
                "Multiply on the attack branch, a short defense branch that breaks.", "World 1", 3, "operators: add and multiply; a broken chain");

            return new[] { first, second, boss };
        }

        // ---------- World 2: changing your board (replacing and ejecting) ----------

        /// <summary>
        /// The pilots the player has by World 2, standing in for progression until SP and rewards exist: the starter deck
        /// with the first upgrade (+1 to one stat of one pilot: Quill's defense, 2 -> 3) and the World 1 boss's reward pilot
        /// Spark (strong but fragile). Mirrors the Unity test deck of the World 2 encounters.
        /// </summary>
        public static IReadOnlyList<PilotStats> WorldTwoRoster => new[]
        {
            new PilotStats("Osis", 2f, 1f),
            new PilotStats("Sil", 2f, 1f),
            new PilotStats("Quill", 1f, 3f),
            new PilotStats("Yawn", 1f, 1f),
            new PilotStats("Brash", 1.5f, 0.5f),
            new PilotStats("Pion", 1f, 1f),
            new PilotStats("Spark", 3f, 0.5f)
        };

        /// <summary>
        /// The second world: changing pilots that are already on the board. 2-1 teaches replacing (ejecting is still off),
        /// 2-2 teaches ejecting, the boss needs both and has a slot opened with AP. Each step is built so that it cannot be won
        /// comfortably without its tool; the lab checks this with the "Does it need...?" table.
        /// </summary>
        public static IReadOnlyList<EncounterCase> WorldTwo()
        {
            // 2-1: a short board whose defense wears down every turn; fresh pilots keep it standing. Replacing comes first;
            // ejecting is taught at the wave change (user, 2026-10-11): pull the worn pilots back to rest while the next enemy
            // arrives.
            var replaceRules = GameRules;
            var firstBoard = new BoardGraph(
                new[] { new SlotNode("R"), new SlotNode("A1", "R", Operation.Add), new SlotNode("D1", "R", Operation.Add) },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A1"), Chain("def", "lane1", ChainRole.Defense, "D1") });
            var first = new EncounterCase("World 2-1: Fresh legs", "Early",
                new BattleSetup(firstBoard, new List<SlotRule>(),
                    new[] { new LaneSpec("lane1", new[] { Enemy("Grinder", 9, false, Act("Grind", 3, 0)), Enemy("Big grinder", 10, false, Act("Grind", 3, 0)) }) },
                    replaceRules, WorldTwoRoster, 1),
                "The defense wears down every turn; swapping a worn pilot for a fresh one keeps it standing.", "World 2", 1, "replacing pilots; ejecting at the wave change");

            // 2-2: attack and defense are separate trees. The first enemy telegraphs one crush that no defense chain can hold;
            // the second hits every turn and holding it needs the best defender. Ejecting the defense root before the crush
            // (ducking) leaves the defenders behind a gap, unhurt, at the cost of the crush landing on the player; not ducking
            // loses them, and with them the defense against the second enemy.
            var ejectRules = GameRules;
            var secondBoard = new BoardGraph(
                new[]
                {
                    new SlotNode("RA"), new SlotNode("A1", "RA", Operation.Add),
                    new SlotNode("RD"), new SlotNode("D1", "RD", Operation.Add)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A1"), Chain("def", "lane1", ChainRole.Defense, "D1") });
            var second = new EncounterCase("World 2-2: Duck!", "Early",
                new BattleSetup(secondBoard, new List<SlotRule>(),
                    new[] { new LaneSpec("lane1", new[]
                    {
                        Enemy("Crusher", 8, false, Act("Jab", 1, 0), Act("Crush", 5, 0), Act("Jab", 1, 0)),
                        Enemy("Hammer", 15, false, Act("Hammer", 4, 0))
                    }) },
                    ejectRules, WorldTwoRoster, 1),
                "A telegraphed crush breaks any defense chain, and the next enemy needs your best defender: duck the crush to keep it.", "World 2", 2, "ejecting pilots");

            // 2-3 (boss): one operator feeds both branches; multiply on the attack. The defense branch is the root plus one slot
            // that costs 1 AP to open (the user, 2026-10-10: a third defense slot made the defense too easy).
            var bossRules = GameRules;
            var bossBoard = new BoardGraph(
                new[]
                {
                    new SlotNode("R"),
                    new SlotNode("A1", "R", Operation.Add), new SlotNode("A2", "A1", Operation.Multiply),
                    new SlotNode("D1", "R", Operation.Add)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A2"), Chain("def", "lane1", ChainRole.Defense, "D1") });
            var boss = new EncounterCase("World 2-3: The foreman (boss)", "Early",
                new BattleSetup(bossBoard, new[] { new SlotRule("D1", true, 1, 0, 0) },
                    new[] { new LaneSpec("lane1", new[] { Enemy("Foreman", 30, false, Act("Grind", 3, 0), Act("Crush", 7, 0), Act("Brace", 1, 3)) }) },
                    bossRules, WorldTwoRoster, 1),
                "Wear, a guard and a crush in turn; a defense slot that costs 1 AP to open.", "World 2", 3, "replacing and ejecting together; a slot opened with AP");

            return new[] { first, second, boss };
        }

        // ---------- World 3: pilot abilities ----------

        /// <summary>
        /// The pilots the player has by World 3 (standing in for progression): the World 2 roster with the second upgrade
        /// (Yawn's defense, 1 -> 2: the user felt short of defense more than attack) and the World 2 boss's reward pilot Bolt,
        /// the first pilot with an ability (Surge, 2 AP for now; the lab compares 1 and 2 AP).
        /// </summary>
        public static IReadOnlyList<PilotStats> WorldThreeRoster => WorldTwoRoster
            .Select(pilot => pilot.Name == "Yawn" ? pilot.WithStats(1f, 2f) : pilot)
            .Concat(new[] { new PilotStats("Bolt", 2.5f, 1.5f, null, new SurgeAbility(2)) })
            .ToList();

        /// <summary>
        /// The third world: pilot abilities, opened by Bolt's Surge. 3-1 teaches using it and its timing (Bolt is in the
        /// opening hand), 3-2 handling the crash turn (swapping or ejecting on the attack side), the boss brings Snipe, which
        /// meets the ability (surging the sniped pilot makes the snipe land on its crash turn).
        /// </summary>
        public static IReadOnlyList<EncounterCase> WorldThree()
        {
            // 3-1: the enemy shells and opens in turn; the attack branch ends in a multiply slot
            var firstBoard = new BoardGraph(
                new[]
                {
                    new SlotNode("R"),
                    new SlotNode("A1", "R", Operation.Add), new SlotNode("A2", "A1", Operation.Multiply),
                    new SlotNode("D1", "R", Operation.Add)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A2"), Chain("def", "lane1", ChainRole.Defense, "D1") });
            var firstSetup = new BattleSetup(firstBoard, new List<SlotRule>(),
                new[] { new LaneSpec("lane1", new[] { Enemy("Shellback", 32, false, Act("Shell", 1, 8), Act("Open", 2, 0)) }) },
                GameRules, WorldThreeRoster, 1) { OpeningHand = new[] { "Bolt" } };
            var first = new EncounterCase("World 3-1: Power surge", "Early", firstSetup,
                "Bolt starts in hand; the enemy shells and opens in turn, so a Surge is worth most on an open turn.", "World 3", 1, "using an ability; timing it");

            // 3-2: steady pressure from two enemies; after a Surge, Bolt's crash turn wants a swap (or an eject at the wave change)
            var secondBoard = new BoardGraph(
                new[]
                {
                    new SlotNode("R"),
                    new SlotNode("A1", "R", Operation.Add), new SlotNode("A2", "A1", Operation.Multiply),
                    new SlotNode("D1", "R", Operation.Add), new SlotNode("D2", "D1", Operation.Add)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A2"), Chain("def", "lane1", ChainRole.Defense, "D2") });
            var second = new EncounterCase("World 3-2: After the crash", "Early",
                new BattleSetup(secondBoard, new List<SlotRule>(),
                    new[] { new LaneSpec("lane1", new[]
                    {
                        Enemy("Bruiser", 16, false, Act("Pound", 4, 0)),
                        Enemy("Big bruiser", 24, false, Act("Pound", 4, 0), Act("Guard", 1, 4))
                    }) },
                    GameRules, WorldThreeRoster, 1),
                "Two enemies keep up the pressure; after a Surge, swap Bolt out for its crash turn (or eject it at the wave change).", "World 3", 2, "the cost of an ability: swapping on the attack side");

            // 3-3 (boss): the marksman snipes the strongest attacker (both stats -50% next turn) between grinds and a crush
            var bossBoard = new BoardGraph(
                new[]
                {
                    new SlotNode("R"),
                    new SlotNode("A1", "R", Operation.Add), new SlotNode("A2", "A1", Operation.Multiply),
                    new SlotNode("D1", "R", Operation.Add), new SlotNode("D2", "D1", Operation.Add)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A2"), Chain("def", "lane1", ChainRole.Defense, "D2") });
            var aim = new EnemyActionData("Aim", new IEnemyEffect[] { new AttackEffect(1), new SnipeEffect(0.5f, 1, PilotTargetRule.HighestAttack) });
            var boss = new EncounterCase("World 3-3: The marksman (boss)", "Early",
                new BattleSetup(bossBoard, new List<SlotRule>(),
                    new[] { new LaneSpec("lane1", new[] { Enemy("Marksman", 48, false, Act("Grind", 3, 0), aim, Act("Crush", 6, 0)) }) },
                    GameRules, WorldThreeRoster, 1),
                "Snipes the strongest attacker (-50% to both stats next turn) between a grind and a crush.", "World 3", 3, "Snipe; abilities against it");

            return new[] { first, second, boss };
        }

        // ---------- World 4: the board's math (tolls, divide, bonus slots) ----------

        /// <summary>
        /// The pilots the player has by World 4: the World 3 roster and the World 3 boss's reward pilot Haze (2 ATK / 0 DEF):
        /// its zero defense pays a defense toll for free (but divides by zero), and its ability No Guard halves both sides'
        /// defense for a turn (a way through a charge's guard).
        /// </summary>
        public static IReadOnlyList<PilotStats> WorldFourRoster => WorldThreeRoster
            .Concat(new[] { new PilotStats("Haze", 2f, 0f, null, new NoGuardAbility(1)) })
            .ToList();

        /// <summary>
        /// The fourth world: the board's math. 4-1 a subtract toll in front of a x2 bonus slot (the weakest defense pays
        /// the toll; Haze pays nothing), 4-2 a divide slot (a defense below 1 multiplies, 0 divides by zero), the boss charges
        /// a big unleash (break its guard with Surge or No Guard, or hold it with a multiplied defense).
        /// </summary>
        public static IReadOnlyList<EncounterCase> WorldFour()
        {
            // 4-1: defense = (R - D1 + D2) x2; attack = R + A1
            var firstBoard = new BoardGraph(
                new[]
                {
                    new SlotNode("R"),
                    new SlotNode("A1", "R", Operation.Add),
                    new SlotNode("D1", "R", Operation.Subtract),
                    new SlotNode("D2", "D1", Operation.Add, new NodeBonus(BonusKind.Multiply, 2f))
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A1"), Chain("def", "lane1", ChainRole.Defense, "D2") });
            var first = new EncounterCase("World 4-1: Pay the toll", "Early",
                new BattleSetup(firstBoard, new List<SlotRule>(),
                    new[] { new LaneSpec("lane1", new[] { Enemy("Collector", 18, false, Act("Smash", 6, 0), Act("Hunker", 0, 5), Act("Jab", 2, 0)) }) },
                    GameRules, WorldFourRoster, 1) { OpeningHand = new[] { "Haze" } },
                "Defense is (R - D1 + D2) x2: the weakest defense pays the toll in D1, and Haze's 0 pays nothing. The enemy hunkers (guard 5, no attack): a free turn for No Guard.", "World 4", 1, "a subtract toll in front of a bonus slot; No Guard on a guard turn");

            // 4-2: defense = R / D1 + D2; attack = (R + A1) x A2
            var secondBoard = new BoardGraph(
                new[]
                {
                    new SlotNode("R"),
                    new SlotNode("A1", "R", Operation.Add), new SlotNode("A2", "A1", Operation.Multiply),
                    new SlotNode("D1", "R", Operation.Divide), new SlotNode("D2", "D1", Operation.Add)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A2"), Chain("def", "lane1", ChainRole.Defense, "D2") });
            var second = new EncounterCase("World 4-2: Divide and conquer", "Early",
                new BattleSetup(secondBoard, new List<SlotRule>(),
                    new[] { new LaneSpec("lane1", new[]
                    {
                        Enemy("Brute", 14, false, Act("Slam", 5, 0)),
                        Enemy("Big brute", 22, false, Act("Slam", 6, 0), Act("Brace", 2, 3))
                    }) },
                    GameRules, WorldFourRoster, 1),
                "Defense is R / D1 + D2: dividing by a defense below 1 multiplies (Brash or Spark double the root); Haze's 0 divides by zero.", "World 4", 2, "divide: a stat below 1 multiplies");

            // 4-3 (boss): attack = (R - A1 + A2) x2; defense = (R + D1) x D2; the ram charges (guard 8) before a big unleash
            var bossBoard = new BoardGraph(
                new[]
                {
                    new SlotNode("R"),
                    new SlotNode("A1", "R", Operation.Subtract),
                    new SlotNode("A2", "A1", Operation.Add, new NodeBonus(BonusKind.Multiply, 2f)),
                    new SlotNode("D1", "R", Operation.Add), new SlotNode("D2", "D1", Operation.Multiply)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A2"), Chain("def", "lane1", ChainRole.Defense, "D2") });
            var charge = new EnemyActionData("Charge", new IEnemyEffect[] { new ChargeEffect(8) });
            var unleash = new EnemyActionData("Unleash", new IEnemyEffect[] { new UnleashEffect(9) });
            var boss = new EncounterCase("World 4-3: The ram (boss)", "Early",
                new BattleSetup(bossBoard, new List<SlotRule>(),
                    new[] { new LaneSpec("lane1", new[] { Enemy("Ram", 40, false, Act("Butt", 3, 0), charge, unleash) }) },
                    GameRules, WorldFourRoster, 1),
                "Charges a guard of 8 before a 9 unleash: break the guard (Surge, No Guard) to stun it, or hold it with the multiplied defense.", "World 4", 3, "Charge; tolls, bonus and multiplied defense together");

            return new[] { first, second, boss };
        }

        // ---------- World 5: two lanes ----------

        /// <summary>The pilots the player has by World 5: the World 4 roster (no new pilot yet; the reward is decided with the economy).</summary>
        public static IReadOnlyList<PilotStats> WorldFiveRoster => WorldFourRoster;

        // a hub root feeding an attack and a defense chain in each lane; children left to right: left lane, then right lane
        private static BoardGraph HubBoard(Operation leftAttack, Operation leftDefense, Operation rightAttack, Operation rightDefense)
        {
            return new BoardGraph(
                new[]
                {
                    new SlotNode("H"),
                    new SlotNode("LA", "H", leftAttack), new SlotNode("LD", "H", leftDefense),
                    new SlotNode("RD", "H", rightDefense), new SlotNode("RA", "H", rightAttack)
                },
                new[]
                {
                    Chain("left-atk", "left", ChainRole.Attack, "LA"), Chain("left-def", "left", ChainRole.Defense, "LD"),
                    Chain("right-atk", "right", ChainRole.Attack, "RA"), Chain("right-def", "right", ChainRole.Defense, "RD")
                });
        }

        /// <summary>
        /// The fifth world: two lanes, an enemy in each. 5-1 two separate fronts sharing 3 AP (which to build, which enemy
        /// to kill first); 5-2 a hub root that feeds both lanes (strong, but worn from both sides, and losing it cuts all four
        /// chains); the boss locks the hub pilot before a crush while a dog bites from the other lane.
        /// </summary>
        public static IReadOnlyList<EncounterCase> WorldFive()
        {
            // 5-1: two separate trees, one per lane
            var firstBoard = new BoardGraph(
                new[]
                {
                    new SlotNode("L"), new SlotNode("LA", "L", Operation.Add), new SlotNode("LD", "L", Operation.Add),
                    new SlotNode("R"), new SlotNode("RD", "R", Operation.Add), new SlotNode("RA", "R", Operation.Add)
                },
                new[]
                {
                    Chain("left-atk", "left", ChainRole.Attack, "LA"), Chain("left-def", "left", ChainRole.Defense, "LD"),
                    Chain("right-atk", "right", ChainRole.Attack, "RA"), Chain("right-def", "right", ChainRole.Defense, "RD")
                });
            var first = new EncounterCase("World 5-1: Two fronts", "Early",
                new BattleSetup(firstBoard, new List<SlotRule>(),
                    new[]
                    {
                        new LaneSpec("left", new[] { Enemy("Jabber", 8, false, Act("Jab", 1, 0), Act("Jab", 2, 0)) }),
                        new LaneSpec("right", new[] { Enemy("Bruiser", 12, false, Act("Pound", 3, 0), Act("Guard", 1, 2)) })
                    },
                    GameRules, WorldFiveRoster, 1),
                "Two enemies, one per lane, each with its own small tree: 3 AP for 6 slots, so pick a front and a kill order.", "World 5", 1, "two lanes: each enemy hits its own lane");

            // 5-2: the hub
            var second = new EncounterCase("World 5-2: The hub", "Early",
                new BattleSetup(HubBoard(Operation.Add, Operation.Add, Operation.Add, Operation.Add), new List<SlotRule>(),
                    new[]
                    {
                        new LaneSpec("left", new[] { Enemy("Raider", 10, false, Act("Hit", 2, 0)) }),
                        new LaneSpec("right", new[] { Enemy("Grunt", 8, false, Act("Hit", 2, 0)), Enemy("Brute", 14, false, Act("Slam", 3, 0), Act("Brace", 1, 2)) })
                    },
                    GameRules, WorldFiveRoster, 1),
                "One hub slot feeds all four chains: strong, but both enemies wear it, and losing it cuts every chain.", "World 5", 2, "a hub shared by both lanes");

            // 5-3 (boss): the warden locks the hub pilot, then crushes its lane; the dog keeps biting the other one
            var pin = new EnemyActionData("Pin", new IEnemyEffect[] { new AttackEffect(1), new LockEffect(2, PilotTargetRule.Trunk) });
            var boss = new EncounterCase("World 5-3: The warden (boss)", "Early",
                new BattleSetup(HubBoard(Operation.Add, Operation.Add, Operation.Multiply, Operation.Add), new List<SlotRule>(),
                    new[]
                    {
                        new LaneSpec("left", new[] { Enemy("Warden", 26, false, Act("Jab", 2, 0), pin, Act("Crush", 5, 0)) }),
                        new LaneSpec("right", new[] { Enemy("Guard dog", 10, false, Act("Bite", 2, 0)) })
                    },
                    GameRules, WorldFiveRoster, 1),
                "The warden locks the hub pilot (it cannot be swapped out) and crushes next turn; swap the hub before the lock lands.", "World 5", 3, "Lock on a shared hub; two lanes");

            return new[] { first, second, boss };
        }

        /// <summary>
        /// A first check of Surge: the enemy alternates a shell (defense 8) and an open turn, so a Surge is worth much more
        /// on an open turn; the attack branch ends in a multiply slot, where a doubled pilot counts most.
        /// </summary>
        public static EncounterCase AbilitySurgeSample()
        {
            var board = new BoardGraph(
                new[]
                {
                    new SlotNode("R"),
                    new SlotNode("A1", "R", Operation.Add), new SlotNode("A2", "A1", Operation.Multiply),
                    new SlotNode("D1", "R", Operation.Add)
                },
                new[] { Chain("atk", "lane1", ChainRole.Attack, "A2"), Chain("def", "lane1", ChainRole.Defense, "D1") });
            var shellback = Enemy("Shellback", 40, false, Act("Shell", 2, 8), Act("Open", 3, 0));
            return new EncounterCase("Ability sample: Surge timing", "Early",
                new BattleSetup(board, new List<SlotRule>(), new[] { new LaneSpec("lane1", new[] { shellback }) }, GameRules, WorldThreeRoster, 1),
                "The enemy shells (defense 8) and opens in turn; a Surge counts most on an open turn and in the multiply slot.", null, 0, "timing an ability");
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
                new BattleSetup(board, new List<SlotRule>(), new[] { new LaneSpec("lane1", new[] { warden }) }, GameRules, StarterRoster, 1),
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
