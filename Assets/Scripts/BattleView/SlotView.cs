using System;
using System.Collections.Generic;
using BoardAuthoring;
using Configurations;
using DG.Tweening;
using FunGame.Battle;
using FunGame.Board;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace BattleView
{
    /// <summary>
    /// The playable look of one pilot slot, as a prefab. Everything it draws is a child of the prefab, so size, position,
    /// sprites and text sizes are set there; this script only fills in the values and switches parts on and off.
    /// <list type="bullet">
    /// <item>PilotBackground: the disc behind the pilot. Its size is the slot's size. Tinted when locked or when the slot
    /// is a valid target.</item>
    /// <item>Pilot: the placed pilot's picture.</item>
    /// <item>StatsBox, holding AttackBox and DefenseBox (two instances of the StatBox prefab, each with a border, a
    /// background, a fill and a number): the pilot's attack and defense, shown while a pilot stands in the slot. The numbers
    /// are coloured by role. A stat is "used" when the slot is in a chain that reads it: then its box is filled (attack
    /// always full, defense by how much is left, which is the old wound bar). An unused stat has no fill and a darkened
    /// border and number.</item>
    /// <item>LockIcon and LockText: shown while the slot is locked, the text under the icon says how it unlocks.</item>
    /// <item>BonusBadge and BonusText: the slot's bonus, shown only when it has one.</item>
    /// <item>Ring: a decorative frame around the slot with the lerp-colour material. This script sets its colours (and the
    /// stat borders') by state: empty, locked, inactive or active (see <see cref="SlotTints"/>).</item>
    /// </list>
    /// Sits at the slot's position and receives clicks (needs a Collider2D).
    /// </summary>
    public class SlotView : MonoBehaviour, ISlotFootprint
    {
        [Header("Parts (assigned in the prefab)")]
        [FormerlySerializedAs("_coin")]
        [SerializeField] private SpriteRenderer _pilotBackground;
        [SerializeField] private SpriteRenderer _pilotRenderer;
        [SerializeField] private StatBoxView _attackBox;
        [SerializeField] private StatBoxView _defenseBox;
        [SerializeField] private TextMeshPro _bonusText;

        [Header("Optional parts (the slot works without them)")]
        [Tooltip("The decorative frame around the slot. Its material is the lerp-colour shader; this script only sets its colours, by state.")]
        [SerializeField] private SpriteRenderer _ring;
        [Tooltip("Parent of both stat boxes; switched on and off as one. Without it only the two boxes are.")]
        [SerializeField] private GameObject _statsBox;
        [Tooltip("Shown when the slot is locked.")]
        [SerializeField] private SpriteRenderer _lockIcon;
        [Tooltip("Under the lock icon: how the slot unlocks.")]
        [SerializeField] private TextMeshPro _lockText;
        [Tooltip("Shown when the slot has a bonus, under the bonus text.")]
        [SerializeField] private SpriteRenderer _bonusBadge;
        [Tooltip("The pilot's name, only for a pilot that has no picture yet.")]
        [FormerlySerializedAs("_centerText")]
        [SerializeField] private TextMeshPro _pilotNameText;

        // INACTIVE LABEL (switched off: an unpowered pilot is shown greyed instead). To bring it back, uncomment this
        // field and the two lines marked INACTIVE LABEL below, uncomment its part in PilotSlotParts.EnsureParts, then run
        // Tools > Fun Game > Upgrade Pilot Slot Prefab.
        // [Tooltip("Shown above the slot when a pilot stands in it but the chain is broken closer to the root.")]
        // [SerializeField] private TextMeshPro _inactiveText;

        [Header("Sizes")]
        [Tooltip("The pilot's picture is scaled to fit in a square of this size (in the units of the Pilot object's parent).")]
        [SerializeField] private float _pilotPictureSize = 0.765f;

        // The slot's own colours live on the prefab. The resting colour of the background is the PilotBackground sprite's
        // colour and the bonus colour is the BonusText colour. The rest are here. Only attack/defense colours (shared by
        // lines, labels and the stat boxes) come from the Battle Visuals.
        [Header("State colours")]
        [Tooltip("Colours of the ring and the stat borders for each state (empty, locked, inactive, active).")]
        [SerializeField] private SlotTints _tints = new SlotTints();
        [Tooltip("The background while the slot is a valid target for the action being chosen.")]
        [SerializeField] private Color _highlightColor = new Color(0.4f, 1f, 0.5f);
        [SerializeField] private Color _lockedColor = new Color(0.25f, 0.25f, 0.3f);
        [SerializeField] private Color _inactiveColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
        [SerializeField] private Color _fallenColor = new Color(0.35f, 0.35f, 0.35f, 0.7f);

        private BattleVisuals _visuals;
        private bool _ready;
        private bool _hasBonus;
        private Color _bonusBaseColor;
        private Color _bonusBadgeBaseColor;
        private PilotUnit _pilot;
        private float _displayWound;
        private bool _locked;
        private bool _highlighted;
        private bool _inactive;
        private Color _backgroundBaseColor;
        private Color _roleColor;
        private SlotVisualState _state;
        private bool _attackUsed;
        private bool _defenseUsed;

        public string SlotId { get; private set; }
        public PilotUnit Pilot => _pilot;
        public event Action<SlotView> Clicked;

        /// <summary>The size of the slot is the size of its background, so it is changed on the prefab's PilotBackground.</summary>
        public float Radius => ViewFactory.SpriteRadius(_pilotBackground, transform);

        /// <param name="roles">Which chains pass through this slot; it decides the colour of the ring.</param>
        public void Init(string slotId, BattleVisuals visuals, BoardLayoutAuthoring.RoleFlags roles, NodeBonus bonus)
        {
            SlotId = slotId;
            _visuals = visuals;
            _roleColor = visuals.ColorForRoles(roles);

            // a stat counts here only if the slot is in a chain that reads it; the other one is shown darkened
            _attackUsed = (roles & BoardLayoutAuthoring.RoleFlags.Attack) != 0;
            _defenseUsed = (roles & BoardLayoutAuthoring.RoleFlags.Defense) != 0;

            if (!HasRequiredParts())
            {
                return;
            }

            if (_pilotBackground != null)
            {
                if (_pilotBackground.sprite == null)
                {
                    _pilotBackground.sprite = visuals.SlotBase;
                }

                _backgroundBaseColor = _pilotBackground.color;
            }

            // the prefab shows sample texts for sizing in prefab mode; start from a clean slate
            _attackBox.Clear();
            _defenseBox.Clear();
            _attackBox.HideFill();
            _defenseBox.HideFill();
            SetText(_lockText, string.Empty);
            SetText(_pilotNameText, string.Empty);
            // INACTIVE LABEL: SetText(_inactiveText, string.Empty);
            _bonusText.text = FormatBonus(bonus);
            _bonusBaseColor = _bonusText.color;
            _hasBonus = bonus.Kind != BonusKind.None;
            if (_bonusBadge != null)
            {
                _bonusBadgeBaseColor = _bonusBadge.color;
                _bonusBadge.enabled = _hasBonus;
            }

            if (_lockIcon != null)
            {
                _lockIcon.enabled = false;
            }

            // clicks land on the background, whatever size the prefab's background has
            var collider = GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                collider.radius = Radius;
            }

            SetPilotPartsVisible(false);
        }

        /// <summary>
        /// Shows the slot as it currently is. <paramref name="powered"/> is false for a pilot that does not count because
        /// the chain is broken further down (a gap or a fallen pilot closer to the start of the chain). That is shown by
        /// greying the pilot and its numbers rather than by a label.
        /// </summary>
        public void Apply(PilotUnit pilot, bool locked, bool powered, string lockDescription)
        {
            if (!_ready)
            {
                return;
            }

            _pilot = pilot;
            _locked = locked;
            _displayWound = pilot != null ? pilot.DefenseWound : 0f;

            var hasPilot = pilot != null;
            var fallen = hasPilot && pilot.Fallen;
            _inactive = hasPilot && !fallen && !powered;

            RefreshBackgroundColor();
            SetState(locked ? SlotVisualState.Locked
                : !hasPilot ? SlotVisualState.Empty
                : fallen || _inactive ? SlotVisualState.Inactive
                : SlotVisualState.Active);

            var source = hasPilot ? pilot.Source as PilotTokenConfiguration : null;
            var sprite = source != null ? source.Sprite : null;
            _pilotRenderer.enabled = hasPilot && sprite != null;
            _pilotRenderer.sprite = sprite;
            ViewFactory.FitSprite(_pilotRenderer, _pilotPictureSize);
            _pilotRenderer.color = fallen ? _fallenColor : (_inactive ? _inactiveColor : Color.white);

            ShowLock(locked, lockDescription);
            SetText(_pilotNameText, !locked && hasPilot && sprite == null ? pilot.Name : string.Empty);
            // INACTIVE LABEL: SetText(_inactiveText, _inactive ? "<b>INACTIVE</b>" : string.Empty);

            if (_hasBonus)
            {
                var alpha = locked || !hasPilot || fallen || _inactive ? 0.5f : 1f;
                _bonusText.color = WithAlpha(_bonusBaseColor, alpha);
                if (_bonusBadge != null)
                {
                    _bonusBadge.color = WithAlpha(_bonusBadgeBaseColor, alpha);
                }
            }

            SetPilotPartsVisible(hasPilot && !fallen);
            UpdateWoundVisual();
        }

        public void SetHighlight(bool on)
        {
            _highlighted = on;
            RefreshBackgroundColor();
        }

        /// <summary>Animates the pilot's wound to a new fraction, also updating the shown defense.</summary>
        public Tween SetWoundAnimated(float wound, float duration)
        {
            return DOTween.To(() => _displayWound, value =>
            {
                _displayWound = value;
                UpdateWoundVisual();
            }, wound, duration).SetEase(Ease.OutCubic);
        }

        public void PlayFallen()
        {
            if (_pilot == null)
            {
                return;
            }

            ViewFactory.TweenColor(_pilotRenderer, _fallenColor, 0.4f);
            transform.DOPunchScale(Vector3.one * 0.25f, 0.35f);
            SetPilotPartsVisible(false);
            SetState(SlotVisualState.Inactive);
        }

        public void Punch(float strength = 0.2f)
        {
            transform.DOKill(true);
            transform.DOPunchScale(Vector3.one * strength, 0.3f);
        }

        public void PlayUnlock()
        {
            _locked = false;
            ShowLock(false, string.Empty);
            RefreshBackgroundColor();
            SetState(SlotVisualState.Empty);
            transform.DOKill(true);
            transform.DOPunchScale(Vector3.one * 0.3f, 0.4f);
        }

        private void OnMouseUpAsButton()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Clicked?.Invoke(this);
        }

        private void RefreshBackgroundColor()
        {
            if (_pilotBackground == null)
            {
                return;
            }

            if (_highlighted)
            {
                _pilotBackground.color = _highlightColor;
            }
            else
            {
                _pilotBackground.color = _locked ? _lockedColor : _backgroundBaseColor;
            }
        }

        /// <summary>Colours the ring and both stat borders for the slot's state. The ring and the active colours follow the chain(s) the slot is in.</summary>
        private void SetState(SlotVisualState state)
        {
            if (!_ready)
            {
                return;
            }

            _state = state;
            LerpColorTint.Apply(_ring, _tints.For(state, _roleColor));
            var attackBorder = _tints.For(state, _visuals.AttackColor);
            var defenseBorder = _tints.For(state, _visuals.DefenseColor);
            _attackBox.SetBorder(_attackUsed ? attackBorder : _tints.Unused(attackBorder));
            _defenseBox.SetBorder(_defenseUsed ? defenseBorder : _tints.Unused(defenseBorder));
        }

        private void ShowLock(bool locked, string description)
        {
            if (_lockIcon != null)
            {
                _lockIcon.enabled = locked;
            }

            if (!locked)
            {
                SetText(_lockText, string.Empty);
                return;
            }

            // until the lock picture exists, say it in words
            var iconShown = _lockIcon != null && _lockIcon.sprite != null;
            if (iconShown)
            {
                SetText(_lockText, description);
            }
            else
            {
                SetText(_lockText, "<b>LOCKED</b>" + (string.IsNullOrEmpty(description) ? string.Empty : "\n" + description));
            }
        }

        private void UpdateWoundVisual()
        {
            if (!_ready || _pilot == null)
            {
                return;
            }

            var remaining = Mathf.Clamp01(1f - _displayWound);

            // a used stat is filled (attack always full, defense by what is left of it); an unused one is empty and dark
            if (_attackUsed)
            {
                _attackBox.SetFill(1f, _tints.FillFor(_state, _visuals.AttackColor));
            }
            else
            {
                _attackBox.HideFill();
            }

            if (_defenseUsed)
            {
                _defenseBox.SetFill(remaining, _tints.FillFor(_state, _visuals.DefenseColor));
            }
            else
            {
                _defenseBox.HideFill();
            }

            var attackColor = _inactive ? _inactiveColor : _visuals.AttackColor;
            var defenseColor = _inactive ? _inactiveColor : _visuals.DefenseColor;
            _attackBox.SetValue(DisplayFormat.Value(_pilot.EffectiveAttack), _attackUsed ? attackColor : _tints.Unused(attackColor));
            _defenseBox.SetValue(DisplayFormat.Value(_pilot.BaseDefense * remaining), _defenseUsed ? defenseColor : _tints.Unused(defenseColor));
        }

        private static string FormatBonus(NodeBonus bonus)
        {
            switch (bonus.Kind)
            {
                case BonusKind.Add:
                    return "+" + DisplayFormat.Value(bonus.Amount);
                case BonusKind.Multiply:
                    return "x" + DisplayFormat.Value(bonus.Amount);
                default:
                    return string.Empty;
            }
        }

        /// <summary>The stat boxes, which exist only while a living pilot stands in the slot.</summary>
        private void SetPilotPartsVisible(bool visible)
        {
            if (!_ready)
            {
                return;
            }

            if (_statsBox != null)
            {
                _statsBox.SetActive(visible);
            }
            else
            {
                _attackBox.gameObject.SetActive(visible);
                _defenseBox.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Checks the parts the slot cannot work without. A slot prefab made before a part existed has it unassigned; say so
        /// plainly (with the fix) instead of failing somewhere inside.
        /// </summary>
        private bool HasRequiredParts()
        {
            var missing = new List<string>();
            if (_pilotBackground == null) missing.Add("PilotBackground");
            if (_pilotRenderer == null) missing.Add("Pilot");
            if (_attackBox == null) missing.Add("AttackBox (StatBoxView)");
            if (_defenseBox == null) missing.Add("DefenseBox (StatBoxView)");
            if (_bonusText == null) missing.Add("BonusText");

            _ready = missing.Count == 0;
            if (!_ready)
            {
                Debug.LogError($"Slot '{SlotId}' is missing parts in its SlotView: {string.Join(", ", missing)}. " +
                               "Run Tools > Fun Game > Upgrade Pilot Slot Prefab, or assign them on the PilotSlot prefab.", this);
            }

            return _ready;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        /// <summary>The colour with its own alpha scaled, so art that is partly transparent stays so.</summary>
        private static Color WithAlpha(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }
    }
}
