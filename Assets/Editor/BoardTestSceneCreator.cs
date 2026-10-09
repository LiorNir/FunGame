using BoardAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Creates a clean scene set up for the board direction: a 1080p-friendly camera (orthographic size 5.4, so one
    /// world unit is 100 pixels) and labelled reference zones for the enemies, board, hand and HUD.
    /// </summary>
    public static class BoardTestSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/BoardTestScene.unity";

        [MenuItem("Tools/Fun Game/Create Board Test Scene")]
        private static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = BoardScreenMetrics.CameraOrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.13f);

            var guides = new GameObject("ScreenZones");
            var zones = guides.AddComponent<ScreenZonesGuide>();
            Undo.RecordObject(zones, "Init zones");
            ResetZones(zones);

            var boardHolder = new GameObject("Board (place a layout prefab here)");

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = boardHolder;

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.in2DMode = true;
                sceneView.LookAt(Vector3.zero, Quaternion.identity, BoardScreenMetrics.CameraOrthographicSize, true, false);
            }

            Debug.Log($"Board test scene created at {ScenePath}. Set the Game view to Full HD (1920x1080).");
        }

        private static void ResetZones(ScreenZonesGuide zones)
        {
            // Reset() fills in the default zones when a component is added in the editor; call it explicitly in case it
            // did not run for a component added from code.
            var serialized = new SerializedObject(zones);
            var list = serialized.FindProperty("_zones");
            if (list.arraySize > 0)
            {
                return;
            }

            var defaults = ScreenZonesGuide.CreateDefaultZones();
            list.arraySize = defaults.Count;
            for (var i = 0; i < defaults.Count; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Name").stringValue = defaults[i].Name;
                element.FindPropertyRelative("Center").vector2Value = defaults[i].Center;
                element.FindPropertyRelative("Size").vector2Value = defaults[i].Size;
                element.FindPropertyRelative("Color").colorValue = defaults[i].Color;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
