using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class CardScript : MonoBehaviour
{
    public GeneralCardConfiguration CardConfiguration;
    private float _attackValue;
    private float _defenseValue;
    [SerializeField] private Image _cardImage;
    [SerializeField] private DragController _dragController;
    private Vector3 _originalPosition;
    private GeneralCardConfiguration _cardConfiguration;

    [SerializeField]private TextMeshProUGUI _attackValueText;
    [SerializeField]private TextMeshProUGUI _defenseValueText;
    [SerializeField]private TextMeshProUGUI _cardDescriptionText;

    public void Init(GeneralCardConfiguration config)
    {
        _attackValue = config.AttackValue;
        _defenseValue = config.DefenseValue;
        _cardImage.sprite = config.CardSprite;

        _attackValueText.text = $"ATK: {_attackValue}";
        _defenseValueText.text = $"DEF: {_defenseValue}";
        _cardDescriptionText.text = config.CardDescription;
        
        _originalPosition = transform.position;
        CardConfiguration = config;
    }

    public void OnClick()
    {
        if (_dragController._isDraggingCard) return;

        _dragController._isDraggingCard = true;
        transform.parent = _dragController.transform;
        _cardImage.isMaskingGraphic = false;
    }
}
