using System;
using UnityEngine;

namespace Combat
{
    [Serializable]
    public class EnemyAction
    {
        public string Name;
        [Min(0)] public int Attack;
        [Min(0)] public int Defense;
    }
}
