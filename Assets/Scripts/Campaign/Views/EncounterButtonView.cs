using System;
using FunGame.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Campaign
{
    /// <summary>
    /// One encounter on the map, as a prefab: a button with a background, a title and a footer line (what a first clear
    /// gives, "cleared" or "locked"). Its colour by state is set here; everything else on the prefab.
    /// </summary>
    public class EncounterButtonView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _footer;

        [Header("Background by state")]
        [SerializeField] private Color _openColor = new Color(0.15f, 0.4f, 0.8f);
        [SerializeField] private Color _clearedColor = new Color(0.2f, 0.5f, 0.3f);
        [SerializeField] private Color _lockedColor = new Color(0.25f, 0.25f, 0.3f);

        public event Action Clicked;

        private void Awake()
        {
            _button.onClick.AddListener(() => Clicked?.Invoke());
        }

        public void Set(string title, string footer, EncounterState state)
        {
            _title.text = title;
            _footer.text = footer;
            _background.color = state == EncounterState.Cleared ? _clearedColor : state == EncounterState.Open ? _openColor : _lockedColor;
            _button.interactable = state != EncounterState.Locked;
        }
    }
}
