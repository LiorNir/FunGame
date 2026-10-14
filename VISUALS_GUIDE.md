# Visuals guide: where each look lives

Everything the player sees is a **prefab** or an **asset**. Scripts only fill in values (texts, numbers, which parts are on), so a look is changed in one place and every screen that uses it follows. Edit these freely: the "Create ..." menus never overwrite an existing prefab.

## Scenes

| Scene | What it is |
|---|---|
| `Assets/Scenes/MapScene.unity` | The campaign menu: the map and the Pilots screen. First scene of the build. Holds a Canvas and a **Campaign Menu** object that places the two screen prefabs. |
| `Assets/Scenes/BattleScene.unity` | The battle. Started from the map it plays that encounter with your pilots; opened on its own it is the test bench (test panel, any encounter, test decks). Holds the **Battle Session**, the board root (move or scale it to move or scale the whole board) and the camera. |

## Campaign menu (`Assets/CampaignViews/Prefabs`)

| Prefab | In charge of |
|---|---|
| `MapScreen` | The whole map screen: background, "Skill points" text, the Pilots / Unlock all / Reset progress buttons, and the `Worlds` container (a vertical layout group) that the world rows are placed in. |
| `WorldRow` | One world: its name and the `Encounters` container (a horizontal layout group) that the encounter buttons are placed in. Name colours (open / locked) are on its `WorldRowView`. |
| `EncounterButton` | One encounter: background, title and footer line ("Reward: +1 SP, Spark joins" / "cleared" / "locked"). Background colours per state (open, cleared, locked) are on its `EncounterButtonView`. |
| `PilotsScreen` | The Pilots screen: background, "Skill points" text, Back to map button, and the `Cards` container (a grid layout group: cell size, spacing and columns are set there). |
| `PilotCard` | One pilot: level ("Lv 2"), picture, name, stats, ability text, +1 ATK / +1 DEF buttons, Refund button. The silhouette tint for pilots not recruited yet and the stat colours are on its `PilotCardView`. |

## Battle (`Assets/BattleViews/Prefabs`)

| Prefab | In charge of |
|---|---|
| `BattleHud` | The whole 2D battle UI: HP, AP and turn texts, the hint line, Play Turn, Undo and Give up buttons, the hand area, the action menu, the test panel, and the end screen (`Outcome`: dim background, result text, Play again and Back to map buttons). |
| `CardView` | One pilot card in the hand: background, picture, name, attack and defense bars (each with a fill, a blinking recovery preview and a label), the ability line and the gold frame. Bar colours for boosts and drops are on the bars (`CardStatBar`). |
| `PilotSlot` | One pilot slot on the board: background disc, pilot picture, decorative ring, ability glow, the two stat boxes, lock icon and text, bonus badge and text, the status lines beside the pilot. State colours are on its `SlotView`. Upgrade it with Tools > Fun Game > Upgrade Pilot Slot Prefab (only adds missing parts). |
| `StatBox` | One stat box (used twice by every pilot slot): border, background, fill, number. Boost / drop number colours and the zero blink colours are on its `StatBoxView`. |
| `OperatorSlot` | One operator on the board (+, -, x, /): base and symbol. |
| `EnemyView` | One enemy: picture, name, health bar and text, intent line, queue line ("Enemy 1/2 next: ..."). |
| `ChainLabel` | The "ATK 5" / "DEF 3" text at the end of each chain. |
| `Popup` | The floating texts (damage, "Surge!", "Charge broken!", ...). |
| `MenuOption` | One line of the click menu (Place, Eject, Use Surge, ...). |

## Assets

| Asset | In charge of |
|---|---|
| `Assets/Rules/BattleVisuals.asset` | The battle's shared looks: which prefabs the battle uses, operator sprites, attack / defense / both / unused colours (lines, chain labels, stat texts all read these), and the "show remaining defense" switch. |
| `Assets/Scripts/Configurations/Pilots/*.asset` | Each pilot's picture (`Sprite`), name and stats. Upgraded test-deck copies live in `Pilots/Upgrades`. |
| `Assets/Encounters/World N/*.asset` | Each enemy's picture, name, health and actions, and each encounter's display name. |
| `Assets/Boards/World N/*.prefab` | Each encounter's board layout (slot positions; slots are `PilotSlot` / `OperatorSlot` instances, so their look comes from those prefabs). |
| `Assets/Art/Placeholders/` | The placeholder pilot squares (orange, teal, crimson). |

## Still set in code (temporary looks, to move into prefabs when they are restyled)

- Status line colours beside a pilot ("Surge ready", "SNIPED", "CRASHED", "next: LOCK"): `BoardView.DescribeEffects`.
- Popup texts and colours during a turn: `BattleSession.PlayEvents`.
- Intent colours for ASSASSINATE and STUNNED: `EnemyView.SetIntent`.
- The divide-by-zero gag animation: `SlotView.PlayDivideByZero`; the red "ERROR" chain label: `BoardView.Refresh`.
- The dark backing behind status lines: `SlotView.SetStatus`.
