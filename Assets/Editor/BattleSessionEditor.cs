using System.Linq;
using BattleView;
using Encounters;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    [CustomEditor(typeof(BattleSession))]
    public class BattleSessionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var visuals = serializedObject.FindProperty("_visuals").objectReferenceValue as BattleVisuals;
            if (visuals == null || visuals.HudPrefab == null || visuals.EnemyPrefab == null)
            {
                EditorGUILayout.HelpBox("The Battle Visuals asset has no prefabs yet. Create the default ones, then edit them freely.", MessageType.Warning);
                if (GUILayout.Button("Create the battle prefabs"))
                {
                    var created = BattleSceneCreator.EnsureVisuals();
                    BattlePrefabCreator.EnsurePrefabs(created, overwrite: false);
                    serializedObject.FindProperty("_visuals").objectReferenceValue = created;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            if (serializedObject.FindProperty("_boardView").objectReferenceValue == null ||
                serializedObject.FindProperty("_enemyAnchors").arraySize == 0 ||
                serializedObject.FindProperty("_playerAnchor").objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("No board root, enemy lane anchors or player anchor in the scene yet. Create them, then move them by hand to place the board, the enemies and the player-related effects.", MessageType.Info);
                if (GUILayout.Button("Create the board root and the anchors"))
                {
                    BattleSceneCreator.EnsureSceneObjects((BattleSession)target);
                    serializedObject.Update();
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Fill the encounter list with every encounter in the project"))
            {
                FillEncounterList();
            }

            EditorGUILayout.Space();
            var session = (BattleSession)target;
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(session.IsRunning ? "Restart battle" : "Start battle", GUILayout.Height(32)))
                    {
                        session.StartBattle();
                    }

                    using (new EditorGUI.DisabledScope(!session.IsRunning))
                    {
                        if (GUILayout.Button("Stop battle", GUILayout.Height(32)))
                        {
                            session.StopBattle();
                        }
                    }
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Press Play, then use these buttons or the test panel at the top of the Game view. You can change the " +
                    "Encounter above while playing and press Restart to try another one.", MessageType.Info);
            }
        }

        private void FillEncounterList()
        {
            var encounters = AssetDatabase.FindAssets("t:EncounterDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<EncounterDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(encounter => encounter != null)
                .OrderBy(encounter => encounter.name)
                .ToList();

            serializedObject.Update();
            var list = serializedObject.FindProperty("_encounters");
            list.arraySize = encounters.Count;
            for (var i = 0; i < encounters.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = encounters[i];
            }

            var current = serializedObject.FindProperty("_encounter");
            if (current.objectReferenceValue == null && encounters.Count > 0)
            {
                current.objectReferenceValue = encounters[0];
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
