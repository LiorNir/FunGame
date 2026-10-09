using Cysharp.Threading.Tasks;
using Enums;
using Lean.Touch;
using UnityEngine;
using System;

namespace Views
{
    public class TokenSlotBaseView : MonoBehaviour
    {
        [SerializeField] private TokenType _type;
        [SerializeField] protected GameObject _activeIconContainer;
        [SerializeField] private SpriteRenderer _inactiveSpriteRenderer;
        [SerializeField] protected SpriteRenderer _activeSpriteRenderer;

        protected bool _isOccupied;
        public event Action StateChanged;

        public bool IsOccupied => _isOccupied;
        
        public bool CheckTokenCompatibility(TokenType type)
        {
            var isTokenCompatible = type == _type && !_isOccupied;
            return isTokenCompatible;
        }

        public void SetInactiveSpriteRendererColor(bool isTouched)
        {
            _inactiveSpriteRenderer.color = isTouched ? Color.green : Color.black; //TODO: temp
        }

        protected virtual void Eject(LeanFinger leanFinger)
        {
            
        }

        protected void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }
    }
}
