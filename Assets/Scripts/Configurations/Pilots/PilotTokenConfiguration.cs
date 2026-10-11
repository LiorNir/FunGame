using System;
using FunGame.Battle;
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

        [Header("Ability")]
        [Tooltip("The pilot's ability (used once per encounter, from the board).")]
        [SerializeField] public PilotAbilityKind AbilityKind;

        [Tooltip("What the ability costs in AP. Every ability has its own cost.")]
        [Min(0)] [SerializeField] public int AbilityApCost = 2;
    }
}
