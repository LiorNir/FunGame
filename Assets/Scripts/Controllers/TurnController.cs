using Combat;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Controllers
{
    public class TurnController : MonoBehaviour
    {
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private StructureController _structureController;
        [SerializeField] private EnemyController _enemyController;
        [SerializeField] private TMP_Text _turnResultText;
        private bool _isResolving;

        public bool CombatEnded => _playerController.IsDefeated || _enemyController.IsDefeated;

        public void ResolveTurn()
        {
            if (_isResolving || CombatEnded)
            {
                return;
            }

            ResolveTurnAnimated().Forget();
        }

        private async UniTaskVoid ResolveTurnAnimated()
        {
            _isResolving = true;
            try
            {
            var enemyAction = _enemyController.ConsumeUpcomingAction();
            if (enemyAction == null)
            {
                SetTurnResult("Enemy has no actions configured.");
                return;
            }

            var damageToEnemy = await _enemyController.ReceivePlayerAttackAnimated(
                Mathf.CeilToInt(_structureController.AttackValue));
            if (_enemyController.IsDefeated)
            {
                _enemyController.FinishCurrentAction();
                SetTurnResult("Enemy defeated.");
                return;
            }

            var damageAfterDefense = await _structureController.AbsorbIncomingDamageAnimated(enemyAction.Attack);
            await _playerController.TakeDamageAnimated(damageAfterDefense);
            SetTurnResult(CreateTurnResult(enemyAction, damageToEnemy, damageAfterDefense));

            _enemyController.FinishCurrentAction();
            if (!_playerController.IsDefeated)
            {
                _enemyController.PreviewNextAction();
            }
            }
            finally
            {
                _isResolving = false;
            }
        }

        private string CreateTurnResult(EnemyAction enemyAction, int playerDamage, int damageAfterDefense)
        {
            var result = $"{enemyAction.Name}: dealt {playerDamage} to enemy. Enemy attacked for {enemyAction.Attack}; {damageAfterDefense} reached player HP.";
            return _playerController.IsDefeated ? result + " Player defeated." : result;
        }

        private void SetTurnResult(string result)
        {
            if (_turnResultText != null)
            {
                _turnResultText.text = result;
            }

            Debug.Log(result);
        }
    }
}
