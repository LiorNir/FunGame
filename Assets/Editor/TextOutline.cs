using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// The game's text look in the battle and campaign prefabs: a black outline on every TextMeshPro text (one outline
    /// material per font, a copy of the font's own material kept in Assets/Art/Fonts, so the vendored TextMesh Pro folder
    /// is untouched), and switching every text to another font in one go. The outline's width and colour are set on the
    /// material, so tuning it there changes every text using that font at once.
    /// </summary>
    public static class TextOutline
    {
        private const string MaterialFolder = "Assets/Art/Fonts";
        private const string FallbackFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private static readonly string[] PrefabFolders = { "Assets/BattleViews/Prefabs", "Assets/CampaignViews/Prefabs" };

        private const float OutlineWidth = 0.2f;

        [MenuItem("Tools/Fun Game/Apply Text Outline")]
        public static void Apply()
        {
            var changed = ForEachText(text =>
            {
                var outline = OutlineOf(text.font);
                if (text.fontSharedMaterial == outline)
                {
                    return false;
                }

                text.fontSharedMaterial = outline;
                return true;
            });
            Debug.Log($"Text outline applied: {changed} prefab(s) changed. The outline materials are in {MaterialFolder}.");
        }

        /// <summary>
        /// Switches every text in the battle and campaign prefabs to the font selected in the Project window, with its black
        /// outline. Select a TextMeshPro font asset, or a font file (.ttf / .otf): its font asset is made next to it the
        /// first time (dynamic SDF, with LiberationSans as fallback for symbols the font lacks, such as the star dots).
        /// </summary>
        [MenuItem("Tools/Fun Game/Use Selected Font For All Texts")]
        public static void UseSelectedFont()
        {
            var font = SelectedFont();
            if (font == null)
            {
                EditorUtility.DisplayDialog("Use Selected Font", "Select a font file (.ttf / .otf) or a TextMeshPro font asset in the Project window first.", "OK");
                return;
            }

            UseFont(font);
        }

        [MenuItem("Tools/Fun Game/Use Selected Font For All Texts", true)]
        private static bool CanUseSelectedFont()
        {
            return Selection.activeObject is TMP_FontAsset || Selection.activeObject is Font;
        }

        /// <summary>Every text of the battle and campaign prefabs gets <paramref name="font"/> and its outline material.</summary>
        public static void UseFont(TMP_FontAsset font)
        {
            var outline = OutlineOf(font);
            var changed = ForEachText(text =>
            {
                if (text.font == font && text.fontSharedMaterial == outline)
                {
                    return false;
                }

                text.font = font;
                text.fontSharedMaterial = outline;
                return true;
            });
            Debug.Log($"All texts now use '{font.name}' with its outline: {changed} prefab(s) changed.");
        }

        /// <summary>The selected font asset, or the font asset of the selected font file (made the first time).</summary>
        private static TMP_FontAsset SelectedFont()
        {
            if (Selection.activeObject is TMP_FontAsset selected)
            {
                return selected;
            }

            if (!(Selection.activeObject is Font source))
            {
                return null;
            }

            var sourcePath = AssetDatabase.GetAssetPath(source);
            var path = $"{Path.GetDirectoryName(sourcePath)}/{Path.GetFileNameWithoutExtension(sourcePath)} SDF.asset".Replace('\\', '/');
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            return existing != null ? existing : CreateFontAsset(source, path);
        }

        /// <summary>The same as Create > TextMeshPro > Font Asset: a dynamic SDF font asset with its atlas and material inside.</summary>
        private static TMP_FontAsset CreateFontAsset(Font source, string path)
        {
            var fontAsset = TMP_FontAsset.CreateFontAsset(source);
            fontAsset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(fontAsset, path);
            fontAsset.atlasTexture.name = fontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
            if (fallback != null)
            {
                fontAsset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        /// <summary>The font's outline material, made from the font's own material the first time.</summary>
        public static Material OutlineOf(TMP_FontAsset font)
        {
            Directory.CreateDirectory(MaterialFolder);
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

        /// <summary>Runs <paramref name="change"/> on every text of the battle and campaign prefabs; returns how many prefabs changed.</summary>
        private static int ForEachText(Func<TMP_Text, bool> change)
        {
            var changed = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", PrefabFolders).Select(AssetDatabase.GUIDToAssetPath))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                var any = false;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true).Where(text => text.font != null))
                {
                    any |= change(text);
                }

                if (any)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }

                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            return changed;
        }
    }
}
