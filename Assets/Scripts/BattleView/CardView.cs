using System;
using Configurations;
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
        [SerializeField] private TextMeshProUGUI _name;
        [Tooltip("Shown only when the card has no stat bars.")]
        [SerializeField] private TextMeshProUGUI _stats;
        [Tooltip("Optional: attack and defense as bars (the defense bar shows how much of it is left). Without them the Stats text is used.")]
        [SerializeField] private CardStatBar _attackBar;
        [SerializeField] private CardStatBar _defenseBar;
        [SerializeField] private Color _normalColor = new Color(0.14f, 0.16f, 0.28f, 0.96f);
        [SerializeField] private Color _selectedColor = new Color(0.25f, 0.4f, 0.3f, 0.98f);

        private PilotUnit _shownPilot;

        public event Action<CardView> Clicked;
        public int Index { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            _button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        public void Set(PilotUnit pilot, int index, bool selected, BattleVisuals visuals)
        {
            Index = index;
            _background.color = selected ? _selectedColor : _normalColor;
            _name.text = pilot.Name;

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

            var attack = DisplayFormat.Value(pilot.EffectiveAttack);
            var defense = DisplayFormat.Value(pilot.EffectiveDefense);
            if (_attackBar != null && _defenseBar != null)
            {
                // the same pilot as last time: its defense slides to the new value (for example while it recovers in the hand)
                var samePilot = pilot == _shownPilot;
                _shownPilot = pilot;
                _stats.text = string.Empty;
                _attackBar.Set($"ATK {attack}", 1f, visuals.AttackColor, samePilot);
                _defenseBar.Set($"DEF {defense}", 1f - pilot.DefenseWound, visuals.DefenseColor, samePilot);
                return;
            }

            var wounded = pilot.DefenseWound > 0.001f ? $" <size=60%>(of {DisplayFormat.Value(pilot.BaseDefense)})</size>" : string.Empty;
            _stats.text = visuals.AttackText($"ATK {attack}") + "\n" + visuals.DefenseText($"DEF {defense}") + wounded;
        }
    }
}
