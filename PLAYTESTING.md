# Playtesting Fun Game

Thanks for trying it! This is an early prototype: the art is placeholder and some screens are rough. What we want to learn is whether the game is clear and fun to think about.

## Running it

- **Build (recommended):** unzip the `FunGame` folder anywhere and run `FunGame.exe` (Windows, 64-bit).
- **From Unity:** open the project with Unity 6000.3.11f1, open `Assets/Scenes/MapScene.unity` and press Play.

## Playing

- The map shows five worlds. Clear the encounters of a world in order; beating a world's boss opens the next world, gives a skill point (SP) and may recruit a new pilot.
- **Pilots** (button on the map): spend SP to give a pilot +1 attack or +1 defense. Each further +1 to the same stat of the same pilot costs 1 SP more.
- In a battle, click a card in your hand or a pilot on the board to see what you can do with it. Hover a slot to see more. **Undo** takes back your last action of the turn; **Give up** ends the battle.
- Each encounter has three stars (shown at the top right during a battle): clearing it, and two harder goals. Replaying for missed stars is optional.
- Please play the way you naturally would. There is no wrong way to play; confusion is useful information.

## Sending your results

The game writes a log of every battle and every map action as you play (nothing to switch on). On the map, press **Open playtest logs**: a folder opens with one `session-...` folder per time you started the game. Zip the `PlaytestLogs` folder and send it, together with any notes on what felt confusing, too easy, too hard or fun.

If the button does not work, the folder is `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Fun Game\PlaytestLogs` (paste it into the Explorer address bar).

The logs hold only game actions (which pilots you placed, what the enemies did); nothing personal. Your progress is saved separately; "Reset progress" on the map starts over.
