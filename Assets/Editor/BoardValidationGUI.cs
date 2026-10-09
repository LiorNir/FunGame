using System.Collections.Generic;
using System.Linq;
using BoardAuthoring;
using FunGame.Board;
using UnityEditor;

namespace FunGame.EditorTools
{
    /// <summary>Shared helpers for showing board validation results in inspectors.</summary>
    public static class BoardValidationGUI
    {
        public static List<ValidationIssue> ValidateBoard(BoardLayoutAuthoring board)
        {
            var issues = BoardLayoutChecks.Validate(board);
            issues.AddRange(BoardValidator.Validate(BoardConverter.ToGraph(board)));
            return issues;
        }

        public static bool HasErrors(IEnumerable<ValidationIssue> issues)
        {
            return BoardValidator.HasErrors(issues);
        }

        public static void Draw(IEnumerable<ValidationIssue> issues)
        {
            var list = issues.ToList();
            if (list.Count == 0)
            {
                EditorGUILayout.HelpBox("No validation issues.", MessageType.Info);
                return;
            }

            foreach (var issue in list)
            {
                EditorGUILayout.HelpBox(issue.Message,
                    issue.Severity == IssueSeverity.Error ? MessageType.Error : MessageType.Warning);
            }
        }
    }
}
