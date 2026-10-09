using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleView
{
    /// <summary>
    /// The testing panel (a child of the HUD prefab): pick an encounter with the arrows, then Start or Stop the battle at
    /// any time. Starting while a battle is running restarts it with the chosen encounter.
    /// </summary>
    public class TestPanelView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _position;
        [SerializeField] private Button _start;
        [SerializeField] private Button _stop;
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;

        public event Action PreviousClicked;
        public event Action NextClicked;
        public event Action StartClicked;
        public event Action StopClicked;

        private void Awake()
        {
            _previous.onClick.AddListener(() => PreviousClicked?.Invoke());
            _next.onClick.AddListener(() => NextClicked?.Invoke());
            _start.onClick.AddListener(() => StartClicked?.Invoke());
            _stop.onClick.AddListener(() => StopClicked?.Invoke());
        }

        public void Show(string encounterName, int number, int total, bool running)
        {
            _label.text = string.IsNullOrEmpty(encounterName) ? "(no encounter)" : encounterName;
            _position.text = total > 0 && number > 0 ? $"{number} / {total}" : string.Empty;
            var startLabel = _start.GetComponentInChildren<TextMeshProUGUI>();
            if (startLabel != null)
            {
                startLabel.text = running ? "Restart" : "Start";
            }

            _stop.interactable = running;
            _previous.interactable = total > 1;
            _next.interactable = total > 1;
        }
    }
}
