using System.Collections.Generic;
using System.Linq;
using FunGame.Battle;
using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// The click flow of the planning phase. Clicking a card or a slot opens a menu of what can be done with it right
    /// now (with the AP cost, or the reason it cannot). Choosing to place a card lights up every legal slot, and a click
    /// on one places the pilot. Escape or a right click cancels.
    /// </summary>
    public class SelectionController : MonoBehaviour
    {
        private BattleSession _session;
        private BoardView _board;
        private HandView _hand;
        private HudView _hud;
        private ActionMenuView _menu;

        private int _placingHandIndex = -1;

        public bool IsPlacing => _placingHandIndex >= 0;
        public int SelectedHandIndex => _placingHandIndex;

        public void Init(BattleSession session, BoardView board, HandView hand, HudView hud, ActionMenuView menu)
        {
            _session = session;
            _board = board;
            _hand = hand;
            _hud = hud;
            _menu = menu;

            _hand.CardClicked += OnCardClicked;
            _board.SlotClicked += OnSlotClicked;
            _menu.Dismissed += Cancel;
        }

        private void OnDestroy()
        {
            if (_hand != null)
            {
                _hand.CardClicked -= OnCardClicked;
            }

            if (_board != null)
            {
                _board.SlotClicked -= OnSlotClicked;
            }

            if (_menu != null)
            {
                _menu.Dismissed -= Cancel;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                Cancel();
            }
        }

        public void Cancel()
        {
            _placingHandIndex = -1;
            if (_menu != null)
            {
                _menu.Close();
            }

            if (_board != null)
            {
                _board.ClearHighlights();
            }

            if (_hud != null)
            {
                _hud.SetHint(string.Empty);
            }

            if (_session != null && _session.Engine != null)
            {
                _hand.Refresh(_session.Engine, -1);
            }
        }

        // ---------- clicks ----------

        private void OnCardClicked(int index, RectTransform cardRect)
        {
            if (_session.IsBusy)
            {
                return;
            }

            Cancel();
            var engine = _session.Engine;
            var pilot = engine.Hand[index];
            var targets = ValidTargets(index);

            var rules = engine.Setup.Rules;
            var costText = rules.PlacePilotCost == rules.ReplacePilotCost
                ? $"{rules.PlacePilotCost} AP"
                : $"{rules.PlacePilotCost} AP, replace {rules.ReplacePilotCost} AP";
            var placeLabel = targets.Count > 0
                ? $"Place  ({costText})"
                : (engine.ActionPoints < Mathf.Min(engine.Setup.Rules.PlacePilotCost, engine.Setup.Rules.ReplacePilotCost)
                    ? "Place  (not enough AP)"
                    : "Place  (no free slot)");

            var options = new List<ActionMenuView.Option>
            {
                new ActionMenuView.Option(_session.Visuals.ExactStats(pilot), false, null),
                new ActionMenuView.Option(placeLabel, targets.Count > 0, () => BeginPlacing(index)),
                new ActionMenuView.Option("Cancel", true, Cancel)
            };
            if (pilot.HasUnusedAbility)
            {
                // the ability can be read from the hand; it is used once the pilot is on the board
                options.Insert(1, new ActionMenuView.Option(
                    $"{pilot.Ability.Name} ({pilot.Ability.ApCost} AP, on the board): {pilot.Ability.Describe()}", false, null));
            }

            _hand.Refresh(engine, index);
            _menu.Show(RectTransformUtility.WorldToScreenPoint(null, cardRect.position) + new Vector2(0f, 230f), options);
        }

        private void OnSlotClicked(SlotView slot)
        {
            if (_session.IsBusy)
            {
                return;
            }

            var engine = _session.Engine;
            if (IsPlacing)
            {
                var handIndex = _placingHandIndex;
                if (engine.CanPlace(handIndex, slot.SlotId))
                {
                    Cancel();
                    _session.Place(handIndex, slot.SlotId);
                }
                else
                {
                    Cancel();
                }

                return;
            }

            _menu.Close();
            var options = BuildSlotOptions(slot.SlotId);
            if (options.Count == 0)
            {
                return;
            }

            options.Add(new ActionMenuView.Option("Cancel", true, Cancel));
            _menu.Show(Camera.main.WorldToScreenPoint(slot.transform.position) + new Vector3(0f, 90f, 0f), options);
        }

        private List<ActionMenuView.Option> BuildSlotOptions(string slotId)
        {
            var engine = _session.Engine;
            var options = new List<ActionMenuView.Option>();

            if (!engine.IsUnlocked(slotId))
            {
                var rule = engine.Setup.SlotRules.FirstOrDefault(candidate => candidate.SlotId == slotId);
                if (rule != null && rule.UnlockApCost > 0)
                {
                    var label = engine.CanUnlock(slotId)
                        ? $"Unlock slot  ({rule.UnlockApCost} AP)"
                        : $"Unlock slot  ({rule.UnlockApCost} AP, not enough AP)";
                    options.Add(new ActionMenuView.Option(label, engine.CanUnlock(slotId), () => _session.Unlock(slotId)));
                }
                else
                {
                    options.Add(new ActionMenuView.Option("Locked  (opens by itself later)", false, null));
                }

                return options;
            }

            if (engine.PilotBySlot.TryGetValue(slotId, out var pilot))
            {
                if (pilot.Fallen)
                {
                    options.Add(new ActionMenuView.Option($"{pilot.Name} has fallen  (place a pilot here to clear it)", false, null));
                }
                else
                {
                    options.Add(new ActionMenuView.Option(_session.Visuals.ExactStats(pilot), false, null));
                    if (pilot.Ability != null)
                    {
                        var ability = pilot.Ability;
                        var abilityBlocker = engine.ActivateBlocker(slotId);
                        options.Add(new ActionMenuView.Option(
                            $"Use {ability.Name}  ({ability.ApCost} AP{(abilityBlocker == null ? string.Empty : ", " + abilityBlocker)}): {ability.Describe()}",
                            abilityBlocker == null, () => _session.Activate(slotId)));
                    }

                    var cost = engine.Setup.Rules.EjectPilotCost;
                    var blocker = engine.EjectBlocker(slotId);
                    var reason = blocker == null ? string.Empty : ", " + blocker;
                    // a wounded pilot pulled back to rest recovers faster on its first turn in the hand: say so where it is chosen
                    var restBonus = engine.Setup.Rules.EjectedRecoveryMultiplier;
                    if (blocker == null && pilot.DefenseWound > 0.001f && restBonus > 1.001f && engine.Setup.Rules.HandRecoveryPerTurn > 0f)
                    {
                        reason += $", rests x{DisplayFormat.Value(restBonus)}";
                    }
                    options.Add(new ActionMenuView.Option($"Eject {pilot.Name} to hand  ({cost} AP{reason})", blocker == null, () => _session.Eject(slotId)));
                }
            }

            return options;
        }

        private void BeginPlacing(int handIndex)
        {
            var engine = _session.Engine;
            _placingHandIndex = handIndex;
            _board.SetHighlights(ValidTargets(handIndex));
            _hand.Refresh(engine, handIndex);
            _hud.SetHint($"Choose a slot for {engine.Hand[handIndex].Name}  (Esc or right-click to cancel)");
        }

        private List<string> ValidTargets(int handIndex)
        {
            var engine = _session.Engine;
            return _board.Slots
                .Select(slot => slot.SlotId)
                .Where(slotId => engine.CanPlace(handIndex, slotId))
                .ToList();
        }
    }
}
