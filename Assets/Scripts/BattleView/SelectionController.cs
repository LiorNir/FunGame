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
            _board.SlotHovered += OnSlotHovered;
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
                _board.SlotHovered -= OnSlotHovered;
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
                _board.ShowPreview(null);
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

        // screen pixels between a hand card's top edge and the menu above it; negative because the card frame's
        // picture has transparent space above its visible top edge (about 24 pixels at 1080p)
        private const float MenuGapAboveCard = -18f;

        /// <summary>The top centre of the hand card on screen (the redrawn card if the hand was refreshed).</summary>
        private Vector2 CardTopOnScreen(int index, RectTransform clicked)
        {
            var rect = _hand.GetCardRect(index);
            if (rect == null)
            {
                rect = clicked;
            }

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return RectTransformUtility.WorldToScreenPoint(null, (corners[1] + corners[2]) * 0.5f);
        }

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
                ? Icons.Ap(rules.PlacePilotCost)
                : $"{Icons.Ap(rules.PlacePilotCost)}, replace {Icons.Ap(rules.ReplacePilotCost)}";
            var placeLabel = targets.Count > 0
                ? $"Place  ({costText})"
                : (engine.ActionPoints < Mathf.Min(engine.Setup.Rules.PlacePilotCost, engine.Setup.Rules.ReplacePilotCost)
                    ? "Place  (not enough AP)"
                    : "Place  (no free slot)");

            // the card itself shows the pilot's name and stats, so the menu starts with the actions
            var options = new List<ActionMenuView.Option>
            {
                new ActionMenuView.Option(placeLabel, targets.Count > 0, () => BeginPlacing(index)),
                new ActionMenuView.Option("Cancel", true, Cancel)
            };
            if (pilot.HasUnusedAbility)
            {
                // the ability can be read from the hand; it is used once the pilot is on the board
                options.Insert(0, new ActionMenuView.Option(
                    $"{pilot.Ability.Name} ({Icons.Ap(pilot.Ability.ApCost)}, on the board): {pilot.Ability.Describe()}", false, null));
            }

            _hand.Refresh(engine, index);
            // above the card (as it stands once raised as the selected card), so the card stays visible
            _menu.Show(CardTopOnScreen(index, cardRect) + new Vector2(0f, MenuGapAboveCard), options, above: true);
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
                        ? $"Unlock slot  ({Icons.Ap(rule.UnlockApCost)})"
                        : $"Unlock slot  ({Icons.Ap(rule.UnlockApCost)}, not enough AP)";
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
                            $"Use {ability.Name}  ({Icons.Ap(ability.ApCost)}{(abilityBlocker == null ? string.Empty : ", " + abilityBlocker)}): {ability.Describe()}",
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
                    options.Add(new ActionMenuView.Option($"Eject {pilot.Name} to hand  ({Icons.Ap(cost)}{reason})", blocker == null, () => _session.Eject(slotId)));
                }
            }

            return options;
        }

        // while placing, hovering a slot where the pilot can go shows what every chain would become
        private void OnSlotHovered(SlotView slot, bool entered)
        {
            var engine = _session.Engine;
            if (!IsPlacing || engine == null || _session.IsBusy)
            {
                return;
            }

            if (!entered || !engine.CanPlace(_placingHandIndex, slot.SlotId))
            {
                _board.ShowPreview(null);
                return;
            }

            var trial = engine.Clone();
            trial.TryPlace(_placingHandIndex, slot.SlotId);
            _board.ShowPreview(trial.EvaluateChains());
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
