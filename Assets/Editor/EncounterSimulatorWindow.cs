using System.Linq;
using Configurations;
using Encounters;
using FunGame.Battle;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Plays an encounter with the scripted player and shows the log of one battle, or a summary of many. Uses the pure
    /// battle engine, so no scene is needed.
    /// </summary>
    public class EncounterSimulatorWindow : EditorWindow
    {
        private EncounterDefinition _encounter;
        private GameRules _gameRules;
        private PilotDeck _deck;
        private int _seed = 1;
        private int _battleCount = 200;
        private Vector2 _scroll;
        private string _output = string.Empty;

        [MenuItem("Tools/Fun Game/Encounter Simulator")]
        private static void OpenFromMenu()
        {
            GetWindow<EncounterSimulatorWindow>("Encounter Simulator");
        }

        public static void Open(EncounterDefinition encounter)
        {
            var window = GetWindow<EncounterSimulatorWindow>("Encounter Simulator");
            window._encounter = encounter;
            window.Show();
        }

        private void OnEnable()
        {
            _gameRules = _gameRules != null ? _gameRules : FindAsset<GameRules>();
            _deck = _deck != null ? _deck : FindAsset<PilotDeck>();
        }

        private void OnGUI()
        {
            _encounter = (EncounterDefinition)EditorGUILayout.ObjectField("Encounter", _encounter, typeof(EncounterDefinition), false);
            _gameRules = (GameRules)EditorGUILayout.ObjectField("Game rules", _gameRules, typeof(GameRules), false);
            _deck = (PilotDeck)EditorGUILayout.ObjectField("Pilot deck", _deck, typeof(PilotDeck), false);
            _seed = EditorGUILayout.IntField("Seed", _seed);
            _battleCount = EditorGUILayout.IntSlider("Battles for the summary", _battleCount, 10, 2000);

            if (_gameRules == null)
            {
                EditorGUILayout.HelpBox("No Game Rules asset: the built-in defaults are used (20 HP, 3 AP, hand of 4). Create one with Tools > Fun Game > Create Default Game Rules And Deck.", MessageType.Info);
            }

            if (_deck == null)
            {
                EditorGUILayout.HelpBox("No Pilot Deck asset. Create one with Tools > Fun Game > Create Default Game Rules And Deck.", MessageType.Warning);
            }

            var canRun = _encounter != null && _deck != null && !BoardValidationGUI.HasErrors(EncounterChecks.Validate(_encounter));
            if (_encounter != null)
            {
                BoardValidationGUI.Draw(EncounterChecks.Validate(_encounter));
            }

            using (new EditorGUI.DisabledScope(!canRun))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Play one battle (show log)"))
                    {
                        var result = BattleRunner.Run(Build(_seed));
                        _output = $"RESULT: {result.Outcome} in {result.Turns} turns, player HP {result.PlayerHealth}\n\n" +
                                  string.Join("\n", result.Log);
                        Debug.Log(_output);
                    }

                    if (GUILayout.Button("Play many (summary)"))
                    {
                        var summary = BattleRunner.RunMany(Build, _seed, _battleCount);
                        _output = summary.ToString();
                        Debug.Log(_output);
                    }
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_output, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private BattleSetup Build(int seed)
        {
            return BattleSetupBuilder.Build(_encounter, _gameRules, _deck, seed);
        }

        private static T FindAsset<T>() where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(asset => asset != null);
        }
    }
}
