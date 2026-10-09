using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FunGame.Board
{
    public sealed class SolverOptions
    {
        /// <summary>
        /// When false (default), as many pilots as possible are placed: all slots are filled, or all pilots are used.
        /// When true, every arrangement including partial ones is evaluated.
        /// </summary>
        public bool AllowPartial { get; set; }

        /// <summary>Slots that cannot be filled (locked). They stay empty and break any chain passing through them.</summary>
        public ISet<string> LockedSlotIds { get; set; } = new HashSet<string>();

        /// <summary>The solver refuses to run if the arrangement count would exceed this.</summary>
        public double MaxArrangements { get; set; } = 5000000;
    }

    public sealed class SolvedArrangement
    {
        /// <summary>Pilot name per slot id. Empty slots are absent.</summary>
        public IReadOnlyDictionary<string, string> PilotBySlotId { get; }
        public IReadOnlyDictionary<string, float> ChainValues { get; }
        public float TotalAttack { get; }
        public float TotalDefense { get; }

        /// <summary>True if an active slot on some chain sits behind a subtract or divide connection.</summary>
        public bool CrossesToll { get; }

        /// <summary>True if an active slot with a bonus contributes to some chain.</summary>
        public bool UsesBonus { get; }

        public SolvedArrangement(
            IReadOnlyDictionary<string, string> pilotBySlotId,
            IReadOnlyDictionary<string, float> chainValues,
            float totalAttack,
            float totalDefense,
            bool crossesToll,
            bool usesBonus)
        {
            PilotBySlotId = pilotBySlotId;
            ChainValues = chainValues;
            TotalAttack = totalAttack;
            TotalDefense = totalDefense;
            CrossesToll = crossesToll;
            UsesBonus = usesBonus;
        }
    }

    public sealed class SolveResult
    {
        public long ArrangementsEvaluated { get; }

        /// <summary>Arrangements that no other arrangement beats in both total attack and total defense, best attack first.</summary>
        public IReadOnlyList<SolvedArrangement> ParetoFrontier { get; }

        /// <summary>The highest value each chain reached across all arrangements.</summary>
        public IReadOnlyDictionary<string, float> BestValuePerChain { get; }

        public SolveResult(
            long arrangementsEvaluated,
            IReadOnlyList<SolvedArrangement> paretoFrontier,
            IReadOnlyDictionary<string, float> bestValuePerChain)
        {
            ArrangementsEvaluated = arrangementsEvaluated;
            ParetoFrontier = paretoFrontier;
            BestValuePerChain = bestValuePerChain;
        }

        public float TollCrossingRate => Rate(arrangement => arrangement.CrossesToll);
        public float BonusUsageRate => Rate(arrangement => arrangement.UsesBonus);

        private float Rate(System.Func<SolvedArrangement, bool> predicate)
        {
            return ParetoFrontier.Count == 0 ? 0f : (float)ParetoFrontier.Count(predicate) / ParetoFrontier.Count;
        }

        public string ToReport()
        {
            var report = new StringBuilder();
            report.AppendLine($"Arrangements evaluated: {ArrangementsEvaluated}");
            report.AppendLine("Best value per chain: " + string.Join(", ",
                BestValuePerChain.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value:0.##}")));
            report.AppendLine($"Pareto frontier ({ParetoFrontier.Count} arrangements):");
            foreach (var arrangement in ParetoFrontier)
            {
                var slots = string.Join(", ",
                    arrangement.PilotBySlotId.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
                report.AppendLine(
                    $"  ATK {arrangement.TotalAttack:0.##} / DEF {arrangement.TotalDefense:0.##}" +
                    $"{(arrangement.CrossesToll ? " [toll]" : string.Empty)}{(arrangement.UsesBonus ? " [bonus]" : string.Empty)}" +
                    $"  ({slots})");
            }

            report.AppendLine($"Frontier arrangements crossing a toll: {TollCrossingRate:P0}");
            report.AppendLine($"Frontier arrangements using a bonus: {BonusUsageRate:P0}");
            return report.ToString();
        }
    }
}
