using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Gives every TextMeshPro text in the game's prefabs a black outline: one outline material per font (a copy of the
    /// font's own material, kept in Assets/Art/Fonts, so the vendored TextMesh Pro folder is untouched), assigned to every
    /// text using that font. Run it again after changing fonts or adding prefabs; the outline's width and colour are set on
    /// the material, so tuning it there changes every text at once.
    /// </summary>
    public static class TextOutline
    {
        private const string MaterialFolder = "Assets/Art/Fonts";
        private static readonly string[] PrefabFolders = { "Assets/BattleViews/Prefabs", "Assets/CampaignViews/Prefabs" };

        private const float OutlineWidth = 0.2f;

        [MenuItem("Tools/Fun Game/Apply Text Outline")]
        public static void Apply()
        {
            Directory.CreateDirectory(MaterialFolder);
            var changed = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", PrefabFolders).Select(AssetDatabase.GUIDToAssetPath))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                var texts = root.GetComponentsInChildren<TMP_Text>(true).Where(text => text.font != null).ToList();
                var any = false;
                foreach (var text in texts)
                {
                    var outline = OutlineOf(text.font);
                    if (text.fontSharedMaterial != outline)
                    {
                        text.fontSharedMaterial = outline;
                        any = true;
                    }
                }

                if (any)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }

                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Text outline applied: {changed} prefab(s) changed. The outline materials are in {MaterialFolder}.");
        }

        /// <summary>The font's outline material, made from the font's own material the first time.</summary>
        public static Material OutlineOf(TMP_FontAsset font)
        {
            var path = $"{MaterialFolder}/{font.name} - Black Outline.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new Material(font.material) { name = Path.GetFileNameWithoutExtension(path) };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
