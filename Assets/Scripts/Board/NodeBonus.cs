namespace FunGame.Board
{
    public enum BonusKind
    {
        None,
        Add,
        Multiply
    }

    /// <summary>An extra effect on a slot, applied to the accumulated value after the slot's operator.</summary>
    public readonly struct NodeBonus
    {
        public BonusKind Kind { get; }
        public float Amount { get; }

        public NodeBonus(BonusKind kind, float amount)
        {
            Kind = kind;
            Amount = amount;
        }

        public static NodeBonus None => default;

        public float Apply(float value)
        {
            switch (Kind)
            {
                case BonusKind.Add:
                    return value + Amount;
                case BonusKind.Multiply:
                    return value * Amount;
                default:
                    return value;
            }
        }
    }
}
