using System.Collections.Generic;

namespace FunGame.Progress
{
    /// <summary>The campaign's structure as plain data: the pilots a new player has and the worlds in order.</summary>
    public sealed class CampaignPlan
    {
        public IReadOnlyList<string> StartingPilots { get; }
        public IReadOnlyList<WorldPlan> Worlds { get; }

        public CampaignPlan(IReadOnlyList<string> startingPilots, IReadOnlyList<WorldPlan> worlds)
        {
            StartingPilots = startingPilots;
            Worlds = worlds;
        }
    }

    /// <summary>
    /// One world: its encounters in play order (the last is the boss), the SP each first clear gives and the pilots that
    /// join when the boss is first beaten.
    /// </summary>
    public sealed class WorldPlan
    {
        public string Name { get; }
        public IReadOnlyList<string> EncounterIds { get; }
        public IReadOnlyList<int> SkillPointRewards { get; }
        public IReadOnlyList<string> RewardPilots { get; }

        public WorldPlan(string name, IReadOnlyList<string> encounterIds, IReadOnlyList<int> skillPointRewards, IReadOnlyList<string> rewardPilots)
        {
            Name = name;
            EncounterIds = encounterIds;
            SkillPointRewards = skillPointRewards;
            RewardPilots = rewardPilots;
        }

        public string BossId => EncounterIds.Count == 0 ? null : EncounterIds[EncounterIds.Count - 1];
    }
}
