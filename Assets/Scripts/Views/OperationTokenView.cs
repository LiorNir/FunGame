using Configurations.Operators;
using Lean.Touch;
using UnityEngine;

namespace Views
{
    public class OperationTokenView : TokenBaseView
    {
        //TODO: @Lior Library for configurations?
        
        private OperationType _operationType;
        private OperatorTokenConfiguration _currentConfiguration;
        
        public void Init(OperatorTokenConfiguration configuration)
        {
            _currentConfiguration = configuration;
            _operationType = configuration.Type;
            SetSprite(configuration.Sprite);
            AddListeners();
        }
        
        protected override void InitSlotOnSuccessfulPlacement()
        {
            if (CurrentTargetSlotView is not OperatorSlotView slotView) return; //TODO: error?
            
            slotView.Init(_currentConfiguration); //TODO: if config is null? Shouldn't be possible though
            base.InitSlotOnSuccessfulPlacement();
        }
        
        // public override void OnClick(LeanFinger leanFinger)
        // {
        //     
        // }
        //
        // public override void OnRelease(LeanFinger leanFinger)
        // {
        //     
        // }
    }
}
