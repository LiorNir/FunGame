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
        [Tooltip("When the last slot shows a status line above its pilot (an ability, a lock), the label sits at least this far above the line's top, so they do not overlap.")]
        [SerializeField] private float _chainLabelStatusGap = 0.05f;

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

        /// <summary>The mouse entered (true) or left (false) a slot.</summary>
        public event Action<SlotView, bool> SlotHovered;

        /// <summary>The mouse entered (true) or left (false) an operator.</summary>
        public event Action<OperatorView, bool> OperatorHovered;

        // chain values a placement would give (shown on the chain labels while placing); null for none
        private Dictionary<string, float> _preview;

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
                if (engine.Setup.Board.TryGetNode(slot.Id, out var node) &&
                    (node.ConnectionOperation == Operation.Subtract || node.ConnectionOperation == Operation.Divide))
                {
                    view.SetToll(BattleVisuals.OperatorSymbol(node.ConnectionOperation.Value).Replace("/", "\u00f7"));
                }
                view.Clicked += clicked => SlotClicked?.Invoke(clicked);
                view.Hovered += (hovered, entered) => SlotHovered?.Invoke(hovered, entered);
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

        /// <summary>The lines beside a pilot: its ability, its lock, the enemy effect aimed at it. Empty when there is nothing to say.</summary>
        private string DescribePilotStatus(string slotId, PilotUnit pilot, string aimedAt)
        {
            if (pilot == null || pilot.Fallen)
            {
                return string.Empty;
            }

            var parts = DescribeEffects(slotId, pilot);
            if (pilot.IsLocked)
            {
                parts.Add($"LOCKED {pilot.LockedTurns}");
            }

            if (aimedAt != null)
            {
                parts.Add($"next: {aimedAt}");
            }

            return string.Join("\n", parts);
        }

        /// <summary>
        /// What is changing the pilot right now, one line each (temporary look): a snipe (\"SNIPED -50%\"), an attack change
        /// (\"SURGE: ATK x2\", \"CRASHED: ATK 0 until rested\"), and an unused ability (\"Surge ready\", greyed with its cost
        /// while the AP is short).
        /// </summary>
        private List<string> DescribeEffects(string slotId, PilotUnit pilot)
        {
            var parts = new List<string>();
            var multiplier = StatModifier.Product(pilot.AttackModifiers);
            var defenseMultiplier = StatModifier.Product(pilot.DefenseModifiers);

            // a snipe lowers both stats by the same share, so the defense shows it
            if (defenseMultiplier < 0.999f)
            {
                parts.Add($"<color=#FF7A5C>SNIPED -{DisplayFormat.Value((1f - defenseMultiplier) * 100f)}%</color>");
            }

            if (pilot.AttackModifiers.Any(modifier => modifier.IsActive && modifier.LastsUntilRested))
            {
                parts.Add("<color=#FF7070>CRASHED: ATK 0 until rested</color>");
            }
            else if (multiplier > 1.001f)
            {
                parts.Add($"<color=#FFD24A>{(pilot.Ability?.Name ?? "BOOST").ToUpperInvariant()}: ATK x{DisplayFormat.Value(multiplier)}</color>");
            }
            else if (multiplier < 0.999f && System.Math.Abs(multiplier - defenseMultiplier) > 0.001f)
            {
                parts.Add(multiplier <= 0.001f ? "ATK 0 this turn" : $"ATK x{DisplayFormat.Value(multiplier)} this turn");
            }

            var ability = DescribeUnusedAbility(slotId, pilot);
            if (ability.Length > 0)
            {
                parts.Add(ability);
            }

            return parts;
        }

        private string DescribeUnusedAbility(string slotId, PilotUnit pilot)
        {
            if (!pilot.HasUnusedAbility)
            {
                return string.Empty;
            }

            return _engine.CanActivate(slotId)
                ? $"<color=#FFD24A>{pilot.Ability.Name} ready</color>"
                : $"<color=#8C8C8C>{pilot.Ability.Name}: needs {pilot.Ability.ApCost} AP</color>";
        }

        /// <summary>Points the board at another copy of the same battle (an undo), without rebuilding it.</summary>
        public void Rebind(BattleEngine engine)
        {
            _engine = engine;
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

            // pilots that a revealed enemy effect is aimed at (for example a coming lock), so the player can react
            var aimedAt = _engine.IntentTargets().GroupBy(target => target.SlotId)
                .ToDictionary(group => group.Key, group => string.Join(" ", group.Select(target => _engine.DescribeEffectOn(target.Effect, target.SlotId))));

            foreach (var pair in _slots)
            {
                _engine.PilotBySlot.TryGetValue(pair.Key, out var pilot);
                var locked = !_engine.IsUnlocked(pair.Key);
                pair.Value.Apply(pilot, locked, _activeSlots.Contains(pair.Key), DescribeLock(pair.Key));
                pair.Value.SetStatus(DescribePilotStatus(pair.Key, pilot, aimedAt.TryGetValue(pair.Key, out var aimed) ? aimed : null));
                var abilityReady = pilot != null && !pilot.Fallen && pilot.HasUnusedAbility;
                pair.Value.SetAbilityReady(abilityReady, abilityReady && _engine.CanActivate(pair.Key));
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
                // a chain that divides by zero warns before the turn is played (temporary look), so the gag is never a trap
                var dividesByZero = result != null && result.DividedByZero;
                var shown = dividesByZero ? "ERROR \u00f70" : DisplayFormat.Value(value);
                if (_preview != null && _preview.TryGetValue(label.Chain.Id, out var after) && Mathf.Abs(after - value) > 0.001f)
                {
                    // while placing: what the chain would become
                    shown += $" <color=#FFFFFF>\u2192 {DisplayFormat.Value(after)}</color>";
                }

                label.Text.text = (isAttack ? "ATK " : "DEF ") + shown;
                label.Text.color = dividesByZero ? new Color(1f, 0.25f, 0.25f) : _visuals.ColorForRole(label.Chain.Role);
                var position = terminal.transform.localPosition + new Vector3(0f, _chainLabelOffsetY + index * _chainLabelSpacing, 0f);
                if (terminal.HasStatus)
                {
                    // just above the status line, whatever its length and size
                    label.Text.ForceMeshUpdate();
                    var halfHeight = label.Text.textBounds.extents.y * label.Text.transform.lossyScale.y;
                    var statusTop = transform.InverseTransformPoint(new Vector3(0f, terminal.StatusTop, 0f)).y;
                    position.y = Mathf.Max(position.y, statusTop + _chainLabelStatusGap + halfHeight + index * _chainLabelSpacing);
                }

                label.Text.transform.localPosition = position;
            }
        }

        /// <summary>Shows on the chain labels what each chain would become (a placement being considered); null clears it.</summary>
        public void ShowPreview(Dictionary<string, ChainResult> after)
        {
            _preview = after?.ToDictionary(pair => pair.Key, pair => pair.Value.DividedByZero ? 0f : pair.Value.Value);
            Refresh();
        }

        private void ForwardOperatorHover(OperatorView view, bool entered)
        {
            OperatorHovered?.Invoke(view, entered);
        }

        /// <summary>The hover explanation of a slot: its pilot (exact stats, ability, effects, role), or what an empty slot does.</summary>
        public string DescribeSlot(SlotView slot)
        {
            if (_engine == null)
            {
                return null;
            }

            var lines = new List<string>();
            _engine.PilotBySlot.TryGetValue(slot.SlotId, out var pilot);
            if (!_engine.IsUnlocked(slot.SlotId))
            {
                lines.Add($"<b>Locked slot</b>: opens at {DescribeLock(slot.SlotId)}.");
            }
            else if (pilot == null)
            {
                lines.Add("<b>Empty slot</b>: place a pilot from your hand here.");
            }
            else if (pilot.Fallen)
            {
                lines.Add($"<b>{pilot.Name} (fallen)</b>: blocks the chain until a pilot is placed on top of it.");
            }
            else
            {
                lines.Add(_visuals.ExactStats(pilot));
                if (pilot.Ability != null)
                {
                    var state = pilot.AbilityUsed ? "used" : $"{pilot.Ability.ApCost} AP, once per battle";
                    lines.Add($"<b>{pilot.Ability.Name}</b> ({state}): {pilot.Ability.Describe()}");
                }

                lines.AddRange(DescribeEffects(slot.SlotId, pilot).Where(effect => !effect.Contains(" ready") && !effect.Contains("needs")));
                if (pilot.IsLocked)
                {
                    lines.Add($"Locked for {pilot.LockedTurns} turn(s): cannot be replaced or ejected.");
                }

                var chains = _lastResults.Values.Where(result => result.Steps.Any(step => step.NodeId == slot.SlotId)).ToList();
                var active = chains.Where(result => result.Steps.Any(step => step.NodeId == slot.SlotId && step.Status == SlotStatus.Active)).ToList();
                if (chains.Count > 0 && active.Count == 0)
                {
                    lines.Add("Cut off: a gap or a fallen pilot closer to the root stops its chains here.");
                }
                else if (active.Count > 0)
                {
                    lines.Add("Counts for: " + string.Join(", ", active.Select(result =>
                        (_engine.Lanes.Count > 1 ? result.Chain.LaneId + " " : string.Empty) + (result.Chain.Role == ChainRole.Attack ? "attack" : "defense"))));
                }
            }

            if (_engine.Setup.Board.TryGetNode(slot.SlotId, out var node))
            {
                if (slot.TollSymbol != null)
                {
                    lines.Add($"<color=#B0B0B0>Toll ({slot.TollSymbol}): its stat is paid, so it is never wounded.</color>");
                }

                var bonus = HoverTexts.Bonus(node.Bonus);
                if (bonus != null)
                {
                    lines.Add(bonus);
                }
            }

            return string.Join("\n", lines);
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
                view.Hovered -= ForwardOperatorHover;
                view.Hovered += ForwardOperatorHover;
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
