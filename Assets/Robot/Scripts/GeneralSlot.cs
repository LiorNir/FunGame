using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GeneralSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Parameters")] [SerializeField]
    private SlotType _slotType;
    [SerializeField] private Image _slotImage;
    [SerializeField] private Sprite _emptySlotImage;
    private bool _isSlotOccupied;
    private float _attack;
    private float _defense;


    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI _attackText;
    [SerializeField] private TextMeshProUGUI _defenseText;

    public float AttackValue => _attack;
    public float DefenceValue => _defense;
    private void Awake()
    {
        _attackText.gameObject.SetActive(false);
        _defenseText.gameObject.SetActive(false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_isSlotOccupied) return;
        var draggedObject = eventData.pointerDrag;
        //bool isObjectACard = draggedObject.GetComponent<DraggableObject>().CardScript != null;
        //TODO: will need to diffrentiate between pilot card and operation card
        var dragScript = draggedObject.GetComponent<DraggableObject>();
        var cardScript = dragScript.CardScript;
        PopulatePilotSlot(cardScript.CardConfiguration);
        dragScript.Discard(); //TODO: will need to deactivate and place out of deck
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_isSlotOccupied) return;
        PlayPointerEnterEffect();
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        
    }

    public void PopulatePilotSlot(GeneralCardConfiguration config)
    {
        _slotImage.sprite = config.CardSprite;
        _attack = config.AttackValue;
        _defense = config.DefenseValue;
        ActivateAndSetTexts();
        _isSlotOccupied = true;
    }

    public void EvacuatePilotSlot()
    {
        _slotImage.sprite = _emptySlotImage;
    }

    // private bool IsCardCompatible(DraggableObject card)
    // {
    //     
    // }

    private void PlayPointerEnterEffect()
    {
        
    }

    private void PlayPointerExitEffect()
    {
        
    }

    private void ActivateAndSetTexts()
    {
        _attackText.gameObject.SetActive(true);
        _attackText.text = $"ATK: {_attack}";
        _defenseText.gameObject.SetActive(true);
        _defenseText.text = $"DEF: {_defense}";
        
    }
}
