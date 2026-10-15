using System.Collections.Generic;
using System.IO;
using System.Linq;
using Configurations;
using Encounters;
using FunGame.Progress;
using UnityEngine;

namespace Campaign
{
    /// <summary>
    /// The campaign in play, kept across scene loads: the campaign asset, the player's profile (loaded from and saved to
    /// a JSON file) and the encounter the map asked the battle scene to play.
    /// </summary>
    public static class CampaignSession
    {
        private const string UnlockAllKey = "FunGame.UnlockAll";

        public static CampaignDefinition Campaign { get; private set; }
        public static PlayerProfile Profile { get; private set; }

        /// <summary>The encounter chosen on the map, for the battle scene; null when the battle scene runs on its own.</summary>
        public static EncounterDefinition PendingEncounter { get; set; }

        /// <summary>The save file: one profile, in the player's data folder.</summary>
        public static string SavePath => Path.Combine(Application.persistentDataPath, "profile.json");

        /// <summary>For testing: every encounter counts as open (kept on this computer, not in the save).</summary>
        public static bool UnlockAll
        {
            get => PlayerPrefs.GetInt(UnlockAllKey, 0) == 1;
            set => PlayerPrefs.SetInt(UnlockAllKey, value ? 1 : 0);
        }

        public static Progression Progression => new Progression(Campaign.ToPlan(), Profile) { UnlockAll = UnlockAll };

        /// <summary>Loads the profile for the campaign (or starts a new one) unless it is already loaded.</summary>
        public static void Begin(CampaignDefinition campaign)
        {
            if (Campaign == campaign && Profile != null)
            {
                return;
            }

            Campaign = campaign;
            Profile = Load(campaign);
        }

        public static void Save()
        {
            if (Profile != null)
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(Profile, true));
            }
        }

        public static void ResetProgress()
        {
            Profile = PlayerProfile.StartOf(Campaign.ToPlan());
            Save();
        }

        /// <summary>
        /// The player's pilots as a deck for the battle: every owned pilot with its upgrades applied (runtime copies, the
        /// pilot assets are not changed).
        /// </summary>
        public static PilotDeck BuildDeck()
        {
            var progression = Progression;
            var pilots = new List<PilotTokenConfiguration>();
            foreach (var id in Profile.OwnedPilots)
            {
                var pilot = Campaign.FindPilot(id);
                if (pilot == null)
                {
                    Debug.LogWarning($"The save names a pilot '{id}' the campaign does not have; left out.");
                    continue;
                }

                var (attack, defense) = progression.UpgradesOf(id);
                if (attack == 0 && defense == 0)
                {
                    pilots.Add(pilot);
                    continue;
                }

                var upgraded = Object.Instantiate(pilot);
                upgraded.name = pilot.name;
                upgraded.AttackValue += attack;
                upgraded.DefenseValue += defense;
                pilots.Add(upgraded);
            }

            var deck = ScriptableObject.CreateInstance<PilotDeck>();
            deck.SetData(pilots);
            return deck;
        }

        private static PlayerProfile Load(CampaignDefinition campaign)
        {
            var plan = campaign.ToPlan();
            if (File.Exists(SavePath))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(SavePath));
                    if (loaded != null && loaded.OwnedPilots.Count > 0)
                    {
                        if (loaded.Version < ProfileMigration.CurrentVersion)
                        {
                            // an older save names pilots and encounters by asset name: switch it to stable ids
                            ProfileMigration.Upgrade(loaded, ProfileMigration.Lookup(campaign.IdsByName()));
                            File.WriteAllText(SavePath, JsonUtility.ToJson(loaded, true));
                        }

                        return loaded;
                    }
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"Could not read the save file ({exception.Message}); starting a new profile.");
                }
            }

            return PlayerProfile.StartOf(plan);
        }

        /// <summary>A short line about a reward, for the map and the end screen: "+1 SP, Spark joins".</summary>
        public static string Describe(VictoryReward reward)
        {
            if (!reward.FirstClear)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (reward.SkillPoints > 0)
            {
                parts.Add($"+{reward.SkillPoints} SP");
            }

            parts.AddRange(reward.NewPilots.Select(id =>
            {
                var pilot = Campaign.FindPilot(id);
                return (pilot != null && !string.IsNullOrEmpty(pilot.DisplayName) ? pilot.DisplayName : id) + " joins";
            }));
            return string.Join(", ", parts);
        }
    }
}
