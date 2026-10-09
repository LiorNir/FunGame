using BoardAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Draws a "+" button beside every pilot slot in the Scene view. Clicking it adds an operator slot and a new
    /// pilot slot after that slot, already connected.
    /// </summary>
    [InitializeOnLoad]
    public static class BoardSceneTools
    {
        private const string ShowButtonsKey = "FunGame.BoardTools.ShowAddButtons";

        static BoardSceneTools()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        public static bool ShowAddButtons
        {
            get => SessionState.GetBool(ShowButtonsKey, true);
            set
            {
                SessionState.SetBool(ShowButtonsKey, value);
                SceneView.RepaintAll();
            }
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!ShowAddButtons)
            {
                return;
            }

            foreach (var layout in FindLayouts())
            {
                foreach (var slot in layout.GetSlots())
                {
                    DrawAddButton(layout, slot);
                }

                foreach (var operatorSlot in layout.GetOperators())
                {
                    DrawAddOutputButton(layout, operatorSlot);
                }
            }
        }

        private static void DrawAddButton(BoardLayoutAuthoring layout, BoardSlotAuthoring slot)
        {
            var size = HandleUtility.GetHandleSize(slot.transform.position) * 0.1f;
            var position = slot.transform.position + new Vector3(slot.WorldRadius + 0.35f, -0.1f, 0f);

            Handles.color = new Color(0.4f, 1f, 0.5f);
            if (Handles.Button(position, Quaternion.identity, size, size * 1.3f, Handles.CircleHandleCap))
            {
                var created = BoardAuthoringUtility.AddOperatorAndSlot(layout, slot);
                Selection.activeGameObject = created.gameObject;
            }

            Handles.Label(position + new Vector3(-size * 0.35f, size * 0.9f, 0f), "+");
        }

        // A small button beside an operator slot that adds another output, so the chain branches there.
        private static void DrawAddOutputButton(BoardLayoutAuthoring layout, BoardOperatorSlotAuthoring operatorSlot)
        {
            var size = HandleUtility.GetHandleSize(operatorSlot.transform.position) * 0.07f;
            var position = operatorSlot.transform.position + new Vector3(operatorSlot.WorldRadius + 0.25f, 0f, 0f);

            Handles.color = new Color(0.4f, 0.8f, 1f);
            if (Handles.Button(position, Quaternion.identity, size, size * 1.3f, Handles.CircleHandleCap))
            {
                var created = BoardAuthoringUtility.AddOutputSlot(layout, operatorSlot);
                Selection.activeGameObject = created.gameObject;
            }

            Handles.Label(position + new Vector3(-size * 0.35f, size * 0.9f, 0f), "+");
        }

        private static BoardLayoutAuthoring[] FindLayouts()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                var root = stage.prefabContentsRoot.GetComponent<BoardLayoutAuthoring>();
                return root == null ? new BoardLayoutAuthoring[0] : new[] { root };
            }

            return Object.FindObjectsByType<BoardLayoutAuthoring>(FindObjectsSortMode.None);
        }
    }
}
