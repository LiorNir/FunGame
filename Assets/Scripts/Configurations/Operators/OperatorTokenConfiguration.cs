using System;
using UnityEngine;

namespace Configurations.Operators
{
    [CreateAssetMenu(fileName = "OperatorTokenConfiguration", menuName = "ScriptableObjects/Tokens/OperatorTokenConfiguration")]
    [Serializable]
    public class OperatorTokenConfiguration : ScriptableObject
    {
        [SerializeField] public Sprite Sprite;
        [SerializeField] public OperationType Type;
    }
}
