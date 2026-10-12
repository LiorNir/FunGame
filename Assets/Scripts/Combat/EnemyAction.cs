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

        [Tooltip("Above 0: a charge. The enemy guards this much (on top of Defense); if the player breaks through the whole guard this turn, the next Unleash does nothing.")]
        [Min(0)] public int ChargeDefense;

        [Tooltip("Above 0: the unleash of a charge, an attack of this much (on top of Attack) that does nothing if the charge was broken. Put it on the action after the charge.")]
        [Min(0)] public int UnleashAttack;
    }
}
