using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleView
{
    /// <summary>
    /// The battle's tutorial panel, part of the HUD prefab: one line at a time with a "Got it" button; lines that arrive
    /// while one is shown wait in a queue. It does not block the board.
    /// </summary>
    public class TutorialView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private Button _ok;

        private readonly Queue<string> _waiting = new Queue<string>();

        private void Awake()
        {
            _ok.onClick.AddListener(ShowNext);
            _panel.SetActive(false);
        }

        public void Show(string line)
        {
            _waiting.Enqueue(line);
            if (!_panel.activeSelf)
            {
                ShowNext();
            }
        }

        public void Clear()
        {
            _waiting.Clear();
            _panel.SetActive(false);
        }

        private void ShowNext()
        {
            if (_waiting.Count == 0)
            {
                _panel.SetActive(false);
                return;
            }

            _text.text = _waiting.Dequeue();
            _panel.SetActive(true);
        }
    }
}
