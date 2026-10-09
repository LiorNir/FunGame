# Fun Game — Reference Image Brief (v2)

Paste this whole document to an image-generating AI, then ask for the shots in "Images to generate".
Everything here is a visual/mood reference for a 2D game in development. Nothing needs to be functional.
This version replaces the first brief: the board is now a branching network of slots, not a body with fixed slots.

## 1. One-paragraph concept

A 2D turn-based battler in the spirit of Slay the Spire, but the core mechanic is **arithmetic**. A human summoner sits at the bottom-center of the screen, seen from behind. In front of them lies a **board**: a network of round **pilot slots** joined by glowing lines. Small animal pilots (fox, hedgehog, snail, bird, rodent...) are placed into the slots. Where two slots are connected, a small gold **operator coin** (+, −, ×, ÷) sits on the connection. Values flow along the lines from the slots nearest the summoner outward to the slots nearest the enemies. Up to two enemies stand at the top of the screen, each with its own set of lines leading to it. The board is different in every battle, so each battle is a small puzzle.

## 2. Art direction

- **Style:** detailed pixel art with painterly lighting (modern high-res pixel look). Strong silhouettes, chunky readable shapes.
- **Mood:** mystical and tactical. Cool blues on the player's side, hot oranges and reds on the enemy side, but the biome can vary (lava cavern, sunlit overgrown ruins, etc.).
- **Camera:** a slightly tilted view onto a flat board, as in a tabletop or card game. The player's side is at the bottom, the enemies at the top. No extreme perspective.
- **Readability first:** numbers, operator symbols, and slot states must be legible at a glance.

## 3. Screen layout (landscape, 16:9)

- **Top bar:** thin strip with player name, a heart with HP, and a small row of relic icons.
- **Top:** one or two enemies. Each has a health bar and an **intent box** next to it showing its next attack (sword icon + number) and/or defense (shield icon + number). When two enemies exist, each has its own intent box.
- **Top corner or beside each enemy:** a **"Next Monster"** preview showing the enemy that will appear after this one is defeated.
- **Middle:** the board (section 4).
- **Bottom center:** the summoner, seen from behind, with a faint glowing magic circle.
- **Bottom:** the player's **hand**: a fan of five pilot cards. In this version the hand holds **only pilots**. Each card shows the animal, its name, its ATK (sword badge) and DEF (shield badge).
- **Bottom-left:** a round **action points** badge (for example "3/4").
- **Bottom-right:** a large "Play Turn" button.

## 4. The board (important)

The board is a **tree**: lines start at slots near the summoner and branch outward toward the enemies. Required features:

- **Pilot slots:** round framed sockets. Filled slots show the animal and its ATK/DEF badges. Empty slots are dark sockets with a faint silhouette.
- **Operator coins:** small gold circles on the connections between two slots, with a clean +, −, ×, or ÷ symbol. They should feel embedded in the lines, not draggable.
- **Line colour = role.** A chain that leads to attack is drawn in **red/orange**; one that leads to defense is drawn in **blue**. Lines glow and flow outward, away from the summoner, like energy moving. Where a slot feeds both an attack and a defense chain, a red line and a blue line leave it together.
- **Start points ("origins"):** each chain begins with a small glowing emblem near the summoner (a sword emblem for attack, a shield emblem for defense).
- **Chain ends ("terminals"):** the last slot of a chain, nearest the enemy. It looks like a normal slot, optionally with a faint sword or shield emblem and the colour of its line. Do not label it "FINAL".
- **Branching and meeting:** one slot may split into several lines. One trunk may feed both enemies' lanes. Occasionally two lines merge into one operator coin that has no pilot of its own.
- **Dormant slots:** a filled slot that is cut off from its chain by an empty slot upstream looks greyed out and dim.
- **Locked slots:** a slot with a chain or padlock over it, clearly different from an empty socket, and a small cost or wave icon.
- **Bonus slots:** a slot with a small badge such as "+2" or "×1.5" next to it, a brighter golden frame, and a subtle glow.
- **Toll connections:** a − or ÷ coin should look slightly more ominous (darker or reddish rim) than + or ×.
- **Fallen pilot:** a pilot that has died stays on the board as a grey, cracked, faded version of its portrait.
- **Value readout:** each lane's current attack and defense totals should be visible somewhere clear (for example a number on the terminal slot or next to the enemy's intent box).

Show the board with roughly 8–12 slots and 6–10 operator coins, so there is enough variety to read as a puzzle.

## 5. Visual effects to illustrate

- **Energy along the lines:** empty parts of a chain are thin, dim and grey-blue. As slots fill, that part of the line becomes brighter, thicker, more saturated, more opaque and wavier, as if powered up.
- **Defense bars:** every pilot in a defense chain shows a small bar. All bars in the chain shrink evenly as the chain takes damage. A separate glowing link between the pilots shows they are still connected. At zero, every pilot in the chain collapses into a fallen state.
- **Attack:** the pilots of an attack chain flash and play a quick attack pose, and a red energy pulse travels along the line to the enemy.
- **Drag preview:** while dragging a pilot over a slot, a small floating tag shows the chain's resulting value (for example "6 → 9") in green if it improves and red if it drops.
- **Action points refusal:** the AP badge shakes and flashes red.
- **Divide by zero (comic):** dividing by 0 triggers a funny punishment. Show an `ERROR` pop-up above the chain and the pilot in the divisor slot being sucked into a tiny black hole.

## 6. Pilot states (small sprites)

For one example pilot (a fox), a mini sheet of states: idle, attack, defeated/fallen, and also "dormant" (greyed out but intact) and "wounded" (bar partly drained).

## 7. Game flow to illustrate

1. **Start Battle** screen: a calm title screen with a glowing "Start Battle" button.
2. **Battle start:** enemies appear, the board is revealed with its operators, the hand is dealt.
3. **Planning:** the player drags a pilot to a slot; intents are visible; AP shows 2/4.
4. **Locked slot unlock:** a locked slot opens (padlock breaks) after the player pays AP.
5. **Player attack:** attack pilots flash, energy runs along the red lines, the enemy's defense number drops, then its health bar.
6. **Enemy attack:** the enemy strikes; the defense bars drain evenly; the chain collapses into fallen pilots.
7. **Next monster:** the defeated enemy fades and the next one appears in its lane.
8. **Victory** and **Defeat** panels, each with a button back to the Start screen.

## 8. Images to generate

1. **Main battle screen** with two enemies, a branching board, intents, "Next Monster" previews, and the pilot hand.
2. **Three boards** with different layouts and operator combinations (labelled), one of them with one trunk feeding both enemies.
3. **Slot states close-up:** empty, filled, dormant, locked, bonus (+2), fallen.
4. **Defense chain close-up:** three linked pilots with matching bars at full, half and empty.
5. **Toll versus normal coins** close-up.
6. **Drag preview** tag in both the improving and worsening states.
7. **Start Battle screen**, and **victory/defeat panels**.
8. **Pilot hand** (five cards) plus the AP badge.
9. **Enemy intent box** variants (attack only, defense only, both).
10. **Pilot state sheet** for the fox.
11. **The divide-by-zero gag** panel.
12. **A summoner character concept**, small, seen from behind.

## 9. Please avoid

- Photorealism or smooth vector/cartoon styles. Keep it pixel-art-based.
- Lines that connect slots at random or leave slots with no connection. Every slot must sit on a clear path from the summoner's side to an enemy.
- Clutter. A few clear numbers beat many small ones.
- Unreadable text. Numbers and the four math symbols must be clean.
- Copyrighted characters or logos.
