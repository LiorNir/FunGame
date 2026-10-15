using System;
using System.Collections.Generic;
using System.Linq;

namespace FunGame.Progress
{
    /// <summary>Brings an older save up to date.</summary>
    public static class ProfileMigration
    {
        /// <summary>The save version written today: pilots and encounters are stored by stable asset id.</summary>
        public const int CurrentVersion = 2;

        /// <summary>
        /// Version 1 stored pilots and encounters by asset name; version 2 by stable id. Every name the lookup knows is
        /// replaced by its id (unknown ones are kept, so nothing is lost silently).
        /// </summary>
        public static void Upgrade(PlayerProfile profile, Func<string, string> idOfName)
        {
            if (profile.Version >= CurrentVersion)
            {
                return;
            }

            string Map(string name)
            {
                return idOfName(name) ?? name;
            }

            profile.OwnedPilots = profile.OwnedPilots.Select(Map).Distinct().ToList();
            profile.ClearedEncounters = profile.ClearedEncounters.Select(Map).Distinct().ToList();
            foreach (var upgrade in profile.Upgrades)
            {
                upgrade.PilotId = Map(upgrade.PilotId);
            }

            profile.Version = CurrentVersion;
        }

        /// <summary>A lookup for <see cref="Upgrade"/> from known names to ids.</summary>
        public static Func<string, string> Lookup(IDictionary<string, string> idsByName)
        {
            return name => name != null && idsByName.TryGetValue(name, out var id) ? id : null;
        }
    }
}
