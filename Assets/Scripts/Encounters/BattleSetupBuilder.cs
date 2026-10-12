using System.Collections.Generic;
using System.Linq;
using BoardAuthoring;
using Combat;
using Configurations;
using FunGame.Battle;
using FunGame.Board;

namespace Encounters
{
    /// <summary>Turns the authored assets (encounter, game rules, pilot deck) into the pure data the battle engine runs on.</summary>
    public static class BattleSetupBuilder
    {
        /// <param name="deck">The starter deck; an encounter with a test deck (<see cref="EncounterDefinition.TestDeck"/>) uses that instead.</param>
        public static BattleSetup Build(EncounterDefinition encounter, GameRules gameRules, PilotDeck deck, int seed)
        {
            if (encounter.TestDeck != null)
            {
                deck = encounter.TestDeck;
            }

            var board = encounter.Board;
            var graph = BoardConverter.ToGraph(board);

            var slotRules = board.GetSlots()
                .Select(slot => new SlotRule(slot.Id, slot.IsLocked, slot.UnlockApCost, slot.UnlockAtWave, slot.UnlockAtTurn, slot.UnlockLaneId))
                .ToList();

            var lanes = encounter.Lanes
                .Select(lane => new LaneSpec(lane.LaneId, lane.Enemies.Where(enemy => enemy != null).Select(ToSpec).ToList()))
                .ToList();

            var pilots = deck.Pilots
                .Where(pilot => pilot != null)
                .Select(pilot => new PilotStats(
                    string.IsNullOrEmpty(pilot.DisplayName) ? pilot.name : pilot.DisplayName,
                    pilot.AttackValue,
                    pilot.DefenseValue,
                    pilot,
                    PilotAbilities.Create(pilot.AbilityKind, pilot.AbilityApCost)))
                .ToList();

            // the encounter decides which mechanics are available in it
            var rules = ToRules(gameRules);
            rules.AllowReplace = encounter.AllowReplace;
            rules.AllowEject = encounter.AllowEject;
            var openingHand = encounter.OpeningHand.Where(pilot => pilot != null)
                .Select(pilot => string.IsNullOrEmpty(pilot.DisplayName) ? pilot.name : pilot.DisplayName).ToList();
            return new BattleSetup(graph, slotRules, lanes, rules, pilots, seed) { OpeningHand = openingHand };
        }

        public static BattleRules ToRules(GameRules gameRules)
        {
            if (gameRules == null)
            {
                return new BattleRules();
            }

            return new BattleRules
            {
                PlayerMaxHealth = gameRules.PlayerMaxHealth,
                StartingActionPoints = gameRules.StartingActionPoints,
                ActionPointsPerTurn = gameRules.ActionPointsPerTurn,
                HandSize = gameRules.HandSize,
                HandRecoveryPerTurn = gameRules.HandRecoveryPerTurn,
                EjectedRecoveryMultiplier = gameRules.EjectedRecoveryMultiplier,
                DefenseStep = gameRules.DefenseStep,
                PlacePilotCost = gameRules.PlacePilotCost,
                ReplacePilotCost = gameRules.ReplacePilotCost,
                EjectPilotCost = gameRules.EjectPilotCost,
                ActivateAbilityCost = gameRules.ActivateAbilityCost
            };
        }

        private static EnemySpec ToSpec(EnemyDefinition enemy)
        {
            var actions = enemy.Actions.Select(ToActionData).ToList();
            return new EnemySpec(enemy.DisplayName, enemy.MaxHealth, actions, enemy.SelectionMode == EnemyActionSelectionMode.Random, enemy);
        }

        private static EnemyActionData ToActionData(EnemyAction action)
        {
            var effects = new EnemyActionData(action.Name, action.Attack, action.Defense).Effects.ToList();
            if (action.LockTurns > 0)
            {
                effects.Add(new LockEffect(action.LockTurns, action.LockTarget));
            }

            if (action.SnipePercent > 0f)
            {
                effects.Add(new SnipeEffect(action.SnipePercent / 100f, action.SnipeTurns, action.SnipeTarget));
            }

            if (action.ChargeDefense > 0)
            {
                effects.Add(new ChargeEffect(action.ChargeDefense));
            }

            if (action.UnleashAttack > 0)
            {
                effects.Add(new UnleashEffect(action.UnleashAttack));
            }

            return new EnemyActionData(action.Name, effects);
        }
    }
}
