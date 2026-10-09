using BoardAuthoring;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    [CustomEditor(typeof(BoardLayoutAuthoring))]
    public class BoardLayoutAuthoringEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var board = (BoardLayoutAuthoring)target;

            EditorGUILayout.HelpBox(
                "This prefab is the whole board. Place pilot slots and operator slots as children of this object and set " +
                "everything about each one on the slot itself (operator, bonus, locking). Click the green + beside a slot " +
                "in the Scene view to add an operator slot and the next pilot slot after it. Mark the last slot of each " +
                "chain with a terminal marker. The pale rectangle is the board area; the faint frame is the camera view. " +
                "For several variations of one board, use \"Create variant of this board\" below and change only what differs.",
                MessageType.Info);

            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Add root slot"))
            {
                var rootCount = 0;
                foreach (var slot in board.GetSlots())
                {
                    if (board.GetIncomingOperators(slot).Count == 0)
                    {
                        rootCount++;
                    }
                }

                var created = BoardAuthoringUtility.AddSlot(board, BoardAuthoringUtility.DefaultRootPosition(board, rootCount));
                Selection.activeGameObject = created.gameObject;
            }

            if (GUILayout.Button("Center all operator slots between their slots"))
            {
                foreach (var operatorSlot in board.GetOperators())
                {
                    BoardAuthoringUtility.CenterBetweenSlots(operatorSlot);
                }
            }

            if (board.transform.Find(BoardLayoutAuthoring.BoundsChildName) == null &&
                GUILayout.Button("Add board-bounds rectangle (missing)"))
            {
                var bounds = BoardLayoutPrefabCreator.CreateBoundsChild(board);
                Undo.RegisterCreatedObjectUndo(bounds, "Add board bounds");
            }

            if (board.transform.position != Vector3.zero && GUILayout.Button("Reset board root to the origin"))
            {
                Undo.RecordObject(board.transform, "Reset board root");
                board.transform.position = Vector3.zero;
            }

            var basePrefab = BoardVariantCreator.FindBasePrefab(board);
            using (new EditorGUI.DisabledScope(basePrefab == null))
            {
                if (GUILayout.Button("Create variant of this board"))
                {
                    if (BoardVariantCreator.HasUnsavedChanges(board))
                    {
                        EditorUtility.DisplayDialog("Save the board first",
                            "This board has unsaved changes. Save the prefab (Ctrl+S) so the variant starts from the saved board.", "OK");
                    }
                    else
                    {
                        if (BoardVariantCreator.CreateInteractively(basePrefab) != null)
                        {
                            GUIUtility.ExitGUI(); // the variant is open now, so this inspector's target is gone
                        }
                    }
                }
            }

            if (basePrefab == null)
            {
                EditorGUILayout.HelpBox("This board is not a prefab. Save it as a prefab to make variants of it.", MessageType.None);
            }

            if (GUILayout.Button("Open in Board Solver"))
            {
                BoardSolverWindow.Open(board);
            }

            BoardSceneTools.ShowAddButtons = EditorGUILayout.Toggle("Show + buttons in Scene view", BoardSceneTools.ShowAddButtons);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            BoardValidationGUI.Draw(BoardValidationGUI.ValidateBoard(board));
        }
    }
}
