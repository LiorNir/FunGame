# Fun Game — Project Handoff

Last updated: 2026-10-09. This file has three parts: **start here** (where the work stopped and what comes next), **the design direction we have agreed on** (branching board, lanes, encounters, worlds that teach one topic at a time) and **what exists in code today**. The playable battle, the board authoring tools, the pure battle core and the balance lab are all built on the new direction; the old fixed-structure code under `Assets/Scripts/Controllers` and `Views` is legacy.

## Start here (state at 2026-10-09)

**Done and working** (all pure code verified with 91 EditMode tests, Unity-side code only compile-checked, see "Not yet verified in Unity"):
- Branching boards as prefabs, encounters as assets, the playable battle with an action menu, the pilot slot prefab with state-coloured ring/stat boxes (LerpColorShader), the pure battle engine, and the **balance lab** (`Tools/BalanceLab`): simulated players play encounters at different roster power and write an HTML report. World 1 (three sample encounters) and the "does it need this mechanic?" checks exist in the lab.
- Rules decided in this period: zero-value defense chains are destroyed by a hit that reaches them (divide-by-zero chains excluded for now); numbers are shown cut to two decimals (`DisplayFormat.Value`); encounters are independent (fresh HP and deck each time), first clears give SP, repeats give shop currency; abilities are one use per encounter and cost AP; replacing and ejecting pilots can be switched off per encounter (`BattleRules.AllowReplace/AllowEject`) and are off in World 1.

