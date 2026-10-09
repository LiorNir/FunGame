using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    public List<GeneralCardConfiguration> _deck;
    [SerializeField] private CardScript _cardPrefab;
    [SerializeField] private Transform _cardContainer;

    public void DrawCards(int amountToDraw)
    {
        var currentAmountDrawn = 0;
        List<GeneralCardConfiguration> cardsToRemove = new List<GeneralCardConfiguration>(); //TODO: make sure this happens!
        for (int i = 0; i < amountToDraw; i++)
        {
            var card = Instantiate(_cardPrefab, _cardContainer);
            var currentConfig = _deck[currentAmountDrawn];
            card.Init(currentConfig);
            cardsToRemove.Add(currentConfig);
            currentAmountDrawn++;
        }

        for (int i = 0; i < cardsToRemove.Count; i++)
        {
            _deck.Remove(cardsToRemove[i]);
        }
    }

    public void ShuffleDeck()
    {
        
    }
}
