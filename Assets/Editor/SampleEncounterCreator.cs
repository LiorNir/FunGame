using System.Collections.Generic;
using System.Linq;
using BoardAuthoring;
using Combat;
using Encounters;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>Creates two test enemies and an encounter that uses one of your board prefabs, as a template to copy.</summary>
    public static class SampleEncounterCreator
    {
        private const string Folder = "Assets/Encounters";

        [MenuItem("Tools/Fun Game/Create Sample Encounter")]
        private static void Create()
        {
            var board = FindBoard();
            if (board == null)
            {
                Debug.LogError("No board prefab found. Create a board first (Assets > Create > Fun Game > Board Layout Prefab).");
                return;
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "Encounters");
            }

            var slime = CreateEnemy("TestSlime", "Test Slime", 20, new List<EnemyAction>
            {
                new EnemyAction { Name = "Balance", Attack = 1, Defense = 1 },
                new EnemyAction { Name = "Attack", Attack = 2, Defense = 0 },
                new EnemyAction { Name = "Defend", Attack = 0, Defense = 2 }
            });
            var golem = CreateEnemy("TestGolem", "Test Golem", 30, new List<EnemyAction>
            {
                new EnemyAction { Name = "Smash", Attack = 3, Defense = 0 },
                new EnemyAction { Name = "Harden", Attack = 1, Defense = 2 }
            });

            var laneId = board.GetChainMarkers().Select(marker => marker.LaneId).FirstOrDefault() ?? "lane1";
            var encounter = ScriptableObject.CreateInstance<EncounterDefinition>();
            encounter.SetData("Test Encounter", board,
                new List<LaneEnemies> { new LaneEnemies(laneId, new List<EnemyDefinition> { slime, golem }) }, 5);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/TestEncounter.asset");
            AssetDatabase.CreateAsset(encounter, path);

            AssetDatabase.SaveAssets();
            Selection.activeObject = encounter;
            Debug.Log($"Sample encounter created at {path}, using board '{board.name}'.");
        }

        private static EnemyDefinition CreateEnemy(string fileName, string displayName, int health, List<EnemyAction> actions)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            enemy.SetData(displayName, health, EnemyActionSelectionMode.Sequential, actions);
            AssetDatabase.CreateAsset(enemy, AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{fileName}.asset"));
            return enemy;
        }

        private static BoardLayoutAuthoring FindBoard()
        {
            var preferred = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BoardLayouts/Variants/BoardLayout (3) Variant.prefab");
            if (preferred != null && preferred.GetComponent<BoardLayoutAuthoring>() != null)
            {
                return preferred.GetComponent<BoardLayoutAuthoring>();
            }

            return AssetDatabase.FindAssets("t:Prefab")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(prefab => prefab != null)
                .Select(prefab => prefab.GetComponent<BoardLayoutAuthoring>())
                .FirstOrDefault(layout => layout != null);
        }
    }
}
