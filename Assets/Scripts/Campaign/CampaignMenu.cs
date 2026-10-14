using Encounters;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Campaign
{
    /// <summary>
    /// The campaign's menu scene: puts the map and Pilots screen prefabs on the canvas and switches between them, records
    /// upgrades in the save, and starts the battle scene with the chosen encounter. All looks live in the prefabs.
    /// </summary>
    public class CampaignMenu : MonoBehaviour
    {
        [SerializeField] private CampaignDefinition _campaign;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private MapScreenView _mapScreenPrefab;
        [SerializeField] private PilotsScreenView _pilotsScreenPrefab;
        [SerializeField] private string _battleScene = "BattleScene";

        private MapScreenView _map;
        private PilotsScreenView _pilots;

        private void Start()
        {
            if (_campaign == null || _canvas == null || _mapScreenPrefab == null || _pilotsScreenPrefab == null)
            {
                Debug.LogError("The campaign menu needs a Campaign, a Canvas and both screen prefabs.", this);
                return;
            }

            CampaignSession.Begin(_campaign);
            _map = Instantiate(_mapScreenPrefab, _canvas.transform);
            _pilots = Instantiate(_pilotsScreenPrefab, _canvas.transform);

            _map.EncounterClicked += Play;
            _map.PilotsClicked += () => Show(pilots: true);
            _map.UnlockAllClicked += () =>
            {
                CampaignSession.UnlockAll = !CampaignSession.UnlockAll;
                Show(pilots: false);
            };
            _map.ResetClicked += () =>
            {
                CampaignSession.ResetProgress();
                Show(pilots: false);
            };

            _pilots.BackClicked += () => Show(pilots: false);
            _pilots.UpgradeClicked += (id, stat) =>
            {
                if (CampaignSession.Progression.TryUpgrade(id, stat))
                {
                    CampaignSession.Save();
                }

                Show(pilots: true);
            };
            _pilots.RefundClicked += id =>
            {
                CampaignSession.Progression.ResetUpgrades(id);
                CampaignSession.Save();
                Show(pilots: true);
            };

            Show(pilots: false);
        }

        private void Show(bool pilots)
        {
            _map.gameObject.SetActive(!pilots);
            _pilots.gameObject.SetActive(pilots);
            if (pilots)
            {
                _pilots.Show(_campaign, CampaignSession.Progression);
            }
            else
            {
                _map.Show(_campaign, CampaignSession.Progression);
            }
        }

        private void Play(EncounterDefinition encounter)
        {
            CampaignSession.PendingEncounter = encounter;
            SceneManager.LoadScene(_battleScene);
        }
    }
}
