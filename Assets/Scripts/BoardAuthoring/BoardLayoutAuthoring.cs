using System;
using System.Collections.Generic;
using FunGame.Board;
using UnityEngine;

namespace BoardAuthoring
{
    /// <summary>
    /// The root of a board layout prefab. Pilot slots are <see cref="BoardSlotAuthoring"/> components and operator
    /// slots are <see cref="BoardOperatorSlotAuthoring"/> components on its children; chains are
    /// <see cref="BoardChainMarker"/> components on terminal slots.
    /// </summary>
    public class BoardLayoutAuthoring : MonoBehaviour
    {
        public const string BoundsChildName = "BoardBounds";

        [Flags]
        public enum RoleFlags
        {
            None = 0,
            Attack = 1,
            Defense = 2
        }

        [Header("Authoring guides (editor only)")]
        [Tooltip("The area of the screen the board may use, in world units (relative to this object).")]
        [SerializeField] private Vector2 _boardSize = BoardScreenMetrics.BoardSize;

        [Tooltip("Centre of the board area, relative to this object.")]
        [SerializeField] private Vector2 _boardCenter = BoardScreenMetrics.BoardCenter;

        [Tooltip("The full camera view in world units (see BoardScreenMetrics). Drawn as a faint frame.")]
        [SerializeField] private Vector2 _screenSize = BoardScreenMetrics.ScreenSize;

        public Vector2 BoardSize => _boardSize;
        public Vector2 BoardCenter => _boardCenter;
        public Vector2 ScreenSize => _screenSize;

        private void OnValidate()
        {
            var bounds = transform.Find(BoundsChildName);
            if (bounds == null)
            {
                return;
            }

            bounds.localPosition = new Vector3(_boardCenter.x, _boardCenter.y, 0f);
            bounds.localScale = new Vector3(_boardSize.x, _boardSize.y, 1f);
        }

        public List<BoardSlotAuthoring> GetSlots()
        {
            return new List<BoardSlotAuthoring>(GetComponentsInChildren<BoardSlotAuthoring>(true));
        }

        public List<BoardOperatorSlotAuthoring> GetOperators()
        {
            return new List<BoardOperatorSlotAuthoring>(GetComponentsInChildren<BoardOperatorSlotAuthoring>(true));
        }

        public List<BoardChainMarker> GetChainMarkers()
        {
            return new List<BoardChainMarker>(GetComponentsInChildren<BoardChainMarker>(true));
        }

        public List<string> GetSlotIds()
        {
            var ids = new List<string>();
            foreach (var slot in GetSlots())
            {
                if (!string.IsNullOrEmpty(slot.Id) && !ids.Contains(slot.Id))
                {
                    ids.Add(slot.Id);
                }
            }

            return ids;
        }

        public List<string> GetOperatorIds()
        {
            var ids = new List<string>();
            foreach (var operatorSlot in GetOperators())
            {
                if (!string.IsNullOrEmpty(operatorSlot.Id) && !ids.Contains(operatorSlot.Id))
                {
                    ids.Add(operatorSlot.Id);
                }
            }

            return ids;
        }

        /// <summary>The operator slots whose To is <paramref name="slot"/>. A valid slot has zero (root) or one.</summary>
        public List<BoardOperatorSlotAuthoring> GetIncomingOperators(BoardSlotAuthoring slot)
        {
            var incoming = new List<BoardOperatorSlotAuthoring>();
            foreach (var operatorSlot in GetOperators())
            {
                if (operatorSlot.HasOutput(slot))
                {
                    incoming.Add(operatorSlot);
                }
            }

            return incoming;
        }

        /// <summary>The pilot slot that feeds <paramref name="slot"/>, or null for a root or an unconnected operator.</summary>
        public BoardSlotAuthoring GetParent(BoardSlotAuthoring slot)
        {
            var incoming = GetIncomingOperators(slot);
            return incoming.Count == 0 ? null : incoming[0].From;
        }

        /// <summary>Which chain roles pass through <paramref name="slot"/> on their way from a root to a terminal.</summary>
        public RoleFlags GetRolesThrough(BoardSlotAuthoring slot)
        {
            var flags = RoleFlags.None;
            foreach (var marker in GetChainMarkers())
            {
                var visited = new HashSet<BoardSlotAuthoring>();
                var current = marker.Slot;
                while (current != null && visited.Add(current))
                {
                    if (current == slot)
                    {
                        flags |= marker.Role == ChainRole.Attack ? RoleFlags.Attack : RoleFlags.Defense;
                        break;
                    }

                    current = GetParent(current);
                }
            }

            return flags;
        }
    }
}
