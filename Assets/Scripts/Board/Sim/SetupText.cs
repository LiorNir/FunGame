using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using FunGame.Battle;
using FunGame.Board;

namespace FunGame.Sim
{
    /// <summary>
    /// A plain-text form of an encounter (board, slot rules, lanes and enemies, rules and the pilot roster), so the
    /// encounters authored in Unity can be played by the simulation tool outside Unity. One fact per line, fields
    /// separated by "|", lines starting with "#" are comments. See <see cref="Write"/> for the lines.
    /// </summary>
    public static class SetupText
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static string Write(string name, string tier, BattleSetup setup, string note = null)
        {
            var text = new StringBuilder();
            text.AppendLine("# encounter for the balance lab; written by the Unity export, read by SetupText.Read");
            text.AppendLine($"name|{Clean(name)}");
            text.AppendLine($"tier|{Clean(tier)}");
            if (!string.IsNullOrEmpty(note))
            {
                text.AppendLine($"note|{Clean(note)}");
            }

            var rules = setup.Rules;
            text.AppendLine($"rules|{rules.PlayerMaxHealth}|{rules.StartingActionPoints}|{rules.ActionPointsPerTurn}|{rules.HandSize}|" +
                            $"{rules.PlacePilotCost}|{rules.ReplacePilotCost}|{rules.EjectPilotCost}|{rules.ActivateAbilityCost}|{(rules.AllowReplace ? 1 : 0)}|{(rules.AllowEject ? 1 : 0)}|{Number(rules.HandRecoveryPerTurn)}|{Number(rules.EjectedRecoveryMultiplier)}");

            foreach (var node in setup.Board.Nodes)
            {
                text.AppendLine($"node|{node.Id}|{node.ParentId}|{(node.ConnectionOperation.HasValue ? node.ConnectionOperation.Value.ToString() : string.Empty)}|" +
                                $"{node.Bonus.Kind}|{Number(node.Bonus.Amount)}");
            }

            foreach (var chain in setup.Board.Chains)
            {
                text.AppendLine($"chain|{chain.Id}|{chain.LaneId}|{chain.Role}|{chain.TerminalId}");
            }

            foreach (var rule in setup.SlotRules)
            {
                text.AppendLine($"slot|{rule.SlotId}|{(rule.Locked ? 1 : 0)}|{rule.UnlockApCost}|{rule.UnlockAtWave}|{rule.UnlockAtTurn}|{rule.UnlockLaneId}");
            }

            foreach (var lane in setup.Lanes)
            {
                text.AppendLine($"lane|{lane.Id}");
                foreach (var enemy in lane.Enemies)
                {
                    text.AppendLine($"enemy|{lane.Id}|{Clean(enemy.Name)}|{enemy.MaxHealth}|{(enemy.RandomSelection ? 1 : 0)}");
                    foreach (var action in enemy.Actions)
                    {
                        // attack and defense are the plain columns; any other effect follows as one field each ("lock 2 MostWounded")
                        var extras = action.Effects.Where(effect => !(effect is AttackEffect) && !(effect is DefendEffect)).Select(WriteEffect);
                        text.AppendLine(string.Join("|", new[] { "action", Clean(action.Name), action.Attack.ToString(CultureInfo.InvariantCulture), action.Defense.ToString(CultureInfo.InvariantCulture) }.Concat(extras)));
                    }
                }
            }

            foreach (var pilot in setup.Deck)
            {
                text.AppendLine($"pilot|{Clean(pilot.Name)}|{Number(pilot.Attack)}|{Number(pilot.Defense)}");
            }

            return text.ToString();
        }

