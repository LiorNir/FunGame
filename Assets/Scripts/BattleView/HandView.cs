using System;
using System.Collections.Generic;
using FunGame.Battle;
using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// The player's hand: one <see cref="CardView"/> prefab per pilot, laid out in a row inside the hand area of the HUD
    /// prefab. Clicking a card raises an event.
    /// </summary>
    public class HandView : MonoBehaviour
    {
        [Tooltip("The cards are placed inside this area, centred along its bottom edge.")]
        [SerializeField] private RectTransform _area;
        [SerializeField] private float _spacing = 190f;
        [SerializeField] private float _raiseWhenSelected = 36f;

        private BattleVisuals _visuals;
        private readonly List<CardView> _cards = new List<CardView>();

        public event Action<int, RectTransform> CardClicked;

        public void Init(BattleVisuals visuals)
        {
            _visuals = visuals;
        }

        public void Clear()
        {
            foreach (var card in _cards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }

            _cards.Clear();
        }

        public RectTransform GetCardRect(int index)
        {
            return index >= 0 && index < _cards.Count ? _cards[index].Rect : null;
        }

        /// <param name="selectedIndex">The card to show raised, or -1.</param>
        public void Refresh(BattleEngine engine, int selectedIndex)
        {
            Clear();
            var count = engine.Hand.Count;
            for (var index = 0; index < count; index++)
            {
                var card = Instantiate(_visuals.CardPrefab, _area);
                var pilot = engine.Hand[index];
                var rules = engine.Setup.Rules;
                // an ejected, wounded pilot that will recover faster if it rests here until the next turn
                var restsFaster = engine.WasEjectedThisTurn(pilot) && pilot.DefenseWound > 0.001f &&
                                  rules.HandRecoveryPerTurn > 0f && rules.EjectedRecoveryMultiplier > 1.001f;
                card.Set(pilot, index, index == selectedIndex, _visuals, restsFaster ? rules.EjectedRecoveryMultiplier : 1f);
                card.Clicked += clicked => CardClicked?.Invoke(clicked.Index, clicked.Rect);
                card.Rect.anchoredPosition = new Vector2((index - (count - 1) * 0.5f) * _spacing, index == selectedIndex ? _raiseWhenSelected : 0f);
                _cards.Add(card);
            }
        }
    }
}
