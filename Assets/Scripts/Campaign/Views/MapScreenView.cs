using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
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

        [Header("Unspent skill points")]
        [Tooltip("While the player has SP to spend, the Pilots button and the SP text flash to this colour and back.")]
        [SerializeField] private Color _unspentFlashColor = new Color(1f, 0.82f, 0.25f);
        [SerializeField] private float _unspentFlashSeconds = 0.6f;

        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly List<Tween> _flashes = new List<Tween>();
        private Color _pilotsColor;
        private Color _skillPointsColor;

        public event Action PilotsClicked;
        public event Action UnlockAllClicked;
        public event Action ResetClicked;
        public event Action OpenLogsClicked;
        public event Action<EncounterDefinition> EncounterClicked;

        private void Awake()
        {
            _pilotsColor = _pilots.targetGraphic != null ? _pilots.targetGraphic.color : Color.white;
            _skillPointsColor = _skillPoints.color;
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
            FlashUnspent(progression.Profile.SkillPoints > 0);

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

        // points the player at the Pilots screen while there are SP to spend there
        private void FlashUnspent(bool flash)
        {
            foreach (var running in _flashes)
            {
                running.Kill();
            }

            _flashes.Clear();
            _skillPoints.color = _skillPointsColor;
            if (_pilots.targetGraphic != null)
            {
                _pilots.targetGraphic.color = _pilotsColor;
            }

            if (!flash || _unspentFlashSeconds <= 0f)
            {
                return;
            }

            _flashes.Add(_skillPoints.DOColor(_unspentFlashColor, _unspentFlashSeconds).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(gameObject));
            if (_pilots.targetGraphic != null)
            {
                _flashes.Add(_pilots.targetGraphic.DOColor(_unspentFlashColor, _unspentFlashSeconds).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(gameObject));
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
