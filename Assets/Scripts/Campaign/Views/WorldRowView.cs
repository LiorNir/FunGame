using TMPro;
using UnityEngine;

namespace Campaign
{
    /// <summary>One world on the map, as a prefab: its name and a container the encounter buttons are placed in (in order).</summary>
    public class WorldRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _name;
        [Tooltip("The encounter buttons are created inside this (give it a layout group to arrange them).")]
        [SerializeField] private RectTransform _encounters;
        [SerializeField] private Color _openNameColor = Color.white;
        [SerializeField] private Color _lockedNameColor = new Color(1f, 1f, 1f, 0.4f);

        public RectTransform Encounters => _encounters;

        public void Set(string worldName, bool open)
        {
            _name.text = worldName;
            _name.color = open ? _openNameColor : _lockedNameColor;
        }
    }
}
