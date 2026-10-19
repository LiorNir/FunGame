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
        [Tooltip("Optional: the encounter's stars and whether each can still be earned (under the turn number).")]
        [SerializeField] private TextMeshProUGUI _stars;
        [SerializeField] private TextMeshProUGUI _hint;
        [SerializeField] private Button _playTurn;
        [SerializeField] private GameObject _outcomePanel;
        [SerializeField] private TextMeshProUGUI _outcomeText;
        [SerializeField] private Button _restart;
        [Tooltip("Optional: the hover explanation box.")]
        [SerializeField] private TooltipView _tooltip;
        [Tooltip("Optional: the tutorial panel.")]
        [SerializeField] private TutorialView _tutorial;
        [Tooltip("Optional: on the end screen, back to the campaign map (shown only when the battle was started from the map).")]
        [SerializeField] private Button _map;
        [Tooltip("Optional: ends the battle as a defeat. The first click asks again on its label; a second click within a few seconds confirms.")]
        [SerializeField] private Button _giveUp;
        [SerializeField] private float _giveUpConfirmSeconds = 3f;
        [Tooltip("Optional: takes back the last action of this turn (placing, replacing, ejecting, an ability, an unlock).")]
        [SerializeField] private Button _undo;

        private float _displayHealth;
        private float _maxHealth;

        public event Action PlayTurnClicked;
        public event Action RestartClicked;
        public event Action MapClicked;
        public event Action GiveUpConfirmed;
        public event Action UndoClicked;

        private TextMeshProUGUI _giveUpLabel;
        private string _giveUpText;
        private bool _giveUpArmed;
        private Tween _giveUpDisarm;

        private void Awake()
        {
            _playTurn.onClick.AddListener(() => PlayTurnClicked?.Invoke());
            _restart.onClick.AddListener(() => RestartClicked?.Invoke());
            if (_map != null)
            {
                _map.onClick.AddListener(() => MapClicked?.Invoke());
                _map.gameObject.SetActive(false);
            }
            _outcomePanel.SetActive(false);
            if (_undo != null)
            {
                _undo.onClick.AddListener(() => UndoClicked?.Invoke());
            }

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
            _actionPoints.text = $"{Icons.ApIcon} {engine.ActionPoints}/{engine.Setup.Rules.ActionPointsPerTurn}";
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
            SetStars(string.Empty);
            _playTurn.interactable = false;
            if (_giveUp != null)
            {
                _giveUp.interactable = false;
            }
        }

        public void SetUndoEnabled(bool enabled)
        {
            if (_undo != null)
            {
                _undo.interactable = enabled;
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

        /// <summary>The stars list (see Campaign.StarText.Live); empty hides it.</summary>
        public void SetStars(string text)
        {
            if (_stars != null)
            {
                _stars.text = text ?? string.Empty;
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

        /// <summary>Where the health text is, in the world (for popups such as the damage taken).</summary>
        public Vector3 HealthWorldPosition(Camera camera)
        {
            var corners = new Vector3[4];
            _health.rectTransform.GetWorldCorners(corners);
            var screen = (corners[0] + corners[2]) * 0.5f;
            return camera != null ? camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z)) : Vector3.zero;
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

        /// <summary>Shows the end screen's way back to the campaign map (only for a battle started from the map).</summary>
        public void SetMapButtonVisible(bool visible)
        {
            if (_map != null)
            {
                _map.gameObject.SetActive(visible);
            }
        }

        public void ShowTooltip(string text)
        {
            if (_tooltip != null)
            {
                _tooltip.Show(text);
            }
        }

        public void HideTooltip()
        {
            if (_tooltip != null)
            {
                _tooltip.Hide();
            }
        }

        public void ShowTutorial(string line)
        {
            if (_tutorial != null)
            {
                _tutorial.Show(line);
            }
        }

        public void ClearTutorial()
        {
            if (_tutorial != null)
            {
                _tutorial.Clear();
            }
        }

        public void HideOutcome()
        {
            _outcomePanel.SetActive(false);
        }

        private void UpdateHealthText()
        {
            _health.text = $"{Icons.HpIcon} {DisplayFormat.Value(_displayHealth)}/{DisplayFormat.Value(_maxHealth)}";
        }
    }
}
