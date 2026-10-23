# Handover for another Claude session

Give this file to a Claude Code session opened in this repository on another computer. `CLAUDE.md` is loaded automatically; this file adds what lived only in the first computer's Claude memory and where the work stopped (2026-10-23).

## Read first

1. `CLAUDE.md`: project, conventions, build/test tools.
2. `PROJECT_HANDOFF.md`, section **Start here**: full state, decisions, open questions. The newest notes are the paragraphs dated 2026-10-20 and 2026-10-21 at the top.
3. `VISUALS_GUIDE.md`: which prefab or asset owns each look.

## Setting up the laptop

- **Git LFS is required** (fonts, some textures and DLLs are LFS files): install Git LFS, run `git lfs install` once, then clone. Without it, those files are text pointers and Unity shows broken fonts and art.
- **Unity 6000.3.11f1** (the exact version; Unity Hub > Installs > Install Editor > Archive). The first project open re-imports everything (`Library/` is not in git): expect several minutes.
- **MCP for Unity** comes with the project (`com.coplaydev.unity-mcp` in `Packages/manifest.json`). In Unity: *Window > MCP for Unity*, then connect it to Claude Code there. Without the connection Claude can still edit code and run the standalone tests, but cannot compile in Unity, play-test or edit prefabs and assets.
- **Standalone tests** need nothing but the editor install: `Tools\TestRunner\run-tests.bat` (expected: **136/136 passed**). It looks for the Unity editor at its default Hub path; if the laptop installs Unity elsewhere, set `UNITY_DATA` to the editor's `Data` folder.

## How the user wants to work (from the first computer's Claude memory)

- **Composition over inheritance:** small single-purpose scripts; game rules in pure C# (`Assets/Scripts/Board`, no UnityEngine) so they are testable and the balance lab can use them.
- **One place per change:** every visual is a prefab used directly everywhere; changing a look touches one prefab or asset only; no colours or sizes duplicated in code; boards are variants of a base prefab.
- **Vocabulary:** AP (in-battle Action Points), SP (meta Skill Points), Board, Encounter, lane, chain, toll node, bonus node. Use them in code and docs.
- **Teach by construction:** each world teaches one topic with about three encounters (boss last); build encounters that demand the new mechanic and prove it with the lab ("does it need...?" check); a mechanic is switched off until its world teaches it.
- **Encounter workflow:** build it, run your own lab check, fix pacing, then hand it to the user to play. Never hand over an untested world.
- **Play before deciding:** rules about feel get tried hands-on in Unity; lab numbers inform, they do not decide.
- **Digestible results:** lab output as a readable HTML report with plain findings that state numbers and limits.
- **Player-facing text:** no pilot, ability or enemy names in tutorial or rules text unless the user asks (exception: Haze's No Guard in 4-1); never the internal word "wound" (say stats recover); no status texts for ability side effects unless asked; write "AP" and SP as icons (`Icons.ApIcon`, `Icons.SpIcon`; tutorial lines keep "AP" in words and the panel converts it).
- **Disable, don't ask:** anything testers should not do is removed from builds, not explained away (Unlock all and Refund upgrades are editor-only via `CampaignSession.TestingTools`).
- **Builds only when the user picks a state for testers.** Committing is fine without asking each time; commit only your own files unless the user says otherwise (the user often has unrelated art work in progress).
- **Time estimates:** give one before starting, built from the real waits (a code change with tests 1-3 min, a Unity recompile or Play-mode check about 1 min, a lab run 1-3 min). Earlier estimates were 3-6x too long. Say which parts need Unity.
- **Never touch Unity while the user is in Play mode** (check `EditorApplication.isPlaying` first).
- **Driving Play mode through MCP:** an unfocused editor does not advance frames. Pause and call `EditorApplication.Step()` per frame (`execute_code`); the first `execute_code` after entering Play mode often returns `success:false`, so run it again. `??` does not work on Unity objects from `GetComponent` in the editor; use `== null`.
- Pronouns: the user's are not stated; use they/them.

## Where the work stopped

- **First friends playtest build** made on 2026-10-21: `Builds/FunGame-2026-10-21.zip` (git-ignored, on the first computer only; contains `HOW TO PLAYTEST.md` = `PLAYTESTING.md`). Testers send back their zipped `PlaytestLogs` folder; per-battle files work again since that build (a "/" in the file name had broken them).
- **Small open choices:** the "Made with Unity" splash can be switched off for free in Unity 6 (*Project Settings > Player > Splash Image > Show Splash Screen*); not done yet. "Reset progress" is still available to testers.
- **Ideas to discuss before building:** tease that future pilots have abilities; a bar for the enemy's guard that shrinks as it absorbs hits; the user finds play defence-heavy for too long (wait for tester feedback); each world's enemies should match the pilots recruited there (the Fog monster textures for Haze's world). Parked rule ideas (Rest Up enemy move, Pilot Fusion, HP Up shop item) are in `PROJECT_HANDOFF.md`.
- **The rectangular pilot slot look** is switchable (Battle Visuals > Slot Shape); the user is still deciding.
- Next likely work: read the testers' logs and turn them into a readable report.
