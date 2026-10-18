# Visuals guide: where each look lives

Everything the player sees is a **prefab** or an **asset**. Scripts only fill in values (texts, numbers, which parts are on), so a look is changed in one place and every screen that uses it follows. Edit these freely: the "Create ..." menus never overwrite an existing prefab.

## Scenes

| Scene | What it is |
|---|---|
| `Assets/Scenes/MapScene.unity` | The campaign menu: the map and the Pilots screen. First scene of the build. Holds a Canvas and a **Campaign Menu** object that places the two screen prefabs. |
| `Assets/Scenes/BattleScene.unity` | The battle. Started from the map it plays that encounter with your pilots; opened on its own it is the test bench (test panel, any encounter, test decks). Holds the **Battle Session**, the **Board Table** under it (tilted 45 degrees; holds Board BG and the board root **Board**: move, tilt or scale the table to move the whole board with its background) and the camera. |

## Campaign menu (`Assets/CampaignViews/Prefabs`)

| Prefab | In charge of |
|---|---|
| `MapScreen` | The whole map screen: background, "Skill points" text, the Pilots / Unlock all / Reset progress / Open playtest logs buttons, and the `Worlds` container (a vertical layout group) that the world rows are placed in. |
| `WorldRow` | One world: its name and the `Encounters` container (a horizontal layout group) that the encounter buttons are placed in. Name colours (open / locked) are on its `WorldRowView`. |
| `EncounterButton` | One encounter: background, title and footer line ("Reward: +1 SP, Spark joins" / "cleared" / "locked"). Background colours per state (open, cleared, locked) are on its `EncounterButtonView`. |
| `PilotsScreen` | The Pilots screen: background, "Skill points" text, Back to map button, and the `Cards` container (a grid layout group: cell size, spacing and columns are set there). |
| `PilotCard` | One pilot: level ("Lv 2"), picture, name, stats, ability text, +1 ATK / +1 DEF buttons, Refund button. The silhouette tint for pilots not recruited yet and the stat colours are on its `PilotCardView`. |

## Battle (`Assets/BattleViews/Prefabs`)

| Prefab | In charge of |
|---|---|
| `BattleHud` | The whole 2D battle UI: HP, AP and turn texts, the hint line, Play Turn, Undo and Give up buttons, the hand area, the action menu, the test panel, the tutorial panel (`Tutorial`: text and "Got it"), the hover explanation box (`Tooltip`: sized to its text, follows the mouse), and the end screen (`Outcome`: dim background, result text, Play again and Back to map buttons). |
| `CardView` | One pilot card in the hand: background, picture, name, attack and defense bars (each with a fill, a blinking recovery preview and a label), the ability line and the gold frame. Bar colours for boosts and drops are on the bars (`CardStatBar`). |
| `PilotSlot` | One pilot slot on the board: shadow (semi-transparent circle under the slot), background disc, pilot picture, decorative ring, ability glow, the two stat boxes (stacked to the left of the pilot), lock icon and text, bonus badge and text, the status lines beside the pilot. State colours are on its `SlotView`. Upgrade it with Tools > Fun Game > Upgrade Pilot Slot Prefab (only adds missing parts). |
| `StatBox` | One stat box (used twice by every pilot slot): border, background, fill, number. Boost / drop number colours and the zero blink colours are on its `StatBoxView`. |
| `OperatorSlot` | One operator on the board (+, -, x, /): shadow, base and symbol. |
| `Connection` | One connection between slots and operators: a strip picture (`Strip`) lying on the board, stretched from end to end and tinted by role. Widths (normal / powered) and the opacity of an unpowered connection are on its `ConnectionView`. |
| `EnemyView` | One enemy: picture, name, health bar and text, intent line, queue line ("Enemy 1/2 next: ..."). |
| `ChainLabel` | The "ATK 5" / "DEF 3" text at the end of each chain. Stands upright on the tilted board (`FaceCamera`). |
| `Popup` | The floating texts (damage, "Surge!", "Charge broken!", ...). |
| `MenuOption` | One line of the click menu (Place, Eject, Use Surge, ...). |

## Assets

| Asset | In charge of |
|---|---|
| `Assets/Campaign/Tutorial.asset` | Every tutorial line: encounter, when it shows, text (see `TUTORIAL_LINES.md`). |
| `Assets/Rules/BattleVisuals.asset` | The battle's shared looks: which prefabs the battle uses, operator sprites, attack / defense / both / unused colours (lines, chain labels, stat texts all read these), and the "show remaining defense" switch. |
| `Assets/Scripts/Configurations/Pilots/*.asset` | Each pilot's picture (`Sprite`), its animation prefab (`Animation Prefab`; upgraded copies use their `Base Pilot`'s), name and stats. Upgraded test-deck copies live in `Pilots/Upgrades`. |
| `Assets/Encounters/World N/*.asset` | Each enemy's picture, **Scale** (1 = the standard size set on the `EnemyView` prefab), name, health and actions, each encounter's display name and its three **Stars** (conditions). |
| `Assets/Boards/World N/*.prefab` | Each encounter's board layout (slot positions; slots are `PilotSlot` / `OperatorSlot` instances, so their look comes from those prefabs). |
| `Assets/Art/PilotAnimations/<pilot>/` | Each pilot's animation prefab: its frames (`Sprite Frames`) and its legacy clips Idle (loops), Attack and Defeated (play once). The clips key the frame number (and anything else, such as scale or position); open the prefab and edit them in the Animation window, which previews the frames. |
| `Assets/Art/Placeholders/` | The placeholder pilot squares (orange, teal, crimson), the connection strip (`ConnectionStrip`) and the shadow circle (`Circle`, a copy of Unity's default circle). |

## Draw order (Order in Layer, all on the Default sorting layer)

| Order | What |
|---|---|
| -20 | Enemies, each as one group (`EnemyView` prefab, "Sorting Order" field; its parts keep their own order inside the group) |
| -10 | The battle scene's board background (between the enemies and the board; any value from -19 to -1 works) |
| -2 | Connections between slots (`Connection` prefab) |
| -1 | Slot and operator shadows |
| 1 to 11 | Pilot and operator slots (glow 1, background 2, pilot 3, ring 4, stat boxes 7 to 10, texts 11) |
| 9 | Chain value labels |
| 100 | Floating popups |
| (UI) | HUD, hand, menus, tooltip and tutorial panel: a screen-space canvas, always on top |

## Still set in code (temporary looks, to move into prefabs when they are restyled)

- Status line colours beside a pilot ("Surge ready", "SNIPED", "CRASHED", "next: LOCK"): `BoardView.DescribeEffects`.
- Popup texts and colours during a turn: `BattleSession.PlayEvents`.
- Intent colours for ASSASSINATE and STUNNED: `EnemyView.SetIntent`.
- The divide-by-zero gag animation: `SlotView.PlayDivideByZero`; the red "ERROR" chain label: `BoardView.Refresh`.
- The dark backing behind status lines: `SlotView.SetStatus`.
- Star dots on the end screen and the map (gold earned, grey missing; the font has no star glyph): `Campaign.StarText`.
- The hover explanation texts: `HoverTexts` (operators, bonuses, enemies) and `BoardView.DescribeSlot` (slots and pilots); the placement preview arrow on chain labels: `BoardView.Refresh`.
