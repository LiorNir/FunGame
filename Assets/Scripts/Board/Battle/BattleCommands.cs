using System.Collections.Generic;

namespace FunGame.Battle
{
    public enum BattleCommandType
    {
        /// <summary>Put a hand pilot in a slot (replacing the pilot there, if any).</summary>
        Place,

        /// <summary>Take a living pilot off the board into the hand.</summary>
        Eject,

        /// <summary>Pay AP to unlock a slot.</summary>
        Unlock,

        /// <summary>Use the ability of the pilot in a slot.</summary>
        Activate
    }

    /// <summary>One thing the player can do during the planning phase. Every player action is a command.</summary>
    public readonly struct BattleCommand
    {
        public BattleCommandType Type { get; }

        /// <summary>For <see cref="BattleCommandType.Place"/>: the position of the pilot in the hand.</summary>
        public int HandIndex { get; }

        public string SlotId { get; }

        private BattleCommand(BattleCommandType type, int handIndex, string slotId)
        {
            Type = type;
            HandIndex = handIndex;
            SlotId = slotId;
        }

        public static BattleCommand Place(int handIndex, string slotId)
        {
            return new BattleCommand(BattleCommandType.Place, handIndex, slotId);
        }

        public static BattleCommand Eject(string slotId)
        {
            return new BattleCommand(BattleCommandType.Eject, -1, slotId);
        }

        public static BattleCommand Unlock(string slotId)
        {
            return new BattleCommand(BattleCommandType.Unlock, -1, slotId);
        }

        public static BattleCommand Activate(string slotId)
        {
            return new BattleCommand(BattleCommandType.Activate, -1, slotId);
        }

        /// <summary>A readable form for logs and reports. Needs the engine the command is meant for, to name the pilot.</summary>
        public string Describe(BattleEngine engine)
        {
            switch (Type)
            {
                case BattleCommandType.Place:
                    var pilot = HandIndex >= 0 && HandIndex < engine.Hand.Count ? engine.Hand[HandIndex].Name : "?";
                    return engine.PilotBySlot.TryGetValue(SlotId, out var occupant)
                        ? $"replace {occupant.Name} with {pilot} in {SlotId}"
                        : $"place {pilot} in {SlotId}";
                case BattleCommandType.Eject:
                    return $"eject {(engine.PilotBySlot.TryGetValue(SlotId, out var ejected) ? ejected.Name : "?")} from {SlotId}";
                case BattleCommandType.Activate:
                    var user = engine.PilotBySlot.TryGetValue(SlotId, out var active) ? active : null;
                    return $"use {user?.Ability?.Name ?? "?"} of {user?.Name ?? "?"} in {SlotId}";
                default:
                    return $"unlock {SlotId}";
            }
        }
    }

    public static class BattleCommands
    {
        /// <summary>
        /// Adds every command the player can usefully give right now. Identical pilots in the hand count once, and a pilot
        /// placed this turn is not replaced or ejected again (that would only waste AP).
        /// </summary>
        public static void Legal(BattleEngine engine, List<BattleCommand> into, bool allowEject = true)
        {
            var seenPilots = new HashSet<string>();
            for (var handIndex = 0; handIndex < engine.Hand.Count; handIndex++)
            {
                var pilot = engine.Hand[handIndex];
                if (!seenPilots.Add($"{pilot.Name}|{pilot.BaseAttack}|{pilot.BaseDefense}|{pilot.DefenseWound}"))
                {
                    continue;
                }

                foreach (var node in engine.Setup.Board.Nodes)
                {
                    if (engine.PilotBySlot.TryGetValue(node.Id, out var occupant) && engine.WasPlacedThisTurn(occupant))
                    {
                        continue;
                    }

                    if (engine.CanPlace(handIndex, node.Id))
                    {
                        into.Add(BattleCommand.Place(handIndex, node.Id));
                    }
                }
            }

            foreach (var pair in engine.PilotBySlot)
            {
                if (allowEject && !engine.WasPlacedThisTurn(pair.Value) && engine.CanEject(pair.Key))
                {
                    into.Add(BattleCommand.Eject(pair.Key));
                }
            }

            foreach (var pair in engine.PilotBySlot)
            {
                if (engine.CanActivate(pair.Key))
                {
                    into.Add(BattleCommand.Activate(pair.Key));
                }
            }

            foreach (var rule in engine.Setup.SlotRules)
            {
                if (engine.CanUnlock(rule.SlotId))
                {
                    into.Add(BattleCommand.Unlock(rule.SlotId));
                }
            }
        }

        /// <summary>Gives the command to the engine. False if it was not allowed.</summary>
        public static bool Apply(BattleEngine engine, BattleCommand command)
        {
            switch (command.Type)
            {
                case BattleCommandType.Place:
                    return engine.TryPlace(command.HandIndex, command.SlotId);
                case BattleCommandType.Eject:
                    return engine.TryEject(command.SlotId);
                case BattleCommandType.Activate:
                    return engine.TryActivate(command.SlotId);
                default:
                    return engine.TryUnlock(command.SlotId);
            }
        }
    }
}
