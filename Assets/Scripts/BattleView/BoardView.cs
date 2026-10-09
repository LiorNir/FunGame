using System;
using System.Collections.Generic;
using System.Linq;
using BoardAuthoring;
using DG.Tweening;
using FunGame.Battle;
using FunGame.Board;
using TMPro;
using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// Builds the playable board from the encounter's board prefab and keeps it in sync with the battle. The board prefab
    /// only says where the slots are and how they connect; the look of the slots, operators and chain labels comes from
    /// the prefabs in the Battle Visuals. Put this component on the scene's board root object: moving or scaling that
    /// object moves or scales the whole board.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [Header("Lines between slots")]
        [SerializeField] private float _lineWidth = 0.05f;
        [SerializeField] private float _poweredLineWidth = 0.1f;

        [Header("Chain value labels")]
        [Tooltip("Height of the label above the last slot of a chain.")]
        [SerializeField] private float _chainLabelOffsetY = 0.72f;
        [SerializeField] private float _chainLabelSpacing = 0.32f;

        private sealed class LineSegment
        {
            public LineRenderer Line;
            public BoardLayoutAuthoring.RoleFlags Roles;
            public List<string> PoweredBySlots = new List<string>();
        }

        private sealed class ChainLabel
        {
            public ChainDefinition Chain;
            public TextMeshPro Text;
        }

        private readonly Dictionary<string, SlotView> _slots = new Dictionary<string, SlotView>();
        private readonly List<LineSegment> _lines = new List<LineSegment>();
        private readonly List<ChainLabel> _labels = new List<ChainLabel>();
        private readonly HashSet<string> _activeSlots = new HashSet<string>();
        private Dictionary<string, ChainResult> _lastResults = new Dictionary<string, ChainResult>();

        private readonly List<GameObject> _created = new List<GameObject>();
        private BattleEngine _engine;
        private BattleVisuals _visuals;

        public event Action<SlotView> SlotClicked;

        private T Track<T>(T created) where T : Component
        {
            _created.Add(created.gameObject);
            return created;
        }

        public SlotView GetSlot(string slotId)
        {
            return _slots.TryGetValue(slotId, out var view) ? view : null;
        }

        public IEnumerable<SlotView> Slots => _slots.Values;

        public void Build(BoardLayoutAuthoring boardPrefab, BattleEngine engine, BattleVisuals visuals)
        {
            Clear();
            _engine = engine;
            _visuals = visuals;

            var board = Track(Instantiate(boardPrefab, transform));
            board.transform.localPosition = Vector3.zero;

            // the pale board-area rectangle is an editing aid only
            var bounds = board.transform.Find(BoardLayoutAuthoring.BoundsChildName);
            if (bounds != null)
            {
                bounds.gameObject.SetActive(false);
            }

            // the slots and operators of the board prefab are the playable views themselves
            foreach (var slot in board.GetSlots())
            {
                var view = slot.GetComponent<SlotView>();
                if (view == null)
                {
                    Debug.LogError($"Slot '{slot.Id}' of the board '{boardPrefab.name}' is not made from the current slot prefab (it has no SlotView). Convert old boards with Tools > Fun Game > Convert Old Boards.", boardPrefab);
                    continue;
                }

                view.Init(slot.Id, visuals, board.GetRolesThrough(slot), slot.Bonus);
                view.Clicked += clicked => SlotClicked?.Invoke(clicked);
                _slots[slot.Id] = view;
            }

            foreach (var operatorSlot in board.GetOperators())
            {
                BuildOperator(board, operatorSlot);
            }

            foreach (var chain in engine.Setup.Board.Chains)
            {
                var label = visuals.ChainLabelPrefab != null
                    ? Track(Instantiate(visuals.ChainLabelPrefab, transform))
                    : Track(ViewFactory.CreateWorldText(transform, string.Empty, string.Empty, 0.3f, Color.white, 9, Vector3.zero));
                label.name = "Chain " + chain.Id;
                _labels.Add(new ChainLabel { Chain = chain, Text = label });
            }

            Refresh();
        }

        public void Clear()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Destroy(created);
                }
            }

            _created.Clear();
            _slots.Clear();
            _lines.Clear();
            _labels.Clear();
            _activeSlots.Clear();
        }

        /// <summary>Brings every slot, line and chain label in line with the engine's current state.</summary>
        public void Refresh()
        {
            if (_engine == null)
            {
                return;
            }

            _lastResults = _engine.EvaluateChains();
            _activeSlots.Clear();
            foreach (var result in _lastResults.Values)
            {
                foreach (var step in result.Steps.Where(step => step.Status == SlotStatus.Active))
                {
                    _activeSlots.Add(step.NodeId);
                }
            }

            foreach (var pair in _slots)
            {
                _engine.PilotBySlot.TryGetValue(pair.Key, out var pilot);
                var locked = !_engine.IsUnlocked(pair.Key);
                pair.Value.Apply(pilot, locked, _activeSlots.Contains(pair.Key), DescribeLock(pair.Key));
            }

            foreach (var segment in _lines)
            {
                var powered = segment.PoweredBySlots.Any(_activeSlots.Contains);
                ApplyLineStyle(segment, powered);
            }

            var perSlotCount = new Dictionary<string, int>();
            foreach (var label in _labels)
            {
                var terminal = GetSlot(label.Chain.TerminalId);
                if (terminal == null)
                {
                    continue;
                }

                perSlotCount.TryGetValue(label.Chain.TerminalId, out var index);
                perSlotCount[label.Chain.TerminalId] = index + 1;

                var isAttack = label.Chain.Role == ChainRole.Attack;
                var value = _lastResults.TryGetValue(label.Chain.Id, out var result) ? result.Value : 0f;
                label.Text.text = (isAttack ? "ATK " : "DEF ") + DisplayFormat.Value(value);
                label.Text.color = _visuals.ColorForRole(label.Chain.Role);
                label.Text.transform.localPosition = terminal.transform.localPosition + new Vector3(0f, _chainLabelOffsetY + index * _chainLabelSpacing, 0f);
            }
        }

        public void SetHighlights(IEnumerable<string> slotIds)
        {
            var wanted = new HashSet<string>(slotIds);
            foreach (var pair in _slots)
            {
                pair.Value.SetHighlight(wanted.Contains(pair.Key));
            }
        }

        public void ClearHighlights()
        {
            SetHighlights(new string[0]);
        }

        /// <summary>Makes the pilots of a lane's chains of one role pulse one after another, as they were before the turn resolved.</summary>
        public void PulseChains(string laneId, ChainRole role)
        {
            var delay = 0f;
            foreach (var result in _lastResults.Values.Where(result => result.Chain.LaneId == laneId && result.Chain.Role == role))
            {
                foreach (var step in result.Steps.Where(step => step.Status == SlotStatus.Active))
                {
                    var view = GetSlot(step.NodeId);
                    if (view == null)
                    {
                        continue;
                    }

                    DOVirtual.DelayedCall(delay, () => view.Punch(0.25f));
                    delay += 0.08f;
                }
            }
        }

        public IEnumerable<SlotView> SlotsOfChains(string laneId, ChainRole role)
        {
            return _lastResults.Values
                .Where(result => result.Chain.LaneId == laneId && result.Chain.Role == role)
                .SelectMany(result => result.Steps.Where(step => step.Status == SlotStatus.Active))
                .Select(step => GetSlot(step.NodeId))
                .Where(view => view != null)
                .Distinct();
        }

        private void BuildOperator(BoardLayoutAuthoring board, BoardOperatorSlotAuthoring operatorSlot)
        {
            var view = operatorSlot.GetComponent<OperatorView>();
            if (view != null)
            {
                view.Show(operatorSlot.Operation, _visuals);
            }

            var roles = BoardLayoutAuthoring.RoleFlags.None;
            var poweredBy = new List<string>();
            foreach (var output in operatorSlot.Outputs.Where(output => output != null))
            {
                roles |= board.GetRolesThrough(output);
                poweredBy.Add(output.Id);
            }

            if (operatorSlot.From != null)
            {
                AddLine(operatorSlot.From.transform.position, operatorSlot.transform.position, roles, poweredBy);
            }

            foreach (var output in operatorSlot.Outputs.Where(output => output != null))
            {
                AddLine(operatorSlot.transform.position, output.transform.position, board.GetRolesThrough(output), new List<string> { output.Id });
            }
        }

        private void AddLine(Vector3 from, Vector3 to, BoardLayoutAuthoring.RoleFlags roles, List<string> poweredBy)
        {
            var line = Track(ViewFactory.CreateLine(transform, "Line", from, to, _lineWidth, 0));
            var segment = new LineSegment { Line = line, Roles = roles, PoweredBySlots = poweredBy };
            _lines.Add(segment);
            ApplyLineStyle(segment, false);
        }

        private void ApplyLineStyle(LineSegment segment, bool powered)
        {
            var color = _visuals.ColorForRoles(segment.Roles);
            if (!powered)
            {
                color.a = 0.35f;
            }

            segment.Line.startColor = color;
            segment.Line.endColor = color;
            var width = powered ? _poweredLineWidth : _lineWidth;
            segment.Line.startWidth = width;
            segment.Line.endWidth = width;
        }

        private string DescribeLock(string slotId)
        {
            var rule = _engine.Setup.SlotRules.FirstOrDefault(candidate => candidate.SlotId == slotId);
            if (rule == null || !rule.Locked)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (rule.UnlockAtTurn > 0)
            {
                parts.Add($"turn {rule.UnlockAtTurn}");
            }

            if (rule.UnlockAtWave > 0)
            {
                parts.Add($"enemy {rule.UnlockAtWave}");
            }

            if (rule.UnlockApCost > 0)
            {
                parts.Add($"{rule.UnlockApCost} AP");
            }

            return string.Join(" / ", parts);
        }
    }
}
