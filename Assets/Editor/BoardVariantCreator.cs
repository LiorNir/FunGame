using System.IO;
using BoardAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// The one place that makes a board a prefab variant of another. A variant stores only what differs from its base, so a
    /// change to the base (a slot moved, a prefab look changed) reaches every variant. Used by the "Create variant of this
    /// board" button, the sample boards and the old-board converter.
    /// </summary>
    public static class BoardVariantCreator
    {
        private const string MenuPath = "Assets/Create/Fun Game/Variant of Selected Board";

        [MenuItem(MenuPath)]
        private static void CreateFromSelection()
        {
            var basePrefab = Selection.activeObject as GameObject;
            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(basePrefab)).Replace('\\', '/');
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{basePrefab.name} Variant.prefab");
            Open(CreateVariant(basePrefab, path));
        }

        [MenuItem(MenuPath, true)]
        private static bool CanCreateFromSelection()
        {
            return IsBoardPrefabAsset(Selection.activeObject);
        }

        public static bool IsBoardPrefabAsset(Object candidate)
        {
            return candidate is GameObject go && EditorUtility.IsPersistent(go) && go.GetComponent<BoardLayoutAuthoring>() != null;
        }

        /// <summary>
        /// Starts a variant: an instance of the base prefab that is not saved yet, so the caller can override what differs
        /// and then call <see cref="Save"/>.
        /// </summary>
        public static GameObject Begin(GameObject basePrefab, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = name;
            return instance;
        }

        /// <summary>Saves the instance as a prefab asset (a variant of its source) and removes the instance from the scene.</summary>
        public static GameObject Save(GameObject variantInstance, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(variantInstance, path);
            Object.DestroyImmediate(variantInstance);
            return saved;
        }

        /// <summary>Saves a variant of the base with no overrides yet.</summary>
        public static GameObject CreateVariant(GameObject basePrefab, string path)
        {
            return Save(Begin(basePrefab, Path.GetFileNameWithoutExtension(path)), path);
        }

        /// <summary>
        /// The board prefab asset a layout belongs to, whether it is selected in the Project window, open in prefab mode or
        /// placed in a scene. Null for a plain scene object that is not part of any prefab.
        /// </summary>
        public static GameObject FindBasePrefab(BoardLayoutAuthoring board)
        {
            var go = board.gameObject;
            if (EditorUtility.IsPersistent(go))
            {
                return go.transform.root.gameObject;
            }

            var stage = PrefabStageUtility.GetPrefabStage(go);
            if (stage != null)
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);
            }

            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                return PrefabUtility.GetCorrespondingObjectFromSource(PrefabUtility.GetOutermostPrefabInstanceRoot(go));
            }

            return null;
        }

        /// <summary>True if the board is open in prefab mode with changes that are not saved to the asset yet.</summary>
        public static bool HasUnsavedChanges(BoardLayoutAuthoring board)
        {
            var stage = PrefabStageUtility.GetPrefabStage(board.gameObject);
            return stage != null && stage.scene.isDirty;
        }

        /// <summary>Asks where to save the variant of the given board, creates it and opens it. Returns null if cancelled.</summary>
        public static GameObject CreateInteractively(GameObject basePrefab)
        {
            var basePath = AssetDatabase.GetAssetPath(basePrefab);
            var folder = Path.GetDirectoryName(basePath).Replace('\\', '/');
            var path = EditorUtility.SaveFilePanelInProject(
                "Create variant of " + basePrefab.name,
                basePrefab.name + " Variant",
                "prefab",
                "The variant stores only what differs from " + basePrefab.name + ".",
                folder);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var variant = CreateVariant(basePrefab, path);
            Open(variant);
            return variant;
        }

        private static void Open(GameObject variant)
        {
            Selection.activeObject = variant;
            AssetDatabase.OpenAsset(variant);
        }
    }
}
