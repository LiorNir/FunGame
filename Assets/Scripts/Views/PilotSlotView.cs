using System.Threading;
using Configurations;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Enums;
using Lean.Touch;
using TMPro;
using UnityEngine;
using Utils;
using Zenject;

namespace Views
{
    public class PilotSlotView : TokenSlotBaseView
    {
        //[SerializeField] private PilotTokenView _tokenView;
        [SerializeField] private PilotSlotType _pilotSlotType;

        [Header("Icons")]
        [SerializeField] private SpriteRenderer _attackIcon;
        [SerializeField] private SpriteRenderer _defenseIcon;
        [SerializeField] private SpriteRenderer _ejectIcon;
        [SerializeField] private SpriteRenderer _abilityIcon;

        [Header("Buttons")]
        [SerializeField] private LeanSelectableByFinger _ejectButton;
        [SerializeField] private LeanSelectableByFinger _abilityButton;
        
        [Header("Texts")]
        [SerializeField] private TMP_Text _attackText;
        [SerializeField] private TMP_Text _defenseText;

        [Header("Animations")]
        [SerializeField] private GameObject _animationParent;
        [SerializeField] private Animation _animation;
        [SerializeField] private AnimationClip _ejectAnimation;
        [SerializeField] private float _defenseChangeDuration = 0.35f;

        private PilotTokenConfiguration _currentConfiguration;
        private float _activeDefenseValue;
        private PilotHandView _pilotHandView;
        private CancellationToken _cancellationToken;

        public PilotTokenConfiguration CurrentConfiguration => _currentConfiguration;
        public float ActiveDefenseValue => _activeDefenseValue;

        [Inject]
        private void InjectDependencies(PilotHandView pilotHandView)
        {
            _pilotHandView = pilotHandView;
        }
        
        public void Init(PilotTokenConfiguration configuration)
        {
            //_tokenView.gameObject.SetActive(true); //TODO: can be part of it's init I guess?
            //_tokenView.Init(configuration);
            _cancellationToken = this.GetCancellationTokenOnDestroy(); //TODO: ok for now, might change
            _currentConfiguration = configuration;
            _activeDefenseValue = configuration.DefenseValue;
            _activeSpriteRenderer.sprite = configuration.Sprite;
            _attackText.text = configuration.AttackValue.ToString();
            UpdateDefenseText();
            _abilityIcon.gameObject.SetActive(configuration.Ability);
            AddListeners(configuration.Ability);
            _animationParent.SetActive(true);
            _activeIconContainer.SetActive(true);
            _isOccupied = true;
            NotifyStateChanged();
        }

        protected override void Eject(LeanFinger leanFinger)
        {
            RemoveListeners();
            PlayEjectSequence().Forget();
        }

        private async UniTask PlayEjectSequence()
        {
            await AnimationUtils.PlayAnimationClipAsync(_animation, _ejectAnimation, _cancellationToken);
            await _pilotHandView.SpawnNewToken(_currentConfiguration);
            _currentConfiguration = null;
            _activeDefenseValue = 0f;
            _isOccupied = false;
            NotifyStateChanged();
        }

        public float AbsorbDefenseDamage(float incomingDamage)
        {
            if (!_isOccupied || incomingDamage <= 0f)
            {
                return incomingDamage;
            }

            var absorbedDamage = Mathf.Min(_activeDefenseValue, incomingDamage);
            _activeDefenseValue -= absorbedDamage;
            UpdateDefenseText();

            if (_activeDefenseValue <= 0f)
            {
                ClearSlotAfterDefenseDepleted();
            }
            else
            {
                NotifyStateChanged();
            }

            return incomingDamage - absorbedDamage;
        }

        public async UniTask<float> AbsorbDefenseDamageAnimated(float incomingDamage)
        {
            if (!_isOccupied || incomingDamage <= 0f)
            {
                return incomingDamage;
            }

            var absorbedDamage = Mathf.Min(_activeDefenseValue, incomingDamage);
            var targetDefenseValue = _activeDefenseValue - absorbedDamage;
            await AnimateDefenseTo(targetDefenseValue);

            if (_activeDefenseValue <= 0f)
            {
                ClearSlotAfterDefenseDepleted();
            }
            else
            {
                NotifyStateChanged();
            }

            return incomingDamage - absorbedDamage;
        }

        private async UniTask AnimateDefenseTo(float targetDefenseValue)
        {
            var startingDefenseValue = _activeDefenseValue;
            if (Mathf.Approximately(startingDefenseValue, targetDefenseValue))
            {
                return;
            }

            await DOTween.To(
                    () => startingDefenseValue,
                    value =>
                    {
                        _activeDefenseValue = value;
                        UpdateDefenseText();
                    },
                    targetDefenseValue,
                    _defenseChangeDuration)
                .SetEase(Ease.OutCubic)
                .AwaitForComplete(cancellationToken: _cancellationToken);

            _activeDefenseValue = targetDefenseValue;
            UpdateDefenseText();
        }

        private void ClearSlotAfterDefenseDepleted()
        {
            RemoveListeners();
            _currentConfiguration = null;
            _activeDefenseValue = 0f;
            _isOccupied = false;
            _activeIconContainer.SetActive(false);
            _animationParent.SetActive(false);
            NotifyStateChanged();
        }

        private void UpdateDefenseText()
        {
            _defenseText.text = _activeDefenseValue.ToString();
        }
        
        private void ActivateAbility(LeanFinger leanFinger)
        {
            //RemoveListeners();
            Debug.Log("Trying to activate pilot ability");
        }

        private void AddListeners(bool doesHaveAbility)
        {
            _ejectButton.OnSelectedFinger.AddListener(Eject);
            if (!doesHaveAbility) return;
            
            _abilityButton.OnSelectedFinger.AddListener(ActivateAbility);
        }

        private void RemoveListeners()
        {
            _ejectButton.OnSelectedFinger.RemoveAllListeners();
            _abilityButton.OnSelectedFinger.RemoveAllListeners();
        }
    }
}
