using System.Collections.Generic;
using System.Linq;

namespace FunGame.Progress
{
    public enum EncounterState
    {
        Locked,
        Open,
        Cleared
    }

    /// <summary>What a won battle gave: SP and pilots on a first clear, nothing on a repeat.</summary>
    public sealed class VictoryReward
    {
        public bool FirstClear { get; }
        public int SkillPoints { get; }
        public IReadOnlyList<string> NewPilots { get; }

        public VictoryReward(bool firstClear, int skillPoints, IReadOnlyList<string> newPilots)
        {
            FirstClear = firstClear;
            SkillPoints = skillPoints;
            NewPilots = newPilots;
        }
    }

    /// <summary>
    /// The campaign rules on top of a <see cref="PlayerProfile"/>: what is unlocked, what a win gives and what upgrades
    /// cost. Encounters of a world open one after another; the next world opens when the previous world's boss is beaten.
    /// </summary>
    public sealed class Progression
    {
        /// <summary>SP for one +1 to one stat of one pilot (the boss of each world gives 1 SP: one upgrade per world).</summary>
        public const int UpgradeCost = 1;

        public CampaignPlan Plan { get; }
        public PlayerProfile Profile { get; }

        /// <summary>For testing: every encounter counts as open.</summary>
        public bool UnlockAll { get; set; }

        public Progression(CampaignPlan plan, PlayerProfile profile)
        {
            Plan = plan;
            Profile = profile;
        }

        public bool IsCleared(string encounterId)
        {
            return Profile.ClearedEncounters.Contains(encounterId);
        }

        public bool IsWorldOpen(int worldIndex)
        {
            return UnlockAll || worldIndex == 0 || IsCleared(Plan.Worlds[worldIndex - 1].BossId);
        }

        public EncounterState StateOf(int worldIndex, int encounterIndex)
        {
            var world = Plan.Worlds[worldIndex];
            var id = world.EncounterIds[encounterIndex];
            if (IsCleared(id))
            {
                return EncounterState.Cleared;
            }

            if (!IsWorldOpen(worldIndex))
            {
                return EncounterState.Locked;
            }

            return UnlockAll || encounterIndex == 0 || IsCleared(world.EncounterIds[encounterIndex - 1])
                ? EncounterState.Open
                : EncounterState.Locked;
        }

        /// <summary>What winning the encounter would give now (for showing it before the battle).</summary>
        public VictoryReward RewardFor(string encounterId)
        {
            if (IsCleared(encounterId) || !TryFind(encounterId, out var world, out var index))
            {
                return new VictoryReward(false, 0, new string[0]);
            }

            var skillPoints = index < world.SkillPointRewards.Count ? world.SkillPointRewards[index] : 0;
            var pilots = encounterId == world.BossId
                ? world.RewardPilots.Where(pilot => !Profile.OwnedPilots.Contains(pilot)).ToList()
                : new List<string>();
            return new VictoryReward(true, skillPoints, pilots);
        }

        /// <summary>Records a win: on a first clear the encounter is marked, its SP added and a boss's pilots recruited.</summary>
        public VictoryReward ClaimVictory(string encounterId)
        {
            var reward = RewardFor(encounterId);
            if (!reward.FirstClear)
            {
                return reward;
            }

            Profile.ClearedEncounters.Add(encounterId);
            Profile.SkillPoints += reward.SkillPoints;
            Profile.OwnedPilots.AddRange(reward.NewPilots);
            return reward;
        }

        /// <summary>The stat points bought for the pilot so far.</summary>
        public (int Attack, int Defense) UpgradesOf(string pilotId)
        {
            var upgrade = Profile.Upgrades.FirstOrDefault(entry => entry.PilotId == pilotId);
            return upgrade == null ? (0, 0) : (upgrade.Attack, upgrade.Defense);
        }

        /// <summary>A pilot's level: 1, plus one for every upgrade bought for it.</summary>
        public int LevelOf(string pilotId)
        {
            var (attack, defense) = UpgradesOf(pilotId);
            return 1 + attack + defense;
        }

        public bool CanUpgrade(string pilotId)
        {
            return Profile.OwnedPilots.Contains(pilotId) && Profile.SkillPoints >= UpgradeCost;
        }

        /// <summary>Spends <see cref="UpgradeCost"/> SP on +1 to one stat of an owned pilot.</summary>
        public bool TryUpgrade(string pilotId, PilotStat stat)
        {
            if (!CanUpgrade(pilotId))
            {
                return false;
            }

            var upgrade = Profile.Upgrades.FirstOrDefault(entry => entry.PilotId == pilotId);
            if (upgrade == null)
            {
                upgrade = new PilotUpgrade { PilotId = pilotId };
                Profile.Upgrades.Add(upgrade);
            }

            if (stat == PilotStat.Attack)
            {
                upgrade.Attack++;
            }
            else
            {
                upgrade.Defense++;
            }

            Profile.SkillPoints -= UpgradeCost;
            return true;
        }

        /// <summary>Takes back every upgrade of the pilot and returns its SP (free while testing).</summary>
        public void ResetUpgrades(string pilotId)
        {
            var upgrade = Profile.Upgrades.FirstOrDefault(entry => entry.PilotId == pilotId);
            if (upgrade == null)
            {
                return;
            }

            Profile.SkillPoints += (upgrade.Attack + upgrade.Defense) * UpgradeCost;
            Profile.Upgrades.Remove(upgrade);
        }

        private bool TryFind(string encounterId, out WorldPlan world, out int index)
        {
            foreach (var candidate in Plan.Worlds)
            {
                index = candidate.EncounterIds.ToList().IndexOf(encounterId);
                if (index >= 0)
                {
                    world = candidate;
                    return true;
                }
            }

            world = null;
            index = -1;
            return false;
        }
    }
}
