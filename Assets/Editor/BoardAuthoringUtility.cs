using BoardAuthoring;
using FunGame.Board;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FunGame.EditorTools
{
    public static class BoardAuthoringUtility
    {
        // Every distance below is built from the sizes of the slot prefabs, so resizing a prefab moves the defaults too.
        private static float PilotRadius => BoardAuthoringAssets.PilotSlotPrefab.Radius;
        private static float OperatorRadius => BoardAuthoringAssets.OperatorSlotPrefab.Radius;

        /// <summary>Distance from a slot to the operator slot placed after it.</summary>
        public static Vector3 OperatorOffset => new Vector3(0f, BoardLayoutMetrics.OperatorOffsetY(PilotRadius, OperatorRadius), 0f);

        /// <summary>Distance from a slot to the next pilot slot of its chain.</summary>
        public static Vector3 NextSlotOffset => new Vector3(0f, BoardLayoutMetrics.NextSlotOffsetY(PilotRadius, OperatorRadius), 0f);

        /// <summary>Where a new root slot goes: along the bottom of the board, inside it and clear of the summoner.</summary>
        public static Vector3 DefaultRootPosition(BoardLayoutAuthoring board, int existingRootCount)
        {
            var pilotRadius = PilotRadius;
            var bottom = board.transform.position + (Vector3)board.BoardCenter
                         + new Vector3(0f, -board.BoardSize.y * 0.5f + BoardLayoutMetrics.RootMarginFromBoardBottom(pilotRadius), 0f);
            return bottom + new Vector3(existingRootCount * BoardLayoutMetrics.RootSlotSpacing(pilotRadius), 0f, 0f);
        }

        public static string NextSlotId(BoardLayoutAuthoring layout)
        {
            var existing = layout.GetSlotIds();
            var number = 1;
            while (existing.Contains($"S{number}"))
            {
                number++;
            }

            return $"S{number}";
        }

        public static string NextOperatorId(BoardLayoutAuthoring layout)
        {
            var existing = layout.GetOperatorIds();
            var number = 1;
            while (existing.Contains($"O{number}"))
            {
                number++;
            }

            return $"O{number}";
        }

        /// <summary>Adds a pilot slot, as a root (nothing flows into it yet).</summary>
        public static BoardSlotAuthoring AddSlot(BoardLayoutAuthoring layout, Vector3 position)
        {
            var id = NextSlotId(layout);
            var instance = (BoardSlotAuthoring)PrefabUtility.InstantiatePrefab(BoardAuthoringAssets.PilotSlotPrefab, layout.transform);
            Undo.RegisterCreatedObjectUndo(instance.gameObject, "Add pilot slot");
            instance.gameObject.name = id;
            instance.transform.position = position;
            instance.Configure(id);
            MarkDirty(instance.gameObject);
            return instance;
        }

        /// <summary>Adds an operator slot and a new pilot slot after <paramref name="from"/>, connected in a chain.</summary>
        public static BoardSlotAuthoring AddOperatorAndSlot(BoardLayoutAuthoring layout, BoardSlotAuthoring from)
        {
            var next = AddSlot(layout, from.transform.position + NextSlotOffset);
            var operatorSlot = AddOperatorSlot(layout, from.transform.position + OperatorOffset);
            operatorSlot.Configure(operatorSlot.Id, from, next);
            MarkDirty(operatorSlot.gameObject);
            return next;
        }

        public static BoardOperatorSlotAuthoring AddOperatorSlot(BoardLayoutAuthoring layout, Vector3 position)
        {
            var id = NextOperatorId(layout);
            var instance = (BoardOperatorSlotAuthoring)PrefabUtility.InstantiatePrefab(
                BoardAuthoringAssets.OperatorSlotPrefab, layout.transform);
            Undo.RegisterCreatedObjectUndo(instance.gameObject, "Add operator slot");
            instance.gameObject.name = id;
            instance.transform.position = position;
            instance.Configure(id, null, null);
            MarkDirty(instance.gameObject);
            return instance;
        }

        /// <summary>Places the operator slot between its From slot and the centre of its outputs.</summary>
        public static void CenterBetweenSlots(BoardOperatorSlotAuthoring operatorSlot)
        {
            if (operatorSlot.From == null)
            {
                return;
            }

            var sum = Vector3.zero;
            var count = 0;
            foreach (var output in operatorSlot.Outputs)
            {
                if (output != null)
                {
                    sum += output.transform.position;
                    count++;
                }
            }

            if (count == 0)
            {
                return;
            }

            Undo.RecordObject(operatorSlot.transform, "Center operator slot");
            operatorSlot.transform.position = (operatorSlot.From.transform.position + sum / count) * 0.5f;
        }

        /// <summary>Adds another pilot slot fed by an existing operator slot, so the chain branches there.</summary>
        public static BoardSlotAuthoring AddOutputSlot(BoardLayoutAuthoring layout, BoardOperatorSlotAuthoring operatorSlot)
        {
            var side = operatorSlot.Outputs.Count % 2 == 0 ? 1f : -1f;
            var step = 1 + operatorSlot.Outputs.Count / 2;
            var pilotRadius = PilotRadius;
            var operatorRadius = OperatorRadius;
            var offset = new Vector3(side * step * BoardLayoutMetrics.RootSlotSpacing(pilotRadius) * 0.5f,
                BoardLayoutMetrics.NextSlotOffsetY(pilotRadius, operatorRadius) - BoardLayoutMetrics.OperatorOffsetY(pilotRadius, operatorRadius), 0f);
            var created = AddSlot(layout, operatorSlot.transform.position + offset);
            Undo.RecordObject(operatorSlot, "Add operator output");
            operatorSlot.AddOutput(created);
            MarkDirty(operatorSlot.gameObject);
            return created;
        }

        public static void AddChainMarker(BoardSlotAuthoring slot, ChainRole role)
        {
            var marker = Undo.AddComponent<BoardChainMarker>(slot.gameObject);
            marker.Configure(FindLaneId(slot), role);
            MarkDirty(slot.gameObject);
        }

        public static BoardLayoutAuthoring FindLayoutInContext()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                return stage.prefabContentsRoot.GetComponent<BoardLayoutAuthoring>();
            }

            return Object.FindFirstObjectByType<BoardLayoutAuthoring>();
        }

        private static string FindLaneId(BoardSlotAuthoring slot)
        {
            var layout = slot.GetComponentInParent<BoardLayoutAuthoring>(true);
            if (layout != null)
            {
                var markers = layout.GetChainMarkers();
                if (markers.Count > 0)
                {
                    return markers[markers.Count - 1].LaneId;
                }
            }

            return "lane1";
        }

        private static void MarkDirty(GameObject go)
        {
            EditorUtility.SetDirty(go);
            if (go.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(go.scene);
            }
        }
    }
}
