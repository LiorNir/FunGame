using System;
using System.Collections.Generic;
using System.Linq;
using Encounters;
using FunGame.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Campaign
{
    /// <summary>
    /// The map screen, as a prefab: the player's SP, one <see cref="WorldRowView"/> per world holding an
    /// <see cref="EncounterButtonView"/> per encounter, and buttons to the Pilots screen and for testing. The rows and
    /// buttons are their own prefabs, assigned here.
    /// </summary>
    public class MapScreenView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _skillPoints;
        [Tooltip("The world rows are created inside this (give it a layout group to arrange them).")]
        [SerializeField] private RectTransform _worlds;
        [SerializeField] private WorldRowView _worldRowPrefab;
        [SerializeField] private EncounterButtonView _encounterPrefab;
        [SerializeField] private Button _pilots;
        [SerializeField] private Button _unlockAll;
        [SerializeField] private Button _reset;
        [Tooltip("Optional: opens the folder with the playtest logs (to zip and send).")]
        [SerializeField] private Button _openLogs;

        private readonly List<GameObject> _rows = new List<GameObject>();

        public event Action PilotsClicked;
        public event Action UnlockAllClicked;
        public event Action ResetClicked;
        public event Action OpenLogsClicked;
        public event Action<EncounterDefinition> EncounterClicked;

        private void Awake()
        {
            _pilots.onClick.AddListener(() => PilotsClicked?.Invoke());
            _unlockAll.onClick.AddListener(() => UnlockAllClicked?.Invoke());
            _reset.onClick.AddListener(() => ResetClicked?.Invoke());
            if (_openLogs != null)
            {
                _openLogs.onClick.AddListener(() => OpenLogsClicked?.Invoke());
            }
        }

        public void Show(CampaignDefinition campaign, Progression progression)
        {
            foreach (var row in _rows)
            {
                Destroy(row);
            }

            _rows.Clear();
            var allStars = campaign.Worlds.Sum(world => world.Encounters.Where(encounter => encounter != null).Sum(encounter => encounter.Stars.Count));
            _skillPoints.text = $"Skill points: {progression.Profile.SkillPoints}   Stars: {progression.TotalStars}/{allStars}";
            SetLabel(_unlockAll, progression.UnlockAll ? "Unlock all: on" : "Unlock all: off");

            for (var worldIndex = 0; worldIndex < campaign.Worlds.Count; worldIndex++)
            {
                var world = campaign.Worlds[worldIndex];
                var row = Instantiate(_worldRowPrefab, _worlds);
                row.Set(world.Name, progression.IsWorldOpen(worldIndex));
                _rows.Add(row.gameObject);

                for (var index = 0; index < world.Encounters.Count; index++)
                {
                    var encounter = world.Encounters[index];
                    var state = progression.StateOf(worldIndex, index);
                    var title = encounter.DisplayName.StartsWith("World ") ? encounter.DisplayName.Substring(6) : encounter.DisplayName;
                    // a cleared encounter shows its stars (the button's colour already says it is cleared)
                    var footer = state == EncounterState.Cleared ? StarText.Row(progression.StarsOf(CampaignDefinition.IdOf(encounter)), encounter.Stars.Count)
                        : state == EncounterState.Locked ? "locked"
                        : CampaignSession.Describe(progression.RewardFor(CampaignDefinition.IdOf(encounter)));
                    var button = Instantiate(_encounterPrefab, row.Encounters);
                    button.Set(title, footer, state);
                    button.Clicked += () => EncounterClicked?.Invoke(encounter);
                }
            }
        }

        private static void SetLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}
