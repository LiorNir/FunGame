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

        [MenuItem("Tools/Fun Game/Create Encounter Assets/World 3")]
        private static void CreateWorldThree()
        {
            var deck = DeckFromRoster(BuiltInEncounters.WorldThreeRoster, $"{DecksFolder}/World 3 Deck.asset");
            Create("World 3", BuiltInEncounters.WorldThree(), deck);
        }

        [MenuItem("Tools/Fun Game/Create Encounter Assets/Ability Sample")]
        private static void CreateAbilitySample()
        {
            var deck = DeckFromRoster(BuiltInEncounters.WorldThreeRoster, $"{DecksFolder}/World 3 Deck.asset");
            Create("Samples", new[] { BuiltInEncounters.AbilitySurgeSample() }, deck);
        }

        [MenuItem("Tools/Fun Game/Create Encounter Assets/Lock Sample")]
        private static void CreateLockSample()
        {
            Create("Samples", new[] { BuiltInEncounters.LockPinnedDown() });
        }

        /// <param name="testDeck">Optional: the deck these encounters are played with (see <see cref="EncounterDefinition.TestDeck"/>).</param>
        /// <param name="update">
        /// Rewrite encounters that already exist (board, enemies, rules) from the code version, keeping their asset paths. For
        /// iterating on a draft before the user has edited it; the menus never do this.
        /// </param>
        public static void Create(string groupName, IEnumerable<EncounterCase> encounters, PilotDeck testDeck = null, bool update = false)
        {
            var boardFolder = EnsureFolder(BoardsFolder, groupName);
            var encounterFolder = EnsureFolder(EncountersFolder, groupName);
            var created = new List<string>();
            var skipped = new List<string>();

            foreach (var encounter in encounters)
            {
                var fileName = FileName(encounter.Name);
                var encounterPath = $"{encounterFolder}/{fileName}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(encounterPath);
                if (existing != null && !update)
                {
                    skipped.Add(encounter.Name);
                    continue;
                }

                var setup = encounter.Setup;
                // updating keeps the board prefab's path (and so every reference to it); a new encounter gets a new board
                var boardPath = existing != null && existing.Board != null
                    ? AssetDatabase.GetAssetPath(existing.Board)
                    : AssetDatabase.GenerateUniqueAssetPath($"{boardFolder}/{fileName}.prefab");
                var boardPrefab = BoardPrefabFromGraph.Create(fileName, setup.Board, boardPath, setup.SlotRules);

                var lanes = setup.Lanes
                    .Select(lane => new LaneEnemies(lane.Id, lane.Enemies.Select(enemy => CreateEnemy(enemy, encounterFolder, update)).ToList()))
                    .ToList();

                var asset = existing != null ? existing : ScriptableObject.CreateInstance<EncounterDefinition>();
                asset.SetData(encounter.Name, boardPrefab.GetComponent<BoardLayoutAuthoring>(), lanes, 0);
                asset.SetAllowedTools(setup.Rules.AllowReplace, setup.Rules.AllowEject);
                asset.SetTestDeck(testDeck);
                // the opening hand names pilots; the deck's assets with those names are used
                asset.SetOpeningHand(setup.OpeningHand
                    .Select(name => (testDeck != null ? testDeck.Pilots : new List<PilotTokenConfiguration>()).FirstOrDefault(pilot => pilot != null && pilot.DisplayName == name))
                    .Where(pilot => pilot != null).ToList());
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(asset, encounterPath);
                }
                else
                {
                    EditorUtility.SetDirty(asset);
                }

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
                upgraded.AbilityKind = FunGame.Battle.PilotAbilities.KindOf(stats.Ability);
                upgraded.AbilityApCost = stats.Ability?.ApCost ?? upgraded.AbilityApCost;
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
        private static EnemyDefinition CreateEnemy(EnemySpec spec, string folder, bool update)
        {
            var path = $"{folder}/{FileName(spec.Name)}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null && !update)
            {
                return existing;
            }

            var enemy = existing != null ? existing : ScriptableObject.CreateInstance<EnemyDefinition>();
            enemy.SetData(spec.Name, spec.MaxHealth,
                spec.RandomSelection ? EnemyActionSelectionMode.Random : EnemyActionSelectionMode.Sequential,
                spec.Actions.Select(ToAction).ToList());
            if (existing == null)
            {
                AssetDatabase.CreateAsset(enemy, path);
            }
            else
            {
                EditorUtility.SetDirty(enemy);
            }

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

            var snipe = data.Effects.OfType<SnipeEffect>().FirstOrDefault();
            if (snipe != null)
            {
                action.SnipePercent = snipe.Share * 100f;
                action.SnipeTurns = snipe.Turns;
                action.SnipeTarget = snipe.TargetRule;
            }

            var unsupported = data.Effects.Where(effect => !(effect is AttackEffect) && !(effect is DefendEffect) && !(effect is LockEffect) && !(effect is SnipeEffect)).ToList();
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
