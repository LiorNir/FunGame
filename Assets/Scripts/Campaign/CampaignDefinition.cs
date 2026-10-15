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

        /// <summary>The id a save file uses for a pilot or an encounter: a stable asset id that survives renames.</summary>
        public static string IdOf(UnityEngine.Object asset)
        {
            switch (asset)
            {
                case PilotTokenConfiguration pilot:
                    return pilot.AssetId;
                case EncounterDefinition encounter:
                    return encounter.AssetId;
                default:
                    return asset != null ? asset.name : null;
            }
        }

        /// <summary>
        /// Asset names to ids for every pilot and encounter of the campaign, plus the names of pilot files renamed since
        /// saves stored names (version 1 saves are read through this, see <see cref="ProfileMigration"/>).
        /// </summary>
        public Dictionary<string, string> IdsByName()
        {
            var ids = new Dictionary<string, string>
            {
                // pilot asset files renamed on 2026-10-15: Bolt -> Bee, Brash -> GreenSlime, Spark -> ElectricSnake, Squirrel -> OldTurtle
                { "Bolt", "6bb03e5842ccc634c9486c36de185362" },
                { "Brash", "0b2595a4689e31145be45ab9682d5e19" },
                { "Spark", "1a34a0706bdd2024784464d98486794b" },
                { "Squirrel", "0d12c5cd38cce124fb76a6db43177f81" }
            };
            var assets = _startingPilots.Concat(_worlds.SelectMany(world => world.RewardPilots)).Cast<UnityEngine.Object>()
                .Concat(_worlds.SelectMany(world => world.Encounters));
            foreach (var asset in assets.Where(asset => asset != null))
            {
                ids[asset.name] = IdOf(asset);
            }

            return ids;
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
