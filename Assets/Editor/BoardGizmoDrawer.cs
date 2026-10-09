using System.Linq;
using BoardAuthoring;
using FunGame.Board;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Draws the board bounds, chain colours, connection arrows and the previewed setup's operators in the Scene view.
    /// </summary>
    public static class BoardGizmoDrawer
    {
        // How far the gizmo ring sits outside the edge of the slot's base.
        private const float SlotRingMargin = 0.04f;

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawLayout(BoardLayoutAuthoring layout, GizmoType gizmoType)
        {
            var origin = layout.transform.position;
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawWireCube(origin, new Vector3(layout.ScreenSize.x, layout.ScreenSize.y, 0f));

            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(
                origin + (Vector3)layout.BoardCenter,
                new Vector3(layout.BoardSize.x, layout.BoardSize.y, 0f));
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawZones(ScreenZonesGuide guide, GizmoType gizmoType)
        {
            var origin = guide.transform.position;
            foreach (var zone in guide.Zones)
            {
                var center = origin + (Vector3)zone.Center;
                var half = zone.Size * 0.5f;
                var corners = new[]
                {
                    center + new Vector3(-half.x, -half.y, 0f),
                    center + new Vector3(-half.x, half.y, 0f),
                    center + new Vector3(half.x, half.y, 0f),
                    center + new Vector3(half.x, -half.y, 0f)
                };
                var outline = zone.Color;
                outline.a = 0.8f;
                Handles.DrawSolidRectangleWithOutline(corners, zone.Color, outline);
                Handles.Label(center + new Vector3(-half.x + 0.1f, half.y - 0.1f, 0f), zone.Name, EditorStyles.boldLabel);
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawSlot(BoardSlotAuthoring slot, GizmoType gizmoType)
        {
            var layout = slot.GetComponentInParent<BoardLayoutAuthoring>(true);
            if (layout == null)
            {
                return;
            }

            var color = ColorFor(layout.GetRolesThrough(slot));
            var position = slot.transform.position;
            var markers = slot.GetComponents<BoardChainMarker>();

            var ringRadius = slot.WorldRadius + SlotRingMargin;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(position, ringRadius);
            if (markers.Length > 0)
            {
                Gizmos.DrawWireSphere(position, ringRadius * 1.25f);
            }

            Handles.Label(position + new Vector3(-0.4f, 0.85f, 0f), BuildSlotLabel(slot, markers), EditorStyles.boldLabel);
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawOperatorSlot(BoardOperatorSlotAuthoring operatorSlot, GizmoType gizmoType)
        {
            var layout = operatorSlot.GetComponentInParent<BoardLayoutAuthoring>(true);
            if (layout == null)
            {
                return;
            }

            var position = operatorSlot.transform.position;
            var hasEnds = operatorSlot.From != null && operatorSlot.Outputs.Count > 0;
            var roles = BoardLayoutAuthoring.RoleFlags.None;
            foreach (var output in operatorSlot.Outputs)
            {
                if (output != null)
                {
                    roles |= layout.GetRolesThrough(output);
                }
            }

            Gizmos.color = hasEnds ? ColorFor(roles) : Color.yellow;
            if (operatorSlot.From != null)
            {
                DrawArrow(operatorSlot.From.transform.position, position);
            }

            foreach (var output in operatorSlot.Outputs)
            {
                if (output != null)
                {
                    DrawArrow(position, output.transform.position);
                }
            }

            Handles.Label(position + new Vector3(-0.07f, 0.15f, 0f), OperatorSymbol(operatorSlot.Operation), BoldStyle(Color.black, 22));
            if (!hasEnds)
            {
                Handles.Label(position + new Vector3(-0.3f, -0.25f, 0f), "not connected", EditorStyles.boldLabel);
            }
        }

        private static void DrawArrow(Vector3 from, Vector3 to)
        {
            Gizmos.DrawLine(from, to);
            var direction = (to - from).normalized;
            if (direction == Vector3.zero)
            {
                return;
            }

            var middle = (from + to) * 0.5f;
            var side = new Vector3(-direction.y, direction.x, 0f) * 0.07f;
            var tip = middle + direction * 0.07f;
            Gizmos.DrawLine(tip, middle - direction * 0.07f + side);
            Gizmos.DrawLine(tip, middle - direction * 0.07f - side);
        }

        private static string BuildSlotLabel(BoardSlotAuthoring slot, BoardChainMarker[] markers)
        {
            var label = string.IsNullOrEmpty(slot.Id) ? "(no id)" : slot.Id;
            foreach (var marker in markers)
            {
                label += $"\n{(marker.Role == ChainRole.Attack ? "ATK" : "DEF")} end ({marker.LaneId})";
            }

            if (slot.BonusKind != BonusKind.None)
            {
                label += $"\nbonus {(slot.BonusKind == BonusKind.Add ? "+" : "x")}{slot.BonusAmount}";
            }

            if (slot.IsLocked)
            {
                label += "\nLOCKED" + DescribeUnlock(slot);
            }

            return label;
        }

        private static string DescribeUnlock(BoardSlotAuthoring slot)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (slot.UnlockAtTurn > 0)
            {
                parts.Add($"turn {slot.UnlockAtTurn}");
            }

            if (slot.UnlockAtWave > 0)
            {
                parts.Add($"wave {slot.UnlockAtWave}");
            }

            if (slot.UnlockApCost > 0)
            {
                parts.Add($"{slot.UnlockApCost} AP");
            }

            return parts.Count == 0 ? string.Empty : " (" + string.Join(", ", parts) + ")";
        }

        private static string OperatorSymbol(Operation operation)
        {
            switch (operation)
            {
                case Operation.Add:
                    return "+";
                case Operation.Subtract:
                    return "-";
                case Operation.Multiply:
                    return "x";
                default:
                    return "/";
            }
        }

        private static GUIStyle BoldStyle(Color color, int fontSize)
        {
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = fontSize };
            style.normal.textColor = color;
            return style;
        }

        // the same colours the battle uses: one source, the Battle Visuals
        private static Color ColorFor(BoardLayoutAuthoring.RoleFlags roles)
        {
            return BoardAuthoringAssets.Visuals.ColorForRoles(roles);
        }
    }
}
