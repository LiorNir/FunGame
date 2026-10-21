using System;
using System.Collections.Generic;
using System.Linq;
using Configurations;
using FunGame.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Campaign
{
    /// <summary>
    /// The Pilots screen, as a prefab: the player's SP and every pilot of the campaign as a <see cref="PilotCardView"/>
    /// (owned ones with upgrade buttons, the rest as silhouettes), plus a way back to the map. The card is its own prefab.
    /// </summary>
    public class PilotsScreenView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _skillPoints;
        [Tooltip("The pilot cards are created inside this (give it a layout group to arrange them).")]
        [SerializeField] private RectTransform _cards;
        [SerializeField] private PilotCardView _cardPrefab;
        [SerializeField] private Button _back;

        private readonly List<GameObject> _shown = new List<GameObject>();

        public event Action BackClicked;
        public event Action<string, PilotStat> UpgradeClicked;
        public event Action<string> RefundClicked;

        private void Awake()
        {
            _back.onClick.AddListener(() => BackClicked?.Invoke());
        }

        public void Show(CampaignDefinition campaign, Progression progression)
        {
            foreach (var shown in _shown)
            {
                Destroy(shown);
            }

            _shown.Clear();
            _skillPoints.text = $"Skill points: {BattleView.Icons.SpIcon} {progression.Profile.SkillPoints}    (each further +1 to a stat costs 1 more)";

            // owned pilots first, in the order they joined; then the ones still to recruit, in campaign order
            var owned = progression.Profile.OwnedPilots.Select(campaign.FindPilot).Where(pilot => pilot != null).ToList();
            var all = campaign.StartingPilots.Concat(campaign.Worlds.SelectMany(world => world.RewardPilots)).Where(pilot => pilot != null).Distinct();
            var pilots = owned.Concat(all.Where(pilot => !owned.Contains(pilot)));
            foreach (var pilot in pilots)
            {
                var id = CampaignDefinition.IdOf(pilot);
                var (attack, defense) = progression.UpgradesOf(id);
                var card = Instantiate(_cardPrefab, _cards);
                card.Set(pilot, owned.Contains(pilot), progression.LevelOf(id), attack, defense,
                    progression.CostOf(id, PilotStat.Attack), progression.CanUpgrade(id, PilotStat.Attack),
                    progression.CostOf(id, PilotStat.Defense), progression.CanUpgrade(id, PilotStat.Defense));
                card.UpgradeAttackClicked += () => UpgradeClicked?.Invoke(id, PilotStat.Attack);
                card.UpgradeDefenseClicked += () => UpgradeClicked?.Invoke(id, PilotStat.Defense);
                card.RefundClicked += () => RefundClicked?.Invoke(id);
                _shown.Add(card.gameObject);
            }
        }
    }
}
