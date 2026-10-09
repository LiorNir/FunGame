using System.IO;
using BattleView;
using BoardAuthoring;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// What the layout authoring tools place: the pilot slot and operator slot prefabs (the same prefabs the battle plays
    /// with, kept in Assets/BattleViews/Prefabs) and the board bounds sprite. They are created on first use.
    /// </summary>
    public static class BoardAuthoringAssets
    {
        private const string BoardLayoutsFolder = "Assets/BoardLayouts";
        private const string Folder = "Assets/BoardLayouts/_Authoring";
        private const string BoundsSpritePath = Folder + "/BoardBounds.png";

        public static BoardSlotAuthoring PilotSlotPrefab => BattlePrefabCreator.GetPilotSlotPrefab();

        public static BoardOperatorSlotAuthoring OperatorSlotPrefab => BattlePrefabCreator.GetOperatorSlotPrefab();

        /// <summary>Creates the two slot prefabs if they are missing, without touching anything else.</summary>
        public static void EnsureSlotPrefabs()
        {
            if (PilotSlotPrefab == null || OperatorSlotPrefab == null)
            {
                Debug.LogError("The slot prefabs could not be created.");
            }
        }

        private static BattleVisuals _fallbackVisuals;

        /// <summary>
        /// The Battle Visuals asset, for tools that draw with the game's colours (the Scene view gizmos). Never creates
        /// anything: without the asset the default colours are used.
        /// </summary>
        public static BattleVisuals Visuals
        {
            get
            {
                var visuals = AssetDatabase.LoadAssetAtPath<BattleVisuals>(BattleSceneCreator.VisualsPath);
                if (visuals != null)
                {
                    return visuals;
                }

                if (_fallbackVisuals == null)
                {
                    _fallbackVisuals = ScriptableObject.CreateInstance<BattleVisuals>();
                    _fallbackVisuals.hideFlags = HideFlags.HideAndDontSave;
                }

                return _fallbackVisuals;
            }
        }

        public static Sprite BoundsSprite
        {
            get
            {
                Ensure();
                return AssetDatabase.LoadAssetAtPath<Sprite>(BoundsSpritePath);
            }
        }

        public static void Ensure()
        {
            EnsureFolder(BoardLayoutsFolder, "Assets", "BoardLayouts");
            EnsureFolder(Folder, BoardLayoutsFolder, "_Authoring");

            if (AssetDatabase.LoadAssetAtPath<Sprite>(BoundsSpritePath) == null)
            {
                CreateBoundsSprite();
            }
        }

        private static void EnsureFolder(string path, string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void CreateBoundsSprite()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            File.WriteAllBytes(Path.Combine(projectRoot, BoundsSpritePath), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(BoundsSpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(BoundsSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 4f; // a 4 pixel square is exactly one world unit
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }
    }
}
