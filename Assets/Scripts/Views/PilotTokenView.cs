using System;
using Configurations;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Lean.Touch;
using TMPro;
using UnityEngine;

namespace Views
{
    public class PilotTokenView : TokenBaseView
    {
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _attackText;
        [SerializeField] private TMP_Text _defenseText;
        
        [SerializeField] private float _rearrangeInHandDuration = 0.2f;

        private PilotTokenConfiguration _currentConfiguration;
        private Action<PilotTokenView> _onSuccessfulPlacement;

        public void Init(PilotTokenConfiguration configuration, Action<PilotTokenView> onSuccessfulPlacement)
        {
            _currentConfiguration = configuration;
            _nameText.text = _currentConfiguration.DisplayName;
            _attackText.text = _currentConfiguration.AttackValue.ToString();
            _defenseText.text = _currentConfiguration.DefenseValue.ToString();
            SetSprite(_currentConfiguration.Sprite);
            _onSuccessfulPlacement = onSuccessfulPlacement;
            AddListeners();
        }

        protected override void InitSlotOnSuccessfulPlacement()
        {
            if (CurrentTargetSlotView is not PilotSlotView slotView) return; //TODO: error?
            
            slotView.Init(_currentConfiguration); //TODO: if config is null? Shouldn't be possible though
            base.InitSlotOnSuccessfulPlacement();
            _onSuccessfulPlacement?.Invoke(this);
        }

        public async UniTask RepositionInHand(float targetXValue)
        {
            LeanSelectable.enabled = false;
            await transform.DOMoveX(targetXValue, _rearrangeInHandDuration).AwaitForComplete(); //TODO: cancellation token
            LeanSelectable.enabled = true;
        }

        public override void OnClick(LeanFinger leanFinger)
        {
            base.OnClick(leanFinger);
        }

        public override void OnRelease(LeanFinger leanFinger)
        {
            base.OnRelease(leanFinger);
        }
    }
}
