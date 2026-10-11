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

        [Tooltip("Above 0: the action also snipes one pilot, lowering both its stats by this many percent from the next turn. The pilot is picked and shown when the action is revealed.")]
        [Range(0f, 100f)] public float SnipePercent;

        [Tooltip("For how many of the player's turns the snipe lasts.")]
        [Min(1)] public int SnipeTurns = 1;

        [Tooltip("Which active pilot the snipe picks.")]
        public PilotTargetRule SnipeTarget = PilotTargetRule.HighestAttack;
    }
}
