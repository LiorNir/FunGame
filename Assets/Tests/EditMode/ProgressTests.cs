using FunGame.Progress;
using NUnit.Framework;

namespace FunGame.Board.Tests
{
    public class ProgressTests
    {
        private static CampaignPlan Plan()
        {
            return new CampaignPlan(new[] { "Osis", "Quill" }, new[]
            {
                new WorldPlan("World 1", new[] { "1-1", "1-2", "1-3" }, new[] { 1, 1, 1 }, new[] { "Spark" }),
                new WorldPlan("World 2", new[] { "2-1", "2-2" }, new[] { 1, 1 }, new string[0])
            });
        }

        private static Progression Fresh()
        {
            var plan = Plan();
            return new Progression(plan, PlayerProfile.StartOf(plan));
        }

        [Test]
        public void ANewPlayer_HasTheStartingPilots_AndOnlyTheFirstEncounterOpen()
        {
            var progression = Fresh();
            CollectionAssert.AreEqual(new[] { "Osis", "Quill" }, progression.Profile.OwnedPilots);
            Assert.AreEqual(EncounterState.Open, progression.StateOf(0, 0));
            Assert.AreEqual(EncounterState.Locked, progression.StateOf(0, 1));
            Assert.AreEqual(EncounterState.Locked, progression.StateOf(1, 0));
        }

        [Test]
        public void ClearingAnEncounter_OpensTheNext_AndTheBossOpensTheNextWorld()
        {
            var progression = Fresh();
            progression.ClaimVictory("1-1");
            Assert.AreEqual(EncounterState.Cleared, progression.StateOf(0, 0));
            Assert.AreEqual(EncounterState.Open, progression.StateOf(0, 1));
            Assert.IsFalse(progression.IsWorldOpen(1));

            progression.ClaimVictory("1-2");
            progression.ClaimVictory("1-3");
            Assert.AreEqual(EncounterState.Open, progression.StateOf(1, 0));
        }

        [Test]
        public void AFirstClear_GivesSp_AndTheBossRecruitsItsPilot_ARepeatGivesNothing()
        {
            var progression = Fresh();
            progression.ClaimVictory("1-1");
            progression.ClaimVictory("1-2");
            var boss = progression.ClaimVictory("1-3");

            Assert.IsTrue(boss.FirstClear);
            CollectionAssert.AreEqual(new[] { "Spark" }, boss.NewPilots);
            Assert.AreEqual(3, progression.Profile.SkillPoints);
            Assert.IsTrue(progression.Profile.OwnedPilots.Contains("Spark"));

            var repeat = progression.ClaimVictory("1-3");
            Assert.IsFalse(repeat.FirstClear);
            Assert.AreEqual(3, progression.Profile.SkillPoints);
            Assert.AreEqual(3, progression.Profile.OwnedPilots.Count);
        }

        [Test]
        public void AnUpgrade_CostsSp_AndCanBeTakenBack()
        {
            var progression = Fresh();
            Assert.IsFalse(progression.TryUpgrade("Quill", PilotStat.Defense)); // no SP yet

            progression.Profile.SkillPoints = 1;
            Assert.IsTrue(progression.TryUpgrade("Quill", PilotStat.Defense));
            Assert.AreEqual(0, progression.Profile.SkillPoints);
            Assert.AreEqual((0, 1), progression.UpgradesOf("Quill"));
            Assert.AreEqual(2, progression.LevelOf("Quill"));
            Assert.AreEqual(1, progression.LevelOf("Osis"));
            Assert.IsFalse(progression.TryUpgrade("Spark", PilotStat.Attack)); // not owned

            progression.ResetUpgrades("Quill");
            Assert.AreEqual(1, progression.Profile.SkillPoints);
            Assert.AreEqual((0, 0), progression.UpgradesOf("Quill"));
        }

        [Test]
        public void EachFurtherUpgradeOfTheSameStat_CostsOneMore_AndARefundReturnsItAll()
        {
            var progression = Fresh();
            progression.Profile.SkillPoints = 7;
            Assert.AreEqual(1, progression.CostOf("Osis", PilotStat.Attack));
            Assert.IsTrue(progression.TryUpgrade("Osis", PilotStat.Attack)); // 1
            Assert.AreEqual(2, progression.CostOf("Osis", PilotStat.Attack));
            Assert.AreEqual(1, progression.CostOf("Osis", PilotStat.Defense)); // the other stat starts over
            Assert.IsTrue(progression.TryUpgrade("Osis", PilotStat.Attack)); // 2
            Assert.IsTrue(progression.TryUpgrade("Osis", PilotStat.Attack)); // 3
            Assert.AreEqual(1, progression.Profile.SkillPoints);
            Assert.IsFalse(progression.CanUpgrade("Osis", PilotStat.Attack)); // the next costs 4

            progression.ResetUpgrades("Osis");
            Assert.AreEqual(7, progression.Profile.SkillPoints);
        }

