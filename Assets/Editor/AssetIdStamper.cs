using System.Linq;
using Configurations;
using Encounters;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Makes sure every pilot and encounter asset carries its stable id (its GUID) before a build, so the built game's
    /// save files can name them (the editor reads ids live; a build only has the stored copy). Also a menu item.
    /// </summary>
    public class AssetIdStamper : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Stamp();
        }

        [MenuItem("Tools/Fun Game/Stamp Asset Ids")]
        public static void Stamp()
        {
            var assets = AssetDatabase.FindAssets("t:PilotTokenConfiguration").Concat(AssetDatabase.FindAssets("t:EncounterDefinition"))
                .Select(guid => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .ToList();
            foreach (var asset in assets)
            {
                var serialized = new SerializedObject(asset);
                var id = serialized.FindProperty("_assetId");
                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
                if (id != null && id.stringValue != guid)
                {
                    id.stringValue = guid;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Asset ids checked on {assets.Count} pilots and encounters.");
        }
    }
}
