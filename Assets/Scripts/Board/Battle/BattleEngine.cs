using System;
using System.Collections.Generic;
using System.Linq;
using FunGame.Board;

namespace FunGame.Battle
{
    /// <summary>
    /// Runs one battle. The caller plays the planning phase with <see cref="TryPlace"/>, <see cref="TryEject"/>,
    /// <see cref="TryUnlock"/> and <see cref="ClearFallen"/>, then calls <see cref="ResolveTurn"/>. Resolution order:
    /// the player attacks every lane, then every surviving enemy attacks (lanes in order), then the next turn starts.
    /// All values are exact (nothing is rounded); see <see cref="DisplayFormat"/> for how they are shown.
    /// </summary>
    public sealed class BattleEngine
    {
        private const float Epsilon = 0.0001f;

        private readonly BattleRandom _random;
        private readonly Dictionary<string, SlotRule> _slotRules;
        private readonly Dictionary<string, PilotUnit> _pilotBySlot = new Dictionary<string, PilotUnit>();
        private readonly HashSet<string> _lockedSlots = new HashSet<string>();
        private readonly HashSet<PilotUnit> _placedThisTurn = new HashSet<PilotUnit>();
        private readonly List<BattleEvent> _events = new List<BattleEvent>();

        public BattleSetup Setup { get; }
        public int Turn { get; private set; }
        public int ActionPoints { get; private set; }
        public float PlayerHealth { get; private set; }
        public BattleOutcome Outcome { get; private set; }
        public List<PilotUnit> Hand { get; } = new List<PilotUnit>();
        public List<PilotUnit> Bank { get; } = new List<PilotUnit>();
        public IReadOnlyList<LaneState> Lanes { get; }
        public IReadOnlyDictionary<string, PilotUnit> PilotBySlot => _pilotBySlot;
        public IReadOnlyCollection<string> LockedSlots => _lockedSlots;
        public List<string> Log { get; } = new List<string>();

        /// <summary>What happened so far, counted by the engine. Copied along when the battle is cloned.</summary>
        public BattleStats Stats { get; private set; } = new BattleStats();

        /// <summary>Whether the text log is written. Switched off in the copies a player uses to look ahead, for speed.</summary>
        public bool Logging { get; set; } = true;

        /// <summary>Whether events are recorded for <see cref="DrainEvents"/>. Switched off in look-ahead copies, for speed.</summary>
        public bool RecordEvents { get; set; } = true;

        /// <summary>True if the pilot was placed during the current planning phase.</summary>
        public bool WasPlacedThisTurn(PilotUnit pilot)
        {
            return _placedThisTurn.Contains(pilot);
        }

        /// <summary>The highest enemy number (1 = first) currently or most recently in any lane.</summary>
        public int Wave => Lanes.Count == 0 ? 0 : Lanes.Max(lane => lane.EnemiesSpawned);

        public BattleEngine(BattleSetup setup) : this(setup, new BattleRandom(setup.Seed))
        {
        }

        private BattleEngine(BattleSetup setup, BattleRandom random)
        {
            Setup = setup;
            _random = random;
            _slotRules = setup.SlotRules.ToDictionary(rule => rule.SlotId);
            Lanes = setup.Lanes.Select(lane => new LaneState(lane)).ToList();
        }

