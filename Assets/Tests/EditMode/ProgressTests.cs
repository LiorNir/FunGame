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

            progression.Profile.SkillPoints = Progression.UpgradeCost;
            Assert.AreEqual(1, Progression.UpgradeCost);
            Assert.IsTrue(progression.TryUpgrade("Quill", PilotStat.Defense));
            Assert.AreEqual(0, progression.Profile.SkillPoints);
            Assert.AreEqual((0, 1), progression.UpgradesOf("Quill"));
            Assert.AreEqual(2, progression.LevelOf("Quill"));
            Assert.AreEqual(1, progression.LevelOf("Osis"));
            Assert.IsFalse(progression.TryUpgrade("Spark", PilotStat.Attack)); // not owned

            progression.ResetUpgrades("Quill");
            Assert.AreEqual(Progression.UpgradeCost, progression.Profile.SkillPoints);
            Assert.AreEqual((0, 0), progression.UpgradesOf("Quill"));
        }

        [Test]
        public void UnlockAll_OpensEverything()
        {
            var progression = Fresh();
            progression.UnlockAll = true;
            Assert.AreEqual(EncounterState.Open, progression.StateOf(1, 1));
        }
    }
}
