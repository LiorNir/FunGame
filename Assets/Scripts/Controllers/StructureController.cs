using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Views;

namespace Controllers
{
    public class StructureController : MonoBehaviour
    {
        [SerializeField] private TMP_Text _attackText;
        [SerializeField] private TMP_Text _defenseText;

        [SerializeField] private PilotSlotView _headPilotView;
        [SerializeField] private PilotSlotView _attackPilotView;
        [SerializeField] private PilotSlotView _secondaryAttackPilotView;
        [SerializeField] private PilotSlotView _defensePilotView;
        [SerializeField] private PilotSlotView _secondaryDefensePilotView;

        [SerializeField] private OperatorSlotView _middleOperatorView;
        [SerializeField] private OperatorSlotView _attackOperatorView;
        [SerializeField] private OperatorSlotView _defenseOperatorView;

        private float _attackValue;
        private float _defenseValue;

        public float AttackValue => _attackValue;
        public float DefenseValue => _defenseValue;

        private void Awake()
        {
            SubscribeToSlotChanges();
            UpdateStructureStats();
        }

        private void OnDestroy()
        {
            UnsubscribeFromSlotChanges();
        }

        private void UpdateStructureStats()
        {
            _attackValue = CalculateAttackValue();
            _attackText.text = _attackValue.ToString();
            _defenseValue = CalculateDefenseValue();
            _defenseText.text = _defenseValue.ToString();
        }

        private float CalculateAttackValue()
        {
            if (!_headPilotView.IsOccupied) return 0f;

            var attackValue = _headPilotView.CurrentConfiguration.AttackValue;
            if (!_attackPilotView.IsOccupied || !_middleOperatorView.IsOccupied) return attackValue;

            attackValue = UpdateValueByOperationType(
                attackValue,
                _attackPilotView.CurrentConfiguration.AttackValue,
                _middleOperatorView.CurrentOperationType);

            if (!_secondaryAttackPilotView.IsOccupied || !_attackOperatorView.IsOccupied) return attackValue;

            return UpdateValueByOperationType(
                attackValue,
                _secondaryAttackPilotView.CurrentConfiguration.AttackValue,
                _attackOperatorView.CurrentOperationType);
        }

        private float CalculateDefenseValue()
        {
            if (!_headPilotView.IsOccupied) return 0f;

            var defenseValue = _headPilotView.ActiveDefenseValue;
            if (!_defensePilotView.IsOccupied || !_middleOperatorView.IsOccupied) return defenseValue;

            defenseValue = UpdateValueByOperationType(
                defenseValue,
                _defensePilotView.ActiveDefenseValue,
                _middleOperatorView.CurrentOperationType);

            if (!_secondaryDefensePilotView.IsOccupied || !_defenseOperatorView.IsOccupied) return defenseValue;

            return UpdateValueByOperationType(
                defenseValue,
                _secondaryDefensePilotView.ActiveDefenseValue,
                _defenseOperatorView.CurrentOperationType);
        }

        private float UpdateValueByOperationType(float firstValue, float secondValue, OperationType operationType)
        {
            switch (operationType)
            {
                case OperationType.Add:
                    return firstValue + secondValue;
                case OperationType.Subtract:
                    return firstValue - secondValue;
                case OperationType.Multiply:
                    return firstValue * secondValue;
                case OperationType.Divide:
                    return secondValue == 0f ? firstValue : firstValue / secondValue;
                default:
                    return firstValue;
            }
        }

        public int AbsorbIncomingDamage(int incomingDamage)
        {
            var remainingDamage = Mathf.Max(0, incomingDamage);
            var defenseSlotsFromFurthestToClosest = new[]
            {
                _secondaryDefensePilotView,
                _defensePilotView,
                _headPilotView
            };

            foreach (var slot in defenseSlotsFromFurthestToClosest)
            {
                remainingDamage = Mathf.CeilToInt(slot.AbsorbDefenseDamage(remainingDamage));
                if (remainingDamage <= 0)
                {
                    break;
                }
            }

            UpdateStructureStats();
            return remainingDamage;
        }

        public async UniTask<int> AbsorbIncomingDamageAnimated(int incomingDamage)
        {
            var remainingDamage = Mathf.Max(0, incomingDamage);
            var defenseSlotsFromFurthestToClosest = new[]
            {
                _secondaryDefensePilotView,
                _defensePilotView,
                _headPilotView
            };

            foreach (var slot in defenseSlotsFromFurthestToClosest)
            {
                remainingDamage = Mathf.CeilToInt(await slot.AbsorbDefenseDamageAnimated(remainingDamage));
                if (remainingDamage <= 0)
                {
                    break;
                }
            }

            UpdateStructureStats();
            return remainingDamage;
        }

        private void SubscribeToSlotChanges()
        {
            _headPilotView.StateChanged += UpdateStructureStats;
            _attackPilotView.StateChanged += UpdateStructureStats;
            _secondaryAttackPilotView.StateChanged += UpdateStructureStats;
            _defensePilotView.StateChanged += UpdateStructureStats;
            _secondaryDefensePilotView.StateChanged += UpdateStructureStats;
            _middleOperatorView.StateChanged += UpdateStructureStats;
            _attackOperatorView.StateChanged += UpdateStructureStats;
            _defenseOperatorView.StateChanged += UpdateStructureStats;
        }

        private void UnsubscribeFromSlotChanges()
        {
            _headPilotView.StateChanged -= UpdateStructureStats;
            _attackPilotView.StateChanged -= UpdateStructureStats;
            _secondaryAttackPilotView.StateChanged -= UpdateStructureStats;
            _defensePilotView.StateChanged -= UpdateStructureStats;
            _secondaryDefensePilotView.StateChanged -= UpdateStructureStats;
            _middleOperatorView.StateChanged -= UpdateStructureStats;
            _attackOperatorView.StateChanged -= UpdateStructureStats;
            _defenseOperatorView.StateChanged -= UpdateStructureStats;
        }
    }
}
