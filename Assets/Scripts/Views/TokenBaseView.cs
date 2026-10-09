using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Enums;
using Lean.Touch;
using UnityEngine;

namespace Views
{
    public class TokenBaseView : MonoBehaviour, IDraggableObject
    {
        [SerializeField] protected LeanSelectableByFinger LeanSelectable;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private TokenType _tokenType;
        [SerializeField] private float _releaseTweenDuration = 0.2f;

        protected TokenSlotBaseView CurrentTargetSlotView;
        private bool _isBeingDragged;
        private LeanFinger _currentFollowingFinger;
        private Vector3 _dragStartPosition;
        public virtual void OnClick(LeanFinger leanFinger)
        {
            if (_isBeingDragged) return;

            _currentFollowingFinger = leanFinger;
            _isBeingDragged = true;
            _dragStartPosition = transform.position;
        }

        public virtual void OnRelease(LeanFinger leanFinger)
        {
            if (CurrentTargetSlotView == null)
            {
                PlayReleaseSequence().Forget();
                return;
            }

            InitSlotOnSuccessfulPlacement();
        }

        protected virtual void InitSlotOnSuccessfulPlacement()
        {
            Reset();
        }

        private async UniTask PlayReleaseSequence()
        {
            _currentFollowingFinger = null;
            var cancellationToken = this.GetCancellationTokenOnDestroy(); //TODO: not good
            await transform.DOMove(_dragStartPosition, _releaseTweenDuration)
                .SetEase(Ease.InOutCubic)
                .AwaitForComplete(cancellationToken: cancellationToken);
            _isBeingDragged = false;
        }

        private void Update() //TODO: try not using update for this...
        {
            if (!_isBeingDragged || _currentFollowingFinger == null) return;

            var fingerTraveledDistance =
                _currentFollowingFinger.GetLastWorldPosition(10) - _currentFollowingFinger.GetStartWorldPosition(10);
            //TODO: camera distance SHOULDN"T be magic number!
            transform.position = _dragStartPosition + fingerTraveledDistance;
        }

        protected void AddListeners()
        {
            LeanSelectable.OnSelectedFinger.AddListener(OnClick);
            LeanSelectable.OnSelectedFingerUp.AddListener(OnRelease);
        }

        private void RemoveListeners()
        {
            LeanSelectable.OnSelectedFinger.AddListener(OnClick);
            LeanSelectable.OnSelectedFingerUp.AddListener(OnRelease);
        }
        
        protected void SetSprite(Sprite sprite)
        {
            _spriteRenderer.sprite = sprite;
        }

        protected void OnTriggerEnter2D(Collider2D targetCollider)
        {
            targetCollider.TryGetComponent<TokenSlotBaseView>(out var slot);
            if (slot == null) return;

            var isSlotCompatible = slot.CheckTokenCompatibility(_tokenType);
            if (!isSlotCompatible) return;

            CurrentTargetSlotView = slot;
            slot.SetInactiveSpriteRendererColor(true);
        }

        protected void OnTriggerExit2D(Collider2D targetCollider)
        {
            if (CurrentTargetSlotView != null)
            {
                CurrentTargetSlotView.SetInactiveSpriteRendererColor(false);
            }
            
            CurrentTargetSlotView = null;
        }

        private void Reset()
        {
            gameObject.SetActive(false); //TODO: return to pool or something
            _currentFollowingFinger = null;
            _isBeingDragged = false;
        }

        protected void OnDestroy()
        {
            RemoveListeners();
        }
    }
}
