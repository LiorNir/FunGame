using System.Collections.Generic;
using System.Linq;
using FunGame.Board;

namespace FunGame.Battle
{
    /// <summary>
    /// A scripted player that plans a whole turn. It tries every combination of AP unlocks and pilot placements it can
    /// afford, scores each resulting board (total attack plus total defense of all chains) and plays the best one,
    /// preferring the cheaper plan on a tie. Unlike a move-by-move player it can build a path through empty slots to
    /// reach a bonus.
    /// </summary>
    public static class PlannerPlayer
    {
        private const float MinimumImprovement = 0.0001f;

        private sealed class Plan
        {
            public List<string> Unlocks = new List<string>();
            public List<(PilotUnit pilot, string slotId)> Placements = new List<(PilotUnit, string)>();
            public float Score = float.NegativeInfinity;
            public int Cost;
        }

        public static void PlayPlanningPhase(BattleEngine engine)
        {
            foreach (var slotId in engine.PilotBySlot.Where(pair => pair.Value.Fallen).Select(pair => pair.Key).ToList())
            {
                engine.ClearFallen(slotId);
            }

            var plan = FindBestPlan(engine);
            foreach (var slotId in plan.Unlocks)
            {
                engine.TryUnlock(slotId);
            }

            foreach (var (pilot, slotId) in plan.Placements)
            {
                engine.TryPlace(engine.Hand.IndexOf(pilot), slotId);
            }
        }

        private static Plan FindBestPlan(BattleEngine engine)
        {
            var rules = engine.Setup.Rules;
            var baselineBoard = engine.PilotBySlot.ToDictionary(pair => pair.Key, pair => pair.Value);
            var baselineScore = Score(engine, baselineBoard, new HashSet<string>(engine.LockedSlots));
            var best = new Plan { Score = baselineScore, Cost = 0 };

            var unlockable = engine.Setup.SlotRules
                .Where(rule => !engine.IsUnlocked(rule.SlotId) && rule.UnlockApCost > 0 && rule.UnlockApCost <= engine.ActionPoints)
                .ToList();

            // every subset of the unlocks the player can pay for
            for (var mask = 0; mask < (1 << unlockable.Count); mask++)
            {
                var chosen = unlockable.Where((rule, index) => (mask & (1 << index)) != 0).ToList();
                var unlockCost = chosen.Sum(rule => rule.UnlockApCost);
                if (unlockCost > engine.ActionPoints)
                {
                    continue;
                }

                var locked = new HashSet<string>(engine.LockedSlots);
                foreach (var rule in chosen)
                {
                    locked.Remove(rule.SlotId);
                }

                var slots = engine.Setup.Board.Nodes
                    .Select(node => node.Id)
                    .Where(id => !locked.Contains(id))
                    .Where(id => !engine.PilotBySlot.TryGetValue(id, out var occupant) || (!occupant.Fallen && !engine.WasPlacedThisTurn(occupant)))
                    .ToList();

                var current = new List<(PilotUnit pilot, string slotId)>();
                Search(engine, slots, 0, engine.Hand.ToList(), new HashSet<PilotUnit>(), baselineBoard, locked,
                    engine.ActionPoints - unlockCost, unlockCost, chosen.Select(rule => rule.SlotId).ToList(), current, best, rules);
            }

            return best;
        }

        private static void Search(
            BattleEngine engine,
            List<string> slots,
            int slotIndex,
            List<PilotUnit> hand,
            HashSet<PilotUnit> used,
            Dictionary<string, PilotUnit> board,
            HashSet<string> locked,
            int apLeft,
            int costSoFar,
            List<string> unlocks,
            List<(PilotUnit pilot, string slotId)> placements,
            Plan best,
            BattleRules rules)
        {
            if (slotIndex == slots.Count)
            {
                var score = Score(engine, board, locked);
                if (score > best.Score + MinimumImprovement || (score > best.Score - MinimumImprovement && costSoFar < best.Cost))
                {
                    best.Score = score;
                    best.Cost = costSoFar;
                    best.Unlocks = new List<string>(unlocks);
                    best.Placements = new List<(PilotUnit, string)>(placements);
                }

                return;
            }

            var slotId = slots[slotIndex];

            // leave the slot as it is
            Search(engine, slots, slotIndex + 1, hand, used, board, locked, apLeft, costSoFar, unlocks, placements, best, rules);

            board.TryGetValue(slotId, out var occupant);
            if (occupant != null && !rules.AllowReplace)
            {
                return;
            }

            var cost = occupant == null ? rules.PlacePilotCost : rules.ReplacePilotCost;
            if (cost > apLeft)
            {
                return;
            }

            foreach (var pilot in hand)
            {
                if (!used.Add(pilot))
                {
                    continue;
                }

                board[slotId] = pilot;
                placements.Add((pilot, slotId));
                Search(engine, slots, slotIndex + 1, hand, used, board, locked, apLeft - cost, costSoFar + cost, unlocks, placements, best, rules);
                placements.RemoveAt(placements.Count - 1);
                if (occupant == null)
                {
                    board.Remove(slotId);
                }
                else
                {
                    board[slotId] = occupant;
                }

                used.Remove(pilot);
            }
        }

        private static float Score(BattleEngine engine, Dictionary<string, PilotUnit> board, HashSet<string> locked)
        {
            SlotContent ContentOf(string slotId)
            {
                if (locked.Contains(slotId) || !board.TryGetValue(slotId, out var pilot) || pilot.Fallen)
                {
                    return SlotContent.Empty;
                }

                return new SlotContent(pilot.EffectiveAttack, pilot.EffectiveDefense);
            }

            return ChainEvaluator.EvaluateAll(engine.Setup.Board, ContentOf).Values.Sum(result => result.Value);
        }
    }
}
