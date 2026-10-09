using System.Collections.Generic;
using System.Linq;
using BoardAuthoring;
using Combat;
using FunGame.Board;

namespace Encounters
{
    /// <summary>Validates an encounter, including its board, its lanes and its enemies.</summary>
    public static class EncounterChecks
    {
        public static List<ValidationIssue> Validate(EncounterDefinition encounter)
        {
            var issues = new List<ValidationIssue>();
            if (encounter.Board == null)
            {
                issues.Add(Error("No board assigned."));
            }
            else
            {
                AddBoardIssues(encounter.Board, issues);
                ValidateLanes(encounter, issues);
            }

            ValidateEnemies(encounter, issues);
            return issues;
        }

        private static void AddBoardIssues(BoardLayoutAuthoring board, List<ValidationIssue> issues)
        {
            var boardIssues = BoardLayoutChecks.Validate(board);
            boardIssues.AddRange(BoardValidator.Validate(BoardConverter.ToGraph(board)));
            foreach (var issue in boardIssues)
            {
                issues.Add(new ValidationIssue(issue.Severity, "Board: " + issue.Message));
            }
        }

        private static void ValidateLanes(EncounterDefinition encounter, List<ValidationIssue> issues)
        {
            var boardLaneIds = encounter.Board.GetChainMarkers().Select(marker => marker.LaneId).Distinct().ToList();
            var seen = new HashSet<string>();
            foreach (var lane in encounter.Lanes)
            {
                if (string.IsNullOrEmpty(lane.LaneId) || !seen.Add(lane.LaneId))
                {
                    issues.Add(Error($"Lane id '{lane.LaneId}' is empty or listed twice."));
                    continue;
                }

                if (!boardLaneIds.Contains(lane.LaneId))
                {
                    issues.Add(Error($"Lane '{lane.LaneId}' does not exist on the board (board lanes: {string.Join(", ", boardLaneIds)})."));
                }

                if (lane.Enemies.Count == 0)
                {
                    issues.Add(Warning($"Lane '{lane.LaneId}' has no enemies."));
                }
            }

            foreach (var boardLaneId in boardLaneIds.Where(id => !seen.Contains(id)))
            {
                issues.Add(Warning($"The board has a lane '{boardLaneId}' that this encounter gives no enemies."));
            }
        }

        private static void ValidateEnemies(EncounterDefinition encounter, List<ValidationIssue> issues)
        {
            var checkedEnemies = new HashSet<EnemyDefinition>();
            foreach (var lane in encounter.Lanes)
            {
                foreach (var enemy in lane.Enemies)
                {
                    if (enemy == null)
                    {
                        issues.Add(Error($"Lane '{lane.LaneId}' has an empty enemy entry."));
                        continue;
                    }

                    if (!checkedEnemies.Add(enemy))
                    {
                        continue;
                    }

                    if (enemy.MaxHealth <= 0)
                    {
                        issues.Add(Error($"Enemy '{enemy.DisplayName}' has no health."));
                    }

                    if (enemy.Actions.Count == 0)
                    {
                        issues.Add(Error($"Enemy '{enemy.DisplayName}' has no actions."));
                    }

                    foreach (var action in enemy.Actions.Where(action => action.Attack == 0 && action.Defense == 0))
                    {
                        issues.Add(Warning($"Enemy '{enemy.DisplayName}' has an action '{action.Name}' that neither attacks nor defends."));
                    }
                }
            }
        }

        private static ValidationIssue Error(string message)
        {
            return new ValidationIssue(IssueSeverity.Error, message);
        }

        private static ValidationIssue Warning(string message)
        {
            return new ValidationIssue(IssueSeverity.Warning, message);
        }
    }
}
