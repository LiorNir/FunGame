using System;
using DG.Tweening;
using FunGame.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleView
{
    /// <summary>
    /// The root of the 2D UI prefab: health, AP and turn texts, the Play Turn button, a hint line and the end-of-battle
    /// panel. The hand area, the action menu and the test panel are children of the same prefab. Everything is positioned
    /// and sized in the prefab; this script only fills in the values.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _health;
        [SerializeField] private TextMeshProUGUI _actionPoints;
        [SerializeField] private TextMeshProUGUI _turn;
        [SerializeField] private TextMeshProUGUI _hint;
        [SerializeField] private Button _playTurn;
        [SerializeField] private GameObject _outcomePanel;
        [SerializeField] private TextMeshProUGUI _outcomeText;
        [SerializeField] private Button _restart;
        [Tooltip("Optional: ends the battle as a defeat. The first click asks again on its label; a second click within a few seconds confirms.")]
        [SerializeField] private Button _giveUp;
        [SerializeField] private float _giveUpConfirmSeconds = 3f;

        private float _displayHealth;
        private float _maxHealth;

        public event Action PlayTurnClicked;
        public event Action RestartClicked;
        public event Action GiveUpConfirmed;

        private TextMeshProUGUI _giveUpLabel;
        private string _giveUpText;
        private bool _giveUpArmed;
        private Tween _giveUpDisarm;

        private void Awake()
        {
            _playTurn.onClick.AddListener(() => PlayTurnClicked?.Invoke());
            _restart.onClick.AddListener(() => RestartClicked?.Invoke());
            _outcomePanel.SetActive(false);
            if (_giveUp != null)
            {
                _giveUpLabel = _giveUp.GetComponentInChildren<TextMeshProUGUI>();
                _giveUpText = _giveUpLabel != null ? _giveUpLabel.text : string.Empty;
                _giveUp.onClick.AddListener(OnGiveUpClicked);
            }
        }

        private void OnGiveUpClicked()
        {
            if (_giveUpArmed)
            {
                DisarmGiveUp();
                GiveUpConfirmed?.Invoke();
                return;
            }

            _giveUpArmed = true;
            if (_giveUpLabel != null)
            {
                _giveUpLabel.text = "Sure? Click again";
            }

            _giveUpDisarm?.Kill();
            _giveUpDisarm = DOVirtual.DelayedCall(_giveUpConfirmSeconds, DisarmGiveUp).SetLink(gameObject);
        }

        private void DisarmGiveUp()
        {
            _giveUpArmed = false;
            _giveUpDisarm?.Kill();
            _giveUpDisarm = null;
            if (_giveUpLabel != null)
            {
                _giveUpLabel.text = _giveUpText;
            }
        }

        public void Refresh(BattleEngine engine, bool canAct)
        {
            _maxHealth = engine.Setup.Rules.PlayerMaxHealth;
            _displayHealth = engine.PlayerHealth;
            UpdateHealthText();
            _actionPoints.text = $"AP {engine.ActionPoints}/{engine.Setup.Rules.ActionPointsPerTurn}";
            _turn.text = $"Turn {engine.Turn}";
            _playTurn.interactable = canAct && engine.Outcome == BattleOutcome.InProgress;
            if (_giveUp != null)
            {
                _giveUp.interactable = canAct && engine.Outcome == BattleOutcome.InProgress;
            }
        }

        /// <summary>Shows the HUD with no battle running.</summary>
        public void ShowIdle()
        {
            _health.text = string.Empty;
            _actionPoints.text = string.Empty;
            _turn.text = string.Empty;
            _hint.text = string.Empty;
            _playTurn.interactable = false;
            if (_giveUp != null)
            {
                _giveUp.interactable = false;
            }
        }

        public void SetPlayEnabled(bool enabled)
        {
            _playTurn.interactable = enabled;
            if (_giveUp != null)
            {
                _giveUp.interactable = enabled;
            }
        }

        public void SetHint(string text)
        {
            _hint.text = text ?? string.Empty;
        }

        public Tween SetHealthAnimated(float health)
        {
            return DOTween.To(() => _displayHealth, value =>
            {
                _displayHealth = value;
                UpdateHealthText();
            }, health, 0.4f).SetEase(Ease.OutCubic);
        }

        public void FlashHealth()
        {
            _health.transform.DOKill(true);
            _health.transform.DOPunchScale(Vector3.one * 0.25f, 0.4f);
        }

        public void ShowOutcome(BattleOutcome outcome, string details)
        {
            var title = outcome == BattleOutcome.Victory ? "VICTORY" : (outcome == BattleOutcome.Defeat ? "DEFEAT" : "ENDED");
            var color = outcome == BattleOutcome.Victory ? "#88FF99" : "#FF7777";
            _outcomeText.text = $"<size=140%><color={color}>{title}</color></size>\n{details}";
            _outcomePanel.SetActive(true);
        }

        public void HideOutcome()
        {
            _outcomePanel.SetActive(false);
        }

        private void UpdateHealthText()
        {
            _health.text = $"HP {DisplayFormat.Value(_displayHealth)}/{DisplayFormat.Value(_maxHealth)}";
        }
    }
}
