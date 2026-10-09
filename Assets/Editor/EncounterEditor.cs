using System.Linq;
using BoardAuthoring;
using Combat;
using Encounters;
using FunGame.Board;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    [CustomEditor(typeof(EncounterDefinition))]
    public class EncounterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var encounter = (EncounterDefinition)target;

            DrawLanesOfBoard(encounter);
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            BoardValidationGUI.Draw(EncounterChecks.Validate(encounter));

            if (GUILayout.Button("Open in Encounter Simulator"))
            {
                EncounterSimulatorWindow.Open(encounter);
            }

            using (new EditorGUI.DisabledScope(encounter.Board == null))
            {
                if (GUILayout.Button("Open board in Board Solver"))
                {
                    BoardSolverWindow.Open(encounter.Board);
                }
            }
        }

        private static void DrawLanesOfBoard(EncounterDefinition encounter)
        {
            if (encounter.Board == null)
            {
                EditorGUILayout.HelpBox("Assign a board prefab to see its lanes.", MessageType.Info);
                return;
            }

            var markers = encounter.Board.GetChainMarkers();
            var laneIds = markers.Select(marker => marker.LaneId).Distinct().ToList();
            if (laneIds.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "This board has no lanes yet. A lane exists once a slot has a terminal marker (Board Chain Marker) with a lane id.",
                    MessageType.Warning);
                return;
            }

            var description = string.Join("\n", laneIds.Select(id =>
            {
                var attack = markers.Count(marker => marker.LaneId == id && marker.Role == ChainRole.Attack);
                var defense = markers.Count(marker => marker.LaneId == id && marker.Role == ChainRole.Defense);
                return $"{id}   ({attack} attack chain(s), {defense} defense chain(s))";
            }));
            EditorGUILayout.HelpBox(
                "Lanes on this board (set on the Board Chain Marker of each terminal slot):\n" + description,
                MessageType.None);

            var missing = laneIds.Where(id => encounter.Lanes.All(lane => lane.LaneId != id)).ToList();
            using (new EditorGUI.DisabledScope(missing.Count == 0))
            {
                if (GUILayout.Button("Add the lanes the encounter is missing"))
                {
                    Undo.RecordObject(encounter, "Add lanes from board");
                    var lanes = encounter.Lanes.ToList();
                    lanes.AddRange(missing.Select(id => new LaneEnemies(id)));
                    encounter.SetData(encounter.DisplayName, encounter.Board, lanes, encounter.RewardSkillPoints);
                    EditorUtility.SetDirty(encounter);
                }
            }
        }
    }
}
