using System;
using System.Collections.Generic;
using System.Linq;
using Configurations;
using Encounters;
using FunGame.Progress;
using UnityEngine;

namespace Campaign
{
    /// <summary>
    /// The campaign as an asset: the pilots a new player starts with and the worlds in play order (encounters, the last
    /// being the boss, and the pilots the boss recruits). Encounters and pilots are identified by their asset names, which
    /// is what the save file stores.
    /// </summary>
    [CreateAssetMenu(fileName = "Campaign", menuName = "ScriptableObjects/Campaign")]
    public class CampaignDefinition : ScriptableObject
    {
        [SerializeField] private List<PilotTokenConfiguration> _startingPilots = new List<PilotTokenConfiguration>();
        [SerializeField] private List<CampaignWorld> _worlds = new List<CampaignWorld>();

        public IReadOnlyList<PilotTokenConfiguration> StartingPilots => _startingPilots;
        public IReadOnlyList<CampaignWorld> Worlds => _worlds;

        public static string IdOf(UnityEngine.Object asset)
        {
            return asset != null ? asset.name : null;
        }

        public void SetData(List<PilotTokenConfiguration> startingPilots, List<CampaignWorld> worlds)
        {
            _startingPilots = startingPilots;
            _worlds = worlds;
        }

        /// <summary>The campaign as plain data for the progression rules.</summary>
        public CampaignPlan ToPlan()
        {
            return new CampaignPlan(
                _startingPilots.Where(pilot => pilot != null).Select(IdOf).ToList(),
                _worlds.Select(world => new WorldPlan(
                    world.Name,
                    world.Encounters.Where(encounter => encounter != null).Select(IdOf).ToList(),
                    world.Encounters.Where(encounter => encounter != null).Select(encounter => encounter.RewardSkillPoints).ToList(),
                    world.RewardPilots.Where(pilot => pilot != null).Select(IdOf).ToList())).ToList());
        }

        /// <summary>Every pilot the campaign knows (starting ones and rewards), by id.</summary>
        public PilotTokenConfiguration FindPilot(string id)
        {
            return _startingPilots.Concat(_worlds.SelectMany(world => world.RewardPilots))
                .FirstOrDefault(pilot => pilot != null && IdOf(pilot) == id);
        }
    }

    [Serializable]
    public class CampaignWorld
    {
        public string Name;

        [Tooltip("In play order; the last one is the boss.")]
        public List<EncounterDefinition> Encounters = new List<EncounterDefinition>();

        [Tooltip("Pilots that join the player when the boss is first beaten.")]
        public List<PilotTokenConfiguration> RewardPilots = new List<PilotTokenConfiguration>();
    }
}
