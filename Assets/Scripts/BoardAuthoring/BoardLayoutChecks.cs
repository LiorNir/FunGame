using System.Collections.Generic;
using FunGame.Board;

namespace BoardAuthoring
{
    /// <summary>Checks that only the authored objects can answer (broken references, bad connections, slot settings).</summary>
    public static class BoardLayoutChecks
    {
        public static List<ValidationIssue> Validate(BoardLayoutAuthoring layout)
        {
            var issues = new List<ValidationIssue>();
            ValidateOperators(layout, issues);
            ValidateSlots(layout, issues);
            return issues;
        }

        private static void ValidateOperators(BoardLayoutAuthoring layout, List<ValidationIssue> issues)
        {
            var operatorIds = new HashSet<string>();
            foreach (var operatorSlot in layout.GetOperators())
            {
                var name = string.IsNullOrEmpty(operatorSlot.Id) ? operatorSlot.name : operatorSlot.Id;
                if (string.IsNullOrEmpty(operatorSlot.Id))
                {
                    issues.Add(Error($"Operator slot '{operatorSlot.name}' has no id."));
                }
                else if (!operatorIds.Add(operatorSlot.Id))
                {
                    issues.Add(Error($"Duplicate operator slot id '{operatorSlot.Id}'."));
                }

                if (operatorSlot.From == null || operatorSlot.Outputs.Count == 0)
                {
                    issues.Add(Error($"Operator slot '{name}' is not connected at both ends (set From and at least one output)."));
                    continue;
                }

                var seenOutputs = new HashSet<BoardSlotAuthoring>();
                foreach (var output in operatorSlot.Outputs)
                {
                    if (output == null)
                    {
                        issues.Add(Error($"Operator slot '{name}' has an empty entry in its outputs."));
                        continue;
                    }

                    if (!seenOutputs.Add(output))
                    {
                        issues.Add(Error($"Operator slot '{name}' lists output '{output.Id}' more than once."));
                    }

                    if (output == operatorSlot.From)
                    {
                        issues.Add(Error($"Operator slot '{name}' connects a slot to itself."));
                    }

                    if (output.GetComponentInParent<BoardLayoutAuthoring>(true) != layout)
                    {
                        issues.Add(Error($"Operator slot '{name}' connects to a slot outside this board."));
                    }
                }

                if (operatorSlot.From.GetComponentInParent<BoardLayoutAuthoring>(true) != layout)
                {
                    issues.Add(Error($"Operator slot '{name}' takes its value from a slot outside this board."));
                }
            }

            var position = layout.transform.position;
            if (position != UnityEngine.Vector3.zero)
            {
                issues.Add(Warning(
                    $"The board root is at {position} instead of the origin. The zones and bounds assume it is at (0, 0, 0)."));
            }
        }

        private static void ValidateSlots(BoardLayoutAuthoring layout, List<ValidationIssue> issues)
        {
            var laneIds = new List<string>();
            foreach (var marker in layout.GetChainMarkers())
            {
                if (!laneIds.Contains(marker.LaneId))
                {
                    laneIds.Add(marker.LaneId);
                }
            }

            foreach (var slot in layout.GetSlots())
            {
                if (layout.GetIncomingOperators(slot).Count > 1)
                {
                    issues.Add(Error(
                        $"Slot '{slot.Id}' has more than one operator slot flowing into it. Merging is not supported yet."));
                }

                if (slot.IsLocked && layout.GetIncomingOperators(slot).Count == 0)
                {
                    issues.Add(Error(
                        $"Slot '{slot.Id}' starts a chain (nothing flows into it) but is locked. A chain's first slot cannot be locked."));
                }

                if (slot.IsLocked && !slot.HasUnlockPath)
                {
                    issues.Add(Warning(
                        $"Slot '{slot.Id}' is locked but has no unlock AP cost, wave or turn, so it can never be unlocked."));
                }

                if (slot.UnlockAtWave > 0 && laneIds.Count > 0)
                {
                    if (string.IsNullOrEmpty(slot.UnlockLaneId) && laneIds.Count > 1)
                    {
                        issues.Add(Error($"Slot '{slot.Id}' unlocks by wave but the board has several lanes. Choose the lane whose enemy number counts."));
                    }
                    else if (!string.IsNullOrEmpty(slot.UnlockLaneId) && !laneIds.Contains(slot.UnlockLaneId))
                    {
                        issues.Add(Error($"Slot '{slot.Id}' unlocks by the waves of lane '{slot.UnlockLaneId}', which is not a lane of this board."));
                    }
                }

                if (slot.BonusKind == BonusKind.Multiply && slot.BonusAmount == 0f)
                {
                    issues.Add(Warning($"Slot '{slot.Id}' has a multiply bonus of 0, which zeroes its chain."));
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
