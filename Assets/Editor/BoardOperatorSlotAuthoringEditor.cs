using BoardAuthoring;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    [CustomEditor(typeof(BoardOperatorSlotAuthoring))]
    public class BoardOperatorSlotAuthoringEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var operatorSlot = (BoardOperatorSlotAuthoring)target;
            var board = operatorSlot.GetComponentInParent<BoardLayoutAuthoring>(true);

            EditorGUILayout.Space();
            if (board != null && GUILayout.Button("Add another output pilot slot"))
            {
                var created = BoardAuthoringUtility.AddOutputSlot(board, operatorSlot);
                Selection.activeGameObject = created.gameObject;
            }

            using (new EditorGUI.DisabledScope(operatorSlot.From == null || operatorSlot.Outputs.Count == 0))
            {
                if (GUILayout.Button("Center between its slots"))
                {
                    BoardAuthoringUtility.CenterBetweenSlots(operatorSlot);
                }
            }

            if (operatorSlot.From == null || operatorSlot.Outputs.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Drag a pilot slot from the Hierarchy into From (nearer the player), and add at least one pilot slot to Outputs.",
                    MessageType.Warning);
            }
            else if (operatorSlot.Outputs.Count > 1)
            {
                EditorGUILayout.HelpBox(
                    "With several outputs the chain branches here: each output receives the incoming value combined with its own pilot using this operator.",
                    MessageType.None);
            }
        }
    }
}
