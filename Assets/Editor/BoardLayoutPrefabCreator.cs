using System.IO;
using BoardAuthoring;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    public static class BoardLayoutPrefabCreator
    {
        [MenuItem("Assets/Create/Fun Game/Board Layout Prefab")]
        private static void CreateInSelectedFolder()
        {
            var folder = GetSelectedFolder();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/BoardLayout.prefab");
            var prefab = CreateLayoutPrefab(path, withFirstSlot: true);
            Selection.activeObject = prefab;
            AssetDatabase.OpenAsset(prefab);

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.in2DMode = true;
                sceneView.LookAt(Vector3.zero, Quaternion.identity, 6f, true, false);
            }
        }

        /// <summary>
        /// Creates a layout prefab: the root, an editor-only board-bounds rectangle and (optionally) a first root
        /// slot. Returns the saved prefab asset.
        /// </summary>
        public static GameObject CreateLayoutPrefab(string path, bool withFirstSlot)
        {
            var root = BuildLayoutRoot(Path.GetFileNameWithoutExtension(path));
            if (withFirstSlot)
            {
                var layout = root.GetComponent<BoardLayoutAuthoring>();
                var slot = (BoardSlotAuthoring)PrefabUtility.InstantiatePrefab(BoardAuthoringAssets.PilotSlotPrefab, root.transform);
                slot.gameObject.name = "S1";
                slot.transform.position = BoardAuthoringUtility.DefaultRootPosition(layout, 0);
                slot.Configure("S1");
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Builds an unsaved layout root with its bounds rectangle. The caller saves or destroys it.</summary>
        public static GameObject BuildLayoutRoot(string name)
        {
            var root = new GameObject(name);
            var layout = root.AddComponent<BoardLayoutAuthoring>();

            CreateBoundsChild(layout);
            return root;
        }

        /// <summary>Adds the editor-only board-bounds rectangle as a child of the board root.</summary>
        public static GameObject CreateBoundsChild(BoardLayoutAuthoring layout)
        {
            var bounds = new GameObject(BoardLayoutAuthoring.BoundsChildName);
            bounds.tag = "EditorOnly"; // stripped from builds
            bounds.transform.SetParent(layout.transform, false);
            bounds.transform.localPosition = new Vector3(layout.BoardCenter.x, layout.BoardCenter.y, 0f);
            bounds.transform.localScale = new Vector3(layout.BoardSize.x, layout.BoardSize.y, 1f);
            var renderer = bounds.AddComponent<SpriteRenderer>();
            renderer.sprite = BoardAuthoringAssets.BoundsSprite;
            renderer.color = new Color(0.3f, 0.6f, 1f, 0.12f);
            renderer.sortingOrder = -100;
            return bounds;
        }

        private static string GetSelectedFolder()
        {
            var selected = Selection.activeObject;
            if (selected == null)
            {
                return "Assets/BoardLayouts";
            }

            var path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path))
            {
                return "Assets/BoardLayouts";
            }

            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path).Replace('\\', '/');
        }
    }
}
