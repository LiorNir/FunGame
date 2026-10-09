namespace FunGame.Battle
{
    /// <summary>
    /// The random numbers of a battle (deck shuffle, random enemy actions). Unlike <see cref="System.Random"/> its whole
    /// state can be copied, so a battle can be cloned exactly or cloned with a fresh stream. A player that looks ahead must
    /// use a fresh stream for its look-ahead, or it would see the real outcome of the dice.
    /// </summary>
    public sealed class BattleRandom
    {
        private ulong _state;

        public BattleRandom(int seed)
        {
            _state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x2545F4914F6CDD1DUL;
            NextUnsigned();
        }

        private BattleRandom(ulong state)
        {
            _state = state;
        }

        /// <summary>An exact copy: it produces the same numbers from here on.</summary>
        public BattleRandom Clone()
        {
            return new BattleRandom(_state);
        }

        /// <summary>A number from 0 up to, not including, <paramref name="maxExclusive"/>.</summary>
        public int Next(int maxExclusive)
        {
            return (int)(NextUnsigned() % (ulong)maxExclusive);
        }

        // SplitMix64
        private ulong NextUnsigned()
        {
            _state += 0x9E3779B97F4A7C15UL;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
