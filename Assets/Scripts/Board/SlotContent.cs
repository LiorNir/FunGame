namespace FunGame.Board
{
    /// <summary>What currently occupies a slot, as the evaluator sees it. The default value is an empty slot.</summary>
    public readonly struct SlotContent
    {
        public bool IsFilled { get; }
        public float Attack { get; }
        public float Defense { get; }

        public SlotContent(float attack, float defense)
        {
            IsFilled = true;
            Attack = attack;
            Defense = defense;
        }

        public static SlotContent Empty => default;

        public float GetStat(ChainRole role)
        {
            return role == ChainRole.Attack ? Attack : Defense;
        }
    }
}
