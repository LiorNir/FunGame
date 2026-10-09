using BoardAuthoring;
using FunGame.Board;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    [CustomEditor(typeof(BoardSlotAuthoring))]
    public class BoardSlotAuthoringEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var slot = (BoardSlotAuthoring)target;
            var layout = slot.GetComponentInParent<BoardLayoutAuthoring>(true);
            if (layout == null)
            {
                EditorGUILayout.HelpBox("This slot is not under a Board Layout Authoring root.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Add operator slot + next pilot slot"))
            {
                var next = BoardAuthoringUtility.AddOperatorAndSlot(layout, slot);
                Selection.activeGameObject = next.gameObject;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Mark as attack terminal"))
                {
                    BoardAuthoringUtility.AddChainMarker(slot, ChainRole.Attack);
                }

                if (GUILayout.Button("Mark as defense terminal"))
                {
                    BoardAuthoringUtility.AddChainMarker(slot, ChainRole.Defense);
                }
            }

            var incoming = layout.GetIncomingOperators(slot);
            EditorGUILayout.HelpBox(
                incoming.Count == 0
                    ? "Root slot: nothing flows into it."
                    : $"Fed by operator slot {incoming[0].Id} (from {(incoming[0].From == null ? "?" : incoming[0].From.Id)}).",
                MessageType.None);

            var roles = layout.GetRolesThrough(slot);
            EditorGUILayout.HelpBox(
                roles == BoardLayoutAuthoring.RoleFlags.None
                    ? "No chain passes through this slot yet."
                    : $"Chains through this slot: {roles}.",
                MessageType.None);
        }
    }
}