        /// <summary>
        /// A separate copy of the battle in its current state, for trying things out. The copy has no log and records no
        /// events. With <paramref name="newRandomSeed"/> its dice are a fresh stream (use this when looking ahead, so the
        /// real future is not known); without it the dice continue exactly as the original's would.
        /// </summary>
        public BattleEngine Clone(int? newRandomSeed = null)
        {
            var clone = new BattleEngine(Setup, newRandomSeed.HasValue ? new BattleRandom(newRandomSeed.Value) : _random.Clone())
            {
                Turn = Turn,
                ActionPoints = ActionPoints,
                PlayerHealth = PlayerHealth,
                Outcome = Outcome,
                Logging = false,
                RecordEvents = false
            };

            var copies = new Dictionary<PilotUnit, PilotUnit>();
            PilotUnit CopyOf(PilotUnit pilot)
            {
                if (!copies.TryGetValue(pilot, out var copy))
                {
                    copy = pilot.Copy();
                    copies[pilot] = copy;
                }

                return copy;
            }

            foreach (var pair in _pilotBySlot)
            {
                clone._pilotBySlot[pair.Key] = CopyOf(pair.Value);
            }

            foreach (var pilot in Hand)
            {
                clone.Hand.Add(CopyOf(pilot));
            }

            foreach (var pilot in Bank)
            {
                clone.Bank.Add(CopyOf(pilot));
            }

            foreach (var pilot in _placedThisTurn)
            {
                clone._placedThisTurn.Add(CopyOf(pilot));
            }

            foreach (var slotId in _lockedSlots)
            {
                clone._lockedSlots.Add(slotId);
            }

            for (var index = 0; index < Lanes.Count; index++)
            {
                var lane = Lanes[index];
                var copy = clone.Lanes[index];
                copy.Current = lane.Current?.Copy();
                copy.EnemiesSpawned = lane.EnemiesSpawned;
            }

            clone.Stats = Stats.Copy();
            return clone;
        }

        /// <summary>Returns the events that happened since the last call, in order, and forgets them.</summary>
        public List<BattleEvent> DrainEvents()
        {
            var drained = new List<BattleEvent>(_events);
            _events.Clear();
            return drained;
        }

        /// <summary>True if anyone reads events or the log. Look-ahead copies switch both off and skip building them.</summary>
        private bool Observed => RecordEvents || Logging;

        /// <summary>Records an event and writes its log line. Every line of the log comes from an event (see <see cref="BattleLogWriter"/>).</summary>
        private void Emit(
            BattleEventType type,
            string laneId = null,
            string subject = null,
            float a = 0f,
            float b = 0f,
            float c = 0f,
            IReadOnlyList<string> slots = null,
            IReadOnlyList<float> slotValues = null,
            string detail = null)
        {
            if (!Observed)
            {
                return;
            }

            var battleEvent = new BattleEvent(type, laneId, subject, a, b, c, slots, slotValues, detail);
            if (RecordEvents)
            {
                _events.Add(battleEvent);
            }

            if (Logging)
            {
                BattleLogWriter.Write(battleEvent, this, Log);
            }
        }

        public void Start()
        {
            var rules = Setup.Rules;
            PlayerHealth = rules.PlayerMaxHealth;
            ActionPoints = rules.StartingActionPoints;
            Turn = 1;
            Outcome = BattleOutcome.InProgress;

            foreach (var rule in Setup.SlotRules.Where(rule => rule.Locked))
            {
                _lockedSlots.Add(rule.SlotId);
            }

            var deck = Setup.Deck.Select((stats, index) => new PilotUnit(stats.Name, stats.Attack, stats.Defense, stats.Source) { Id = index }).ToList();
            Shuffle(deck);
            Bank.AddRange(deck);

            Emit(BattleEventType.TurnStarted, a: Turn);
            foreach (var lane in Lanes)
            {
                SpawnNextEnemy(lane);
            }

            ApplyUnlocks();
            DrawToHandSize();
            Emit(BattleEventType.PlanningStarted, a: PlayerHealth, b: ActionPoints);
        }

        // ---------- planning phase ----------

        public bool IsUnlocked(string slotId)
        {
            return !_lockedSlots.Contains(slotId);
        }

        /// <summary>The AP a placement into the slot would cost: place if empty, replace if occupied by a living pilot.</summary>
        public int PlacementCost(string slotId)
        {
            return _pilotBySlot.TryGetValue(slotId, out var occupant) && !occupant.Fallen ? Setup.Rules.ReplacePilotCost : Setup.Rules.PlacePilotCost;
        }

