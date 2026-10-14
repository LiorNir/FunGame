using System;
using Configurations;
using FunGame.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Campaign
{
    /// <summary>
    /// One pilot on the Pilots screen, as a prefab: picture, name, stats (with bought upgrades marked), ability and the
    /// upgrade buttons. A pilot the player does not have yet is shown as a silhouette with its name hidden.
    /// </summary>
    public class PilotCardView : MonoBehaviour
    {
        [SerializeField] private Image _picture;
        [SerializeField] private TextMeshProUGUI _name;
        [Tooltip("Optional: the pilot's level (1 plus its upgrades).")]
        [SerializeField] private TextMeshProUGUI _level;
        [SerializeField] private TextMeshProUGUI _stats;
        [SerializeField] private TextMeshProUGUI _ability;
        [SerializeField] private Button _upgradeAttack;
        [SerializeField] private Button _upgradeDefense;
        [Tooltip("Takes back this pilot's upgrades and returns the SP (free while testing).")]
        [SerializeField] private Button _refund;

        [Header("Look")]
        [Tooltip("The picture's tint for a pilot the player does not have yet: black keeps the sprite's shape as a silhouette.")]
        [SerializeField] private Color _silhouetteColor = Color.black;
        [SerializeField] private Color _attackColor = new Color(1f, 0.4f, 0.3f);
        [SerializeField] private Color _defenseColor = new Color(0.35f, 0.65f, 1f);
        [SerializeField] private Color _upgradeColor = new Color(0.5f, 1f, 0.5f);

        public event Action UpgradeAttackClicked;
        public event Action UpgradeDefenseClicked;
        public event Action RefundClicked;

        private void Awake()
        {
            _upgradeAttack.onClick.AddListener(() => UpgradeAttackClicked?.Invoke());
            _upgradeDefense.onClick.AddListener(() => UpgradeDefenseClicked?.Invoke());
            _refund.onClick.AddListener(() => RefundClicked?.Invoke());
        }

        /// <param name="owned">False: shown as a silhouette, without stats, level or buttons.</param>
        /// <param name="attackCost">SP for the next +1 ATK, shown on its button.</param>
        public void Set(PilotTokenConfiguration pilot, bool owned, int level, int attackUpgrades, int defenseUpgrades,
                        int attackCost, bool canUpgradeAttack, int defenseCost, bool canUpgradeDefense)
        {
            if (_level != null)
            {
                _level.text = owned ? $"Lv {level}" : string.Empty;
            }

            _picture.sprite = pilot.Sprite;
            _picture.preserveAspect = true;
            _picture.color = owned ? Color.white : _silhouetteColor;
            _name.text = owned ? (string.IsNullOrEmpty(pilot.DisplayName) ? pilot.name : pilot.DisplayName) : "???";

            _upgradeAttack.gameObject.SetActive(owned);
            _upgradeDefense.gameObject.SetActive(owned);
            _refund.gameObject.SetActive(owned && attackUpgrades + defenseUpgrades > 0);
            _upgradeAttack.interactable = canUpgradeAttack;
            _upgradeDefense.interactable = canUpgradeDefense;
            SetLabel(_upgradeAttack, $"+1 ATK ({attackCost} SP)");
            SetLabel(_upgradeDefense, $"+1 DEF ({defenseCost} SP)");

            if (!owned)
            {
                _stats.text = "not recruited yet";
                _ability.text = string.Empty;
                return;
            }

            _stats.text = Stat("ATK", pilot.AttackValue, attackUpgrades, _attackColor) + "   " + Stat("DEF", pilot.DefenseValue, defenseUpgrades, _defenseColor);
            var ability = PilotAbilities.Create(pilot.AbilityKind, pilot.AbilityApCost);
            _ability.text = ability == null ? string.Empty : $"{ability.Name} ({ability.ApCost} AP): {ability.Describe()}";
        }

        private static void SetLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = text;
            }
        }

        private string Stat(string label, float baseValue, int upgrades, Color color)
        {
            var value = DisplayFormat.Value(baseValue + upgrades);
            var bonus = upgrades > 0 ? $" <color=#{ColorUtility.ToHtmlStringRGB(_upgradeColor)}>(+{upgrades})</color>" : string.Empty;
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{label} {value}</color>{bonus}";
        }
    }
}
