namespace FunGame.Battle
{
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
}
