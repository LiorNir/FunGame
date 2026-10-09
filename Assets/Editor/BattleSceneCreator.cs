using System.Linq;
using BattleView;
using BoardAuthoring;
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
    /// Creates a scene in which an encounter can be played: a camera, a canvas, a board root and enemy lane anchors, and a
    /// Battle Session set up with the game rules, the starter deck, the encounters found and a Battle Visuals asset with
    /// its prefabs.
    /// </summary>
    public static class BattleSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/BattleScene.unity";
        public const string VisualsPath = DefaultGameAssetCreator.Folder + "/BattleVisuals.asset";

        [MenuItem("Tools/Fun Game/Open Battle Test Scene")]
        private static void OpenOrCreate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (System.IO.File.Exists(ScenePath))
            {
                // the shared slot prefabs (used by the layouts and the battle alike) must exist whichever way the scene opens
                // (only these: EnsurePrefabs would re-assign the visuals' prefab fields, replacing any variant assigned there)
                BoardAuthoringAssets.EnsureSlotPrefabs();
                EditorSceneManager.OpenScene(ScenePath);
            }
            else
            {
                Create();
            }

            var oldBoards = OldBoardConverter.CountOldStyleBoards();
            if (oldBoards > 0)
            {
                Debug.LogWarning($"{oldBoards} board prefab(s) are not made from the current slot prefabs and will not play. Run Tools > Fun Game > Convert Old Boards.");
            }
        }

        private static void Create()
        {
            var (rules, deck) = DefaultGameAssetCreator.EnsureAssets();
            var visuals = EnsureVisuals();
            BattlePrefabCreator.EnsurePrefabs(visuals, overwrite: false);

            var allEncounters = AssetDatabase.FindAssets("t:EncounterDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<EncounterDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(candidate => candidate != null)
                .OrderBy(candidate => candidate.name)
                .ToList();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = BoardScreenMetrics.CameraOrthographicSize;
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

            var sessionObject = new GameObject("Battle Session");
            var session = sessionObject.AddComponent<BattleSession>();
            var serialized = new SerializedObject(session);
            serialized.FindProperty("_encounter").objectReferenceValue = allEncounters.FirstOrDefault();
            var list = serialized.FindProperty("_encounters");
            list.arraySize = allEncounters.Count;
            for (var i = 0; i < allEncounters.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = allEncounters[i];
            }

            serialized.FindProperty("_gameRules").objectReferenceValue = rules;
            serialized.FindProperty("_deck").objectReferenceValue = deck;
            serialized.FindProperty("_visuals").objectReferenceValue = visuals;
            serialized.FindProperty("_canvas").objectReferenceValue = canvas;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EnsureSceneObjects(session);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = sessionObject;

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.in2DMode = true;
                sceneView.LookAt(Vector3.zero, Quaternion.identity, BoardScreenMetrics.CameraOrthographicSize, true, false);
            }

            Debug.Log(allEncounters.Count == 0
                ? $"Battle test scene created at {ScenePath}. No Encounter asset was found: assign one on the Battle Session."
                : $"Battle test scene created at {ScenePath}. Set the Game view to Full HD, press Play and use the test panel (or the Battle Session buttons) to start an encounter.");
        }

        /// <summary>
        /// Makes sure the scene has a board root (with a Board View) and two enemy lane anchors, and assigns them on the
        /// session. Existing ones are kept. Move, rotate or scale these objects by hand to place the board and the enemies.
        /// </summary>
        public static void EnsureSceneObjects(BattleSession session)
        {
            var serialized = new SerializedObject(session);

            var boardProperty = serialized.FindProperty("_boardView");
            if (boardProperty.objectReferenceValue == null)
            {
                var boardObject = new GameObject("Board");
                boardProperty.objectReferenceValue = boardObject.AddComponent<BoardView>();
                Undo.RegisterCreatedObjectUndo(boardObject, "Create board root");
            }

            var anchors = serialized.FindProperty("_enemyAnchors");
            if (anchors.arraySize == 0)
            {
                var parent = new GameObject("Enemy Anchors");
                Undo.RegisterCreatedObjectUndo(parent, "Create enemy anchors");
                anchors.arraySize = 2;
                for (var i = 0; i < 2; i++)
                {
                    var anchor = new GameObject("Enemy Lane " + (i + 1));
                    anchor.transform.SetParent(parent.transform, false);
                    anchor.transform.position = new Vector3(i == 0 ? -4.6f : 4.6f, 3.1f, 0f);
                    anchors.GetArrayElementAtIndex(i).objectReferenceValue = anchor.transform;
                }
            }

            var playerAnchorProperty = serialized.FindProperty("_playerAnchor");
            if (playerAnchorProperty.objectReferenceValue == null)
            {
                var playerAnchor = new GameObject("Player Anchor");
                playerAnchor.transform.position = new Vector3(-7.5f, 4.4f, 0f);
                Undo.RegisterCreatedObjectUndo(playerAnchor, "Create player anchor");
                playerAnchorProperty.objectReferenceValue = playerAnchor.transform;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(session);
        }

        public static BattleVisuals EnsureVisuals()
        {
            DefaultGameAssetCreator.EnsureFolder();
            var visuals = AssetDatabase.LoadAssetAtPath<BattleVisuals>(VisualsPath);
            if (visuals != null)
            {
                return visuals;
            }

            visuals = ScriptableObject.CreateInstance<BattleVisuals>();
            visuals.SlotBase = Load("Assets/Textures/Coin.png");
            visuals.BarSprite = Load("Assets/BoardLayouts/_Authoring/BoardBounds.png");
            visuals.AddSprite = Load("Assets/Textures/AddToken.png");
            visuals.SubtractSprite = Load("Assets/Textures/SubtractToken.png");
            visuals.MultiplySprite = Load("Assets/Textures/MultiplyToken.png");
            visuals.DivideSprite = Load("Assets/Textures/DivideToken.png");
            visuals.AttackIcon = Load("Assets/Textures/Icons_Attack_Texture.png");
            visuals.DefenseIcon = Load("Assets/Textures/Icons_Defense_Texture.png");
            AssetDatabase.CreateAsset(visuals, VisualsPath);
            AssetDatabase.SaveAssets();
            return visuals;
        }

        private static Sprite Load(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
