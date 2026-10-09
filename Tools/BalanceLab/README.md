# Balance lab

Plays the game's encounters with simulated players and writes a report you can read in a browser. It runs outside Unity
(it uses the pure battle code in `Assets/Scripts/Board`), so a full run does not need the editor open.

## Using it

1. **In Unity:** Tools > Fun Game > Export Encounters For Balance Lab. This writes every encounter, with the game rules and the
   starter deck, to `Tools/BalanceLab/encounters/*.txt`. Run it again after any change to encounters, boards, enemies, rules or the deck.
2. **Optional:** list each encounter's tier and intended power in `tiers.txt` (see the comments in that file).
3. **Run:** double-click `run-lab.bat`, or from a terminal: `Tools\BalanceLab\run-lab.bat report`.
   It builds the tool with Unity's own compiler and takes a few minutes. More options: `--runs 200` (battles per power step, default 100),
   `--runs-intended 500` (battles at the intended power, default 300), `--builtin` (use the made-up sample encounters instead).
4. **Read:** open `Tools/BalanceLab/reports/balance-report.html`. The same numbers are in `balance-report.csv` for a spreadsheet.

If there are no exported encounters, the report uses a built-in sample set so the tool can always be tried.

## How difficulty is measured

Pilots do not level up in the test. Each encounter is played with the starter roster at different **power**: every stat of
every pilot multiplied by the same number (x1.0 is the roster as it is). An encounter's difficulty is the power at which a
player starts winning. Three simulated players are used: **Careful** (reads the enemy's announced action, plans ahead),
**Casual** (this turn only, never takes a pilot back) and **Greedy** (the old script that ignores the enemy). See the last
section of the report for what to distrust.

## Where the code is

- `Assets/Scripts/Board/Battle`: the battle engine (copyable, with counters and a `ResolveCombat` step the players look ahead with).
- `Assets/Scripts/Board/Sim`: the players, scoring, power ladder, batch summaries, the text form of an encounter, built-in samples.
- `Tools/BalanceLab`: the console tool and the report writer. Tests for the pure parts are in `Assets/Tests/EditMode/SimTests.cs`.