**Tools you can run** (all outside Unity, using the editor's own compiler and runtime; set `UNITY_DATA` if Unity is not at the default path):
- `Tools\TestRunner\run-tests.bat`: builds and runs the EditMode tests (`91/91 passed` expected).
- `bash Tools/check-unity-compile.sh`: compile-checks all Unity-side code (needs Library/ScriptAssemblies from a previous Unity compile). Prints only `check done` when clean.
- `Tools\BalanceLab\run-lab.bat report [--only "World 1"] [--runs N]`: the balance report (see `Tools/BalanceLab/README.md`). `... inspect "name" rung Bot seed count` prints single battles, `... ablate N "name"` shows win rates with and without a mechanic.

**Verified in Unity (2026-10-09, through MCP for Unity, which is now connected):** the 91 EditMode tests pass in Unity's Test Runner; *Export Encounters For Balance Lab* writes the three project encounters (Test1-3) to `Tools/BalanceLab/encounters` and the lab reads them (Test1 and Test2 are won even at power 0.2; Test3 needs power 0.8 for a 50% careful win and 1.2 for 90%).

**Not yet verified in Unity** (compiles, never run there): *Upgrade Pilot Slot Prefab*, the two new checkboxes on `EncounterDefinition`, and the playable battle after the engine changes (own random generator, split `ResolveCombat`, counters). Open the battle scene and play one encounter.

**Waiting on the user:**
1. Fill `Tools/BalanceLab/tiers.txt` (tier, intended power, world, position, teaches) for the real encounters.
2. Decisions: (b) Tier targets (careful/casual win rate, health left): Tutorial 97/85/80, Early 92/75/65, Mid 70/45/50, Late 55/30/35 are guesses. (c) Where divide and bonus slots are taught (proposal: a short interlude after World 1's boss and first upgrade, because dividing by a stat below 1 multiplies the chain). (d) Enemy abilities one per boss after the pilot tool they interact with, or all after the pilot topics. (e) The SP economy discussion (cost per upgrade, one or both stats, grinding) is still to be had.

**Lock (enemy effect, done 2026-10-10):** an enemy action can lock one active pilot for N of the player's turns; a locked pilot can be neither ejected nor replaced (a fallen one can still be cleared). The target is picked when the intent is revealed (rule per action: Random, MostWounded, HighestAttack, HighestDefense, Trunk = the pilot in the most chains) and marked above the pilot ("next: LOCK 2", then "LOCKED 2"); if the pilot has left the board when the enemy acts, the lock misses. In Unity: `LockTurns` and `LockTarget` on each `EnemyAction`; the pilot slot prefab got a `PilotStatusText` part (added by *Upgrade Pilot Slot Prefab*, run through MCP); the eject menu shows the reason it is blocked (`BattleEngine.EjectBlocker`). Lab: action lines take extra effect fields (`lock 2 Trunk`), the careful bot is penalised for a locked pilot in a defense chain the enemy can break (`ScoreWeights.LockedAtRisk`), and the "Does it need...?" table gained "reacting to locks" (careful bot vs one blind to locks) plus pilots-lost and health columns. Teaching sample: "Lock sample: Pinned down" (built-in).

**World 1 is playable in Unity (2026-10-10):** *Tools > Fun Game > Create Encounter Assets > World 1* (and *> Lock Sample*) turns the built-in lab encounters into a board prefab, enemy assets and an encounter asset each (`Assets/Boards/World 1`, `Assets/Encounters/World 1`, samples under `Samples`). It is a one-time import: the assets are the source from then on, and an existing encounter asset is never overwritten. Boards are built by `BoardPrefabFromGraph` (also used by *Create Sample Boards*), which spreads branches `BoardLayoutMetrics.BranchSideOffset` (2.5) to the sides so slots above the root row stand beside the enemy, as on the hand-made boards. The battle scene's encounter list starts with World 1, then the Lock sample, then Test1-3. Checked in Play mode through MCP: all three World 1 boards render, and the Lock sample marks its target. Exported back to the lab, the assets play like the code versions. The Unity starter deck (`Assets/Rules/StarterDeck.asset`) has 6 pilots: Osis 2/1, Sil 2/1, Quill 1/2, Yawn 1/1, Haze 1.5/0.5 and Pion 1/1 (the butterfly, whose asset file is `Squirrel.asset`; added 2026-10-10). The lab's `BuiltInEncounters.StarterRoster` is a copy of it with the same names, stats and order, so World 1 gives identical lab results from the code and from the Unity assets. Visual notes: the chain labels of a terminal right under the enemy overlap its health text (1-1), and the stat boxes under a filled root slot touch the hand cards.

**First hands-on playtest of World 1 (user, 2026-10-10):** the feel is solid for a first world; placing and attacking is easy, as intended. Problems and what was done:
- *Turns with nothing to do.* With replace and eject off, 1-1 and 1-2 had turns where the only action was pressing Play Turn. Enemy health lowered: Training dummy 8 -> 4 (1-1 is now won on turn 1 in about half the battles, when the hand holds both 2-attack pilots, otherwise turn 2), Scrapper 14 -> 7 (1-2 is won on turn 2 or 3). Changed in the Unity enemy assets and in `BuiltInEncounters`.
- *Player HP feels too safe.* The lab (new `--player-hp N` option of `run-lab.bat report`) shows that lowering HP from 20 to 10 hardly changes how safe World 1 is for a player who keeps up a defense (the careful bot ends the boss with 87-92% health either way, losing about 1.5 HP); HP only matters for a player who goes all-in on attack, as the user did. To make the boss feel dangerous for every style, its attack is the lever, not the HP pool. Not changed yet (GameRules HP is global).
- *Unused AP.* Lab data agrees: in World 1 the careful bot leaves 1.7-2.2 of its 3 AP unused per turn. The user is considering AP that refills by 1 per turn with a cap (saving as a strategy; abilities could trade AP between turns), but World 1 is meant to be basic, so decide with data from later worlds. Note: the bots never save AP on purpose (they judge one turn), so the lab cannot yet measure a carry-over rule fairly.
- *Fallen vs inactive looked alike.* A fallen pilot now has its own slot state (`SlotVisualState.Fallen`, ring colours `SlotTints._fallen`) and a darker picture (`_fallenColor` on the PilotSlot prefab, 0.15 instead of 0.35), so a dead pilot no longer looks like one that is only cut off.
- *Clearing the fallen (decided 2026-10-10, on trial, keep an eye on it):* clearing a fallen pilot while ejecting was not allowed read as inconsistent. Now a fallen pilot stays where it fell until a pilot is placed on top of it; that costs the normal place AP (not the replace cost), works even where replacing is off, and the fallen pilot leaves the battle (it does not go to the hand). The separate "Clear fallen" menu action is gone (clicking a fallen pilot says to place a pilot there), and the search bot no longer clears fallen pilots on its own. `BattleEngine.ClearFallen` still exists for code. World 1 lab results unchanged. Also open: a player may wonder why a stronger pilot in hand cannot replace a weaker one before replacing is taught (the user thinks this is minor).

**Second playtest of World 1 (user, 2026-10-10): "solid first draft".** 1-1 and 1-2 feel right now (one action-less turn is fine in a tutorial; tutorial texts may come later). The boss gives real choices and its order of events is a good lesson (place, absorb, defense reaches 0, chain breaks, replace the fallen root, win). Changes made: player health 20 -> 10 (`GameRules.asset` and the lab copy `BuiltInEncounters.GameRules`); the boss's subtract slot D2 was removed (no pilot could use it, since there are no negative stats; a toll only makes sense with something worth paying for behind it, so subtract comes back with bonus slots), keeping the defense branch short so it breaks; fallen pilots are now near black. Lab at HP 10: the boss is still always won, in 3.2-3.5 turns, careful bot 87% health left, 0.7-0.9 pilots lost per battle.

**World 2 plan (agreed 2026-10-10):**
- Topic: changing your board, replacing and ejecting (no abilities yet). Three encounters (three feels like the minimum for a world): 2-1 replacing (a weak or wounded pilot stuck in an important slot, a stronger one arrives), 2-2 ejecting (a telegraphed hit that would destroy a chain; pull the key pilot out; tests whether dodging has a cost at HP 10), 2-3 boss needing both plus one slot opened with AP. A good place to show an operator slot with two outputs (a branch at an operator, already supported). An operator with two *inputs* is a merge, which the pure model does not support yet (deferred in the design).
- Progression is emulated, not built: **an upgrade is +1 to one stat of one pilot** (user, 2026-10-10: players should be able to specialise pilots, so no raising everything). World 2 is played with a test deck: the starter pilots with the first upgrade applied, plus the World 1 boss's reward pilot **Spark** (3 ATK / 0.5 DEF, strong but fragile, so you want it attacking but out of big hits; `Assets/Scripts/Configurations/Pilots/Spark.asset`, placeholder sprite `Assets/Art/Placeholders/PilotPlaceholder.png`). `EncounterDefinition` has an optional **Test Deck** (used by `BattleSetupBuilder`, so by the battle, the simulator window and the lab export); until progression exists it stands in for "the pilots you have by now". Which single upgrade the test deck assumes is decided while building World 2 (the lab can compare the possible ones).

**World 2 first draft (built 2026-10-10, waiting on the user's playtest).** Menu *Create Encounter Assets > World 2*; assets in `Assets/Encounters/World 2`, boards in `Assets/Boards/World 2`, test deck `Assets/Rules/World 2 Deck.asset` (starter pilots with Quill upgraded to 1/3, plus Spark; the upgraded Quill is `Pilots/Upgrades/Quill (1-3).asset`, made from the lab roster by `DeckFromRoster`). In the battle scene's list after World 1. Lab copy: `BuiltInEncounters.WorldTwo()` and `WorldTwoRoster`.
- **2-1 Fresh legs** (replacing; ejecting off): root shared by a short attack and a short defense branch; two Grinders in a row (9 and 10 HP) that grind 3 every turn. Replacing worn pilots keeps the defense up. Careful bot: 100% wins, 88% health, 1.0 pilots lost; without replacing 60% health and 3.3 lost (casual: 100% -> 62% wins).
- **2-2 Duck!** (ejecting): attack and defense are separate trees; the Crusher (20 HP) jabs 1 and once crushes for 6, more than any defense chain holds. Ejecting the defense root leaves the defenders behind a gap, so they are not hit and the player takes the damage instead: a choice of health against pilots, not a must for winning. Careful bot: 1.0 pilots lost and 65% health with ejecting, 1.9 lost and 69% without; the casual bot, which never ejects, loses 3.0.
- **2-3 The foreman (boss)**: the root feeds both branches through **one operator with two outputs** (the board builder now groups branches that leave a slot through the same operation); multiply on the attack branch; the defense branch's second slot opens for 1 AP. Foreman 30 HP: grind 3, crush 7, brace 1/3. Careful 100% wins with 41% health, casual 81%; without replacing casual drops to 24%. **The AP slot is not worth buying for the bots** (they do as well with it shut; casual better): check in the playtest whether it reads as a choice or should go.
- New rule **hand recovery**: a pilot resting in the hand gets back 0.5 of its full defense per turn (`GameRules.HandRecoveryPerTurn`, lab copy `BuiltInEncounters.GameRules`). It was in the design but never built; without it replacing and ejecting barely mattered (a swapped-out pilot came back just as worn). No effect on World 1, where pilots never return to the hand.
- **Careful bot changed:** `ScoreWeights.PilotLost` 30 -> 15. At 30 it let itself die rather than risk a pilot (with HP 10, doing nothing for 3 turns while taking hits); World 1 results barely move (boss: 87% health, 0.8 pilots lost).
- The report's "Needs" column judges by win rate only, so it shows "nothing special" for World 2, whose lessons show in health and pilots lost; use `run-lab.bat ablate N "World 2"` (now shows health, pilots lost, turns, idle AP, replaces and ejects with each mechanic taken away).

**First playtest of World 2 (user, 2026-10-10):** "felt good"; 2-3 was the most tense and interesting encounter so far.
- *2-1:* fun, plenty to do, but maybe too long for this point, and a second enemy appearing came as a surprise (maybe too early for a two-enemy wave). Replacing (placing onto an occupied slot) is correct here; ejecting (taking a pilot back without placing another) is what is off.
- *2-2:* the user did not think of ejecting: they softened the crush with two defense pilots. Turn 3 had nothing to do (two attacks were enough to win), which felt flat. The ejecting lesson needs either a tutorial pointer or a construction where ducking is clearly the better play.
- *2-3:* first real sense of danger (5 HP left after the crush). Not knowing the enemy's moves made it tense in a good way: a player can struggle the first time and play better once the moveset is known (later: adapt the deck with specialised pilots or matching abilities). The AP slot was bought once with a spare AP.
- *Attack is trivial:* the game revolves around the defense line-up; for attack the only decision is "strongest pilots, and swap the shared root". Snipe, Swap and attack-side choices (operators that make order matter, enemy effects on attackers) should change that.

**World 2 after its playtest (2026-10-10):**
- *2-1 Fresh legs* stays as it is (the user liked it; it is also a candidate for introducing waves if 2-1 changes later).
- *2-2 Duck!* rebuilt to reward ducking: attack and defense trees of two slots each; the Crusher (8 HP: jab 1, **crush 5**, jab 1) is followed by the Hammer (15 HP, hits 4 every turn), which needs the best defender (Quill). Ducking the crush keeps Quill alive and resting.
- *2-3 boss*: the third defense slot is gone; the defense is the root plus one slot (D1) that costs 1 AP to open (user: a stronger defense would remove the boss's tension).
- **Finding: under the current rules ducking never saves health.** Ducking (ejecting a defense root) costs the whole hit; letting the chain break costs only what it cannot absorb, plus its pilots. With 7 pilots and short fights, pilots are cheap, and replacing the root with a cheap pilot beats ejecting it. A 5-pilot deck did not change this. Lab, rule as it is: careful bot ducks once in 2-2, loses 0.5 pilots, 50% health; casual (never ejects) 98% wins, 80% health, 2 pilots lost.
- **Rejected (user, 2026-10-11): "a broken chain absorbs nothing".** It made ducking the clear answer but was too drastic a change (2-1 and the boss got much harder); removed from the code.
- **Ejected pilots rest faster (decided 2026-10-11, on trial):** a pilot that is ejected and rests in the hand for the whole turn recovers `GameRules.EjectedRecoveryMultiplier` (2) times as fast on its first turn of rest; with the 0.5 rate it comes back fully healed next turn, where a replaced pilot needs two turns. Placing it again the same turn (to move it to another chain) is allowed but gives no bonus, so there is nothing to abuse and no "cannot redeploy" rule is needed. It fits the costs: replacing is 1 AP, ejecting and then placing is 2, and the extra AP buys the head start. The eject menu says "rests x2" for a wounded pilot, and while the bonus is pending the pilot's hand card shows a green "rest x2" on its defense bar (temporary look; `CardView._restBonusColor`), which goes away when the bonus is applied or the pilot is placed again. Note for playtests: ejecting needs a free hand slot, and the hand refills to 4 every turn, so a pilot has to be placed before one can be ejected. Lab (World 2): results unchanged (wins, health, pilots lost), but the careful bot ejects more and replaces less in 2-2 (1.5 ejects vs 1.0) and the gambler starts ejecting (0.5 vs 0). The bots now count a wounded pilot in the hand by the wound it will have after the next turn's recovery (`BattleEngine.WoundAfterRest`); before, hand pilots were not counted at all, so the lab could not see any recovery rule.
- Other uses of ejecting to look for: moving a pilot to another chain (rare and hard to force), reacting to Snipe and Swap (later).
- *Updating drafts:* `BuiltInEncounterAssetCreator.Create(..., update: true)` (code only, not on a menu) rewrites existing encounter assets, boards and enemies from the code version, keeping their paths. Only for drafts the user has not edited.

**Second playtest of World 2 (user, 2026-10-11): "good encounters"; the card stat bars look nice.**
- *2-1:* a different hand gave a different, interesting fight; the user felt an incentive to swap (for a bigger value). Key moment, turn 3: with the first enemy about to die and a second coming, ejecting the worn defenders, placing fodder and attacking for the kill would let them recover over the wave change. **The wave transition may be the natural place to teach ejecting** (ejecting is off in 2-1 now).
- *2-2:* Quill at 2 of 3 defense heals fully whether swapped or ejected, so the eject bonus only matters for badly worn pilots. The user ejected with a free hand slot made first, and once had more useful actions than AP (ejecting both defenders and placing two new ones), which is a good sign.
- *2-3:* a nice attack-side plan for once (swapping a 1.5 attacker for a 2, then reusing the 1.5 in the root for the kill). Quill came back at 1.5, not full: the user placed a pilot straight onto Quill's slot (a replace, normal rest) rather than ejecting first. Turn-1 split asked about: in 300 lab battles per bot neither bot opens the 1 AP slot on turn 1, and having Quill in the opening hand barely matters (about 4 HP left either way, all wins). Roster note: apart from the upgraded Quill there is no 2-defense pilot, so blocking the boss's first hit depends on drawing Quill.
- *"Guaranteed win in two turns"* comes up again and again: once the kill is certain, nothing pushes the player to play well. Worlds were first meant as gauntlets with HP carried over partly for this reason. Ideas: the post-battle score (currency for pilots kept, HP kept, actions used) has to be worth the player's while; optional challenges per stage, or per boss, with a set reward ("finish with X HP or more", "no defense chain breaks") (maybe too early).
- *Odd remaining values:* wounds spread proportionally leave defenses like 0.17 or 0.07, which are ugly to read and hard to reason with. It comes from the exact proportional wound model; to discuss (options: display only, rounding wounds to steps, or a minimum below which a pilot counts as 0).
- *Still defense-centred*; noted again.
- UI added after this playtest: the hand card's defense bar shows a semi-transparent green preview of where the defense will be after the next turn's recovery (`CardStatBar._preview`, added to the prefab by *Add Missing Parts To Battle Prefabs*); with the eject bonus it reaches further.

**Watch list (decisions on trial, revisit with data or playtests):**
- *Hand recovery 0.5 per turn:* may make one pilot with far more defense than the others (an over-tuned specialist) the best answer in nearly every defensive situation: block, rest two turns, block again.
- *The AP-paid slot:* the bots gain nothing from it in 2-3, and the casual bot does better without it. A red flag; the user is sure it can work. Likely reasons: AP is rarely scarce (1-2 of 3 AP go unused per turn in Worlds 1-2), so its cost means little, and one more defense slot adds little. Ideas: a slot whose position makes it strong (completing a multiply, a bonus slot later), longer encounters (more HP, two lanes, several waves) where a permanent extra slot pays back, or making AP scarcer (see AP carry-over).
- *Placing onto a fallen pilot clears it* (2026-10-10).
- *A hit equal to a chain's value leaves it at 0* (2026-10-09).

**Ideas parked (user, 2026-10-10):**
- *Post-battle score for currency (shop money):* points from mechanics used (pilots placed, ejected, abilities activated...) minus penalties (pilots lost, HP lost...). To stop grinding weak levels, a multiplier that shrinks the reward more the further the player has progressed past that world.
- *Enemy moveset knowledge as progression:* the first fight against an enemy is played blind; later fights benefit from knowing its moves.

**UI from the playtests (done 2026-10-10, not yet seen by the user):**
- Hand cards show attack and defense as bars (`CardStatBar`, parts AttackBar/DefenseBar on the CardView prefab) instead of the "(of 3)" text; a recovering pilot's defense bar is part full and slides at the turn change. The bar shows the share left, not the maximum value. Without the bars the card falls back to its Stats text.
- Enemies in a lane with several enemies show "Enemy 1/2   next: Big grinder" under their health (`QueueText` on the EnemyView prefab); a lane with one enemy shows nothing.
- Both parts were added to the existing prefabs by *Tools > Fun Game > Add Missing Parts To Battle Prefabs* (only missing parts are added) and are part of *Create Battle Prefabs* too.

**Tutorial notes** (things a real tutorial or UI pointer must teach, collected from the playtests):
- World 1: place a pilot and attack; a chain only counts up to its first gap (prefix rule); a hit equal to a defense chain's value leaves it standing at 0, and the next hit breaks it; a broken chain's pilots fall, and the fallen block the chain until a pilot is placed on top of them; a chain at 0 under multiply is not revived by swapping one pilot.
- World 2: a new action, replacing (placing a pilot onto an occupied slot); a replaced or ejected pilot recovers in the hand; ejecting to duck a big hit (pilots behind a gap are not hit, the player takes the damage); more enemies may follow the first one; slots can be bought with AP.
- Enemies: intents show only the next move; telegraphed targets (Lock) can be dodged by moving the pilot.

**Design notes from the user (2026-10-10):**
- **Dodging by ejecting is fine, but taking a big hit to the face must have consequences.** Levers to balance it: player HP, pilot defense values, what counts as a "big hit", or a mix; also a limit on ejects per turn or a higher eject cost. Needs thought and data.
- **AP carry-over (idea, only if data shows it):** if battles often end turns with unused AP, consider letting AP carry over with a smaller refill per turn (1 or 2 instead of a full 3) and a cap (saving 3 does not give 4). The lab already records unused AP per battle (`avg_ap_unused` in the csv); decide from that.
- **No multi-turn look-ahead for the bot:** a real player is not meant to plan several turns ahead, so a bot that does would give unrealistic predictions. The bot's problem in the Lock sample is how it weighs pilots against health, not its horizon.

**Lab finding (2026-10-10), not resolved:** in the Lock sample the lock-blind careful bot does better than the reacting one (81% vs 42% wins, 0.6 vs 1.1 pilots lost). The log shows why: before every big hit the careful bot ejects its whole board to save its pilots, deals no damage that turn and loses on health; a lock stops that, so it helps the bot. The Gambler profile wins 100%. So the careful bot (one-turn horizon, PilotLost weight 30) over-protects pilots, and "Does it need...?" verdicts for enemy mechanics cannot be trusted until the careful bot weighs pilots against health better (not by looking further ahead, see the design notes). Design question raised by it: ejecting a whole board to dodge a telegraphed hit (1 AP per pilot, plus re-placing) is a strong defensive move; is that intended?

**Next steps for Claude (agreed order):**
0. ~~Let the user play World 1 in Unity~~ (done, two playtests; World 1 is a solid first draft). ~~Build World 2~~ (first draft done, see above); next: the user's World 2 playtest.
1. Stage 2 of the lab, one mechanic at a time, each with unit tests, a test encounter that *demands* it, a lab check, and a bot decision for it: ~~Lock~~ (done, see above; its teaching sample waits on a better bot), fix how the careful bot weighs pilots against health (it over-protects pilots; no multi-turn look-ahead, see the design notes), **Snipe** (telegraphed, player reacts by ejecting), **abilities** (one use per encounter, cost AP; the bot decides *when* through timing styles, not multi-turn look-ahead), then Swap and random effects. Abilities list from the user: simple buff X for N turns; heal X defense on a pilot; discard X and draw X; +stats then self-lock until it dies; big buff that decays each turn; swap this pilot with another on the board; random stat change or random swap.
2. World 2 (pilot tools: replace, eject, AP unlock, first ability from the World 1 boss pilot) built the same way as World 1; then the interlude (bonus slot, divide).
3. Stage 3 content (pilot archetypes with a power budget, about 30 encounters across 3 world tiers, deck cap and deck choice) and Stage 4 economy lab (see "Balance lab" below).
4. Pilot **library screen** (user idea, 2026-10-09): shows every pilot in the game; pilots the player owns are shown in full with some info, pilots not owned yet as a silhouette. Fits the recruit-after-boss and shop rewards.

## Project

- **Engine:** Unity 6.3 (`6000.3.11f1`), URP 2D.
- **Scenes:** `Assets/Scenes/TestScene2.unity` is the old greybox for the abandoned fixed-structure design and should be ignored for layout sizing. New board work uses `Assets/Scenes/BoardTestScene.unity` (menu `Tools/Fun Game/Create Board Test Scene`): camera orthographic size 5.4 at 1920x1080, so 1 world unit = 100 px, screen 19.2 x 10.8 units. Reference zones (enemy lanes, board band 16.4 x 4.64 centred at (0,-0.27), hand, summoner, AP badge, Play Turn) come from `BoardScreenMetrics` / `ScreenZonesGuide`. Slot sizes are not stored anywhere in code: they are read from the `PilotSlot` / `OperatorSlot` prefabs (the size of `PilotBackground` on the pilot slot, `Base` on the operator slot, through `ISlotFootprint`), and the default distances in the authoring tools are built from them (`BoardLayoutMetrics`).
- **Libraries in use:** TextMeshPro, Lean Touch, DOTween, UniTask, Zenject.
- **Scripting approach:** composition over inheritance. Small scripts with one responsibility, reusable where possible. Game rules should live in pure C# classes (no `MonoBehaviour`) so they can be unit-tested and used by the solver tool.
- **Reference art:** `ReferenceImageBrief.md` (brief for an image-generating AI) and the mock images the user generated from it (not in the repo).

## Working principles (from the user)

- **One place per change.** Every part that needs graphics or adjustment is a prefab, and a change to it must touch only that prefab: no second copy, no matching editor-only object, no duplicate numbers in code or in another asset. Dynamic things are created from prefabs, never built in code. Scene positions (board root, enemy lane anchors, player anchor) are plain objects you move by hand.
- **Variants.** Where things come in variations (boards especially), build them as prefab variants of a base prefab so a base change reaches all variants; only differences are stored in a variant.
- Composition over inheritance; small single-purpose scripts; game rules in pure C# (see CLAUDE.md).

### Where each look/number lives (one source each)

- **Slot size and shape:** the `PilotSlot` / `OperatorSlot` prefabs. For the pilot slot scale `PilotBackground` (for the operator slot, its "Base" child); gizmos, the Scene "+" buttons, the click collider and the default spacing follow.
- **Pilot slot parts:** defined once in `PilotSlotParts` (editor), used both to build a fresh prefab and to upgrade an existing one (Tools > Fun Game > Upgrade Pilot Slot Prefab; only missing parts are added, your edits are kept). Parts: PilotBackground, Pilot, Ring (decorative, lerp-colour material, coloured by state), StatsBox (AttackBox and DefenseBox, two instances of the `StatBox` prefab: border, background, fill, number; change the look once in `StatBox.prefab`; a stat is "used" when the slot is in a chain of that role (`RoleFlags` through the slot): a used stat's box is filled (attack always full, defense shrinks from the right as it is wounded, replacing the old wound bar), an unused stat has no fill and a darkened border and number via `SlotTints._unusedBrightness`), PilotNameText (fallback for pilots without a picture), LockIcon + LockText, BonusBadge + BonusText. All parts except the core five are optional for `SlotView`.
- **Slot colours:** resting background colour = the `PilotBackground` sprite colour, bonus text = the `BonusText` colour; the valid-target highlight, locked, inactive and fallen tints = fields on `SlotView`. All on the prefab. There is no INACTIVE label: an unpowered pilot is shown greyed (pilot, wound bar, numbers, bonus).
- **Ring and stat-border colours (LerpColorShader):** the shader blends `_DarkColor` to `_BrightColor` by the black-and-white picture on the material (`_BaseMap`). `SlotView` sets both per renderer through `LerpColorTint` (a `MaterialPropertyBlock`, so slots share the materials). Which colours for which state is `SlotTints` on the `SlotView` (prefab): fixed pairs for Locked / Inactive (a fallen pilot counts as Inactive); Active = the chain's colour from `BattleVisuals` (ring: attack / defense / both / unused by the chains through the slot; attack border: attack colour; defense border: defense colour) with the dark side derived (`_activeDarkShade`); Empty = the same chain colour dimmed (`_emptyBrightness`, `_emptyDarkShade`) as a hint of which chain the slot feeds. Each stat box is a `StatBoxView` (border + number) on the `StatBox` prefab.
- **Attack / defense / both / unused colours:** `BattleVisuals` only. Lines, chain labels, wound bars, texts and the Scene view gizmos all read them.
- **Spacing gaps:** the three gap constants in `BoardLayoutMetrics`; every distance is derived from the prefab radii plus a gap.
- **Board variations:** real prefab variants, made with "Create variant of this board" on the layout root (or Assets > Create > Fun Game > Variant of Selected Board). All code that makes variants goes through `BoardVariantCreator` (inspector button, sample boards, Convert Old Boards).

## Glossary (agreed vocabulary)

| Term | Meaning |
| --- | --- |
| **AP** | Action Points. In-battle only. Spent on placing a pilot, replacing a pilot, activating an ability, unlocking a slot. |
| **SP** | Skill Points. Meta-progression between battles: raise a pilot's stats, unlock or upgrade abilities. Cost matches the value gained. Other currencies (money, potions) are deferred. |
| **Pilot** | An animal token with an ATK and a DEF value. Placed into pilot slots. |
| **Board** | One authored prefab that defines a whole board: pilot slots, operator slots (each holding its operator), bonuses, locking, connections, lanes and chain endpoints. Variations are prefab variants. (Replaces the earlier Layout + Setup split.) |
| **Encounter** | One asset holding everything needed to run a battle: layout, setup, lanes, enemy queues, player rules, rewards. The player's pilot deck is separate. |
| **Lane** | An enemy position on the board with its enemy queue and its chains. Max two lanes. |
| **Chain** | The path from a root slot to a terminal slot, of role Attack or Defense, belonging to one lane. |
| **Root** | The slot nearest the player where a chain starts. |
| **Terminal** | The last slot of a chain, nearest the enemy. Just a normal slot, but its role and lane are fixed in the layout. |
| **Toll node** | A connection whose operator is `−` or `÷` (usually detrimental). |
| **Bonus node** | A pilot slot with an added effect, e.g. `+N` or `×N` applied to the accumulated value. |

## Design direction (agreed)

### Core idea

Operators are no longer something the player places. They are part of the **board** for a battle and fixed by the board prefab. The player decides **which pilots go in which slots** (and which slots to unlock) to get the best values from that battle's math. Each battle is a small puzzle. Because operators are fixed, all four (`+ − × ÷`) have a purpose: − and ÷ are tolls, and bonus nodes are the payoff for crossing them.

The earlier "operators are scarce cards in hand" idea is dropped. The hand holds **pilots only**.

### Board model

- The board is a **tree of slots** flowing outward from roots (nearest the player) to terminals (nearest the enemies), with an operator on each connection. Branching (a slot feeding several children) is allowed. A hub shared by two lanes is allowed.
- **Locking rule:** the first slot of a chain (a root, nothing flowing into it) can never be locked (a validation error).
- **Merges** (two parents into one node, needing a binary operator with ordered inputs) are valid but deferred until after the tree version works. If one input of a merge is empty, the junction passes the filled side through.
- Every operator must have a pilot slot downstream or be a merge. An operator with nothing to combine is invalid.
- Layouts are authored to be readable. Extra UI (flow direction, previews) reinforces readability rather than fixing random-looking boards.
- Rendered flat with a camera or art angle for the look. Note `TokenBaseView` projects dragging at a hardcoded distance of 10; a tilted camera needs proper ray-to-plane projection.

### Evaluation rules

- A pilot passes on **both** of its stats. Each chain reads the stat its role needs (Attack chain reads ATK, Defense chain reads DEF), evaluated per path root → terminal.
- Flow direction fixes operand order: `acc = acc OP pilotStat`, accumulated upstream value on the left. So `−` and `÷` are unambiguous.
- **Empty rule (prefix rule):** evaluation starts at the root and **stops at the first gap**. A chain's value is the accumulation at the last unbroken slot on its path. The root slot is mandatory (empty root means 0). Pilots behind a gap are dormant (inactive, take no damage) and need a visible dormant state. Locked slots and fallen pilots behave like gaps.
- `÷ 0`: see "Divide by zero" below. `− 0` is a free pass.
- **Zero-value defense chain (decided 2026-10-04):** zero stats stay allowed (low and zero-stat pilots are useful cheap tolls). A defense chain whose value is 0 cannot absorb anything, so an enemy attack that *reaches* it destroys it (its pilots fall) and passes on in full to the next chain or the player. If an earlier chain absorbs the whole attack, the zero chain is not reached and survives. This replaces the old skip that left pilots in a zero chain unhurtable. A chain with no pilots has nothing to lose. **Exactly-equal hit (decided 2026-10-09, on trial):** a hit equal to a chain's whole value leaves the chain standing at 0 with its contributing pilots fully wounded; only a hit above its value destroys it, and the next hit that reaches a chain at 0 destroys it through the rule above. The player has to be taught this. Consequence to watch: a chain at 0 can be brought back by replacing a single pilot when its operators add (0 + fresh pilot), but not when they multiply (a fully wounded pilot keeps a × chain at 0), whereas a destroyed chain has to be rebuilt from the root. Lab effect (World 1, careful player, power 1.0): pilots lost per battle fell from 0.57 to 0.09 on step 2 and from 1.48 to 0.81 on the boss. Tests: `BattleTests` "a hit equal to a chain's whole value". **Exception for now:** a chain that is 0 because of a divide by zero is still left alone (undecided, see below). Tests: `BattleTests` "chains worth zero".
- Bonus nodes apply after the slot's operator, only when the slot is filled and contiguous. Bonuses apply to the chain that passes through that node.
- Shared pilots (feeding both an attack and a defense chain) are intentional risk and part of the gameplay.
- **A chain at 0 under multiply (user, 2026-10-10):** replacing one pilot does not revive a × chain at 0, because a fully wounded pilot keeps the product at 0. The user likes this as a source of decisions; keep it.

### Divide by zero (joke feature, undecided details)

Dividing by 0 will get a deliberately comic punishment. Direction: the chain value shows `ERROR` or similar and counts as 0 for that turn, with a slapstick animation (e.g. the pilot in the divisor slot gets sucked into a tiny black hole). The drag preview must warn before it happens so it is a gag, not an unfair trap. This makes a 0-stat pilot perfect for `−` slots but forbidden in `÷` slots.

### Defense: per-pilot wounds (replaces the earlier "chain debt" idea)

- Damage is stored **on each pilot** as a wound fraction, not on the chain. A pilot's effective DEF = base DEF × (1 − wound).
- Example (all Add): pilots 3, 5, 2 → chain value 10. An attack for 5 leaves 5. Every pilot loses 50% of its DEF → 1.5, 2.5, 1. The bars show exactly this.
- A chain's remaining defense is the chain evaluated with the pilots' **effective** stats. When it reaches 0, **every pilot in the chain dies** at once.
- **Allocation rule (needed for non-Add operators):** every *contributing* pilot loses the same fraction `w`, found so the chain loses exactly the damage taken (closed form `w = damage / value` for Add; for other operators solve numerically, e.g. bisection). Example: `3 × 5 × 2 = 30`, 15 damage → each pilot loses ~20.6%, not 50%, so remaining value is 15.
- **Toll pilots are exempt from wounds:** a pilot whose reduction would *raise* the chain value (a divisor, a subtrahend) takes no wounds, and shows no bar. It still dies with the chain. Equal scaling leaves `a ÷ b` unchanged, so ÷ chains could not be damaged otherwise.
- Wounds live on the pilot, so they travel with it: replacing a wounded pilot with a fresh one restores that pilot's share, ejecting returns a wounded pilot to the hand where it can recover per turn, and pilots behind a gap take no damage (breaking a chain can protect a pilot).
- Enemy attacks resolve in a fixed order, **left to right across lanes**. A pilot lost to one lane's hit shortens shared trunks for the next.
- The solver tool must verify that chain value is monotonic in the uniform fraction for contributing pilots.

### Pilot stat modifiers (planned)

Effective ATK and DEF come from a runtime state per pilot: base stat plus modifiers (wounds, temporary ability effects such as "×2 attack this turn, then halve it", enemy debuffs). The evaluator reads effective stats only. ATK has no way to be lost yet except through abilities and enemy actions.

Pilot state also carries **status flags** that are not stats. First one: **Lock**, which stops a pilot from being ejected **or replaced** for X turns (decided 2026-10-10; implemented, see "Start here"). It can come from an enemy action (any active pilot can be targeted), or be the negative cost of a pilot ability. Open: does Lock also block using the pilot's ability? Fallen pilots must always be clearable for 0 AP, even if they were locked, or a dead pilot could jam a chain permanently.

### Enemy intents are fully telegraphed

Every enemy effect is shown to the player before the player acts, including **which pilots** are targeted by Snipe, Lock or any future pilot-specific effect, so the player can react (e.g. eject or replace a near-death pilot that is about to be locked). Consequences:
- Any randomness (random action choice, random targets) is resolved when the intent is revealed, never during resolution.
- Targets are chosen at reveal and stored in the intent. Intents target a **pilot**, not a slot. If that pilot has left the board by resolution, the effect fizzles (simple and readable; "re-aiming" enemies can be a later enemy trait).
- Target pilots get a clear on-board marker (crosshair, lock icon), and the enemy's intent box lists each effect.
- Data shape (composition): an enemy action is a list of effects (Attack, Defend, Snipe, Lock, ...), each with a target rule (e.g. strongest ATK pilot, most wounded, trunk pilot, random) that is evaluated at reveal.
- Reacting costs AP, which creates the tension between building the board and dodging.

### Enemy action types

Besides attack and defend, the enemy will get **Snipe**: it directly reduces values or applies another effect (such as Lock) to specific pilots on the board. A hit on a trunk pilot is severe because of the prefix rule, so snipes need to be previewed in the enemy intent like other actions.

### Pilot death

- A dead pilot is **gone from the battle** (first thing to try; creates stakes and pushes the player to use more of their roster).
- Open: should a dead pilot leave the board automatically, or stay as a "fallen" pilot (possible hooks: revive for half values) until cleared? Decided (2026-10-10, on trial): it stays, and **placing a pilot on top of it clears it** at the normal place cost (no separate clear action), so it is never punishing. A fallen pilot breaks the chain like a gap, so clearing it matters. A "clear all fallen" shortcut will probably be needed.
- Ejecting a living pilot returns it to the hand, where it recovers some of its wound each turn it stays there (amount to be tuned). This works because wounds live on the pilot.

### Lanes, enemies and waves

- Up to **two lanes**. A lane has an enemy position, an enemy **queue**, and its chains (at least one Attack and one Defense). The lane's attack chain value hits that lane's enemy. That enemy's attack hits that lane's defense chain.
- When a lane's enemy dies, the next in its queue spawns in that lane at the start of the next turn.
- **Slot unlocking:** locked slots can unlock when an enemy of the queue arrives (wave), at the start of a given turn of the battle (usually turn 2 or 3), or by the player paying AP; any condition unlocks it. Only **pilot** slots (not operators). Unlocking affects that encounter's board only.
- Idle finals (a lane whose enemy is dead and queue is empty): decision deferred.
- Enemy intent (next attack/defense) should be shown per enemy.

### Player interaction: contextual actions (agreed direction)

Selecting something in play (a card in hand, a pilot on the board, an operator slot...) opens a small **contextual action menu** listing what that thing can do right now, in the style of card games like Yu-Gi-Oh: e.g. a hand pilot offers Place, Discard, Activate ability (where allowed), Show details; a pilot on the board offers Eject, Activate ability, Show details. Choosing Place highlights every vacant, legal slot and a click on one places the pilot. Design consequences:
- Every player action is a **command** (PlacePilot, DiscardPilot, EjectPilot, ActivateAbility, UnlockSlot, ...). The menu is a view over "which commands are available for this thing", computed by a pure rules layer, so the same list drives AP checking: unaffordable options are shown disabled with their cost and reason instead of silently failing.
- Drag and drop can stay as an optional shortcut that issues the same commands. Click-based flow also works for touch without drag precision.

### Hand limit and discarding (open, with a recommendation)

- A **hand size limit** is wanted. Returning a wounded pilot to the hand (to recover) needs a free hand slot, so holding it costs something and the player may have to discard another pilot.
- Discarding is a possible mechanic. Choose one: (a) discard is free and you draw only at the start of the next turn, or (b) discard + draw is an AP-costing action. **Recommendation: (a)**, with the "discard and draw now for AP" idea kept as a possible ability later.
- Open: does a discarded pilot go to the bottom of the bank (recycled) or is it gone for the battle? Pilot death already means "gone", so recycling is the suggestion for discard.

### Ideas parked for later

- Enemy/ability effect **Swap**: swaps the places of two pilots on the board (random or deliberate), telegraphed in advance like every other intent.
- An ability that **changes an operator** on the board, possibly as an interruption.
- A **procedural layout generator** (editor tool, possibly driven by a Claude skill that writes its parameters) that builds board prefabs within the board bounds from guidelines: slot count range, branching, lock chance, operator weights. The validator rules double as the constraints (for example the first slot of a chain can never be locked), and the solver gates results by par value.

### Pilots, SP and progression

- Between battles the player spends **SP** to raise a pilot's stats or unlock/upgrade abilities. Stats are meant to grow well beyond the current 0–2 range.
- Growth makes Multiply dominate Add under fixed budgets, so keep a cap or steep cost curve per stat. Low/zero-stat pilots keep value as cheap tolls.
- Open: special abilities, special-effect nodes, and how pilot ability costs AP.
- **HP between encounters (decided 2026-10-06):** HP is fresh (max) at the start of every encounter, because encounters are independent (see below). Healing still matters inside a long encounter.

### Game structure and economy (agreed direction, 2026-10-06; numbers not set)

- **Independent encounters.** Each encounter stands alone: HP and the deck start fresh, and one clear is a win for it. Rewards from a first clear are kept. The player may replay any encounter. A stressful encounter is made with many waves (attrition inside one battle), not by chaining encounters into a single run. This keeps encounters individually measurable and removes mid-run shops and run-loss rules.
- **Worlds.** A world is a set of encounters ending in a boss. Clearing the previous world (not necessarily in one go) unlocks the next. After the boss the player recruits at least one pilot (more in larger worlds); a world's grand prize (probably a pilot) is for clearing it.
- **Deck.** A deck has a maximum size, so the player chooses which pilots to bring from the collection. The hand is 4. Early encounters are short and simple and need few pilots; more pilots are earned or bought as the game goes on. Upgrading pilots that were not used so far is a reason to replay.
- **Replay and grinding.** SP comes from **first clears**. Repeat clears give a separate currency spent in **per-world shops**, which sell better items and a limited number of SP points. SP costs more in each later world and later worlds pay more currency per repeat clear, so grinding an old world is possible but very ineffective. If that is not enough, repeat rewards of an already completed world are cut hard or removed. The simulator should test for exploitable grind loops once numbers exist.
- **Abilities (decided 2026-10-06): one use per encounter, and using one costs AP** (a per-ability amount, with strong abilities costing more). Timing is part of the decision, so a bot has to decide *when* to spend an ability, not only whether. AP per turn and ability costs are tunable parameters.
- **SP economy still to discuss:** cost per upgrade and whether it rises with the stat, whether an upgrade raises both stats or one of the two (the player's choice), and how repeat clears are rewarded.

### Difficulty ranking (idea, agreed direction)

The simulator can rank encounters by difficulty (win rate and turns for a given pilot roster), which will be used to assign encounters to parts of the game from easiest to hardest. How player progression (stronger pilots, unlocked abilities) enters the ranking is deferred: likely by simulating with rosters that represent each stage of the game.

### Starting rules (agreed 2026-10-03, in the GameRules asset)

Player HP 20. 3 AP at the start of the battle, and AP is **refreshed to 3 at the start of every turn** (unspent AP does not carry over). Hand of 4 pilots. The player's pilots come from a separate `PilotDeck` asset. Costs default to 1 AP for placing, replacing and activating an ability.

### Turn flow (target)

1. Battle starts from a Start Battle screen. The encounter sets up the board and enemies. The player draws pilots.
2. Planning: the player spends AP on actions. An action without enough AP is refused with feedback. Enemy intents are visible. A live preview shows the resulting chain value while dragging.
3. The player presses Play Turn. **All input is locked** during resolution (except a future quit button).
4. Player attack per lane (enemy defense, then enemy HP). All contributing pilots play Attack, then return to Idle. Check for dead enemy: victory UI if all queues are clear, otherwise the next enemy spawns next turn.
5. Enemies attack left to right (defense chain wounds, then player HP). Chains that hit 0 play Defeated and the pilots die.
6. Check for player death: defeat UI and return to Start Battle.
7. Next turn: show enemy intents, refill AP, refill the hand from the bank.

### Configuration (proposed, to be seen as a template)

```
BoardPrefab (the whole board, one asset; variations are prefab variants)
  pilot slots: id, position, bonus (+N / xN), locking (locked, unlock AP cost, unlock at wave, unlock at turn)
  operator slots: id, position, From pilot slot, one or more output pilot slots, operator (+ - x /)
  chain markers on terminal slots: lane, role (attack/defense)

EncounterDefinition (ScriptableObject)
  board: board prefab (or a pool of variants)
  lanes[]: id (must match a lane marker on the board), enemy queue
           entry: EnemyDefinition or EnemyPool, trigger (start / after previous dies / turn N)
  player rules: starting AP, AP per turn, hand size, cost config
  rewards: SP amount
  win: all queues cleared; lose: player HP 0
```

- An `EncounterPool` (weighted list) plus an editor generator that bakes prefab variants of a board can provide variety. The solver filters generated variants. `player rules` in the encounter should be dropped: starting AP, AP per turn, hand size and action costs are global (`GameRules`).
- The user has asked to see a first template (actual ScriptableObjects) before locking this down.
- A "field creation skill" was discussed. Verdict: build the validator and solver tool first; a skill (or JSON import) can be added afterwards.

### Solver / testing tool (build early)

A pure C# evaluator shared by the game and an editor tool. The tool brute-forces pilot arrangements over a roster (≈ thousands of permutations), reports a **Pareto frontier** of (attack, defense) per lane, how often the best arrangement passes through toll nodes, and validates layouts (single root per tree, no cycles, terminals tagged, every lane has an attack and defense chain, no dead-end operators). The same data sets enemy par values.

## What exists in code today

### Pure battle core and simulator (2026-10-03)

`Assets/Scripts/Board/Battle` (namespace `FunGame.Battle`, inside the Unity-free `FunGame.Board` assembly) runs a whole battle: `BattleEngine` (state and turn resolution), `WoundAllocator` (per-pilot wounds), `PlannerPlayer` (a scripted player that searches every affordable combination of unlocks and placements each turn), `BattleRunner` (one battle or many with different seeds). `BattleSetupBuilder` (`Assets/Scripts/Encounters`) turns an `EncounterDefinition` + `GameRules` + `PilotDeck` into the pure `BattleSetup`; `Tools/Fun Game/Encounter Simulator` plays an encounter and shows the log or a win-rate summary. Covered by EditMode tests (`BattleTests`).

Rules as implemented:
- Turn order: planning (place / replace / unlock for AP, clearing a fallen pilot is free), then the player attacks every lane (attack chains summed, minus the enemy's revealed defense; values are exact floats), then every surviving enemy attacks (lanes in order) into the lane's defense chains, then the next turn begins (AP refreshed, next enemy spawns in empty lanes, others reveal their next action, unlocks by turn/wave, hand drawn up to the limit).
- Enemy damage on a defense chain becomes wounds on its contributing pilots (see wound model). A chain that cannot absorb its damage is destroyed: its active pilots fall, and the remainder goes to the next defense chain of the lane, then to the player (exact, not rounded).
- Wave unlocks are per lane: a slot set to wave 2 of lane X unlocks when the second enemy of lane X has appeared (the first died and the next spawned at the start of a turn). The slot names the lane (`Unlock Lane Id`); it may stay empty only on a one-lane board, and a board with several lanes requires it (validation error otherwise).
- Eject: `TryEject` takes a living pilot back to the hand for `EjectPilotCost` AP (placeholder, default 1) and needs a free hand slot; a fallen pilot is cleared for free. The scripted player does not use it yet.
- Not simulated yet: pilot abilities, discarding, recovery of wounded pilots in hand, Snipe/Swap, rewards, deck choice, pilots growing between encounters (see "Balance lab" below for what is simulated and how).

### Balance lab (Stage 1 done 2026-10-07)

Purpose: rank encounters by difficulty and learn what drives it, by playing them with simulated players, with no Unity needed. Code: `Assets/Scripts/Board/Sim` (pure), console tool and report in `Tools/BalanceLab` (see its README: export encounters from Unity with Tools > Fun Game > Export Encounters For Balance Lab, then run `Tools\BalanceLab\run-lab.bat report`). Output: `Tools/BalanceLab/reports/balance-report.html` (+ csv).

- **Engine additions:** `BattleEngine.Clone` (exact copy, or with a fresh random stream for look-ahead), `ResolveCombat` (the fighting only, before the next turn's random draws and intents), `BattleStats` counters, `Logging`/`RecordEvents` switches, own copyable `BattleRandom` instead of `System.Random`, and player actions as `BattleCommand`s (`BattleCommands.Legal/Apply`). **Since 2026-10-09:** an enemy action is a list of effects (`IEnemyEffect` in `EnemyEffects.cs`: `AttackEffect`, `DefendEffect`; Guard effects shield the enemy while the player attacks, Act effects run on the enemy turn), so Lock, Snipe and later effects are new small classes, not engine changes; `EnemyActionData(name, attack, defense)` still builds the plain kind. The text log is written from the events by `BattleLogWriter` (every log line is an event; planning actions raise events too, which the views ignore), and `BattleStats.Copy` copies every counter. The refactor left the World 1 lab report byte-identical.
- **Players:** `SearchPlayer` reads the revealed enemy intent, plays each candidate plan out on a copy (`ResolveCombat`) and scores the result (`PlanScorer`), keeping the best N partial plans (beam search). Profiles: Casual (width 4, this turn only, never ejects), Careful (width 48), Reference (all plans, slow). `GreedyPlayer` is the old `PlannerPlayer`. A look-ahead never reads the real dice (tested).
- **Power ladder:** instead of spending SP, the starter roster's stats are multiplied by 1 + 0.1 x rung (rung -8..); an encounter's difficulty is the power at which a player wins 50/90% (`PowerLadder`, `Lab.RunLadder`). Roster power is a stand-in for upgrades; abilities are not part of it yet.
- **Target feel (starting guesses, in `TargetFeel`):** careful / casual win rate and health left after a win at the *intended power* of the encounter: Tutorial 97% / 85% / 80%, Early 92% / 75% / 65%, Mid 70% / 45% / 50%, Late 55% / 30% / 35%. Early encounters are deliberately more forgiving than mid-game ones. Tier and intended power per encounter go in `Tools/BalanceLab/tiers.txt`.
- **Staging (agreed):** Stage 1 harness (done). Stage 2: mechanics one at a time with tests and a test encounter each: Lock, Snipe (with the player reacting), abilities (one use per encounter, cost AP, timing is a decision; the bot gets timing styles, not multi-turn look-ahead, see the design notes), Swap and random effects. Stage 3: content: pilot archetypes with a power budget and about 30 encounters across 3 world tiers. Stage 4: economy lab: deck cap and deck choice, SP-spending styles, marginal value of a stat point or ability to inform prices, and grind-loop exploit tests under candidate reward numbers.

### Curriculum: worlds that teach one topic at a time (direction, 2026-10-09)

- **Worlds are short (about three encounters) and each introduces one topic**, ending in a boss that rewards a new pilot plus SP for a first upgrade. Encounters are *constructed to demand* the new mechanic, not found by chance; the lab then verifies it (see below). The same checks are meant to guide encounter creation in general and to allocate encounters to worlds and difficulty.
- **A mechanic is not available until it has been taught.** `BattleRules.AllowReplace` and `AllowEject` (set per encounter on `EncounterDefinition`, default on) switch replacing and ejecting off; encounters of earlier worlds must be winnable without them. Locked-slot rules already work per encounter.
- **World 1 (placement and operators):** step 1 two slots and a harmless enemy; step 2 a slot that opens on turn 2; step 3 (boss) a longer board with add, subtract and multiply. Divide and bonus slots join when they fit (lab data: dividing by a stat below 1 multiplies the chain, so divide reads best once stats are above 1, after the first upgrade). Replace and eject are off. After the boss: first upgrade and the new pilot, who may show an ability.
- **World 2 (pilot tools)** covers the rest of the pilot-related mechanics: replacing and ejecting pilots, AP-paid slot unlocks, abilities. Enemy abilities depend on these: Snipe (telegraphed) is the natural way to teach ejecting, and Lock only makes sense after ejecting is known. Whether enemy abilities come after all the pilot topics or one per boss is open; test pacing before deciding.
- **Lab checks for a world** (report section "World N: how it ramps"): the power needed rises step by step; tutorial-tier steps need little thinking (casual about as good as careful); win rates match the tier; and the "Does it need...?" table, which re-plays each encounter with replacing, ejecting or self-opening slots taken away. A teaching step should show a big drop without its mechanic; every other step should not depend on mechanics that are switched off. Worlds, positions and what each step teaches are assigned in `Tools/BalanceLab/tiers.txt` (`name|Tier|power|World|position|teaches`).

### Number display rule (agreed 2026-10-03)

All values are kept exact (no rounding in the rules: HP, damage and chain values are floats). They are only **cut, not rounded, to two decimals for display** (10.5783 shows as 10.57, a wounded 2/3 as 0.66, trailing zeros dropped), by `DisplayFormat.Value`; a value above zero never shows as 0, and a float a hair under a whole number (0.99999994) shows as that number. Changed from one decimal on 2026-10-05 because wounds spread proportionally and one decimal made a chain's parts look like they did not add up to its total (0.6 + 0.3 vs 1). One formatter for every number: attack and defense, HP and damage alike, with no attack-specific handling (attack values are simply whole numbers for now).

### Playable battle (2026-10-03)

`Assets/Scripts/BattleView` (namespace `BattleView`) plays an encounter with the player's own input. `BattleSession` builds a `BattleEngine` from an encounter and plays each turn's resolution back from the engine's `BattleEvent`s (the engine state is already final, so each event carries the numbers to animate). Everything player-facing is a **prefab** (created once by `Tools/Fun Game/Create Battle Prefabs` into `Assets/BattleViews/Prefabs`, then owned and edited freely; never overwritten unless reset): `PilotSlot` and `OperatorSlot` (one object each: the slot of a board layout and its playable look, with `BoardSlotAuthoring`/`SlotView` and `BoardOperatorSlotAuthoring`/`OperatorView` on the same prefab; boards are built from them, so a layout and the battle show the same object; `Tools/Fun Game/Convert Old Boards` rebuilds boards made from the earlier editor-only slot prefabs), `EnemyView`, `CardView`, `ChainLabel`, `Popup` and `BattleHud` (the whole 2D UI incl. `HandView` area, `ActionMenuView`, `TestPanelView` and the end screen). They are referenced from the `BattleVisuals` asset. The scene supplies a board root (`BoardView`; move or scale it to move or scale the whole board) and enemy lane anchors (`EnemiesView` puts an `EnemyView` at each). Scripts fill values only; sizes, offsets and texts are set in the prefabs, `SelectionController` (click flow: click a card or slot, get a menu of what is possible with its AP cost or the reason it is not; Place highlights every legal slot; Esc or right-click cancels). `BattleVisuals` is a ScriptableObject holding the sprites and colours so art can be swapped while testing; the attack/defense/both colours are decided in one place (`ColorForRoles`) and used by lines, pilot bars, chain labels and stat texts. A pilot cut off from its chain is drawn grey with an INACTIVE label. Clicking outside an open menu dismisses it like Cancel. One scene is the test bench for every encounter: `Tools/Fun Game/Open Battle Test Scene` opens (or creates) `Assets/Scenes/BattleScene.unity` and the default assets. On the Battle Session, assign an Encounter (or fill the list with every encounter in the project), press Play and use the on-screen test panel (arrows to pick an encounter, Start/Restart, Stop) or the Inspector buttons. Stopping or restarting works at any time, even mid-resolution, and the Encounter can be changed while playing. Set the Game view to Full HD. Not yet: abilities, discarding, drag and drop, Start Battle screen.

### Combat rules implemented (legacy fixed structure)

- Structure: head, attack, secondary-attack, defense, secondary-defense pilot slots and three operator slots (middle, attack, defense). Attack = `((H op1 A) op2 S)`, left to right, defense likewise with its own pilots. The middle operator is shared.
- Player damage hits enemy DEF first (animated), then enemy HP.
- Enemy damage drains defense pilots furthest to closest (secondary defense, defense, head) by each pilot's raw `ActiveDefenseValue`. **This ignores operators**, so the displayed DEF total can differ from what actually absorbs. The per-pilot wound model above replaces this.
- Win/loss: 0 HP. After the player's attack, a dead enemy ends the turn.
- Enemy actions are an inspector list (name, attack, defense), Sequential or Random selection; the upcoming action is revealed and shown in TMP labels that hide at 0.
- Tween duration 0.35s on `PlayerController`, `EnemyController`, `PilotSlotView`.

### Key files

| File | Responsibility |
| --- | --- |
| `Assets/Scripts/Controllers/PlayerController.cs` | Player HP; shuffles the deck and populates the pilot/operator hands. |
| `Assets/Scripts/Controllers/StructureController.cs` | Calculates structure attack/defense; absorbs enemy damage. To be replaced by the graph evaluator. |
| `Assets/Scripts/Views/PilotSlotView.cs` | A placed pilot: runtime active defense, eject, removal at 0 defense. |
| `Assets/Scripts/Controllers/EnemyController.cs` | Enemy HP, inline action list, preview, selection. Needs to be driven by an `EnemyDefinition`. |
| `Assets/Scripts/Controllers/TurnController.cs` | `ResolveTurn()` for the Play Turn button; sequential awaited resolution with a re-entry guard. |
| `Assets/Scripts/Views/PilotAnimationView.cs` | Idle/Attack/Defeated sprite sequences with per-sequence Loop. A new play call cancels the previous one. Placed on `TestMogli` in `TestScene2` (it contains throwaway test code to delete). |
| `Assets/Scripts/Views/PilotHandView.cs`, `OperatorsHandView.cs` | Hands (pre-created token views). |
| `Assets/Scripts/Installers/CombatInstaller.cs` | Zenject binding for `PilotHandView`. |

### Known bugs and gaps

- All four operator assets (`Add`, `Subtract`, `Multiply`, `Divide`) have `Type: 0`, so they may all be Add. Verify in the Inspector.
- `PlayerController.SetInitialPilots/Operators` use `Count - 1`, so one fewer token than intended spawns.
- `PilotHandView.SpawnNewToken` re-activates a parked token without re-initialising it with the ejected pilot's config.
- `OperatorsHandView` bounds check uses `>=` instead of `>`.
- `TokenBaseView.RemoveListeners` calls `AddListener`.
- `OperationType` lives in the legacy `Assets/Robot/Scripts/Type` folder.
- No enemy ScriptableObject exists yet; enemy data is inline in the scene.
- No input lock during resolution (pilots can be ejected mid-turn).
- Automated tests do not exist; the project has never been compile-verified outside the Unity Editor.
- The generated `.gitignore` already covers Unity folders; extra standard entries were appended.

## Plan

Rough order: 0 → 1 → 2 → 3 → 4, with 5–7 proceeding once the logic is stable.

**Phase 0: prep and decisions**
1. Fix the known bugs above (at minimum the operator asset types).
2. **Done (2026-10-01):** pure graph model, evaluator and validator in `Assets/Scripts/Board` (asmdef `FunGame.Board`, no Unity references) with EditMode tests in `Assets/Tests/EditMode`. Covers tree flow, prefix rule, bonus nodes, ÷0, subtract clamp; merges still deferred. Verified by compiling with the editor's Roslyn and running the 23 tests through a small shim runner, not yet through Unity's Test Runner.
3. **Done (2026-10-01, first version):** `BoardSolver` (pure, in `FunGame.Board`, tested) plus an editor window at `Tools/Fun Game/Board Solver` that runs it on built-in sample boards (`SampleBoards`) with a pilot roster taken from the project and a stat scale slider. Reports the attack/defense Pareto frontier, best value per chain, and how often frontier arrangements cross a toll or use a bonus. Still to do: solve real layout/setup assets (needs the ScriptableObject templates), AP-limited and wound-aware analysis, enemy par values.
4. **Done (2026-10-03, merged model, awaiting hands-on feedback):** board authoring. A board is **one prefab** edited in the Scene view: `BoardLayoutAuthoring` root (with an editor-only board-bounds rectangle), `BoardSlotAuthoring` per pilot slot (id, bonus, locked + unlock AP cost / wave / turn), `BoardOperatorSlotAuthoring` per operator slot (From pilot slot, one or more output pilot slots so a chain can branch at an operator, plus the operator), `BoardChainMarker` on terminal slots (lane + role). Slots are placed freely; a green + button in the Scene view adds an operator slot and the next pilot slot. Slot prefabs and the bounds sprite are auto-created in `Assets/BoardLayouts/_Authoring`. Variations use prefab variants. `BoardConverter` turns a board into the pure model; the solver window takes a board prefab. Global rules (starting AP, AP per turn, hand size, action costs) are in a `GameRules` ScriptableObject. Menus: `Assets/Create/Fun Game/Board Layout Prefab`, `Tools/Fun Game/Create Sample Boards`, `Tools/Fun Game/Create Board Test Scene`. `EncounterDefinition` and enemy assets are still to do (Phase 1).

**Phase 1: battle loop skeleton**
5. **Data done (2026-10-03):** `EnemyDefinition` (health, sprite, action list, selection mode) in `Assets/Scripts/Combat`, `EncounterDefinition` (board prefab, per-lane enemy queues, SP reward) and `EncounterChecks` (validation of board + lanes + enemies) in `Assets/Scripts/Encounters`; menu `Tools/Fun Game/Create Sample Encounter`. Still to do: `EnemyController` becomes runtime-only and is driven by an `EnemyDefinition`; pure battle core (see below).
6. `BattleFlow` (Start, PlayerTurn, Resolving, Victory, Defeat), Start Battle screen, victory/defeat panels returning to Start, full reset.
7. Lane concept in data (ship one lane first), enemy queues and next-enemy spawn.

**Phase 2: hand and deck**
8. Shared `Deck` and `HandRefiller` for pilots only (the operator hand goes away). Refill from the bank each turn.

**Phase 3: action points**
9. `ActionPointsService`, `ActionCostConfig` (place, replace, ability, unlock slot, eject fallen = 0), AP display.
10. Action gate used by every token/slot action, with refusal feedback. AP refill each turn.
11. Slot unlocking by AP, wave and turn.

**Phase 4: board in the scene**
12. Test scene built from a layout prefab, driven by the graph evaluator.
13. Drag preview showing the resulting chain value; dormant/fallen/locked slot states.

**Phase 5: turn UX and resolution**
14. `InputLock` for the resolve sequence; per-enemy intent display.
15. Per-pilot wound model and pilot death, pilot stat modifiers, Snipe; fixed enemy attack order.
16. Attack animation for contributing pilots, then return to idle; ability animation; Defeated animation awaited on chain death.

**Phase 6: visuals**
17. Per-pilot defense bars and connection graphics, energy lines shader (slot filled = brighter and more distorted), hand UI, summoner sprite, ÷0 gag.

**Phase 7: progression**
18. SP spending screen, pilot stat/ability upgrades, rewards from encounters, encounter pool and field generator.

## Not implemented yet

Everything in the Design direction above beyond what the "what exists" half describes: action points, lanes and waves, the board model, slot unlocking, bonus nodes, the per-pilot wound model, pilot death and recovery, input lock, start/victory/defeat screens, draw/refill, pilot abilities, skill points, the solver tool.
