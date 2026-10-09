using System.Collections.Generic;
using System.Linq;
using BoardAuthoring;
using Configurations;
using FunGame.Board;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    public class BoardSolverWindow : EditorWindow
    {
        private BoardLayoutAuthoring _board;
        private int _sampleIndex;
        private bool _allowPartial;
        private bool _assumeLockedSlotsUnlocked;
        private float _statScale = 1f;
        private Vector2 _scroll;
        private string _report = string.Empty;

        private List<PilotTokenConfiguration> _pilots = new List<PilotTokenConfiguration>();
        private List<bool> _included = new List<bool>();

        [MenuItem("Tools/Fun Game/Board Solver")]
        private static void OpenFromMenu()
        {
            GetWindow<BoardSolverWindow>("Board Solver");
        }

        public static void Open(BoardLayoutAuthoring board)
        {
            var window = GetWindow<BoardSolverWindow>("Board Solver");
            window._board = board;
            window.Show();
        }

        private void OnEnable()
        {
            RefreshPilots();
        }

        private void RefreshPilots()
        {
            _pilots = AssetDatabase.FindAssets("t:PilotTokenConfiguration")
                .Select(guid => AssetDatabase.LoadAssetAtPath<PilotTokenConfiguration>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(pilot => pilot != null)
                .OrderBy(pilot => pilot.name)
                .ToList();
            _included = _pilots.Select(_ => true).ToList();
        }

        private void OnGUI()
        {
            _board = (BoardLayoutAuthoring)EditorGUILayout.ObjectField("Board prefab", _board, typeof(BoardLayoutAuthoring), true);
            if (_board == null)
            {
                _sampleIndex = EditorGUILayout.Popup("Or built-in sample", _sampleIndex, SampleBoards.Names.ToArray());
            }

            _allowPartial = EditorGUILayout.Toggle("Allow partial placements", _allowPartial);
            _assumeLockedSlotsUnlocked = EditorGUILayout.Toggle("Treat locked slots as unlocked", _assumeLockedSlotsUnlocked);
            _statScale = EditorGUILayout.Slider("Stat scale", _statScale, 0.5f, 10f);

            EditorGUILayout.LabelField("Roster", EditorStyles.boldLabel);
            for (var i = 0; i < _pilots.Count; i++)
            {
                var pilot = _pilots[i];
                _included[i] = EditorGUILayout.ToggleLeft(
                    $"{pilot.name}   ATK {pilot.AttackValue}  DEF {pilot.DefenseValue}", _included[i]);
            }

            if (GUILayout.Button("Refresh pilots"))
            {
                RefreshPilots();
            }

            var roster = BuildRoster();
            var graph = GetGraph(out var lockedSlotIds, out var issues);

            var usableSlots = graph.Nodes.Count(node => !lockedSlotIds.Contains(node.Id));
            var count = BoardSolver.CountArrangements(usableSlots, roster.Count, _allowPartial);
            EditorGUILayout.HelpBox($"{usableSlots} usable slots, {roster.Count} pilots: {count:N0} arrangements.", MessageType.None);
            BoardValidationGUI.Draw(issues);

            using (new EditorGUI.DisabledScope(BoardValidator.HasErrors(issues) || roster.Count == 0))
            {
                if (GUILayout.Button("Solve"))
                {
                    Solve(graph, roster, lockedSlotIds);
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private BoardGraph GetGraph(out HashSet<string> lockedSlotIds, out List<ValidationIssue> issues)
        {
            if (_board == null)
            {
                var sample = SampleBoards.Create(_sampleIndex);
                lockedSlotIds = new HashSet<string>();
                issues = BoardValidator.Validate(sample);
                return sample;
            }

            lockedSlotIds = _assumeLockedSlotsUnlocked ? new HashSet<string>() : BoardConverter.GetLockedSlotIds(_board);
            issues = BoardValidationGUI.ValidateBoard(_board);
            return BoardConverter.ToGraph(_board);
        }

        private List<PilotStats> BuildRoster()
        {
            var roster = new List<PilotStats>();
            for (var i = 0; i < _pilots.Count; i++)
            {
                if (!_included[i])
                {
                    continue;
                }

                var pilot = _pilots[i];
                roster.Add(new PilotStats(pilot.name, pilot.AttackValue * _statScale, pilot.DefenseValue * _statScale));
            }

            return roster;
        }

        private void Solve(BoardGraph graph, List<PilotStats> roster, HashSet<string> lockedSlotIds)
        {
            try
            {
                var options = new SolverOptions { AllowPartial = _allowPartial, LockedSlotIds = lockedSlotIds };
                var result = BoardSolver.Solve(graph, roster, options);
                _report = result.ToReport();
                Debug.Log(_report);
            }
            catch (System.InvalidOperationException exception)
            {
                _report = exception.Message;
            }
        }
    }
}
