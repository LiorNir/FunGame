using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableObject : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    //[SerializeField] private Image _cardFrame;
    [SerializeField] private CardScript _cardScript;
    [SerializeField] private CanvasGroup _canvasGroup;
    private Vector2 _startingPosition;

    public CardScript CardScript => _cardScript;

    public void OnPointerDown(PointerEventData eventData)
    {
        
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        //_cardFrame.raycastTarget = false;
        _canvasGroup.blocksRaycasts = false;
        _startingPosition = transform.position;
        transform.position = eventData.position;
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        transform.position = _startingPosition;
        _canvasGroup.blocksRaycasts = true;
        //_cardFrame.raycastTarget = true;
    }

    public void Discard()
    {
        Destroy(this.gameObject); //TODO: TEMP!!!
    }
}
