using System.Collections.Generic;
using System.Linq;
using BoardAuthoring;
using Encounters;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    [CustomPropertyDrawer(typeof(LaneIdAttribute))]
    public class LaneIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use [LaneId] on a string field.");
                return;
            }

            var laneIds = GetBoardLaneIds(property.serializedObject.targetObject);
            if (laneIds.Count == 0)
            {
                // No board (or no chain markers on it): fall back to plain text.
                property.stringValue = EditorGUI.TextField(position, label, property.stringValue);
                return;
            }

            var options = new List<string>(laneIds);
            var current = property.stringValue;
            var isMissing = !string.IsNullOrEmpty(current) && !laneIds.Contains(current);
            if (isMissing)
            {
                options.Add($"(not on board: {current})");
            }

            var selected = isMissing ? options.Count - 1 : laneIds.IndexOf(current);
            EditorGUI.BeginChangeCheck();
            var chosen = EditorGUI.Popup(position, label.text, selected, options.ToArray());
            if (EditorGUI.EndChangeCheck() && chosen >= 0 && chosen < laneIds.Count)
            {
                property.stringValue = laneIds[chosen];
            }
        }

        public static List<string> GetBoardLaneIds(Object target)
        {
            BoardLayoutAuthoring board = null;
            if (target is EncounterDefinition encounter)
            {
                board = encounter.Board;
            }
            else if (target is BoardSlotAuthoring slot)
            {
                board = slot.GetComponentInParent<BoardLayoutAuthoring>(true);
            }

            return board == null
                ? new List<string>()
                : board.GetChainMarkers().Select(marker => marker.LaneId).Distinct().ToList();
        }
    }
}