        public static EncounterCase Read(string text, string fallbackName = "Encounter")
        {
            var name = fallbackName;
            var tier = "Mid";
            string note = null;
            var rules = new BattleRules();
            var nodes = new List<SlotNode>();
            var chains = new List<ChainDefinition>();
            var slotRules = new List<SlotRule>();
            var laneIds = new List<string>();
            var enemiesByLane = new Dictionary<string, List<EnemySpec>>();
            var pilots = new List<PilotStats>();

            string enemyLane = null, enemyName = null;
            int enemyHealth = 0;
            var enemyRandom = false;
            List<EnemyActionData> enemyActions = null;

            void FinishEnemy()
            {
                if (enemyName != null)
                {
                    enemiesByLane[enemyLane].Add(new EnemySpec(enemyName, enemyHealth, enemyActions, enemyRandom));
                    enemyName = null;
                }
            }

            var lineNumber = 0;
            foreach (var rawLine in text.Split('\n'))
            {
                lineNumber++;
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }

                var fields = line.Split('|');
                try
                {
                    switch (fields[0])
                    {
                        case "name":
                            name = fields[1];
                            break;
                        case "tier":
                            tier = fields[1];
                            break;
                        case "note":
                            note = fields[1];
                            break;
                        case "rules":
                            rules = new BattleRules
                            {
                                PlayerMaxHealth = Int(fields[1]), StartingActionPoints = Int(fields[2]), ActionPointsPerTurn = Int(fields[3]),
                                HandSize = Int(fields[4]), PlacePilotCost = Int(fields[5]), ReplacePilotCost = Int(fields[6]),
                                EjectPilotCost = Int(fields[7]), ActivateAbilityCost = Int(fields[8]),
                                AllowReplace = fields.Length <= 9 || fields[9] == "1", AllowEject = fields.Length <= 10 || fields[10] == "1",
                                HandRecoveryPerTurn = fields.Length > 11 ? Float(fields[11]) : 0f,
                                EjectedRecoveryMultiplier = fields.Length > 12 && fields[12].Length > 0 ? Float(fields[12]) : 1f
                            };
                            break;
                        case "node":
                            nodes.Add(new SlotNode(
                                fields[1],
                                fields[2].Length == 0 ? null : fields[2],
                                fields[3].Length == 0 ? (Operation?)null : (Operation)Enum.Parse(typeof(Operation), fields[3]),
                                new NodeBonus((BonusKind)Enum.Parse(typeof(BonusKind), fields[4]), Float(fields[5]))));
                            break;
                        case "chain":
                            chains.Add(new ChainDefinition(fields[1], fields[2], (ChainRole)Enum.Parse(typeof(ChainRole), fields[3]), fields[4]));
                            break;
                        case "slot":
                            slotRules.Add(new SlotRule(fields[1], fields[2] == "1", Int(fields[3]), Int(fields[4]), Int(fields[5]), fields.Length > 6 ? fields[6] : null));
                            break;
                        case "lane":
                            FinishEnemy();
                            laneIds.Add(fields[1]);
                            enemiesByLane[fields[1]] = new List<EnemySpec>();
                            break;
                        case "enemy":
                            FinishEnemy();
                            enemyLane = fields[1];
                            enemyName = fields[2];
                            enemyHealth = Int(fields[3]);
                            enemyRandom = fields[4] == "1";
                            enemyActions = new List<EnemyActionData>();
                            break;
                        case "action":
                            var effects = new EnemyActionData(fields[1], Int(fields[2]), Int(fields[3])).Effects.ToList();
                            effects.AddRange(fields.Skip(4).Where(field => field.Trim().Length > 0).Select(ReadEffect));
                            enemyActions.Add(new EnemyActionData(fields[1], effects));
                            break;
                        case "pilot":
                            pilots.Add(new PilotStats(fields[1], Float(fields[2]), Float(fields[3])));
                            break;
                        default:
                            throw new FormatException($"unknown line type '{fields[0]}'");
                    }
                }
                catch (Exception exception) when (exception is FormatException || exception is IndexOutOfRangeException || exception is ArgumentException || exception is NullReferenceException)
                {
                    throw new FormatException($"line {lineNumber} ('{line}'): {exception.Message}", exception);
                }
            }

            FinishEnemy();
            var lanes = laneIds.Select(id => new LaneSpec(id, enemiesByLane[id])).ToList();
            var setup = new BattleSetup(new BoardGraph(nodes, chains), slotRules, lanes, rules, pilots, 1);
            return new EncounterCase(name, tier, setup, note);
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty).Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
        }

        private static string Number(float value)
        {
            return value.ToString("R", Culture);
        }

        private static string WriteEffect(IEnemyEffect effect)
        {
            if (effect is LockEffect lockEffect)
            {
                return $"lock {lockEffect.Turns} {lockEffect.TargetRule}";
            }

            throw new FormatException($"no text form for the effect {effect.GetType().Name}");
        }

        /// <summary>An effect field of an action line: "lock TURNS TARGETRULE" (target rule: Random, MostWounded, HighestAttack, HighestDefense).</summary>
        private static IEnemyEffect ReadEffect(string field)
        {
            var parts = field.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0].ToLowerInvariant())
            {
                case "lock":
                    return new LockEffect(Int(parts[1]), parts.Length > 2 ? (PilotTargetRule)Enum.Parse(typeof(PilotTargetRule), parts[2], true) : PilotTargetRule.Random);
                default:
                    throw new FormatException($"unknown effect '{parts[0]}'");
            }
        }

        private static int Int(string value)
        {
            return int.Parse(value, Culture);
        }

        private static float Float(string value)
        {
            return float.Parse(value, Culture);
        }
    }
}
