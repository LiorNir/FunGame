using System.Collections.Generic;
using System.Linq;
using BoardAuthoring;
using FunGame.Battle;
using FunGame.Board;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Builds a board prefab from a pure board graph (and, optionally, its slot locking): the slots are the shared slot
    /// prefabs, distances come from their sizes, and the slots are laid out as a tidy tree. Used by the menus that turn
    /// code-defined boards (samples, built-in encounters) into real assets.
    /// </summary>
    public static class BoardPrefabFromGraph
    {
        public static GameObject Create(string name, BoardGraph graph, string path, IEnumerable<SlotRule> slotRules = null)
        {
            var root = BoardLayoutPrefabCreator.BuildLayoutRoot(name);
            var rules = (slotRules ?? Enumerable.Empty<SlotRule>()).ToDictionary(rule => rule.SlotId);

            var positions = ComputePositions(graph, root.GetComponent<BoardLayoutAuthoring>());
            var slotsById = new Dictionary<string, BoardSlotAuthoring>();
            foreach (var node in graph.Nodes)
            {
                var slot = (BoardSlotAuthoring)PrefabUtility.InstantiatePrefab(BoardAuthoringAssets.PilotSlotPrefab, root.transform);
                slot.gameObject.name = node.Id;
                slot.transform.localPosition = positions[node.Id];
                slot.Configure(node.Id);
                slot.ConfigureBonus(node.Bonus.Kind, node.Bonus.Amount);
                if (rules.TryGetValue(node.Id, out var rule))
                {
                    slot.ConfigureLock(rule.Locked, rule.UnlockApCost, rule.UnlockAtWave, rule.UnlockAtTurn, rule.UnlockLaneId);
                }

                slotsById[node.Id] = slot;
            }

            // one operator slot per parent and operation: branches that leave a slot through the same operation share it
            // (an operator slot with several outputs); a single child keeps the old name op_<child>
            var groups = graph.Nodes.Where(node => node.ParentId != null)
                .GroupBy(node => (node.ParentId, Operation: node.ConnectionOperation ?? Operation.Add));
            foreach (var group in groups)
            {
                var childIds = group.Select(node => node.Id).ToList();
                var operatorId = childIds.Count == 1 ? $"op_{childIds[0]}" : $"op_{group.Key.ParentId}_{group.Key.Operation}";
                var operatorSlot = (BoardOperatorSlotAuthoring)PrefabUtility.InstantiatePrefab(
                    BoardAuthoringAssets.OperatorSlotPrefab, root.transform);
                operatorSlot.gameObject.name = operatorId;
                var childCentre = childIds.Aggregate(Vector3.zero, (sum, id) => sum + positions[id]) / childIds.Count;
                operatorSlot.transform.localPosition = (positions[group.Key.ParentId] + childCentre) * 0.5f;
                operatorSlot.Configure(operatorId, slotsById[group.Key.ParentId], slotsById[childIds[0]], group.Key.Operation);
                foreach (var childId in childIds.Skip(1))
                {
                    operatorSlot.AddOutput(slotsById[childId]);
                }
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

        /// <summary>
        /// A tidy-tree placement: roots along the bottom of the board, children one row up, branches spread to the sides.
        /// Slots above the root row are kept out of the middle column, where the enemy stands.
        /// </summary>
        private static Dictionary<string, Vector3> ComputePositions(BoardGraph graph, BoardLayoutAuthoring layout)
        {
            var pilotRadius = BoardAuthoringAssets.PilotSlotPrefab.Radius;
            var spacing = Mathf.Max(BoardLayoutMetrics.RootSlotSpacing(pilotRadius), 2f * BoardLayoutMetrics.BranchSideOffset);
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
            var side = BoardLayoutMetrics.BranchSideOffset;
            foreach (var id in positions.Keys.ToList())
            {
                var position = positions[id] + offset;
                if (position.y > rootRowY + 0.01f && Mathf.Abs(position.x - layout.BoardCenter.x) < side * 0.5f)
                {
                    position.x = layout.BoardCenter.x + side;
                }

                positions[id] = position;
            }

            // spread for the tilted board (the battle scene's Board Table, 45 degrees): the outward direction looks shorter
            // on screen, so rows are stretched more than columns; within limits that keep the board between the hand, the
            // enemies and the HUD buttons
            var halfWidth = positions.Values.Max(position => Mathf.Abs(position.x - layout.BoardCenter.x));
            var spreadHalfWidth = Mathf.Min(halfWidth * SideStretch, Mathf.Max(halfWidth, MaxStretchedHalfWidth), MaxHalfWidth);
            var sideStretch = halfWidth > 0f ? spreadHalfWidth / halfWidth : 1f;
            var rowSpan = positions.Values.Max(position => position.y) - rootRowY;
            var rowStretch = rowSpan > 0f ? Mathf.Min(RowStretch, MaxRowSpan / rowSpan) : RowStretch;
            foreach (var id in positions.Keys.ToList())
            {
                var position = positions[id];
                positions[id] = new Vector3(
                    layout.BoardCenter.x + (position.x - layout.BoardCenter.x) * sideStretch,
                    rootRowY + (position.y - rootRowY) * rowStretch,
                    position.z);
            }

            // rows above the first branch row step outward, so a link between two rows is long enough on the tilted board
            // for its operator to sit clear of both slots
            var rows = positions.Values.Select(position => Mathf.Round(position.y * 100f)).Distinct().OrderBy(y => y).ToList();
            foreach (var id in positions.Keys.ToList())
            {
                var position = positions[id];
                var row = rows.IndexOf(Mathf.Round(position.y * 100f));
                var outward = position.x - layout.BoardCenter.x;
                if (row >= 2 && Mathf.Abs(outward) > 0.01f)
                {
                    position.x += Mathf.Sign(outward) * DiagonalStep * (row - 1);
                    positions[id] = position;
                }
            }

            return positions;
        }

        // the spread applied to every board on 2026-10-15 and checked on screen (see the handoff), kept here so new boards
        // match: rows up to 1.6x apart but the top row at most MaxRowSpan above the root row (3 rows: 2.3 apart); columns
        // up to 1.4x but not past MaxStretchedHalfWidth, and no board wider than MaxHalfWidth (clear of the HUD buttons)
        private const float RowStretch = 1.6f;
        private const float MaxRowSpan = 4.6f;
        private const float SideStretch = 1.4f;
        private const float MaxStretchedHalfWidth = 4.5f;
        private const float MaxHalfWidth = 6.6f;
        // how far each row above the first branch row moves outward (the stat boxes beside the slots need the room)
        private const float DiagonalStep = 2f;

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
