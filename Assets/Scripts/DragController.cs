using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class DragController : MonoBehaviour
{
    [SerializeField] public bool _isDraggingCard;
    [SerializeField] private CardScript _currentCard;
    [SerializeField] private Transform _cardContainer;

    private void Update()
    {
        transform.position = Input.mousePosition;
    }
    
    
}
