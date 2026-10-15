namespace Configurations
{
    /// <summary>
    /// Stable ids for assets the save file refers to (pilots, encounters): Unity's asset GUID, which survives renaming and
    /// moving the file. In the editor it is read from the asset database; a build reads the copy stored on the asset
    /// (stamped in the editor, and before every build). Falls back to the asset's name if neither exists.
    /// </summary>
    public static class AssetIds
    {
        public static string Of(UnityEngine.Object asset, string stored)
        {
            if (asset == null)
            {
                return null;
            }
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
            {
                return UnityEditor.AssetDatabase.AssetPathToGUID(path);
            }
#endif
            return string.IsNullOrEmpty(stored) ? asset.name : stored;
        }

#if UNITY_EDITOR
        /// <summary>Stores the asset's GUID in its id field if it is missing or out of date (editor only).</summary>
        public static void Stamp(UnityEngine.Object asset, ref string stored)
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            if (stored != guid)
            {
                stored = guid;
                UnityEditor.EditorUtility.SetDirty(asset);
            }
        }
#endif
    }
}
