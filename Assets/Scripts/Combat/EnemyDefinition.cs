using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>The design data of one enemy: its health, look and the actions it chooses between.</summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "ScriptableObjects/Enemy")]
    public class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _sprite;

        [Tooltip("How big this enemy is drawn: 1 = the standard size set on the EnemyView prefab, 1.5 = half again as big.")]
        [Min(0.1f)] [SerializeField] private float _scale = 1f;
        [Min(1)] [SerializeField] private int _maxHealth = 20;

        [Tooltip("Sequential repeats the actions in order. Random picks one and reveals it before the turn it is used.")]
        [SerializeField] private EnemyActionSelectionMode _selectionMode;

        [SerializeField] private List<EnemyAction> _actions = new List<EnemyAction>();

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public Sprite Sprite => _sprite;
        public float Scale => _scale;
        public int MaxHealth => _maxHealth;
        public EnemyActionSelectionMode SelectionMode => _selectionMode;
        public IReadOnlyList<EnemyAction> Actions => _actions;

        public void SetData(string displayName, int maxHealth, EnemyActionSelectionMode selectionMode, List<EnemyAction> actions)
        {
            _displayName = displayName;
            _maxHealth = maxHealth;
            _selectionMode = selectionMode;
            _actions = actions;
        }
    }
}
