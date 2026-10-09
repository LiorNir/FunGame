using BoardAuthoring;
using FunGame.Board;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Creates a board prefab from each built-in sample board, as examples to open, move around and copy (built by
    /// <see cref="BoardPrefabFromGraph"/>). The one variation is a real prefab variant of its base.
    /// </summary>
    public static class SampleBoardAssetCreator
    {
        private const string Folder = "Assets/Boards";

        [MenuItem("Tools/Fun Game/Create Sample Boards")]
        private static void Create()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "Boards");
            }

            var fileNames = new[] { "SplitPair", "TwoLaneHub" };
            GameObject splitPair = null;
            for (var index = 0; index < fileNames.Length; index++)
            {
                var prefab = CreateBoard(fileNames[index], SampleBoards.Create(index));
                if (index == 0)
                {
                    splitPair = prefab;
                }
            }

            CreateVariant(splitPair);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Sample boards created in {Folder}.");
        }

        private static GameObject CreateBoard(string name, BoardGraph graph)
        {
            return BoardPrefabFromGraph.Create(name, graph, AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{name}.prefab"));
        }

        /// <summary>Makes a variant of the base board, in which every operator is flipped: the same layout, a different puzzle.</summary>
        private static void CreateVariant(GameObject baseBoard)
        {
            if (baseBoard == null)
            {
                return;
            }

            var instance = BoardVariantCreator.Begin(baseBoard, "SplitPair_Variant");
            foreach (var operatorSlot in instance.GetComponent<BoardLayoutAuthoring>().GetOperators())
            {
                var serialized = new SerializedObject(operatorSlot);
                var operation = serialized.FindProperty("_operation");
                operation.enumValueIndex = operation.enumValueIndex == (int)Operation.Add
                    ? (int)Operation.Multiply
                    : (int)Operation.Add;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            BoardVariantCreator.Save(instance, AssetDatabase.GenerateUniqueAssetPath($"{Folder}/SplitPair_Variant.prefab"));
        }
    }
}
