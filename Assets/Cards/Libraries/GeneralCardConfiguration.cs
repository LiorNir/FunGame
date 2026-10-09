using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GeneralCard", menuName = "ScriptableObjects/Cards/GeneralCard")]
public class GeneralCardConfiguration : ScriptableObject
{
    [SerializeField] private Sprite _cardSprite;
    [SerializeField] private float _attackValue; //TODO: this will later move specifically to pilot cards
    [SerializeField] private float _defenseValue;//TODO: same as above
    [SerializeField] private string _cardDescription;

    public Sprite CardSprite => _cardSprite;
    public float AttackValue => _attackValue;
    public float DefenseValue => _defenseValue;
    public string CardDescription => _cardDescription;

    public virtual void OnCardPlayed()
    {
        
    }

}
