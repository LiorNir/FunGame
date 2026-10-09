using System.Collections.Generic;
using System.Linq;
using BoardAuthoring;
using FunGame.Board;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Creates a board prefab from each built-in sample board, as examples to open, move around and copy. The slots are the
    /// shared slot prefabs, distances come from their sizes, and the one variation is a real prefab variant of its base.
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
            var path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{name}.prefab");
            var root = BoardLayoutPrefabCreator.BuildLayoutRoot(name);

            var positions = ComputePositions(graph, root.GetComponent<BoardLayoutAuthoring>());
            var slotsById = new Dictionary<string, BoardSlotAuthoring>();
            foreach (var node in graph.Nodes)
            {
                var slot = (BoardSlotAuthoring)PrefabUtility.InstantiatePrefab(BoardAuthoringAssets.PilotSlotPrefab, root.transform);
                slot.gameObject.name = node.Id;
                slot.transform.localPosition = positions[node.Id];
                slot.Configure(node.Id);
                slot.ConfigureBonus(node.Bonus.Kind, node.Bonus.Amount);
                slotsById[node.Id] = slot;
            }

            foreach (var node in graph.Nodes.Where(node => node.ParentId != null))
            {
                var operatorId = $"op_{node.Id}";
                var operatorSlot = (BoardOperatorSlotAuthoring)PrefabUtility.InstantiatePrefab(
                    BoardAuthoringAssets.OperatorSlotPrefab, root.transform);
                operatorSlot.gameObject.name = operatorId;
                operatorSlot.transform.localPosition = (positions[node.ParentId] + positions[node.Id]) * 0.5f;
                operatorSlot.Configure(operatorId, slotsById[node.ParentId], slotsById[node.Id],
                    node.ConnectionOperation ?? Operation.Add);
            }

            foreach (var chain in graph.Chains)
            {
                var marker = slotsById[chain.TerminalId].gameObject.AddComponent<BoardChainMarker>();
                marker.Configure(chain.LaneId, chain.Role);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
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

        /// <summary>A simple tidy-tree placement: roots along the bottom of the board, children one row up.</summary>
        private static Dictionary<string, Vector3> ComputePositions(BoardGraph graph, BoardLayoutAuthoring layout)
        {
            var pilotRadius = BoardAuthoringAssets.PilotSlotPrefab.Radius;
            var spacing = BoardLayoutMetrics.RootSlotSpacing(pilotRadius);
            var rowDistance = BoardLayoutMetrics.NextSlotOffsetY(pilotRadius, BoardAuthoringAssets.OperatorSlotPrefab.Radius);

            var children = graph.Nodes
                .Where(node => node.ParentId != null)
                .GroupBy(node => node.ParentId)
                .ToDictionary(group => group.Key, group => group.Select(node => node.Id).ToList());

            var positions = new Dictionary<string, Vector3>();
            var nextLeafX = 0f;
            foreach (var root in graph.Nodes.Where(node => node.IsRoot))
            {
                Place(root.Id, 0, children, positions, ref nextLeafX, spacing, rowDistance);
                nextLeafX += spacing * 0.5f;
            }

            // centred across the board, with the root row where a new root slot would go
            var minX = positions.Values.Min(position => position.x);
            var maxX = positions.Values.Max(position => position.x);
            var rootRowY = layout.BoardCenter.y - layout.BoardSize.y * 0.5f + BoardLayoutMetrics.RootMarginFromBoardBottom(pilotRadius);
            var offset = new Vector3(layout.BoardCenter.x - (minX + maxX) * 0.5f, rootRowY, 0f);
            foreach (var id in positions.Keys.ToList())
            {
                positions[id] += offset;
            }

            return positions;
        }

        private static float Place(
            string id,
            int depth,
            Dictionary<string, List<string>> children,
            Dictionary<string, Vector3> positions,
            ref float nextLeafX,
            float spacing,
            float rowDistance)
        {
            float x;
            if (children.TryGetValue(id, out var childIds))
            {
                var childXs = new List<float>();
                foreach (var childId in childIds)
                {
                    childXs.Add(Place(childId, depth + 1, children, positions, ref nextLeafX, spacing, rowDistance));
                }

                x = (childXs.Min() + childXs.Max()) * 0.5f;
            }
            else
            {
                x = nextLeafX;
                nextLeafX += spacing;
            }

            positions[id] = new Vector3(x, depth * rowDistance, 0f);
            return x;
        }
    }
}
