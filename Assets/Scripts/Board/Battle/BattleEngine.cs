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

            clone.Stats = new BattleStats
            {
                Places = Stats.Places, Replaces = Stats.Replaces, Ejects = Stats.Ejects, Unlocks = Stats.Unlocks,
                ApSpent = Stats.ApSpent, ApWasted = Stats.ApWasted, DamageDealt = Stats.DamageDealt,
                DamageTaken = Stats.DamageTaken, DamageAbsorbed = Stats.DamageAbsorbed, PilotsFallen = Stats.PilotsFallen,
                ChainsDestroyed = Stats.ChainsDestroyed, EnemiesDefeated = Stats.EnemiesDefeated, DivideByZeros = Stats.DivideByZeros
            };
            return clone;
        }

        /// <summary>Returns the events that happened since the last call, in order, and forgets them.</summary>
        public List<BattleEvent> DrainEvents()
        {
            var drained = new List<BattleEvent>(_events);
            _events.Clear();
            return drained;
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

            var deck = Setup.Deck.Select(stats => new PilotUnit(stats.Name, stats.Attack, stats.Defense, stats.Source)).ToList();
            Shuffle(deck);
            Bank.AddRange(deck);

            if (RecordEvents)
            {
                _events.Add(new BattleEvent(BattleEventType.TurnStarted, a: Turn));
            }
            foreach (var lane in Lanes)
            {
                SpawnNextEnemy(lane);
            }

            ApplyUnlocks();
            DrawToHandSize();
            if (Logging)
            {
                Log.Add($"Battle start. HP {Fmt(PlayerHealth)}, AP {ActionPoints}, hand: {string.Join(", ", Hand)}");
            }
            LogIntents();
        }

        // ---------- planning phase ----------

        public bool IsUnlocked(string slotId)
        {
            return !_lockedSlots.Contains(slotId);
        }

        /// <summary>The AP a placement into the slot would cost: place if empty, replace if occupied by a living pilot.</summary>
        public int PlacementCost(string slotId)
        {
            return _pilotBySlot.ContainsKey(slotId) ? Setup.Rules.ReplacePilotCost : Setup.Rules.PlacePilotCost;
        }

        /// <summary>True if the hand pilot can be placed in the slot right now.</summary>
        public bool CanPlace(int handIndex, string slotId)
        {
            if (Outcome != BattleOutcome.InProgress || handIndex < 0 || handIndex >= Hand.Count || !IsUnlocked(slotId))
            {
                return false;
            }

            _pilotBySlot.TryGetValue(slotId, out var occupant);
            return (occupant == null || (!occupant.Fallen && Setup.Rules.AllowReplace)) && PlacementCost(slotId) <= ActionPoints;
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
            if (occupant != null)
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

            if (Logging)
            {
                Log.Add($"  Place {pilot.Name} in {slotId}{(occupant == null ? string.Empty : $" (replacing {occupant.Name})")} [-{cost} AP, {ActionPoints} left]");
            }
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
            if (Logging)
            {
                Log.Add($"  Unlock {slotId} [-{cost} AP, {ActionPoints} left]");
            }
            return true;
        }

        public bool CanEject(string slotId)
        {
            return Outcome == BattleOutcome.InProgress && Setup.Rules.AllowEject && _pilotBySlot.TryGetValue(slotId, out var pilot) && !pilot.Fallen &&
                   Setup.Rules.EjectPilotCost <= ActionPoints && Hand.Count < Setup.Rules.HandSize;
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
            if (Logging)
            {
                Log.Add($"  Eject {pilot.Name} from {slotId} [-{cost} AP, {ActionPoints} left]");
            }
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
            if (Logging)
            {
                Log.Add($"  Cleared fallen {pilot.Name} from {slotId}");
            }
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
        /// Only the fighting of a turn: the player attacks, then the enemies attack. The battle is left at the end of the
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
            if (Logging)
            {
                Log.Add($"--- Turn {Turn} resolves ---");
                LogBoard();
            }

            // the board does not change while the player attacks, so its chains are read once
            var chainResults = EvaluateChains();
            foreach (var lane in Lanes.Where(lane => lane.Current != null))
            {
                PlayerAttacks(lane, chainResults);
            }

            if (Lanes.All(lane => lane.IsCleared))
            {
                Outcome = BattleOutcome.Victory;
                if (RecordEvents)
                {
                    _events.Add(new BattleEvent(BattleEventType.Victory));
                }
                if (Logging)
                {
                    Log.Add("VICTORY");
                }
                return;
            }

            foreach (var lane in Lanes.Where(lane => lane.Current != null))
            {
                EnemyAttacks(lane);
                if (PlayerHealth <= Epsilon)
                {
                    PlayerHealth = Math.Max(0f, PlayerHealth);
                    Outcome = BattleOutcome.Defeat;
                    if (RecordEvents)
                    {
                        _events.Add(new BattleEvent(BattleEventType.Defeat));
                    }
                    if (Logging)
                    {
                        Log.Add("DEFEAT");
                    }
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
                if (RecordEvents)
                {
                    _events.Add(new BattleEvent(BattleEventType.DivideByZero, lane.Spec.Id, result.Chain.Id, slots: new[] { result.DivideByZeroNodeId }));
                }
                if (Logging)
                {
                    Log.Add($"  !! DIVIDE BY ZERO in chain {result.Chain.Id} at {result.DivideByZeroNodeId}: the chain collapses to 0");
                }
            }

            var attack = results.Where(result => result.Chain.Role == ChainRole.Attack).Sum(result => result.Value);
            var defense = (float)enemy.Intent.Defense;
            var damage = Math.Max(0f, attack - defense);
            enemy.Health -= damage;
            Stats.DamageDealt += damage;
            // No attack, no event: the views have nothing to show when the player has no attack value.
            if (attack > Epsilon)
            {
                if (RecordEvents)
                {
                    _events.Add(new BattleEvent(BattleEventType.PlayerAttacked, lane.Spec.Id, a: attack, b: defense, c: damage));
                }
            }

            if (Logging)
            {
                Log.Add($"  [{lane.Spec.Id}] player attacks {enemy.Spec.Name}: {Fmt(attack)} vs defense {Fmt(defense)} -> {Fmt(damage)} damage, enemy HP {Fmt(Math.Max(0f, enemy.Health))}/{enemy.Spec.MaxHealth}");
            }
            if (damage > Epsilon)
            {
                if (RecordEvents)
                {
                    _events.Add(new BattleEvent(BattleEventType.EnemyDamaged, lane.Spec.Id, a: Math.Max(0f, enemy.Health), b: enemy.Spec.MaxHealth));
                }
            }

            if (enemy.Health <= Epsilon)
            {
                enemy.Health = 0f;
                Stats.EnemiesDefeated++;
                if (RecordEvents)
                {
                    _events.Add(new BattleEvent(BattleEventType.EnemyDefeated, lane.Spec.Id, enemy.Spec.Name));
                }
                if (Logging)
                {
                    Log.Add($"  [{lane.Spec.Id}] {enemy.Spec.Name} is defeated");
                }
                lane.Current = null;
            }
        }

        private void EnemyAttacks(LaneState lane)
        {
            var enemy = lane.Current;
            var attack = enemy.Intent.Attack;
            if (RecordEvents)
            {
                _events.Add(new BattleEvent(BattleEventType.EnemyAttacked, lane.Spec.Id, enemy.Intent.Name, attack));
            }
            if (attack <= 0)
            {
                if (Logging)
                {
                    Log.Add($"  [{lane.Spec.Id}] {enemy.Spec.Name} uses {enemy.Intent.Name} (no attack)");
                }
                return;
            }

            var remaining = (float)attack;
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
                List<string> wounded = null;
                var chainSurvives = absorbed < value - Epsilon &&
                                    WoundAllocator.TryApplyDamage(Setup.Board, chain, value, absorbed, ContentOf, _pilotBySlot, out wounded);
                if (chainSurvives)
                {
                    if (RecordEvents || Logging)
                    {
                        var after = ChainEvaluator.Evaluate(Setup.Board, chain, ContentOf).Value;
                        if (RecordEvents)
                        {
                            _events.Add(new BattleEvent(BattleEventType.ChainAbsorbed, lane.Spec.Id, chain.Id, absorbed, value, after,
                                wounded, wounded.Select(slotId => _pilotBySlot[slotId].DefenseWound).ToList()));
                        }

                        if (Logging)
                        {
                            Log.Add($"  [{lane.Spec.Id}] chain {chain.Id} absorbs {Fmt(absorbed)} of {Fmt(value)} (now {Fmt(after)}); pilots wounded");
                        }
                    }
                }
                else
                {
                    DestroyChain(lane, chain, absorbed, value);
                }

                remaining -= absorbed;
            }

            var damageToPlayer = remaining <= Epsilon ? 0f : remaining;
            PlayerHealth -= damageToPlayer;
            Stats.DamageTaken += damageToPlayer;
            if (damageToPlayer > 0f)
            {
                if (RecordEvents)
                {
                    _events.Add(new BattleEvent(BattleEventType.PlayerDamaged, lane.Spec.Id, a: damageToPlayer, b: Math.Max(0f, PlayerHealth)));
                }
            }

            if (Logging)
            {
                Log.Add($"  [{lane.Spec.Id}] {enemy.Spec.Name} attacks for {attack} ({enemy.Intent.Name}): {Fmt(damageToPlayer)} reaches the player, HP {Fmt(Math.Max(0f, PlayerHealth))}/{Setup.Rules.PlayerMaxHealth}");
            }
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
                    fallenSlots.Add(step.NodeId);
                }
            }

            Stats.PilotsFallen += fallenSlots.Count;
            Stats.ChainsDestroyed++;

            if (RecordEvents)
            {
                _events.Add(new BattleEvent(BattleEventType.ChainDestroyed, lane.Spec.Id, chain.Id, absorbed, value, slots: fallenSlots));
            }
            if (Logging)
            {
                Log.Add($"  chain {chain.Id} absorbs {Fmt(absorbed)} of {Fmt(value)} and is destroyed; fallen: {string.Join(", ", fallenSlots.Select(slot => $"{_pilotBySlot[slot].Name}@{slot}"))}");
            }
        }

        private void StartNextTurn()
        {
            Turn++;
            _placedThisTurn.Clear();
            ActionPoints = Setup.Rules.ActionPointsPerTurn;
            if (RecordEvents)
            {
                _events.Add(new BattleEvent(BattleEventType.TurnStarted, a: Turn));
            }
            if (Logging)
            {
                Log.Add($"--- Turn {Turn} begins ---");
            }

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
            DrawToHandSize();
            if (Logging)
            {
                Log.Add($"  HP {Fmt(PlayerHealth)}, AP {ActionPoints}, hand: {string.Join(", ", Hand)}");
            }
            LogIntents();
        }

        private void SpawnNextEnemy(LaneState lane)
        {
            var enemy = new EnemyUnit(lane.Spec.Enemies[lane.EnemiesSpawned]);
            lane.EnemiesSpawned++;
            lane.Current = enemy;
            if (RecordEvents)
            {
                _events.Add(new BattleEvent(BattleEventType.EnemySpawned, lane.Spec.Id, enemy.Spec.Name, enemy.Health, enemy.Spec.MaxHealth, lane.EnemiesSpawned));
            }
            RevealIntent(lane);
            if (Logging)
            {
                Log.Add($"  [{lane.Spec.Id}] enemy {lane.EnemiesSpawned} appears: {enemy.Spec.Name} HP {Fmt(enemy.Health)}");
            }
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

            if (RecordEvents)
            {
                _events.Add(new BattleEvent(BattleEventType.IntentRevealed, lane.Spec.Id, enemy.Intent.Name, enemy.Intent.Attack, enemy.Intent.Defense));
            }
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
                    if (RecordEvents)
                    {
                        _events.Add(new BattleEvent(BattleEventType.SlotUnlocked, subject: rule.SlotId));
                    }
                    if (Logging)
                    {
                        Log.Add($"  Slot {rule.SlotId} unlocks ({(byTurn ? $"turn {Turn}" : $"wave {rule.UnlockAtWave} of lane {rule.UnlockLaneId}")})");
                    }
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
                if (Logging)
                {
                    Log.Add("STALEMATE (turn limit reached)");
                }
            }
        }

        private void LogIntents()
        {
            foreach (var lane in Lanes.Where(lane => lane.Current != null))
            {
                if (Logging)
                {
                    Log.Add($"  [{lane.Spec.Id}] {lane.Current.Spec.Name} HP {Fmt(lane.Current.Health)}/{lane.Current.Spec.MaxHealth} intends: {lane.Current.Intent}");
                }
            }
        }

        private void LogBoard()
        {
            var results = EvaluateChains().Values.OrderBy(result => result.Chain.Id)
                .Select(result => $"{result.Chain.Id}={Fmt(result.Value)}");
            var placed = _pilotBySlot.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}");
            if (Logging)
            {
                Log.Add($"  board: {string.Join(" ", placed)} | chains: {string.Join(", ", results)}");
            }
        }

        private static string Fmt(float value)
        {
            return DisplayFormat.Value(value);
        }
    }
}
