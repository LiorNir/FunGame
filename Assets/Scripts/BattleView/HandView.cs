using System;
using System.Collections.Generic;
using DG.Tweening;
using FunGame.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace BattleView
{
    /// <summary>
    /// The player's hand: one <see cref="CardView"/> prefab per pilot, laid out in a row inside the hand area of the HUD
    /// prefab. Clicking a card raises an event. A card whose pilot was not in the hand before scales in (the turn's new
    /// cards, a pilot back from the board), and one whose pilot left (placed) scales out.
    /// </summary>
    public class HandView : MonoBehaviour
    {
        [Tooltip("The cards are placed inside this area, centred along its bottom edge.")]
        [SerializeField] private RectTransform _area;
        [SerializeField] private float _spacing = 190f;
        [SerializeField] private float _raiseWhenSelected = 36f;
        [Tooltip("How long a new card takes to scale in.")]
        [SerializeField] private float _appearSeconds = 0.25f;
        [Tooltip("How long a card whose pilot left the hand (placed) takes to scale out.")]
        [SerializeField] private float _leaveSeconds = 0.15f;

        private BattleVisuals _visuals;
        private readonly List<CardView> _cards = new List<CardView>();
        // the pilot of each card, to tell which cards are new and which pilots left since the last refresh
        private readonly List<int> _cardPilots = new List<int>();

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
            _cardPilots.Clear();
        }

        // removes the cards before they are made again: one whose pilot is no longer in the hand scales out first
        private void RemoveCards(BattleEngine engine)
        {
            var stillInHand = new HashSet<int>();
            foreach (var pilot in engine.Hand)
            {
                stillInHand.Add(pilot.Id);
            }

            for (var index = 0; index < _cards.Count; index++)
            {
                var card = _cards[index];
                if (card == null)
                {
                    continue;
                }

                if (stillInHand.Contains(_cardPilots[index]) || _leaveSeconds <= 0f)
                {
                    Destroy(card.gameObject);
                    continue;
                }

                foreach (var graphic in card.GetComponentsInChildren<Graphic>())
                {
                    graphic.raycastTarget = false;
                }

                var leaving = card.gameObject;
                card.Rect.DOScale(0f, _leaveSeconds).SetEase(Ease.InBack).SetLink(leaving).OnComplete(() => Destroy(leaving));
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
            var before = new HashSet<int>(_cardPilots);
            RemoveCards(engine);
            _cardPilots.Clear();
            var count = engine.Hand.Count;
            for (var index = 0; index < count; index++)
            {
                var card = Instantiate(_visuals.CardPrefab, _area);
                var pilot = engine.Hand[index];
                var rules = engine.Setup.Rules;
                // an ejected, wounded pilot that will recover faster if it rests here until the next turn
                var restsFaster = engine.WasEjectedThisTurn(pilot) && pilot.DefenseWound > 0.001f &&
                                  rules.HandRecoveryPerTurn > 0f && rules.EjectedRecoveryMultiplier > 1.001f;
                // where its stats will be next turn if it rests here (the bars preview it when that is higher than now)
                var defenseAfterRest = StatShares.DefenseAfterRest(pilot, engine.WoundAfterRest(pilot));
                card.Set(pilot, index, index == selectedIndex, _visuals, restsFaster ? rules.EjectedRecoveryMultiplier : 1f,
                         defenseAfterRest, StatShares.AttackAfterRest(pilot, engine.AttackAfterRest(pilot)));
                card.Clicked += clicked => CardClicked?.Invoke(clicked.Index, clicked.Rect);
                card.Rect.anchoredPosition = new Vector2((index - (count - 1) * 0.5f) * _spacing, index == selectedIndex ? _raiseWhenSelected : 0f);
                _cards.Add(card);
                _cardPilots.Add(pilot.Id);
                if (!before.Contains(pilot.Id) && _appearSeconds > 0f)
                {
                    var full = card.Rect.localScale;
                    card.Rect.localScale = Vector3.zero;
                    card.Rect.DOScale(full, _appearSeconds).SetEase(Ease.OutBack).SetLink(card.gameObject);
                }
            }
        }
    }
}
