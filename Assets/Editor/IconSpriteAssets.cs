using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Keeps the inline text icons (BattleView.Icons) in step with their pictures: every picture in the icons folder has a
    /// TextMeshPro sprite asset of the same name next to it, updated whenever the picture is imported or replaced. To change
    /// an icon, overwrite its picture (same file name); texts find the sprite asset by name, so nothing else changes.
    /// </summary>
    public class IconSpriteAssets : AssetPostprocessor
    {
        public const string Folder = "Assets/Art/TextIcons/Resources/Sprite Assets";

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported.Concat(moved).Where(path => path.StartsWith(Folder + "/") && path.EndsWith(".png")))
            {
                Rebuild(path);
            }
        }

        [MenuItem("Tools/Fun Game/Rebuild Icon Sprite Assets")]
        public static void RebuildAll()
        {
            foreach (var path in Directory.GetFiles(Folder, "*.png").Select(path => path.Replace('\\', '/')))
            {
                Rebuild(path);
            }
        }

        /// <summary>
        /// Brings one icon's sprite asset up to date with its picture: updated in place when it exists (TextMeshPro's
        /// "Update Sprite Asset"), otherwise made (the same as Create > TextMeshPro > Sprite Asset on the picture).
        /// </summary>
        public static void Rebuild(string picturePath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(picturePath);
            if (texture == null)
            {
                return;
            }

            var assetPath = Path.ChangeExtension(picturePath, ".asset");
            var spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(assetPath);
            if (spriteAsset != null)
            {
                var update = TextMeshProMenu("UpdateSpriteAsset");
                if (update != null)
                {
                    spriteAsset.spriteSheet = texture;
                    update.Invoke(null, new object[] { new MenuCommand(spriteAsset) });
                    SitOnTheLine(spriteAsset);
                    EditorUtility.SetDirty(spriteAsset);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"Icon sprite asset updated: {assetPath}");
                    return;
                }

                AssetDatabase.DeleteAsset(assetPath);
            }

            var previous = Selection.activeObject;
            Selection.activeObject = texture;
            EditorApplication.ExecuteMenuItem("Assets/Create/TextMeshPro/Sprite Asset");
            Selection.activeObject = previous;
            var made = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(assetPath);
            if (made != null)
            {
                SitOnTheLine(made);
                EditorUtility.SetDirty(made);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Icon sprite asset made: {assetPath}");
        }

        // a share of the icon's height above the baseline (the rest hangs below, like a letter's descender), and a gap after it
        private const float RiseShare = 0.85f;
        private const float GapShare = 0.08f;

        /// <summary>
        /// Lays each icon out like a letter: starting where the text is (no shift to the left), mostly above the baseline,
        /// and moving the text after it by its width plus a small gap. The import otherwise uses the picture's centred
        /// pivot, which draws the icon half over the text before it and half below the line.
        /// </summary>
        private static void SitOnTheLine(TMP_SpriteAsset spriteAsset)
        {
            foreach (var glyph in spriteAsset.spriteGlyphTable)
            {
                var width = glyph.glyphRect.width;
                var height = glyph.glyphRect.height;
                glyph.metrics = new UnityEngine.TextCore.GlyphMetrics(width, height, 0f, height * RiseShare, width * (1f + GapShare));
            }

            spriteAsset.UpdateLookupTables();
        }

        private static MethodInfo TextMeshProMenu(string name)
        {
            var menu = typeof(TMP_SpriteAsset).Assembly.GetTypes().Concat(System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeTypes))
                .FirstOrDefault(type => type.FullName == "TMPro.EditorUtilities.TMP_SpriteAssetMenu");
            return menu?.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(MenuCommand) }, null);
        }

        private static System.Type[] SafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException)
            {
                return new System.Type[0];
            }
        }
    }
}