        /// <summary>
        /// True if the hand pilot can be placed in the slot right now: an empty slot, a fallen pilot's slot (placing clears
        /// the fallen pilot, at the normal place cost, even where replacing is not allowed) or a living pilot's slot when
        /// replacing is allowed and the pilot is not locked.
        /// </summary>
        public bool CanPlace(int handIndex, string slotId)
        {
            if (Outcome != BattleOutcome.InProgress || handIndex < 0 || handIndex >= Hand.Count || !IsUnlocked(slotId))
            {
                return false;
            }

            _pilotBySlot.TryGetValue(slotId, out var occupant);
            return (occupant == null || occupant.Fallen || (!occupant.IsLocked && Setup.Rules.AllowReplace)) && PlacementCost(slotId) <= ActionPoints;
        }

        public bool TryPlace(int handIndex, string slotId)
        {
            if (!CanPlace(handIndex, slotId))
            {
                return false;
            }

            _pilotBySlot.TryGetValue(slotId, out var occupant);
            var cost = PlacementCost(slotId);
            var pilot = Hand[handIndex];
            Hand.RemoveAt(handIndex);
            if (occupant != null && occupant.Fallen)
            {
                // a fallen pilot is gone from the battle: placing on it only clears it away
                _pilotBySlot.Remove(slotId);
                Emit(BattleEventType.FallenCleared, subject: occupant.Name, slots: new[] { slotId });
                occupant = null;
            }
            else if (occupant != null)
            {
                Hand.Add(occupant);
            }

            _pilotBySlot[slotId] = pilot;
            _placedThisTurn.Add(pilot);
            ActionPoints -= cost;
            Stats.ApSpent += cost;
            if (occupant == null)
            {
                Stats.Places++;
            }
            else
            {
                Stats.Replaces++;
            }

            Emit(BattleEventType.PilotPlaced, subject: pilot.Name, a: cost, b: ActionPoints, slots: new[] { slotId }, detail: occupant?.Name);
            return true;
        }

        public bool CanUnlock(string slotId)
        {
            return Outcome == BattleOutcome.InProgress && !IsUnlocked(slotId) && _slotRules.TryGetValue(slotId, out var rule) &&
                   rule.UnlockApCost > 0 && rule.UnlockApCost <= ActionPoints;
        }

        public bool TryUnlock(string slotId)
        {
            if (!CanUnlock(slotId))
            {
                return false;
            }

            var cost = _slotRules[slotId].UnlockApCost;
            ActionPoints -= cost;
            Stats.ApSpent += cost;
            Stats.Unlocks++;
            _lockedSlots.Remove(slotId);
            Emit(BattleEventType.SlotBought, subject: slotId, a: cost, b: ActionPoints);
            return true;
        }

        public bool CanEject(string slotId)
        {
            return EjectBlocker(slotId) == null;
        }

        /// <summary>Why the pilot in the slot cannot be ejected right now, in a few words; null if it can.</summary>
        public string EjectBlocker(string slotId)
        {
            if (Outcome != BattleOutcome.InProgress)
            {
                return "the battle is over";
            }

            if (!Setup.Rules.AllowEject)
            {
                return "not available yet";
            }

            if (!_pilotBySlot.TryGetValue(slotId, out var pilot) || pilot.Fallen)
            {
                return "no living pilot";
            }

            if (pilot.IsLocked)
            {
                return $"locked for {pilot.LockedTurns} turn{(pilot.LockedTurns == 1 ? string.Empty : "s")}";
            }

            if (Setup.Rules.EjectPilotCost > ActionPoints)
            {
                return "not enough AP";
            }

            return Hand.Count < Setup.Rules.HandSize ? null : "hand is full";
        }

