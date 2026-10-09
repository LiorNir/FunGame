using Configurations;
using Configurations.Operators;
using UnityEngine;

namespace Views
{
    public class OperatorSlotView : TokenSlotBaseView
    {
        [SerializeField] private OperationTokenView _tokenView;
        //[SerializeField] private Sprite _disabledSprite;

        public OperationType CurrentOperationType { get; private set; }

        public void Init(OperatorTokenConfiguration configuration) //TODO: DRY from pilot script
        {
            _tokenView.gameObject.SetActive(true); //TODO: can be part of it's init I guess?
            CurrentOperationType = configuration.Type;
            _tokenView.Init(configuration);
            _isOccupied = true;
            NotifyStateChanged();
        }
    }
}
