using System.Collections.Generic;
using Combat;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Controllers
{
    public class EnemyController : MonoBehaviour
    {
        [Header("Enemy Stats")]
        [SerializeField] private int _maxHealth = 20;
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private TMP_Text _upcomingAttackText;
        [SerializeField] private TMP_Text _upcomingDefenseText;
        [SerializeField] private float _statChangeDuration = 0.35f;

        [Header("Turn Actions")]
        [SerializeField] private EnemyActionSelectionMode _actionSelectionMode;
        [SerializeField] private List<EnemyAction> _actions = new();

        private int _currentHealth;
        private int _nextSequentialActionIndex;
        private EnemyAction _upcomingAction;
        private EnemyAction _activeAction;
        private int _activeDefense;

        public int CurrentHealth => _currentHealth;
        public bool IsDefeated => _currentHealth <= 0;

        private void Awake()
        {
            ResetCombatState();
        }

        public void ResetCombatState()
        {
            _currentHealth = _maxHealth;
            _nextSequentialActionIndex = 0;
            _upcomingAction = null;
            _activeAction = null;
            _activeDefense = 0;
            UpdateHealthText();
            PreviewNextAction();
        }

        public EnemyAction PreviewNextAction()
        {
            if (_upcomingAction != null)
            {
                return _upcomingAction;
            }

            if (_actions.Count == 0)
            {
                UpdateUpcomingActionTexts();
                return null;
            }

            if (_actionSelectionMode == EnemyActionSelectionMode.Random)
            {
                _upcomingAction = _actions[Random.Range(0, _actions.Count)];
            }
            else
            {
                _upcomingAction = _actions[_nextSequentialActionIndex];
                _nextSequentialActionIndex = (_nextSequentialActionIndex + 1) % _actions.Count;
            }

            UpdateUpcomingActionTexts();
            return _upcomingAction;
        }

        public EnemyAction ConsumeUpcomingAction()
        {
            var action = PreviewNextAction();
            _activeAction = action;
            _activeDefense = action?.Defense ?? 0;
            _upcomingAction = null;
            UpdateUpcomingActionTexts();
            return action;
        }

        public async UniTask<int> ReceivePlayerAttackAnimated(int incomingDamage)
        {
            var remainingDamage = Mathf.Max(0, incomingDamage);
            var absorbedDamage = Mathf.Min(_activeDefense, remainingDamage);
            if (absorbedDamage > 0)
            {
                await AnimateActiveDefenseTo(_activeDefense - absorbedDamage);
                remainingDamage -= absorbedDamage;
            }

            if (remainingDamage > 0)
            {
                await AnimateHealthTo(Mathf.Max(0, _currentHealth - remainingDamage));
            }

            return remainingDamage;
        }

        public void FinishCurrentAction()
        {
            _activeAction = null;
            _activeDefense = 0;
            UpdateUpcomingActionTexts();
        }

        public int TakeDamage(int damage)
        {
            var appliedDamage = Mathf.Max(0, damage);
            _currentHealth = Mathf.Max(0, _currentHealth - appliedDamage);
            UpdateHealthText();
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
                    _statChangeDuration)
                .SetEase(Ease.OutCubic)
                .AwaitForComplete(cancellationToken: this.GetCancellationTokenOnDestroy());

            _currentHealth = targetHealth;
            UpdateHealthText();
        }

        private async UniTask AnimateActiveDefenseTo(int targetDefense)
        {
            var startingDefense = _activeDefense;
            await DOTween.To(
                    () => startingDefense,
                    value =>
                    {
                        _activeDefense = Mathf.RoundToInt(value);
                        UpdateUpcomingActionTexts();
                    },
                    targetDefense,
                    _statChangeDuration)
                .SetEase(Ease.OutCubic)
                .AwaitForComplete(cancellationToken: this.GetCancellationTokenOnDestroy());

            _activeDefense = targetDefense;
            UpdateUpcomingActionTexts();
        }

        private void UpdateHealthText()
        {
            if (_healthText != null)
            {
                _healthText.text = $"Enemy HP: {_currentHealth}/{_maxHealth}";
            }
        }

        private void UpdateUpcomingActionTexts()
        {
            var displayedAction = _activeAction ?? _upcomingAction;
            var displayedDefense = _activeAction != null ? _activeDefense : displayedAction?.Defense ?? 0;
            SetUpcomingStatText(_upcomingAttackText, "ATK", displayedAction?.Attack ?? 0);
            SetUpcomingStatText(_upcomingDefenseText, "DEF", displayedDefense);
        }

        private void SetUpcomingStatText(TMP_Text text, string label, int value)
        {
            if (text == null)
            {
                return;
            }

            var shouldDisplay = value > 0;
            text.gameObject.SetActive(shouldDisplay);
            if (shouldDisplay)
            {
                text.text = $"{label}: {value}";
            }
        }
    }
}
