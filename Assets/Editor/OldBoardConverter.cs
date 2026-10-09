using System.Collections.Generic;
using System.Linq;
using BattleView;
using BoardAuthoring;
using Encounters;
using FunGame.Board;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Rebuilds boards made before the slot prefabs were merged (slots made from the old editor-only authoring prefab, or
    /// plain objects) from the current slot prefabs. Everything is carried over: ids, positions, operators, outputs,
    /// bonuses, locking and chain markers. A board that was a prefab variant becomes a variant of the converted base, so
    /// the base-and-variant structure survives and only the differences are stored in each variant. The old boards stay
    /// untouched; the new ones are saved next to them as "(converted)" and are overwritten if the tool runs again. The
    /// encounters that used an old (or earlier converted) board are pointed at the new one.
    /// </summary>
    public static class OldBoardConverter
    {
        private static readonly string[] Folders = { "Assets/BoardLayouts", "Assets/Boards" };

        private sealed class SlotData
        {
            public string Id;
            public Vector3 Position;
            public BonusKind BonusKind;
            public float BonusAmount;
            public bool Locked;
            public int UnlockApCost;
            public int UnlockAtWave;
            public int UnlockAtTurn;
            public string UnlockLaneId;
            public List<(string lane, ChainRole role)> Markers = new List<(string, ChainRole)>();
        }

        private sealed class OperatorData
        {
            public string Id;
            public Vector3 Position;
            public Operation Operation;
            public string FromId;
            public List<string> OutputIds = new List<string>();
        }

        private sealed class BoardData
        {
            public string Path;
            public string Name;
            public string BasePath;
            public string DestinationPath;
            public Vector2 BoardSize;
            public Vector2 BoardCenter;
            public Vector2 ScreenSize;
            public List<SlotData> Slots = new List<SlotData>();
            public List<OperatorData> Operators = new List<OperatorData>();
        }

        /// <summary>How many board prefabs are not made from the current slot prefabs yet (the converter would rebuild them).</summary>
        public static int CountOldStyleBoards()
        {
            return FindOldStyleBoards().Count;
        }

        private static List<(string path, GameObject prefab, BoardLayoutAuthoring board)> FindOldStyleBoards()
        {
            var existing = Folders.Where(AssetDatabase.IsValidFolder).ToArray();
            var found = new List<(string, GameObject, BoardLayoutAuthoring)>();
            if (existing.Length == 0)
            {
                return found;
            }

            var paths = AssetDatabase.FindAssets("t:Prefab", existing)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !path.Contains("/_Authoring/") && !path.EndsWith(" (converted).prefab"));
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var board = prefab != null ? prefab.GetComponent<BoardLayoutAuthoring>() : null;
                if (board != null && IsOldStyle(board))
                {
                    found.Add((path, prefab, board));
                }
            }

            return found;
        }

        [MenuItem("Tools/Fun Game/Convert Old Boards")]
        private static void ConvertAll()
        {
            // 1. read everything first, while every board is still intact (variants need their base)
            var oldBoards = FindOldStyleBoards().Select(old => Read(old.path, old.prefab, old.board)).ToList();

            if (oldBoards.Count == 0)
            {
                Debug.Log("No old-style boards found: every board already uses the current slot prefabs.");
                return;
            }

            // 2. remember which encounters use which board
            var encounters = AssetDatabase.FindAssets("t:EncounterDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<EncounterDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(encounter => encounter != null && encounter.Board != null)
                .ToList();
            var encounterBoardPaths = encounters.ToDictionary(encounter => encounter, encounter => AssetDatabase.GetAssetPath(encounter.Board));

            // 3. write the new boards, bases before the variants that depend on them
            var written = new HashSet<string>();
            var remaining = new List<BoardData>(oldBoards);
            var converted = 0;
            while (remaining.Count > 0)
            {
                var ready = remaining.FirstOrDefault(data =>
                    data.BasePath == null || written.Contains(data.BasePath) || oldBoards.All(other => other.Path != data.BasePath));
                if (ready == null)
                {
                    ready = remaining[0]; // a cycle should be impossible; do not loop forever anyway
                }

                remaining.Remove(ready);
                var baseDestination = ready.BasePath == null
                    ? null
                    : (oldBoards.FirstOrDefault(other => other.Path == ready.BasePath)?.DestinationPath ?? ready.BasePath);
                Write(ready, baseDestination);
                written.Add(ready.Path);
                converted++;
                Debug.Log($"Converted '{ready.Path}' -> '{ready.DestinationPath}'{(baseDestination != null ? $" as a variant of '{baseDestination}'" : string.Empty)}.");
            }

            // 4. point the encounters at the new boards
            foreach (var encounter in encounters)
            {
                var boardPath = encounterBoardPaths[encounter];
                var data = oldBoards.FirstOrDefault(candidate => candidate.Path == boardPath || candidate.DestinationPath == boardPath);
                if (data == null)
                {
                    continue;
                }

                var newBoard = AssetDatabase.LoadAssetAtPath<GameObject>(data.DestinationPath).GetComponent<BoardLayoutAuthoring>();
                var serialized = new SerializedObject(encounter);
                serialized.FindProperty("_board").objectReferenceValue = newBoard;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(encounter);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{converted} board(s) converted. The old boards are untouched; delete them once you are happy with the new ones.");
        }

        private static bool IsOldStyle(BoardLayoutAuthoring board)
        {
            return board.GetSlots().Any(slot => slot.GetComponent<SlotView>() == null) ||
                   board.GetOperators().Any(operatorSlot => operatorSlot.GetComponent<OperatorView>() == null);
        }

        private static BoardData Read(string path, GameObject prefab, BoardLayoutAuthoring board)
        {
            var settings = new SerializedObject(board);
            var directory = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var data = new BoardData
            {
                Path = path,
                Name = board.name,
                DestinationPath = $"{directory}/{board.name} (converted).prefab",
                BoardSize = settings.FindProperty("_boardSize").vector2Value,
                BoardCenter = settings.FindProperty("_boardCenter").vector2Value,
                ScreenSize = settings.FindProperty("_screenSize").vector2Value
            };

            if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant)
            {
                var basePrefab = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
                data.BasePath = basePrefab != null ? AssetDatabase.GetAssetPath(basePrefab) : null;
            }

            foreach (var slot in board.GetSlots())
            {
                var slotData = new SlotData
                {
                    Id = slot.Id,
                    Position = board.transform.InverseTransformPoint(slot.transform.position),
                    BonusKind = slot.BonusKind,
                    BonusAmount = slot.BonusAmount,
                    Locked = slot.IsLocked,
                    UnlockApCost = slot.UnlockApCost,
                    UnlockAtWave = slot.UnlockAtWave,
                    UnlockAtTurn = slot.UnlockAtTurn,
                    UnlockLaneId = slot.UnlockLaneId
                };
                foreach (var marker in slot.GetComponents<BoardChainMarker>())
                {
                    slotData.Markers.Add((marker.LaneId, marker.Role));
                }

                data.Slots.Add(slotData);
            }

            foreach (var operatorSlot in board.GetOperators())
            {
                data.Operators.Add(new OperatorData
                {
                    Id = operatorSlot.Id,
                    Position = board.transform.InverseTransformPoint(operatorSlot.transform.position),
                    Operation = operatorSlot.Operation,
                    FromId = operatorSlot.From != null ? operatorSlot.From.Id : null,
                    OutputIds = operatorSlot.Outputs.Where(output => output != null).Select(output => output.Id).ToList()
                });
            }

            return data;
        }

        /// <summary>Writes the converted board. With a base path it becomes a variant of that base, otherwise a standalone board.</summary>
        private static void Write(BoardData data, string baseDestination)
        {
            GameObject root;
            if (baseDestination == null)
            {
                root = BoardLayoutPrefabCreator.BuildLayoutRoot(data.Name + " (converted)");
            }
            else
            {
                root = BoardVariantCreator.Begin(AssetDatabase.LoadAssetAtPath<GameObject>(baseDestination), data.Name + " (converted)");
            }

            var layout = root.GetComponent<BoardLayoutAuthoring>();
            ApplyBoardSettings(layout, data);

            var slotsById = layout.GetSlots().ToDictionary(slot => slot.Id);
            foreach (var slotData in data.Slots)
            {
                if (!slotsById.TryGetValue(slotData.Id, out var slot))
                {
                    slot = (BoardSlotAuthoring)PrefabUtility.InstantiatePrefab(BoardAuthoringAssets.PilotSlotPrefab, root.transform);
                    slot.gameObject.name = slotData.Id;
                    slot.Configure(slotData.Id);
                    slotsById[slotData.Id] = slot;
                }

                slot.transform.localPosition = slotData.Position;
                slot.ConfigureBonus(slotData.BonusKind, slotData.BonusAmount);
                slot.ConfigureLock(slotData.Locked, slotData.UnlockApCost, slotData.UnlockAtWave, slotData.UnlockAtTurn, slotData.UnlockLaneId);
                foreach (var (lane, role) in slotData.Markers)
                {
                    if (!slot.GetComponents<BoardChainMarker>().Any(marker => marker.LaneId == lane && marker.Role == role))
                    {
                        slot.gameObject.AddComponent<BoardChainMarker>().Configure(lane, role);
                    }
                }

                RecordIfInstance(slot.transform);
                RecordIfInstance(slot);
            }

            var operatorsById = layout.GetOperators().ToDictionary(operatorSlot => operatorSlot.Id);
            foreach (var operatorData in data.Operators)
            {
                if (!operatorsById.TryGetValue(operatorData.Id, out var operatorSlot))
                {
                    operatorSlot = (BoardOperatorSlotAuthoring)PrefabUtility.InstantiatePrefab(BoardAuthoringAssets.OperatorSlotPrefab, root.transform);
                    operatorSlot.gameObject.name = operatorData.Id;
                    operatorsById[operatorData.Id] = operatorSlot;
                }

                operatorSlot.transform.localPosition = operatorData.Position;
                slotsById.TryGetValue(operatorData.FromId ?? string.Empty, out var from);
                var outputs = operatorData.OutputIds
                    .Select(id => slotsById.TryGetValue(id, out var found) ? found : null)
                    .Where(found => found != null)
                    .ToList();
                operatorSlot.Configure(operatorData.Id, from, outputs.FirstOrDefault(), operatorData.Operation);
                foreach (var output in outputs.Skip(1))
                {
                    operatorSlot.AddOutput(output);
                }

                RecordIfInstance(operatorSlot.transform);
                RecordIfInstance(operatorSlot);
            }

            BoardVariantCreator.Save(root, data.DestinationPath);
        }

        private static void ApplyBoardSettings(BoardLayoutAuthoring layout, BoardData data)
        {
            var settings = new SerializedObject(layout);
            settings.FindProperty("_boardSize").vector2Value = data.BoardSize;
            settings.FindProperty("_boardCenter").vector2Value = data.BoardCenter;
            settings.FindProperty("_screenSize").vector2Value = data.ScreenSize;
            settings.ApplyModifiedPropertiesWithoutUndo();

            var bounds = layout.transform.Find(BoardLayoutAuthoring.BoundsChildName);
            if (bounds != null && !PrefabUtility.IsPartOfPrefabInstance(bounds))
            {
                bounds.localPosition = new Vector3(data.BoardCenter.x, data.BoardCenter.y, 0f);
                bounds.localScale = new Vector3(data.BoardSize.x, data.BoardSize.y, 1f);
            }
        }

        /// <summary>Direct field changes on an object of a prefab instance must be recorded to become overrides of the variant.</summary>
        private static void RecordIfInstance(Object target)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(target))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
        }
    }
}
