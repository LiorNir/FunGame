using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoardAuthoring;
using Combat;
using Configurations;
using Encounters;
using FunGame.Battle;
using FunGame.Sim;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Turns encounters defined in code for the balance lab (<see cref="BuiltInEncounters"/>) into real Unity assets: a board
    /// prefab, enemy assets and an encounter asset each, so they can be played in the battle scene. This is a one-time
    /// import: from then on the assets are the source and can be edited freely. Existing encounter assets are never
    /// overwritten (delete one to import it again).
    /// </summary>
    public static class BuiltInEncounterAssetCreator
    {
        private const string BoardsFolder = "Assets/Boards";
        private const string EncountersFolder = "Assets/Encounters";
        private const string DecksFolder = "Assets/Rules";
        private const string PilotsFolder = "Assets/Scripts/Configurations/Pilots";

        [MenuItem("Tools/Fun Game/Create Encounter Assets/World 1")]
        private static void CreateWorldOne()
        {
            Create("World 1", BuiltInEncounters.WorldOne());
        }

        [MenuItem("Tools/Fun Game/Create Encounter Assets/World 2")]
        private static void CreateWorldTwo()
        {
            var deck = DeckFromRoster(BuiltInEncounters.WorldTwoRoster, $"{DecksFolder}/World 2 Deck.asset");
            Create("World 2", BuiltInEncounters.WorldTwo(), deck);
        }

        [MenuItem("Tools/Fun Game/Create Encounter Assets/Lock Sample")]
        private static void CreateLockSample()
        {
            Create("Samples", new[] { BuiltInEncounters.LockPinnedDown() });
        }

        /// <param name="testDeck">Optional: the deck these encounters are played with (see <see cref="EncounterDefinition.TestDeck"/>).</param>
        public static void Create(string groupName, IEnumerable<EncounterCase> encounters, PilotDeck testDeck = null)
        {
            var boardFolder = EnsureFolder(BoardsFolder, groupName);
            var encounterFolder = EnsureFolder(EncountersFolder, groupName);
            var created = new List<string>();
            var skipped = new List<string>();

            foreach (var encounter in encounters)
            {
                var fileName = FileName(encounter.Name);
                var encounterPath = $"{encounterFolder}/{fileName}.asset";
                if (AssetDatabase.LoadAssetAtPath<EncounterDefinition>(encounterPath) != null)
                {
                    skipped.Add(encounter.Name);
                    continue;
                }

                var setup = encounter.Setup;
                var boardPrefab = BoardPrefabFromGraph.Create(fileName, setup.Board,
                    AssetDatabase.GenerateUniqueAssetPath($"{boardFolder}/{fileName}.prefab"), setup.SlotRules);

                var lanes = setup.Lanes
                    .Select(lane => new LaneEnemies(lane.Id, lane.Enemies.Select(enemy => CreateEnemy(enemy, encounterFolder)).ToList()))
                    .ToList();

                var asset = ScriptableObject.CreateInstance<EncounterDefinition>();
                asset.SetData(encounter.Name, boardPrefab.GetComponent<BoardLayoutAuthoring>(), lanes, 0);
                asset.SetAllowedTools(setup.Rules.AllowReplace, setup.Rules.AllowEject);
                asset.SetTestDeck(testDeck);
                AssetDatabase.CreateAsset(asset, encounterPath);
                created.Add(encounter.Name);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{groupName}: created {created.Count} encounter(s) in {encounterFolder} (boards in {boardFolder})" +
                      (created.Count > 0 ? $": {string.Join(", ", created)}" : string.Empty) +
                      (skipped.Count > 0 ? $". Already there, left as they are: {string.Join(", ", skipped)}" : string.Empty) + ".");
        }

        /// <summary>
        /// A deck asset holding the lab roster's pilots, in its order. Each pilot is found among the pilot assets by display
        /// name; if the roster's stats differ (an upgrade), an upgraded copy with the same picture is made in Pilots/Upgrades
        /// ("Quill (1-3)"). An existing deck asset is kept as it is.
        /// </summary>
        public static PilotDeck DeckFromRoster(IReadOnlyList<FunGame.Board.PilotStats> roster, string path)
        {
            var existingDeck = AssetDatabase.LoadAssetAtPath<PilotDeck>(path);
            if (existingDeck != null)
            {
                return existingDeck;
            }

            var pilots = AssetDatabase.FindAssets("t:PilotTokenConfiguration", new[] { PilotsFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<PilotTokenConfiguration>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(pilot => pilot != null)
                .ToList();
            var chosen = new List<PilotTokenConfiguration>();
            foreach (var stats in roster)
            {
                var named = pilots.Where(pilot => pilot.DisplayName == stats.Name).ToList();
                var exact = named.FirstOrDefault(pilot => Mathf.Approximately(pilot.AttackValue, stats.Attack) && Mathf.Approximately(pilot.DefenseValue, stats.Defense));
                if (exact != null)
                {
                    chosen.Add(exact);
                    continue;
                }

                var basePilot = named.FirstOrDefault();
                if (basePilot == null)
                {
                    Debug.LogWarning($"No pilot asset is named '{stats.Name}'; left out of {path}.");
                    continue;
                }

                var upgradesFolder = EnsureFolder(PilotsFolder, "Upgrades");
                var upgraded = Object.Instantiate(basePilot);
                upgraded.AttackValue = stats.Attack;
                upgraded.DefenseValue = stats.Defense;
                AssetDatabase.CreateAsset(upgraded, AssetDatabase.GenerateUniqueAssetPath(
                    $"{upgradesFolder}/{stats.Name} ({stats.Attack:0.##}-{stats.Defense:0.##}).asset"));
                pilots.Add(upgraded);
                chosen.Add(upgraded);
            }

            var deck = ScriptableObject.CreateInstance<PilotDeck>();
            deck.SetData(chosen);
            AssetDatabase.CreateAsset(deck, path);
            return deck;
        }

        /// <summary>One enemy asset per enemy name in the folder; an enemy shared by several encounters is created once.</summary>
        private static EnemyDefinition CreateEnemy(EnemySpec spec, string folder)
        {
            var path = $"{folder}/{FileName(spec.Name)}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null)
            {
                return existing;
            }

            var enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            enemy.SetData(spec.Name, spec.MaxHealth,
                spec.RandomSelection ? EnemyActionSelectionMode.Random : EnemyActionSelectionMode.Sequential,
                spec.Actions.Select(ToAction).ToList());
            AssetDatabase.CreateAsset(enemy, path);
            return enemy;
        }

        /// <summary>The asset form of an action: attack and defense, and a lock if the action has one.</summary>
        private static EnemyAction ToAction(EnemyActionData data)
        {
            var action = new EnemyAction { Name = data.Name, Attack = data.Attack, Defense = data.Defense };
            var lockEffect = data.Effects.OfType<LockEffect>().FirstOrDefault();
            if (lockEffect != null)
            {
                action.LockTurns = lockEffect.Turns;
                action.LockTarget = lockEffect.TargetRule;
            }

            var unsupported = data.Effects.Where(effect => !(effect is AttackEffect) && !(effect is DefendEffect) && !(effect is LockEffect)).ToList();
            if (unsupported.Count > 0)
            {
                Debug.LogWarning($"Action '{data.Name}': enemy assets cannot hold {string.Join(", ", unsupported.Select(effect => effect.Describe()))} yet; left out.");
            }

            return action;
        }

        private static string EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(parent).Replace('\\', '/'), Path.GetFileName(parent));
            }

            var folder = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(parent, name);
            }

            return folder;
        }

        /// <summary>A file name made from a display name ("World 1-1: First steps" -> "World 1-1 First steps").</summary>
        private static string FileName(string displayName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(displayName.Where(character => !invalid.Contains(character)).ToArray());
            return string.Join(" ", cleaned.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
