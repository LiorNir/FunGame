using System.Collections.Generic;
using BattleView;
using FunGame.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Campaign
{
    /// <summary>
    /// The map screen (temporary look, built in code): the player's SP, one row per world with a button per encounter
    /// (open, cleared or locked, with what a first clear gives), and testing buttons. Choosing an encounter loads the
    /// battle scene with it.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        [SerializeField] private CampaignDefinition _campaign;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private string _battleScene = "BattleScene";

        [Header("Look")]
        [SerializeField] private Color _openColor = new Color(0.15f, 0.4f, 0.8f);
        [SerializeField] private Color _clearedColor = new Color(0.2f, 0.5f, 0.3f);
        [SerializeField] private Color _lockedColor = new Color(0.25f, 0.25f, 0.3f);
        [SerializeField] private Color _buttonColor = new Color(0.3f, 0.32f, 0.42f);
        [SerializeField] private Vector2 _encounterSize = new Vector2(330f, 120f);
        [SerializeField] private float _rowHeight = 160f;

        private readonly List<GameObject> _built = new List<GameObject>();

        private void Start()
        {
            if (_campaign == null || _canvas == null)
            {
                Debug.LogError("The map needs a Campaign and a Canvas.", this);
                return;
            }

            CampaignSession.Begin(_campaign);
            Build();
        }

        private void Build()
        {
            foreach (var built in _built)
            {
                Destroy(built);
            }

            _built.Clear();
            var root = ViewFactory.CreateUiRoot(_canvas.transform, "Map", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _built.Add(root.gameObject);
            var progression = CampaignSession.Progression;

            var header = ViewFactory.CreateUiText(root, "Header", $"Skill points: {progression.Profile.SkillPoints}", 46f, Color.white, new Vector2(800f, 70f), Vector2.zero, TextAlignmentOptions.Left);
            Anchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(60f, -40f));

            AddTopButton(root, "Reset progress", new Vector2(-60f, -40f), () =>
            {
                CampaignSession.ResetProgress();
                Build();
            });
            AddTopButton(root, CampaignSession.UnlockAll ? "Unlock all: on" : "Unlock all: off", new Vector2(-420f, -40f), () =>
            {
                CampaignSession.UnlockAll = !CampaignSession.UnlockAll;
                Build();
            });

            var worlds = _campaign.Worlds;
            var top = (worlds.Count - 1) * _rowHeight * 0.5f - 60f;
            for (var worldIndex = 0; worldIndex < worlds.Count; worldIndex++)
            {
                var world = worlds[worldIndex];
                var y = top - worldIndex * _rowHeight;
                var open = progression.IsWorldOpen(worldIndex);
                var label = ViewFactory.CreateUiText(root, world.Name, world.Name, 40f, open ? Color.white : new Color(1f, 1f, 1f, 0.4f),
                    new Vector2(300f, 80f), new Vector2(-720f, y), TextAlignmentOptions.Left);
                label.fontStyle = FontStyles.Bold;

                for (var index = 0; index < world.Encounters.Count; index++)
                {
                    AddEncounter(root, progression, worldIndex, index, new Vector2(-380f + index * (_encounterSize.x + 30f), y));
                }
            }
        }

        private void AddEncounter(RectTransform root, Progression progression, int worldIndex, int index, Vector2 position)
        {
            var encounter = _campaign.Worlds[worldIndex].Encounters[index];
            var state = progression.StateOf(worldIndex, index);
            var color = state == EncounterState.Cleared ? _clearedColor : state == EncounterState.Open ? _openColor : _lockedColor;
            var title = encounter.DisplayName.StartsWith("World ") ? encounter.DisplayName.Substring(6) : encounter.DisplayName;
            var reward = CampaignSession.Describe(progression.RewardFor(CampaignDefinition.IdOf(encounter)));
            var footer = state == EncounterState.Cleared ? "cleared" : state == EncounterState.Locked ? "locked" : reward;
            var button = ViewFactory.CreateButton(root, encounter.name, $"{title}\n<size=70%>{footer}</size>", _encounterSize, position, color, 28f);
            button.interactable = state != EncounterState.Locked;
            button.onClick.AddListener(() =>
            {
                CampaignSession.PendingEncounter = encounter;
                SceneManager.LoadScene(_battleScene);
            });
        }

        private void AddTopButton(RectTransform root, string label, Vector2 offset, UnityEngine.Events.UnityAction onClick)
        {
            var button = ViewFactory.CreateButton(root, label, label, new Vector2(320f, 64f), Vector2.zero, _buttonColor, 28f);
            Anchor((RectTransform)button.transform, new Vector2(1f, 1f), offset);
            button.onClick.AddListener(onClick);
        }

        private static void Anchor(RectTransform rect, Vector2 corner, Vector2 offset)
        {
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.anchoredPosition = offset;
        }
    }
}
