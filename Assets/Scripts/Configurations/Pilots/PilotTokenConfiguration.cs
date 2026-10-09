using System;
using UnityEngine;

namespace Configurations
{
    [CreateAssetMenu(fileName = "PilotTokenConfiguration", menuName = "ScriptableObjects/Tokens/PilotTokenConfiguration")]
    [Serializable]
    public class PilotTokenConfiguration : ScriptableObject
    {
        [SerializeField] public string Name;
        [SerializeField] public string DisplayName;
        [SerializeField] public Sprite Sprite;
        [SerializeField] public float AttackValue;
        [SerializeField] public float DefenseValue;
        [SerializeField] public bool Ability; //TODO: temp bool just for indication
    }
}
