# Tutorial lines (first draft, for review)

The lines live in **`Assets/Campaign/Tutorial.asset`** (one list: encounter, trigger, turn, text); edit them there. This page is the draft as created, for reviewing them in one read. Each line shows once per save in campaign battles (always on the battle test bench), in the tutorial panel at the left of the battle screen, with a "Got it" button.

**Triggers available:** BattleStart, TurnStart (with a turn number), ChainAtZero (first time a chain is hit down to exactly 0), ChainBroken, FreeRest (while planning: the board would defeat a lane's enemy this turn and another follows it), NextEnemy (a second or later enemy appears), EnemyLocks, EnemySnipes, EnemyAssassinates, EnemyCharges (when the intent is revealed), ChargeBroken, AbilityUsed. More can be added.

| Encounter | When | Line |
|---|---|---|
| 1-1 First steps | Battle start | Click a pilot in your hand, then a highlighted slot, to place it. Placing costs 1 AP, and you have 3 AP every turn. |
| 1-1 First steps | Battle start | Pilots form chains from the root (the bottom slot) outward. The red value is your attack on the enemy, the blue value your defense against its hit. Press Play Turn when you are ready. |
| 1-1 First steps | Turn 2 | Pilots stay on the board between turns. Their defense takes the enemy's hits; the blue bar shows what is left. Hover anything to learn more. |
| 1-2 A door opens | Battle start | A locked slot: the text under the lock says when it opens. A chain only counts up to its first empty slot, so fill it from the root outward. |
| 1-3 The gatekeeper | Battle start | The board splits: one branch attacks, the other defends, and the root counts for both. The x slot multiplies everything before it. |
| 1-3 The gatekeeper | First chain at 0 | A hit equal to a chain's defense leaves it standing at 0. The next hit that reaches it breaks it. |
| 1-3 The gatekeeper | First chain broken | A broken chain's pilots fall and block their slots. Place a new pilot on a fallen one to clear it. |
| 2-1 Fresh legs | Battle start | New: you can place a pilot on top of another (a replace). The replaced pilot goes back to your hand, where its stats recover every turn. |
| 2-1 Fresh legs | Free rest (the board will defeat this enemy and another follows) | Your board will defeat this enemy this turn, and the next one only arrives next turn: a free turn to rest. Click a worn pilot and choose Eject. A pilot that rests a whole turn in your hand after an eject recovers twice as fast. |
| 2-2 Duck! | Battle start | Ejecting also dodges: a pilot that is not on the board cannot be hit. The damage reaches you instead, but the pilot survives. |
| 2-3 The foreman | Battle start | This locked slot can be opened now by paying AP: click it. |
| 3-1 Power surge | Battle start | One of your pilots has an ability. Place it, then click it to use the ability (once per battle, it costs AP). A glow marks a pilot whose ability is ready. |
| 3-1 Power surge | Ability used | That ability doubles the pilot's attack this turn, then its attack drops to 0 until it rests in your hand. Plan the swap. |
| 3-3 The marksman | Snipe revealed | The enemy aims a snipe at the marked pilot: -50% to both its stats next turn. Swap the pilot out to dodge it, or take the hit. |
| 3-3 The marksman | Assassinate revealed | A snipe aimed at a pilot with nothing left assassinates it: the pilot falls. |
| 4-1 Pay the toll | Battle start | A minus slot is a toll: the chain loses its pilot's stat there instead of gaining it. Pay with your weakest pilot (a 0 costs nothing) to reach the x2 slot behind it. A pilot on a toll never takes damage. |
| 4-1 Pay the toll | Turn 2 | The enemy hunkers down: a big guard and no attack. Haze's No Guard halves both sides' defense this turn, which costs you nothing while the enemy is not attacking. |
| 4-2 Divide and conquer | Battle start | A divide slot divides the chain by its pilot's stat. Dividing by less than 1 multiplies! Dividing by 0 is a very bad idea. |
| 4-3 The ram | Battle start | Defense can multiply too: the x slot on the blue chain multiplies everything before it. |
| 4-3 The ram | Charge revealed | The enemy is charging: a big guard now and a huge attack next turn. Break through the whole guard this turn and it is stunned instead. |
| 4-3 The ram | Charge broken | Charge broken: the huge attack will not happen. |
| 5-1 Two fronts | Battle start | Two lanes: each enemy attacks the defense on its side, and each side's attack hits its own enemy. 3 AP for both fronts: choose where to spend them. |
| 5-2 The hub | Battle start | The hub slot feeds all four chains. Strong, but both enemies wear it down, and if it breaks every chain is cut. |
| 5-3 The warden | Lock revealed | The enemy will lock the marked pilot: it cannot be replaced or ejected for 2 turns. Swap it out now if you want it free. |

**Not covered by a line yet** (candidates): Undo and Give up; chains evaluating left to right ((1 + 3) x 5 = 20, a surprise in 3-3); the rising upgrade cost and the Pilots screen (a map-side tutorial, which does not exist yet); assassinate in a world where it is likely to happen.

Writing rule (user, 2026-10-20): no pilot, ability or enemy names in tutorial lines unless the user asks for one (Haze's No Guard in 4-1 stays: it matters there); write "AP" in words, it is shown as the AP icon.
