using System;
using System.Collections.Generic;
using Configurations;
using Configurations.Operators;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using Views;

namespace Controllers
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Player Stats")] [SerializeField]
        private int _maxHealth = 20;
        [SerializeField] private int _currentHealth;
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private float _healthChangeDuration = 0.35f;

        [SerializeField] private int _energyAmount; //TODO: initial energy?
        [SerializeField] private int _initialPilotsAmount = 5; //TODO: get this from somewhere, should be editable
        [SerializeField] private int _initialOperatorsAmount = 4; //TODO: get this from somewhere, should be editable

        [Header("Hand")]
        [SerializeField] private PilotHandView _pilotHandView;
        [SerializeField] private OperatorsHandView _operatorsHandView;

        [Header("Deck")]
        [SerializeField] private List<PilotTokenConfiguration> _pilotsInDeck;
        [SerializeField] private List<OperatorTokenConfiguration> _operatorsInDeck;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _maxHealth;
        public bool IsDefeated => _currentHealth <= 0;

        //TODO: TEMP
        private void Awake()
        {
            Init();
        }

        public void Init()
        {
            _currentHealth = _maxHealth;
            UpdateHealthText();
            SetInitialPilots();
            SetInitialOperators();
        }

        public int TakeDamage(int damage)
        {
            var appliedDamage = Mathf.Max(0, damage);
            _currentHealth = Mathf.Max(0, _currentHealth - appliedDamage);
            UpdateHealthText();
            return appliedDamage;
        }

        public async UniTask<int> TakeDamageAnimated(int damage)
        {
            var appliedDamage = Mathf.Max(0, damage);
            var targetHealth = Mathf.Max(0, _currentHealth - appliedDamage);
            await AnimateHealthTo(targetHealth);
            return appliedDamage;
        }

        private async UniTask AnimateHealthTo(int targetHealth)
        {
            var startingHealth = _currentHealth;
            if (startingHealth == targetHealth)
            {
                return;
            }

            await DOTween.To(
                    () => startingHealth,
                    value =>
                    {
                        _currentHealth = Mathf.RoundToInt(value);
                        UpdateHealthText();
                    },
                    targetHealth,
                    _healthChangeDuration)
                .SetEase(Ease.OutCubic)
                .AwaitForComplete(cancellationToken: this.GetCancellationTokenOnDestroy());

            _currentHealth = targetHealth;
            UpdateHealthText();
        }

        private void UpdateHealthText()
        {
            if (_healthText != null)
            {
                _healthText.text = $"HP: {_currentHealth}/{_maxHealth}";
            }
        }

        private void SetInitialPilots()
        {
            ShuffleList(_pilotsInDeck);
            var lastPilotIndex = _pilotsInDeck.Count - 1;
            var pilotsToSpawn = Math.Min(_initialPilotsAmount, lastPilotIndex);
            for (int index = 0; index < pilotsToSpawn; index++)
            {
                _pilotHandView.InitPilotTokenAtIndex(index, _pilotsInDeck[index]);
            }
        }

        private void SetInitialOperators() //TODO: some duplication from Pilots above, try to generify
        {
            ShuffleList(_operatorsInDeck);
            var lastOperatorIndex = _operatorsInDeck.Count - 1;
            var operatorsToSpawn = Math.Min(_initialOperatorsAmount, lastOperatorIndex);
            for (int index = 0; index < operatorsToSpawn; index++)
            {
                _operatorsHandView.InitOperatorTokenAtIndex(index, _operatorsInDeck[index]);
            }
        }

        private void ShuffleList<T>(List<T> listToShuffle) //TODO: to util or something
        {
            var count = listToShuffle.Count;
            var lastIndex = count - 1;
            for (var i = 0; i < lastIndex; ++i)
            {
                var randomIndex = UnityEngine.Random.Range(i, count);
                var tmp = listToShuffle[i];
                listToShuffle[i] = listToShuffle[randomIndex];
                listToShuffle[randomIndex] = tmp;
            }
        }
    }
}