        [Test]
        public void AnOldSave_IsSwitchedFromNamesToIds_KeepingWhatItCannotMap()
        {
            var profile = new PlayerProfile { Version = 1 };
            profile.OwnedPilots.AddRange(new[] { "Bolt", "Quill", "Mystery" });
            profile.ClearedEncounters.Add("1-1");
            profile.Upgrades.Add(new PilotUpgrade { PilotId = "Bolt", Attack = 2 });
            var ids = new System.Collections.Generic.Dictionary<string, string> { { "Bolt", "id-bee" }, { "Quill", "id-quill" }, { "1-1", "id-11" } };

            ProfileMigration.Upgrade(profile, ProfileMigration.Lookup(ids));

            CollectionAssert.AreEqual(new[] { "id-bee", "id-quill", "Mystery" }, profile.OwnedPilots);
            CollectionAssert.AreEqual(new[] { "id-11" }, profile.ClearedEncounters);
            Assert.AreEqual("id-bee", profile.Upgrades[0].PilotId);
            Assert.AreEqual(ProfileMigration.CurrentVersion, profile.Version);

            ProfileMigration.Upgrade(profile, ProfileMigration.Lookup(new System.Collections.Generic.Dictionary<string, string> { { "id-bee", "wrong" } }));
            Assert.AreEqual("id-bee", profile.OwnedPilots[0]); // an up-to-date save is left alone
        }

        [Test]
        public void UnlockAll_OpensEverything()
        {
            var progression = Fresh();
            progression.UnlockAll = true;
            Assert.AreEqual(EncounterState.Open, progression.StateOf(1, 1));
        }

        // ---------- stars ----------

        private static int Rate(bool won, float health, int lost, int turns)
        {
            return StarRating.Evaluate(StarRating.Default, new BattleEnd(won, health, 10f, lost, turns));
        }

        [Test]
        public void AWin_EarnsEachStarWhoseConditionItMeets()
        {
            Assert.AreEqual(0b111, Rate(true, 6f, 0, 5)); // 6 HP is exactly 60%
            Assert.AreEqual(0b101, Rate(true, 5.75f, 0, 5)); // short of the health star
            Assert.AreEqual(0b011, Rate(true, 9f, 1, 5)); // a pilot lost
        }

        [Test]
        public void ALostBattle_EarnsNoStar_EvenWithHealthAndPilotsLeft()
        {
            Assert.AreEqual(0, Rate(false, 9f, 0, 3)); // for example given up early
        }

        [Test]
        public void NoStarIsRequiredForAnother()
        {
            var stars = new[] { new StarCondition(StarConditionKind.WinWithoutLosses), new StarCondition(StarConditionKind.WinWithinTurns, 4f) };
            Assert.AreEqual(0b10, StarRating.Evaluate(stars, new BattleEnd(true, 2f, 10f, 2, 4)));
            Assert.AreEqual(0b01, StarRating.Evaluate(stars, new BattleEnd(true, 2f, 10f, 0, 5)));
        }

        [Test]
        public void EarnedStars_StayEarned_AcrossReplays()
        {
            var progression = Fresh();
            Assert.AreEqual(0b011, progression.RecordStars("1-1", 0b011));
            Assert.AreEqual(0b100, progression.RecordStars("1-1", 0b101)); // only the third is new
            Assert.AreEqual(0, progression.RecordStars("1-1", 0b001)); // a worse replay takes nothing away
            Assert.AreEqual(0b111, progression.StarsOf("1-1"));
            Assert.AreEqual(3, progression.TotalStars);
            Assert.AreEqual(0, progression.StarsOf("1-2"));
        }

        [Test]
        public void DuringTheBattle_AStarIsLostOnceItCanNoLongerBeMet()
        {
            BattleEnd Now(float health, int lost, int turn) => new BattleEnd(false, health, 10f, lost, turn, finished: false);
            var health = new StarCondition(StarConditionKind.WinWithHealth, 0.8f);
            var noLoss = new StarCondition(StarConditionKind.WinWithoutLosses);
            var quick = new StarCondition(StarConditionKind.WinWithinTurns, 4f);
            var clear = new StarCondition(StarConditionKind.Clear);

            Assert.IsTrue(health.IsStillPossible(Now(8f, 0, 2)));
            Assert.IsFalse(health.IsStillPossible(Now(7.75f, 0, 2)));
            Assert.IsTrue(noLoss.IsStillPossible(Now(10f, 0, 2)));
            Assert.IsFalse(noLoss.IsStillPossible(Now(10f, 1, 2)));
            Assert.IsTrue(quick.IsStillPossible(Now(10f, 0, 4)));
            Assert.IsFalse(quick.IsStillPossible(Now(10f, 0, 5)));
            Assert.IsTrue(clear.IsStillPossible(Now(1f, 3, 9)));

            // once it is over, a lost battle keeps none of them
            Assert.IsFalse(clear.IsStillPossible(new BattleEnd(false, 5f, 10f, 0, 3)));
            Assert.IsTrue(health.IsStillPossible(new BattleEnd(true, 9f, 10f, 0, 3)));
        }

        [Test]
        public void TheHealthStar_NeedsItsShareRoundedUpToTheQuarterStep()
        {
            var star = new StarCondition(StarConditionKind.WinWithHealth, 0.65f);
            Assert.AreEqual(6.5f, star.RequiredHealth(10f), 0.0001f);
            Assert.AreEqual("Win with 6.5+ HP", star.Describe(10f));
        }
    }
}