        /// <summary>
        /// Takes a living pilot off the board and back into the hand, without placing another. Needs the AP and a free
        /// hand slot. (Fallen pilots are removed for free with <see cref="ClearFallen"/>.)
        /// </summary>
        public bool TryEject(string slotId)
        {
            if (!CanEject(slotId))
            {
                return false;
            }

            var pilot = _pilotBySlot[slotId];
            var cost = Setup.Rules.EjectPilotCost;
            _pilotBySlot.Remove(slotId);
            Hand.Add(pilot);
            ActionPoints -= cost;
            Stats.ApSpent += cost;
            Stats.Ejects++;
            Emit(BattleEventType.PilotEjected, subject: pilot.Name, a: cost, b: ActionPoints, slots: new[] { slotId });
            return true;
        }

        /// <summary>Removes a fallen pilot from its slot. Free.</summary>
        public bool ClearFallen(string slotId)
        {
            if (!_pilotBySlot.TryGetValue(slotId, out var pilot) || !pilot.Fallen)
            {
                return false;
            }

            _pilotBySlot.Remove(slotId);
            Emit(BattleEventType.FallenCleared, subject: pilot.Name, slots: new[] { slotId });
            return true;
        }

        // ---------- evaluation ----------

        public SlotContent ContentOf(string slotId)
        {
            if (_lockedSlots.Contains(slotId) || !_pilotBySlot.TryGetValue(slotId, out var pilot) || pilot.Fallen)
            {
                return SlotContent.Empty;
            }

            return new SlotContent(pilot.EffectiveAttack, pilot.EffectiveDefense);
        }

        public Dictionary<string, ChainResult> EvaluateChains()
        {
            return ChainEvaluator.EvaluateAll(Setup.Board, ContentOf);
        }

        public float SumChains(ChainRole role, string laneId = null)
        {
            return EvaluateChains().Values
                .Where(result => result.Chain.Role == role && (laneId == null || result.Chain.LaneId == laneId))
                .Sum(result => result.Value);
        }

        // ---------- resolution ----------

        /// <summary>Resolves the turn (the player attacks, then the enemies attack) and starts the next one.</summary>
        public void ResolveTurn()
        {
            ResolveCombat();
            if (Outcome == BattleOutcome.InProgress)
            {
                StartNextTurn();
            }
        }

        /// <summary>
        /// Only the fighting of a turn: the player attacks, then the enemies act. The battle is left at the end of the
        /// turn, before the next one starts, so nothing random (new intents, new hand) has happened yet. A player that looks
        /// ahead stops here. Call it once per turn.
        /// </summary>
        public void ResolveCombat()
        {
            if (Outcome != BattleOutcome.InProgress)
            {
                return;
            }

            Stats.ApWasted += ActionPoints;

            // a lock counts the player's turns: the planning phase that just ended was one of them
            foreach (var pilot in _pilotBySlot.Values.Where(pilot => pilot.IsLocked))
            {
                pilot.LockedTurns--;
            }

            Emit(BattleEventType.TurnResolving, a: Turn);

            // the board does not change while the player attacks, so its chains are read once
            var chainResults = EvaluateChains();
            foreach (var lane in Lanes.Where(lane => lane.Current != null))
            {
                PlayerAttacks(lane, chainResults);
            }

            if (Lanes.All(lane => lane.IsCleared))
            {
                Outcome = BattleOutcome.Victory;
                Emit(BattleEventType.Victory);
                return;
            }

            foreach (var lane in Lanes.Where(lane => lane.Current != null))
            {
                if (EnemyActs(lane))
                {
                    return;
                }
            }
        }

