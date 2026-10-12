using System;
using System.Collections.Generic;
using System.Linq;

namespace FunGame.Battle
{
    /// <summary>
    /// Writes the battle's text log from its events, so the log always tells what the views are shown. It is called the
    /// moment an event is raised, so it may also read the engine's state at that moment (hand, board, enemy health).
    /// </summary>
    public static class BattleLogWriter
    {
        public static void Write(BattleEvent battleEvent, BattleEngine engine, List<string> log)
        {
            var lane = battleEvent.LaneId;
            switch (battleEvent.Type)
            {
                case BattleEventType.TurnStarted:
                    log.Add($"--- Turn {Fmt(battleEvent.A)} begins ---");
                    break;

                case BattleEventType.PlanningStarted:
                    log.Add($"  HP {Fmt(battleEvent.A)}, AP {Fmt(battleEvent.B)}, hand: {string.Join(", ", engine.Hand)}");
                    foreach (var state in engine.Lanes.Where(state => state.Current != null))
                    {
                        var enemy = state.Current;
                        var aimedAt = engine.IntentTargets().Where(target => target.LaneId == state.Spec.Id)
                            .Select(target => $"{target.Effect.Describe()} on {target.Pilot.Name}@{target.SlotId}").ToList();
                        var aims = aimedAt.Count == 0 ? string.Empty : $" -> {string.Join(", ", aimedAt)}";
                        log.Add($"  [{state.Spec.Id}] {enemy.Spec.Name} HP {Fmt(enemy.Health)}/{enemy.Spec.MaxHealth} intends: {enemy.Intent}{aims}");
                    }

                    break;

                case BattleEventType.PilotPlaced:
                    var replacing = battleEvent.Detail == null ? string.Empty : $" (replacing {battleEvent.Detail})";
                    log.Add($"  Place {battleEvent.Subject} in {SlotOf(battleEvent)}{replacing} {Cost(battleEvent)}");
                    break;

                case BattleEventType.PilotEjected:
                    log.Add($"  Eject {battleEvent.Subject} from {SlotOf(battleEvent)} {Cost(battleEvent)}");
                    break;

                case BattleEventType.SlotBought:
                    log.Add($"  Unlock {battleEvent.Subject} {Cost(battleEvent)}");
                    break;

                case BattleEventType.AbilityActivated:
                    log.Add($"  {battleEvent.Subject} uses {battleEvent.Detail} in {SlotOf(battleEvent)} {Cost(battleEvent)}");
                    break;

                case BattleEventType.FallenCleared:
                    log.Add($"  Cleared fallen {battleEvent.Subject} from {SlotOf(battleEvent)}");
                    break;

                case BattleEventType.TurnResolving:
                    log.Add($"--- Turn {Fmt(battleEvent.A)} resolves ---");
                    var chains = engine.EvaluateChains().Values.OrderBy(result => result.Chain.Id)
                        .Select(result => $"{result.Chain.Id}={Fmt(result.Value)}");
                    var placed = engine.PilotBySlot.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}");
                    log.Add($"  board: {string.Join(" ", placed)} | chains: {string.Join(", ", chains)}");
                    break;

                case BattleEventType.EnemySpawned:
                    log.Add($"  [{lane}] enemy {Fmt(battleEvent.C)} appears: {battleEvent.Subject} HP {Fmt(battleEvent.A)}");
                    break;

                case BattleEventType.SlotUnlocked:
                    log.Add($"  Slot {battleEvent.Subject} unlocks ({battleEvent.Detail})");
                    break;

                case BattleEventType.DivideByZero:
                    log.Add($"  !! DIVIDE BY ZERO in chain {battleEvent.Subject} at {SlotOf(battleEvent)}: the chain collapses to 0");
                    break;

                case BattleEventType.PlayerAttacked:
                    var target = engine.Lanes.First(state => state.Spec.Id == lane).Current;
                    var health = target == null ? "0" : $"{Fmt(Math.Max(0f, target.Health))}/{target.Spec.MaxHealth}";
                    log.Add($"  [{lane}] player attacks {target?.Spec.Name}: {Fmt(battleEvent.A)} vs defense {Fmt(battleEvent.B)} -> {Fmt(battleEvent.C)} damage, enemy HP {health}");
                    break;

                case BattleEventType.EnemyDefeated:
                    log.Add($"  [{lane}] {battleEvent.Subject} is defeated");
                    break;

                case BattleEventType.EnemyActed:
                    var actor = engine.Lanes.First(state => state.Spec.Id == lane).Current;
                    log.Add($"  [{lane}] {actor?.Spec.Name} uses {actor?.Intent}");
                    break;

                case BattleEventType.EnemyAttacked:
                    log.Add($"  [{lane}] attacks for {Fmt(battleEvent.A)}");
                    break;

                case BattleEventType.ChainAbsorbed:
                    log.Add($"  [{lane}] chain {battleEvent.Subject} absorbs {Fmt(battleEvent.A)} of {Fmt(battleEvent.B)} (now {Fmt(battleEvent.C)}); pilots wounded");
                    break;

                case BattleEventType.ChainDestroyed:
                    var fallen = battleEvent.Slots.Select(slot => $"{engine.PilotBySlot[slot].Name}@{slot}");
                    log.Add($"  [{lane}] chain {battleEvent.Subject} absorbs {Fmt(battleEvent.A)} of {Fmt(battleEvent.B)} and is destroyed; fallen: {string.Join(", ", fallen)}");
                    break;

                case BattleEventType.PlayerDamaged:
                    log.Add($"  [{lane}] {Fmt(battleEvent.A)} reaches the player, HP {Fmt(battleEvent.B)}/{engine.Setup.Rules.PlayerMaxHealth}");
                    break;

                case BattleEventType.PilotLocked:
                    log.Add($"  [{lane}] {battleEvent.Subject}@{SlotOf(battleEvent)} is locked for {Fmt(battleEvent.A)} turn(s)");
                    break;

                case BattleEventType.PilotSniped:
                    log.Add($"  [{lane}] {battleEvent.Subject}@{SlotOf(battleEvent)} is sniped: -{Fmt(battleEvent.A * 100f)}% to both stats for {Fmt(battleEvent.B)} turn(s) from next turn");
                    break;

                case BattleEventType.PilotAssassinated:
                    log.Add($"  [{lane}] {battleEvent.Subject}@{SlotOf(battleEvent)} was spent and is assassinated (falls)");
                    break;

                case BattleEventType.EffectFizzled:
                    log.Add($"  [{lane}] {battleEvent.Subject} does nothing: {battleEvent.Detail}");
                    break;

                case BattleEventType.Victory:
                    log.Add("VICTORY");
                    break;

                case BattleEventType.Defeat:
                    log.Add($"DEFEAT ({battleEvent.Detail})");
                    break;

                case BattleEventType.Stalemate:
                    log.Add("STALEMATE (turn limit reached)");
                    break;
            }
        }

        private static string SlotOf(BattleEvent battleEvent)
        {
            return battleEvent.Slots.FirstOrDefault();
        }

        private static string Cost(BattleEvent battleEvent)
        {
            return $"[-{Fmt(battleEvent.A)} AP, {Fmt(battleEvent.B)} left]";
        }

        private static string Fmt(float value)
        {
            return DisplayFormat.Value(value);
        }
    }
}
