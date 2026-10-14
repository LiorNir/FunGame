using System.Collections.Generic;
using System.Linq;
using Campaign;
using Configurations;
using Encounters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Builds the campaign from the project: a Campaign asset (the starter deck's pilots; one world per
    /// Assets/Encounters/World N folder, encounters in name order, the boss's reward pilot), gives every world encounter
    /// 1 SP for a first clear, creates the map scene and puts the map and battle scenes in the build (map first).
    /// </summary>
    public static class CampaignAssetCreator
    {
        private const string CampaignPath = "Assets/Campaign/Campaign.asset";
        private const string MapScenePath = "Assets/Scenes/MapScene.unity";
        private const string BattleScenePath = "Assets/Scenes/BattleScene.unity";

        // the pilot each world's boss recruits (by display name); the World 1 to 3 rewards are the pilots that joined the test decks
        private static readonly Dictionary<int, string> RewardPilots = new Dictionary<int, string> { { 1, "Spark" }, { 2, "Bolt" }, { 3, "Brash" } };

        [MenuItem("Tools/Fun Game/Create Campaign And Map")]
        private static void Create()
        {
            CreateCampaign();
            // assign the asset as it is on disk (the instance made while creating it can be replaced on import)
            CreateMapScene(AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath));
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MapScenePath, true), new EditorBuildSettingsScene(BattleScenePath, true) };
            Debug.Log($"Campaign with {campaign.Worlds.Count} worlds at {CampaignPath}; map scene at {MapScenePath}; build scenes set (map first).");
        }

        public static CampaignDefinition CreateCampaign()
        {
            var starter = AssetDatabase.LoadAssetAtPath<PilotDeck>("Assets/Rules/StarterDeck.asset");
            var pilots = AssetDatabase.FindAssets("t:PilotTokenConfiguration", new[] { "Assets/Scripts/Configurations/Pilots" })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => !path.Contains("/Upgrades/"))
                .Select(path => AssetDatabase.LoadAssetAtPath<PilotTokenConfiguration>(path))
                .ToList();

            var worlds = new List<CampaignWorld>();
            for (var number = 1; AssetDatabase.IsValidFolder($"Assets/Encounters/World {number}"); number++)
            {
                var encounters = AssetDatabase.FindAssets("t:EncounterDefinition", new[] { $"Assets/Encounters/World {number}" })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<EncounterDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                    .OrderBy(encounter => encounter.name)
                    .ToList();
                foreach (var encounter in encounters.Where(encounter => encounter.RewardSkillPoints == 0))
                {
                    var serialized = new SerializedObject(encounter);
                    serialized.FindProperty("_rewardSkillPoints").intValue = 1;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                var reward = RewardPilots.TryGetValue(number, out var name) ? pilots.FirstOrDefault(pilot => pilot.DisplayName == name) : null;
                worlds.Add(new CampaignWorld
                {
                    Name = $"World {number}",
                    Encounters = encounters,
                    RewardPilots = reward != null ? new List<PilotTokenConfiguration> { reward } : new List<PilotTokenConfiguration>()
                });
            }

            var campaign = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);
            if (campaign == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Campaign"))
                {
                    AssetDatabase.CreateFolder("Assets", "Campaign");
                }

                campaign = ScriptableObject.CreateInstance<CampaignDefinition>();
                AssetDatabase.CreateAsset(campaign, CampaignPath);
            }

            campaign.SetData(starter != null ? starter.Pilots.ToList() : new List<PilotTokenConfiguration>(), worlds);
            EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssets();
            return campaign;
        }

        private static void CreateMapScene(CampaignDefinition campaign)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.13f);

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var map = new GameObject("Map").AddComponent<MapView>();
            var serialized = new SerializedObject(map);
            serialized.FindProperty("_campaign").objectReferenceValue = campaign;
            serialized.FindProperty("_canvas").objectReferenceValue = canvas;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, MapScenePath);
        }
    }
}
