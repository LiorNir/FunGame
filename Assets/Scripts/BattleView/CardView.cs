using System;
using Configurations;
using Cysharp.Threading.Tasks;
using FunGame.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleView
{
    /// <summary>
    /// One pilot card in the hand, as a prefab: a background, the pilot's picture, name and stats. The card's size is the
    /// size of the prefab's root; everything inside is positioned and sized in the prefab.
    /// </summary>
    public class CardView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Image _picture;
        [Tooltip("Optional: plays the pilot asset's Idle frames on the picture. Pilots without frames keep their still picture.")]
        [SerializeField] private Views.PilotAnimationView _pictureAnimation;
        [SerializeField] private TextMeshProUGUI _name;
        [Tooltip("Shown only when the card has no stat bars.")]
        [SerializeField] private TextMeshProUGUI _stats;
        [Tooltip("Optional: attack and defense as bars (the defense bar shows how much of it is left). Without them the Stats text is used.")]
        [SerializeField] private CardStatBar _attackBar;
        [SerializeField] private CardStatBar _defenseBar;
        [Tooltip("Optional: the ability's name and cost (\"Surge · 2 AP\"), shown while the pilot has an unused ability.")]
        [SerializeField] private TextMeshProUGUI _abilityText;
        [Tooltip("Optional: a frame switched on while the pilot has an unused ability (temporary look).")]
        [SerializeField] private Outline _abilityFrame;
        [SerializeField] private Color _normalColor = new Color(0.14f, 0.16f, 0.28f, 0.96f);
        [SerializeField] private Color _selectedColor = new Color(0.25f, 0.4f, 0.3f, 0.98f);
        [Tooltip("Colour of the temporary \"rest x2\" mark on an ejected pilot that will recover faster if it stays in the hand this turn.")]
        [SerializeField] private Color _restBonusColor = new Color(0.5f, 1f, 0.5f);

        private PilotUnit _shownPilot;

        public event Action<CardView> Clicked;
        public int Index { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            _button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        /// <param name="restBonus">Above 1: the pilot was ejected this turn and will recover this many times as fast if it
        /// rests in the hand until the next turn; the card marks it (temporary look: "rest x2" on the defense).</param>
        /// <param name="defenseAfterRest">The share of its defense the pilot will have next turn if it rests here (wounds
        /// recover, effects run out), previewed on the defense bar when higher than now; negative for none.</param>
        /// <param name="attackAfterRest">The same for attack (a snipe running out, the Surge crash ending in the hand).</param>
        public void Set(PilotUnit pilot, int index, bool selected, BattleVisuals visuals, float restBonus = 1f, float defenseAfterRest = -1f,
                        float attackAfterRest = -1f)
        {
            Index = index;
            _background.color = selected ? _selectedColor : _normalColor;
            _name.text = pilot.Name;
            ShowAbility(pilot);

            var source = pilot.Source as PilotTokenConfiguration;
            if (source != null && source.Sprite != null)
            {
                _picture.sprite = source.Sprite;
                _picture.color = Color.white;
                _picture.preserveAspect = true;
            }
            else
            {
                _picture.sprite = null;
                _picture.color = new Color(1f, 1f, 1f, 0.08f);
            }

            if (_pictureAnimation != null)
            {
                _pictureAnimation.SetSequences(source != null ? source.IdleAnimation : null, null, null);
                _pictureAnimation.PlayIdleAnimation().Forget();
            }

            var attack = DisplayFormat.Value(pilot.EffectiveAttack);
            var defense = visuals.DefenseNumber(pilot);
            var restMark = restBonus > 1.001f
                ? $" <color=#{ColorUtility.ToHtmlStringRGB(_restBonusColor)}>rest x{DisplayFormat.Value(restBonus)}</color>"
                : string.Empty;
            if (_attackBar != null && _defenseBar != null)
            {
                // the same pilot as last time: its defense slides to the new value (for example while it recovers in the hand)
                var samePilot = pilot == _shownPilot;
                _shownPilot = pilot;
                _stats.text = string.Empty;
                // the bars show what is left of each stat, whatever lowered it (wounds, a snipe, the Surge crash)
                _attackBar.Set($"ATK {attack}", StatShares.Attack(pilot), visuals.AttackColor, samePilot, StatChanges.OfAttack(pilot));
                _defenseBar.Set($"DEF {defense}{restMark}", StatShares.Defense(pilot), visuals.DefenseColor, samePilot, StatChanges.OfDefense(pilot));
                _defenseBar.SetPreview(defenseAfterRest);
                _attackBar.SetPreview(attackAfterRest);
                return;
            }

            var wounded = pilot.DefenseWound > 0.001f ? $" <size=60%>(of {DisplayFormat.Value(pilot.BaseDefense)})</size>" : string.Empty;
            _stats.text = visuals.AttackText($"ATK {attack}") + "\n" + visuals.DefenseText($"DEF {defense}") + wounded + restMark;
        }

        private void ShowAbility(PilotUnit pilot)
        {
            var ready = pilot.HasUnusedAbility;
            if (_abilityText != null)
            {
                _abilityText.text = ready ? $"{pilot.Ability.Name} · {pilot.Ability.ApCost} AP" : string.Empty;
            }

            if (_abilityFrame != null)
            {
                _abilityFrame.enabled = ready;
            }
        }
    }
}
