using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FunGame.Battle;

namespace FunGame.Sim
{
    /// <summary>Something that plays the planning phase of a battle.</summary>
    public interface IPlayer
    {
        string Name { get; }

        /// <summary>Gives the engine the commands for this turn. The caller resolves the turn afterwards.</summary>
        void PlayPlanningPhase(BattleEngine engine);
    }

    /// <summary>How a <see cref="SearchPlayer"/> plays: how many options it keeps in mind and what it pays attention to.</summary>
    public sealed class BotProfile
    {
        public string Name { get; }

        /// <summary>How many partial plans are kept after each step. Fewer means a player that misses good combinations.</summary>
        public int BeamWidth { get; }

        public bool AllowEject { get; }
        public ScoreWeights Weights { get; }

        public BotProfile(string name, int beamWidth, bool allowEject, ScoreWeights weights)
        {
            Name = name;
            BeamWidth = beamWidth;
            AllowEject = allowEject;
            Weights = weights;
        }

        /// <summary>Sees only this turn, keeps a few options, never ejects. A relaxed first-time player.</summary>
        public static BotProfile Casual => new BotProfile("Casual", 4, false, ScoreWeights.Short);

        /// <summary>Plans for the next turn too, ejects when it pays, keeps many options (48: close to trying everything in every spot checked).</summary>
        public static BotProfile Careful => new BotProfile("Careful", 48, true, ScoreWeights.Full);

        /// <summary>Like Careful, but happy to lose pilots for damage: sacrifices what a careful player protects.</summary>
        public static BotProfile Gambler => new BotProfile("Gambler", 48, true, new ScoreWeights { PilotLost = 6f, Wound = 1f });

        /// <summary>Tries everything. Slow: for checking the others, not for batches.</summary>
        public static BotProfile Reference => new BotProfile("Reference", int.MaxValue, true, ScoreWeights.Full);
    }

    /// <summary>What a player decided for one turn, for reports.</summary>
    public sealed class PlannedTurn
    {
        public int Turn { get; set; }
        public IReadOnlyList<string> Actions { get; set; } = new string[0];

        /// <summary>The commands behind <see cref="Actions"/>, in the order they were given.</summary>
        public IReadOnlyList<BattleCommand> Commands { get; set; } = new BattleCommand[0];

        public Prediction Prediction { get; set; }

        /// <summary>The same turn if the player did nothing, to see what the plan gained.</summary>
        public Prediction DoingNothing { get; set; }

        public override string ToString()
        {
            return Actions.Count == 0
                ? $"turn {Turn}: no action; {Prediction}"
                : $"turn {Turn}: {string.Join("; ", Actions)} -> {Prediction}";
        }
    }

    /// <summary>
    /// A player that reads the telegraphed enemy actions. For every plan it considers, it plays the turn out on a copy of
    /// the battle (its attack and the enemy's revealed attack are known) and judges the result. Plans are built one command
    /// at a time and only the best few are kept (a beam search), which is how many options the player holds in mind.
    /// It never looks at anything random that has not happened yet.
    /// </summary>
    public sealed class SearchPlayer : IPlayer
    {
        private const float Tolerance = 0.001f;
        private const int LookAheadSeed = 7919;

        private sealed class Node
        {
            public BattleEngine State;
            public readonly List<BattleCommand> Commands = new List<BattleCommand>();
            public Prediction Prediction;
            public int ApSpent;
        }

        private readonly BotProfile _profile;

        public string Name => _profile.Name;

        /// <summary>The decision made in the latest call, for reports.</summary>
        public PlannedTurn LastPlan { get; private set; }

        public SearchPlayer(BotProfile profile)
        {
            _profile = profile;
        }

        public void PlayPlanningPhase(BattleEngine engine)
        {
            foreach (var slotId in engine.PilotBySlot.Where(pair => pair.Value.Fallen).Select(pair => pair.Key).ToList())
            {
                engine.ClearFallen(slotId);
            }

            var best = Search(engine, out var doingNothing);

            // describe the chosen plan by playing it out once more on a copy (only the winner is worth the text)
            var describing = engine.Clone(LookAheadSeed);
            var actions = new List<string>();
            foreach (var command in best.Commands)
            {
                actions.Add(command.Describe(describing));
                BattleCommands.Apply(describing, command);
            }

            foreach (var command in best.Commands)
            {
                BattleCommands.Apply(engine, command);
            }

            LastPlan = new PlannedTurn { Turn = engine.Turn, Actions = actions, Commands = best.Commands.ToList(), Prediction = best.Prediction, DoingNothing = doingNothing };
        }

        private Node Search(BattleEngine real, out Prediction doingNothing)
        {
            var baseline = real.Stats;
            var root = new Node { State = real.Clone(LookAheadSeed) };
            Evaluate(root, baseline);
            doingNothing = root.Prediction;

            var best = root;
            var beam = new List<Node> { root };
            var legal = new List<BattleCommand>();
            while (beam.Count > 0)
            {
                var candidates = new List<Node>();
                var seen = new HashSet<string>();
                foreach (var parent in beam)
                {
                    legal.Clear();
                    BattleCommands.Legal(parent.State, legal, _profile.AllowEject);
                    foreach (var command in legal)
                    {
                        var child = new Node { State = parent.State.Clone(LookAheadSeed) };
                        var apBefore = child.State.ActionPoints;
                        if (!BattleCommands.Apply(child.State, command) || !seen.Add(Key(child.State)))
                        {
                            continue;
                        }

                        child.Commands.AddRange(parent.Commands);
                        child.Commands.Add(command);
                        child.ApSpent = parent.ApSpent + (apBefore - child.State.ActionPoints);
                        Evaluate(child, baseline);
                        candidates.Add(child);
                    }
                }

                candidates.Sort((a, b) => b.Prediction.Score.CompareTo(a.Prediction.Score));
                foreach (var candidate in candidates)
                {
                    if (candidate.Prediction.Score > best.Prediction.Score + Tolerance ||
                        (candidate.Prediction.Score > best.Prediction.Score - Tolerance && candidate.ApSpent < best.ApSpent))
                    {
                        best = candidate;
                    }
                }

                beam = candidates.Take(_profile.BeamWidth).ToList();
            }

            return best;
        }

        private void Evaluate(Node node, BattleStats baseline)
        {
            var afterCombat = node.State.Clone(LookAheadSeed);
            afterCombat.ResolveCombat();
            node.Prediction = PlanScorer.Score(afterCombat, baseline, _profile.Weights, node.ApSpent);
        }

        /// <summary>Two plans that leave the same board, locks and AP are the same plan, whatever order they were built in.</summary>
        private static string Key(BattleEngine engine)
        {
            var builder = new StringBuilder();
            builder.Append(engine.ActionPoints).Append('|');
            foreach (var pair in engine.PilotBySlot.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var pilot = pair.Value;
                builder.Append(pair.Key).Append('=').Append(pilot.Name).Append(':').Append(pilot.DefenseWound).Append(pilot.Fallen ? "x" : string.Empty).Append(';');
            }

            builder.Append('|');
            foreach (var slotId in engine.LockedSlots.OrderBy(id => id, StringComparer.Ordinal))
            {
                builder.Append(slotId).Append(',');
            }

            return builder.ToString();
        }
    }

    /// <summary>The scripted player from before, which maximises the sum of all chains and does not read intents.</summary>
    public sealed class GreedyPlayer : IPlayer
    {
        public string Name => "Greedy";

        public void PlayPlanningPhase(BattleEngine engine)
        {
            PlannerPlayer.PlayPlanningPhase(engine);
        }
    }
}
