using System;
using FunGame.Battle;
using UnityEngine;

namespace Combat
{
    [Serializable]
    public class EnemyAction
    {
        public string Name;
        [Min(0)] public int Attack;
        [Min(0)] public int Defense;

        [Tooltip("Above 0: the action also locks one pilot for this many of the player's turns (it cannot be ejected or replaced). The pilot is picked and shown when the action is revealed.")]
        [Min(0)] public int LockTurns;

        [Tooltip("Which active pilot the lock picks.")]
        public PilotTargetRule LockTarget = PilotTargetRule.Trunk;
    }
}
