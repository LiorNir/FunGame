using System;
using System.Collections.Generic;

namespace FunGame.Progress
{
    /// <summary>
    /// What the player has achieved, as saved between sessions: cleared encounters, unspent SP, recruited pilots and the
    /// upgrades bought for them. Plain data with public fields so it can be written as JSON; the rules that read and change
    /// it live in <see cref="Progression"/>.
    /// </summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        /// <summary>The save format version, for reading older saves later.</summary>
        public int Version = 1;

        public int SkillPoints;

        /// <summary>Ids of the encounters cleared at least once (see <see cref="WorldPlan.EncounterIds"/>).</summary>
        public List<string> ClearedEncounters = new List<string>();

        /// <summary>Ids of the pilots the player has, in the order they joined.</summary>
        public List<string> OwnedPilots = new List<string>();

        public List<PilotUpgrade> Upgrades = new List<PilotUpgrade>();

        /// <summary>Ids of the tutorial lines already shown (each is shown once per save).</summary>
        public List<string> SeenTutorials = new List<string>();

        /// <summary>A fresh profile that owns the campaign's starting pilots.</summary>
        public static PlayerProfile StartOf(CampaignPlan plan)
        {
            var profile = new PlayerProfile();
            profile.OwnedPilots.AddRange(plan.StartingPilots);
            return profile;
        }
    }

    /// <summary>The stat points bought for one pilot (each +1 to attack or defense).</summary>
    [Serializable]
    public sealed class PilotUpgrade
    {
        public string PilotId;
        public int Attack;
        public int Defense;
    }

    /// <summary>One of a pilot's two stats.</summary>
    public enum PilotStat
    {
        Attack,
        Defense
    }
}