        private void PlayerAttacks(LaneState lane, Dictionary<string, ChainResult> chainResults)
        {
            var enemy = lane.Current;
            var results = chainResults.Values.Where(result => result.Chain.LaneId == lane.Spec.Id).ToList();
            foreach (var result in results.Where(result => result.DividedByZero))
            {
                Stats.DivideByZeros++;
                Emit(BattleEventType.DivideByZero, lane.Spec.Id, result.Chain.Id, slots: new[] { result.DivideByZeroNodeId });
            }

            var guard = new EnemyEffectContext(this, lane, enemy.Intent);
            foreach (var effect in enemy.Intent.Effects.Where(effect => effect.Phase == EnemyEffectPhase.Guard))
            {
                effect.Resolve(guard);
            }

            var attack = results.Where(result => result.Chain.Role == ChainRole.Attack).Sum(result => result.Value);
            var defense = guard.Block;
            var damage = Math.Max(0f, attack - defense);
            enemy.Health -= damage;
            Stats.DamageDealt += damage;
            Emit(BattleEventType.PlayerAttacked, lane.Spec.Id, a: attack, b: defense, c: damage);
            if (damage > Epsilon)
            {
                Emit(BattleEventType.EnemyDamaged, lane.Spec.Id, a: Math.Max(0f, enemy.Health), b: enemy.Spec.MaxHealth);
            }

            if (enemy.Health <= Epsilon)
            {
                enemy.Health = 0f;
                Stats.EnemiesDefeated++;
                Emit(BattleEventType.EnemyDefeated, lane.Spec.Id, enemy.Spec.Name);
                lane.Current = null;
            }
        }

