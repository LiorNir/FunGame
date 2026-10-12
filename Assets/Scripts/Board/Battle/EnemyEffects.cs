using System.Collections.Generic;

namespace FunGame.Battle
{
    /// <summary>How an effect that targets a pilot picks it when the intent is revealed. Only active pilots can be picked.</summary>
    public enum PilotTargetRule
    {
        Random,

        /// <summary>The pilot that has lost the largest share of its defense.</summary>
        MostWounded,

        HighestAttack,
        HighestDefense,

        /// <summary>The pilot that counts in the most chains (a shared trunk); ties go to the one nearest the root.</summary>
        Trunk
    }

    /// <summary>
    /// An effect aimed at one pilot. The pilot is chosen when the intent is revealed, so the player sees it and can react; if
    /// the pilot has left the board by the time the effect resolves, the effect fizzles.
    /// </summary>
    public interface ITargetedEffect : IEnemyEffect
    {
        PilotTargetRule TargetRule { get; }
    }

    /// <summary>When an enemy effect happens during a turn's resolution.</summary>
    public enum EnemyEffectPhase
    {
        /// <summary>While the player attacks: the effect shields the enemy (it adds to the defense the player's attack must pass).</summary>
        Guard,

        /// <summary>On the enemy's own turn, after the player attacked, in the order the action lists its effects.</summary>
        Act
    }

    /// <summary>
    /// One thing an enemy action does. An action is a list of effects (attack, defend, and later snipe, lock...), so a new
    /// kind of enemy behaviour is a new small effect class instead of a change to the engine's resolution.
    /// </summary>
    public interface IEnemyEffect
    {
        EnemyEffectPhase Phase { get; }

        /// <summary>Applies the effect. Guard effects add to <see cref="EnemyEffectContext.Block"/>; Act effects change the battle.</summary>
        void Resolve(EnemyEffectContext context);

        /// <summary>A short text for logs and intent boxes, for example "ATK 3".</summary>
        string Describe();
    }

    /// <summary>What an effect may read and change while it resolves.</summary>
    public sealed class EnemyEffectContext
    {
        public BattleEngine Engine { get; }
        public LaneState Lane { get; }
        public EnemyActionData Action { get; }

        /// <summary>Guard phase: the enemy's defense against the player's attack this turn, summed over its Guard effects.</summary>
        public float Block { get; set; }

        /// <summary>
        /// For an <see cref="ITargetedEffect"/>: the ids of the pilots it chose when the intent was revealed (see
        /// <see cref="PilotUnit.Id"/>). Empty if there was nobody to target.
        /// </summary>
        public IReadOnlyList<int> Targets { get; set; } = new int[0];

        public EnemyEffectContext(BattleEngine engine, LaneState lane, EnemyActionData action)
        {
            Engine = engine;
            Lane = lane;
            Action = action;
        }
    }

    /// <summary>Hits the lane's defense chains in order; what they cannot absorb reaches the player.</summary>
    public sealed class AttackEffect : IEnemyEffect
    {
        public int Amount { get; }
        public EnemyEffectPhase Phase => EnemyEffectPhase.Act;

        public AttackEffect(int amount)
        {
            Amount = amount;
        }

        public void Resolve(EnemyEffectContext context)
        {
            context.Engine.HitLane(context.Lane, context.Action.Name, Amount);
        }

        public string Describe()
        {
            return $"ATK {Amount}";
        }
    }

    /// <summary>Lowers the player's attack on this enemy for the turn by its amount.</summary>
    public sealed class DefendEffect : IEnemyEffect
    {
        public int Amount { get; }
        public EnemyEffectPhase Phase => EnemyEffectPhase.Guard;

        public DefendEffect(int amount)
        {
            Amount = amount;
        }

        public void Resolve(EnemyEffectContext context)
        {
            context.Block += Amount;
        }

        public string Describe()
        {
            return $"DEF {Amount}";
        }
    }

    /// <summary>
    /// Locks the targeted pilot for a number of the player's turns: while locked it can be neither ejected nor replaced.
    /// A fallen pilot can still be cleared.
    /// </summary>
    public sealed class LockEffect : ITargetedEffect
    {
        public int Turns { get; }
        public PilotTargetRule TargetRule { get; }
        public EnemyEffectPhase Phase => EnemyEffectPhase.Act;

        public LockEffect(int turns, PilotTargetRule targetRule)
        {
            Turns = turns;
            TargetRule = targetRule;
        }

        public void Resolve(EnemyEffectContext context)
        {
            context.Engine.LockPilots(context.Lane, context.Targets, Turns, Describe());
        }

        public string Describe()
        {
            return $"LOCK {Turns}";
        }
    }

    /// <summary>
    /// Snipe: lowers both stats of the targeted pilot by a share (50% = half) of their current value for some of the
    /// player's turns, starting the turn after the enemy acts. Both stats, so a hit matters whichever chain the pilot is in.
    /// Misses if the pilot has left the board by then. Aimed at a spent pilot (every stat its chains read is 0, see
    /// <see cref="BattleEngine.IsSpent"/>) it becomes Assassinate: the pilot falls, which cuts its chains there.
    /// </summary>
    public sealed class SnipeEffect : ITargetedEffect
    {
        /// <summary>The share taken off both stats, 0 to 1.</summary>
        public float Share { get; }

        public int Turns { get; }
        public PilotTargetRule TargetRule { get; }
        public EnemyEffectPhase Phase => EnemyEffectPhase.Act;

        public SnipeEffect(float share, int turns, PilotTargetRule targetRule)
        {
            Share = share;
            Turns = turns;
            TargetRule = targetRule;
        }

        public void Resolve(EnemyEffectContext context)
        {
            context.Engine.SnipePilots(context.Lane, context.Targets, Share, Turns, Describe());
        }

        public const string AssassinateText = "ASSASSINATE";

        public string Describe()
        {
            return $"SNIPE -{DisplayFormat.Value(Share * 100f)}%";
        }
    }
}