        /// <summary>The lane's enemy carries out its revealed action. Returns true if the player was defeated.</summary>
        private bool EnemyActs(LaneState lane)
        {
            var action = lane.Current.Intent;
            Emit(BattleEventType.EnemyActed, lane.Spec.Id, action.Name);
            var context = new EnemyEffectContext(this, lane, action);
            for (var index = 0; index < action.Effects.Count; index++)
            {
                var effect = action.Effects[index];
                if (effect.Phase != EnemyEffectPhase.Act)
                {
                    continue;
                }

                context.Targets = index < lane.Current.IntentTargets.Count ? lane.Current.IntentTargets[index] : new int[0];
                effect.Resolve(context);
                if (PlayerHealth <= Epsilon)
                {
                    PlayerHealth = Math.Max(0f, PlayerHealth);
                    Outcome = BattleOutcome.Defeat;
                    Emit(BattleEventType.Defeat);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// An enemy attack on a lane: the lane's defense chains absorb it in order (as wounds on their pilots, or by being
        /// destroyed), and what they cannot absorb hurts the player. Used by <see cref="AttackEffect"/>.
        /// </summary>
        internal void HitLane(LaneState lane, string actionName, float attack)
        {
            Emit(BattleEventType.EnemyAttacked, lane.Spec.Id, actionName, attack);
            if (attack <= 0f)
            {
                return;
            }

            var remaining = attack;
            foreach (var chain in Setup.Board.Chains.Where(chain => chain.LaneId == lane.Spec.Id && chain.Role == ChainRole.Defense))
            {
                if (remaining <= Epsilon)
                {
                    break;
                }

                var result = ChainEvaluator.Evaluate(Setup.Board, chain, ContentOf);
                var value = result.Value;
                if (value <= Epsilon)
                {
                    // A chain worth nothing cannot absorb any of this attack, so the attack destroys it (its pilots fall) and
                    // passes on in full, like any chain that cannot take the damage. Otherwise a pilot standing in a chain
                    // worth 0 could never be hurt. A chain with no pilots has nothing to lose, and one that is 0 because of a
                    // divide by zero is left alone for now (undecided).
                    if (!result.DividedByZero && result.Steps.Any(step => step.Status == SlotStatus.Active))
                    {
                        DestroyChain(lane, chain, 0f, value);
                    }

                    continue;
                }

                var absorbed = Math.Min(value, remaining);
                Stats.DamageAbsorbed += absorbed;
                // A hit equal to the chain's whole value leaves it standing at 0 (its pilots fully wounded); only a hit
                // above its value destroys it. The next hit that reaches a chain at 0 destroys it (see above).
                List<string> wounded = null;
                var chainSurvives = remaining <= value + Epsilon &&
                                    WoundAllocator.TryApplyDamage(Setup.Board, chain, value, absorbed, ContentOf, _pilotBySlot, out wounded);
                if (chainSurvives)
                {
                    if (Observed)
                    {
                        var after = ChainEvaluator.Evaluate(Setup.Board, chain, ContentOf).Value;
                        Emit(BattleEventType.ChainAbsorbed, lane.Spec.Id, chain.Id, absorbed, value, after,
                            wounded, wounded.Select(slotId => _pilotBySlot[slotId].DefenseWound).ToList());
                    }
                }
                else
                {
                    if (!Setup.Rules.BrokenChainAbsorbs)
                    {
                        // the chain breaks without softening the hit (see BattleRules.BrokenChainAbsorbs)
                        Stats.DamageAbsorbed -= absorbed;
                        absorbed = 0f;
                    }

                    DestroyChain(lane, chain, absorbed, value);
                }

                remaining -= absorbed;
            }

            var damageToPlayer = remaining <= Epsilon ? 0f : remaining;
            PlayerHealth -= damageToPlayer;
            Stats.DamageTaken += damageToPlayer;
            if (damageToPlayer > 0f)
            {
                Emit(BattleEventType.PlayerDamaged, lane.Spec.Id, a: damageToPlayer, b: Math.Max(0f, PlayerHealth));
            }
        }

        /// <summary>
        /// Locks the targeted pilots for the given number of the player's turns (a longer lock already on a pilot is kept).
        /// A target that has left the board, or fallen, is missed. Used by <see cref="LockEffect"/>.
        /// </summary>
        internal void LockPilots(LaneState lane, IReadOnlyList<int> targetIds, int turns, string effectName)
        {
            if (targetIds.Count == 0)
            {
                Emit(BattleEventType.EffectFizzled, lane.Spec.Id, effectName, detail: "there was no pilot to target");
                return;
            }

            foreach (var id in targetIds)
            {
                var slotId = SlotOfPilot(id);
                if (slotId == null)
                {
                    Emit(BattleEventType.EffectFizzled, lane.Spec.Id, effectName, detail: "its target left the board");
                    continue;
                }

                var pilot = _pilotBySlot[slotId];
                pilot.LockedTurns = Math.Max(pilot.LockedTurns, turns);
                Stats.PilotsLocked++;
                Emit(BattleEventType.PilotLocked, lane.Spec.Id, pilot.Name, pilot.LockedTurns, slots: new[] { slotId });
            }
        }

        /// <summary>The slot of a living pilot on the board, or null if the pilot is not there.</summary>
        public string SlotOfPilot(int pilotId)
        {
            foreach (var pair in _pilotBySlot)
            {
                if (pair.Value.Id == pilotId && !pair.Value.Fallen)
                {
                    return pair.Key;
                }
            }

            return null;
        }

        /// <summary>The pilots the revealed enemy intents are aimed at, with the effect and lane, for markers on the board.</summary>
        public IEnumerable<IntentTarget> IntentTargets()
        {
            foreach (var lane in Lanes.Where(lane => lane.Current != null))
            {
                var enemy = lane.Current;
                for (var index = 0; index < enemy.Intent.Effects.Count && index < enemy.IntentTargets.Count; index++)
                {
                    foreach (var id in enemy.IntentTargets[index])
                    {
                        var slotId = SlotOfPilot(id);
                        if (slotId != null)
                        {
                            yield return new IntentTarget(lane.Spec.Id, enemy.Intent.Effects[index], slotId, _pilotBySlot[slotId]);
                        }
                    }
                }
            }
        }

        /// <summary>Slots whose pilot counts in at least one chain right now (placed, living and not cut off by a gap).</summary>
        private List<string> ActiveSlots()
        {
            var active = new HashSet<string>();
            foreach (var result in EvaluateChains().Values)
            {
                foreach (var step in result.Steps.Where(step => step.Status == SlotStatus.Active))
                {
                    active.Add(step.NodeId);
                }
            }

            return active.Where(slotId => _pilotBySlot.ContainsKey(slotId)).OrderBy(slotId => slotId, StringComparer.Ordinal).ToList();
        }

        private IReadOnlyList<int> ChooseTarget(PilotTargetRule rule)
        {
            var candidates = ActiveSlots();
            if (candidates.Count == 0)
            {
                return new int[0];
            }

            string chosen;
            switch (rule)
            {
                case PilotTargetRule.MostWounded:
                    chosen = Best(candidates, pilot => pilot.DefenseWound);
                    break;
                case PilotTargetRule.HighestAttack:
                    chosen = Best(candidates, pilot => pilot.EffectiveAttack);
                    break;
                case PilotTargetRule.HighestDefense:
                    chosen = Best(candidates, pilot => pilot.EffectiveDefense);
                    break;
                case PilotTargetRule.Trunk:
                    chosen = Trunk(candidates);
                    break;
                default:
                    chosen = candidates[_random.Next(candidates.Count)];
                    break;
            }

            return new[] { _pilotBySlot[chosen].Id };
        }

        /// <summary>The active slot in the most chains; ties go to the one with the fewest slots between it and its root.</summary>
        private string Trunk(List<string> slotIds)
        {
            var chainCount = new Dictionary<string, int>();
            var depth = new Dictionary<string, int>();
            foreach (var result in EvaluateChains().Values)
            {
                for (var index = 0; index < result.Steps.Count && result.Steps[index].Status == SlotStatus.Active; index++)
                {
                    var slotId = result.Steps[index].NodeId;
                    chainCount[slotId] = chainCount.TryGetValue(slotId, out var count) ? count + 1 : 1;
                    depth[slotId] = index;
                }
            }

            return slotIds.OrderByDescending(slotId => chainCount.TryGetValue(slotId, out var count) ? count : 0)
                .ThenBy(slotId => depth.TryGetValue(slotId, out var steps) ? steps : int.MaxValue)
                .ThenBy(slotId => slotId, StringComparer.Ordinal)
                .First();
        }

        /// <summary>The slot whose pilot scores highest; ties go to the first slot in id order.</summary>
        private string Best(List<string> slotIds, Func<PilotUnit, float> score)
        {
            var best = slotIds[0];
            foreach (var slotId in slotIds)
            {
                if (score(_pilotBySlot[slotId]) > score(_pilotBySlot[best]) + Epsilon)
                {
                    best = slotId;
                }
            }

            return best;
        }

        private void DestroyChain(LaneState lane, ChainDefinition chain, float absorbed, float value)
        {
            var result = ChainEvaluator.Evaluate(Setup.Board, chain, ContentOf);
            var fallenSlots = new List<string>();
            foreach (var step in result.Steps.Where(step => step.Status == SlotStatus.Active))
            {
                if (_pilotBySlot.TryGetValue(step.NodeId, out var pilot) && !pilot.Fallen)
                {
                    pilot.Fallen = true;
                    pilot.LockedTurns = 0;
                    fallenSlots.Add(step.NodeId);
                }
            }

            Stats.PilotsFallen += fallenSlots.Count;
            Stats.ChainsDestroyed++;

            Emit(BattleEventType.ChainDestroyed, lane.Spec.Id, chain.Id, absorbed, value, slots: fallenSlots);
        }

        private void StartNextTurn()
        {
            Turn++;
            _placedThisTurn.Clear();
            ActionPoints = Setup.Rules.ActionPointsPerTurn;
            Emit(BattleEventType.TurnStarted, a: Turn);

            foreach (var lane in Lanes)
            {
                if (lane.Current == null)
                {
                    if (lane.HasMoreEnemies)
                    {
                        SpawnNextEnemy(lane);
                    }
                }
                else
                {
                    RevealIntent(lane);
                }
            }

            ApplyUnlocks();
            RecoverHand();
            DrawToHandSize();
            Emit(BattleEventType.PlanningStarted, a: PlayerHealth, b: ActionPoints);
        }

        /// <summary>Pilots resting in the hand get back part of their defense (see <see cref="BattleRules.HandRecoveryPerTurn"/>).</summary>
        private void RecoverHand()
        {
            var recovery = Setup.Rules.HandRecoveryPerTurn;
            if (recovery <= 0f)
            {
                return;
            }

            foreach (var pilot in Hand)
            {
                pilot.DefenseWound = Math.Max(0f, pilot.DefenseWound - recovery);
            }
        }

        private void SpawnNextEnemy(LaneState lane)
        {
            var enemy = new EnemyUnit(lane.Spec.Enemies[lane.EnemiesSpawned]);
            lane.EnemiesSpawned++;
            lane.Current = enemy;
            Emit(BattleEventType.EnemySpawned, lane.Spec.Id, enemy.Spec.Name, enemy.Health, enemy.Spec.MaxHealth, lane.EnemiesSpawned);
            RevealIntent(lane);
        }

        private void RevealIntent(LaneState lane)
        {
            var enemy = lane.Current;
            var actions = enemy.Spec.Actions;
            if (enemy.Spec.RandomSelection)
            {
                enemy.Intent = actions[_random.Next(actions.Count)];
            }
            else
            {
                enemy.Intent = actions[enemy.NextSequentialAction % actions.Count];
                enemy.NextSequentialAction++;
            }

            // targets are chosen now, so the player sees them while planning (see ITargetedEffect)
            var targets = new List<IReadOnlyList<int>>();
            List<string> targetSlots = null;
            foreach (var effect in enemy.Intent.Effects)
            {
                var chosen = effect is ITargetedEffect targeted ? ChooseTarget(targeted.TargetRule) : new int[0];
                targets.Add(chosen);
                foreach (var id in chosen)
                {
                    (targetSlots = targetSlots ?? new List<string>()).Add(SlotOfPilot(id));
                }
            }

            enemy.IntentTargets = targets;
            Emit(BattleEventType.IntentRevealed, lane.Spec.Id, enemy.Intent.Name, enemy.Intent.Attack, enemy.Intent.Defense, slots: targetSlots);
        }

        private void ApplyUnlocks()
        {
            foreach (var rule in Setup.SlotRules)
            {
                if (!_lockedSlots.Contains(rule.SlotId))
                {
                    continue;
                }

                var byTurn = rule.UnlockAtTurn > 0 && Turn >= rule.UnlockAtTurn;
                var byWave = rule.UnlockAtWave > 0 && WaveOfLane(rule.UnlockLaneId) >= rule.UnlockAtWave;
                if (byTurn || byWave)
                {
                    _lockedSlots.Remove(rule.SlotId);
                    Emit(BattleEventType.SlotUnlocked, subject: rule.SlotId,
                        detail: byTurn ? $"turn {Turn}" : $"wave {rule.UnlockAtWave} of lane {rule.UnlockLaneId}");
                }
            }
        }

        /// <summary>The number of the enemy (1 = first) that has appeared in a lane. An empty id means the board's only lane.</summary>
        private int WaveOfLane(string laneId)
        {
            var lane = string.IsNullOrEmpty(laneId)
                ? (Lanes.Count == 1 ? Lanes[0] : null)
                : Lanes.FirstOrDefault(candidate => candidate.Spec.Id == laneId);
            return lane != null ? lane.EnemiesSpawned : Wave;
        }

        private void DrawToHandSize()
        {
            while (Hand.Count < Setup.Rules.HandSize && Bank.Count > 0)
            {
                Hand.Add(Bank[Bank.Count - 1]);
                Bank.RemoveAt(Bank.Count - 1);
            }
        }

        private void Shuffle<T>(List<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                var temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        public void MarkStalemate()
        {
            if (Outcome == BattleOutcome.InProgress)
            {
                Outcome = BattleOutcome.Stalemate;
                Emit(BattleEventType.Stalemate);
            }
        }
    }
}
