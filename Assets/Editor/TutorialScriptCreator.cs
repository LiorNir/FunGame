using System.Collections.Generic;
using System.Linq;
using BattleView;
using Encounters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Creates the tutorial asset (Assets/Campaign/Tutorial.asset) with the first draft of every line, and assigns it to the
    /// Battle Session of the battle scene. From then on the asset is the place to edit the lines (an existing asset is
    /// never overwritten).
    /// </summary>
    public static class TutorialScriptCreator
    {
        private const string TutorialPath = "Assets/Campaign/Tutorial.asset";

        // encounter asset name, trigger, turn (TurnStart only), text
        private static readonly (string Encounter, TutorialTrigger Trigger, int Turn, string Text)[] Draft =
        {
            ("World 1-1 First steps", TutorialTrigger.BattleStart, 1, "Click a pilot in your hand, then a highlighted slot, to place it. Placing costs 1 AP, and you have 3 AP every turn."),
            ("World 1-1 First steps", TutorialTrigger.BattleStart, 1, "Pilots form chains from the root (the bottom slot) outward. The red value is your attack on the enemy, the blue value your defense against its hit. Press Play Turn when you are ready."),
            ("World 1-1 First steps", TutorialTrigger.TurnStart, 2, "Pilots stay on the board between turns. Their defense takes the enemy's hits; the blue bar shows what is left. Hover anything to learn more."),
            ("World 1-2 A door opens", TutorialTrigger.BattleStart, 1, "A locked slot: the text under the lock says when it opens. A chain only counts up to its first empty slot, so fill it from the root outward."),
            ("World 1-3 The gatekeeper (boss)", TutorialTrigger.BattleStart, 1, "The board splits: one branch attacks, the other defends, and the root counts for both. The x slot multiplies everything before it."),
            ("World 1-3 The gatekeeper (boss)", TutorialTrigger.ChainAtZero, 1, "A hit equal to a chain's defense leaves it standing at 0. The next hit that reaches it breaks it."),
            ("World 1-3 The gatekeeper (boss)", TutorialTrigger.ChainBroken, 1, "A broken chain's pilots fall and block their slots. Place a new pilot on a fallen one to clear it."),
            ("World 2-1 Fresh legs", TutorialTrigger.BattleStart, 1, "New: you can place a pilot on top of another (a replace). The replaced pilot goes back to your hand, where it recovers defense every turn."),
            ("World 2-1 Fresh legs", TutorialTrigger.NextEnemy, 1, "A fresh enemy. Worn pilots can rest: click one and choose Eject. An ejected pilot that rests a whole turn in your hand recovers twice as fast."),
            ("World 2-2 Duck!", TutorialTrigger.BattleStart, 1, "Ejecting also dodges: a pilot that is not on the board cannot be hit. The damage reaches you instead, but the pilot survives."),
            ("World 2-3 The foreman (boss)", TutorialTrigger.BattleStart, 1, "This locked slot can be opened now by paying AP: click it."),
            ("World 3-1 Power surge", TutorialTrigger.BattleStart, 1, "Bolt has an ability, Surge. Place Bolt, then click it to use the ability (once per battle, it costs AP). A glow marks a pilot whose ability is ready."),
            ("World 3-1 Power surge", TutorialTrigger.AbilityUsed, 1, "Surge doubles Bolt's attack this turn, then its attack drops to 0 until it rests in your hand. Plan the swap."),
            ("World 3-3 The marksman (boss)", TutorialTrigger.EnemySnipes, 1, "The enemy aims a snipe at the marked pilot: -50% to both its stats next turn. Swap the pilot out to dodge it, or take the hit."),
            ("World 3-3 The marksman (boss)", TutorialTrigger.EnemyAssassinates, 1, "A snipe aimed at a pilot with nothing left assassinates it: the pilot falls."),
            ("World 4-1 Pay the toll", TutorialTrigger.BattleStart, 1, "A subtract slot is a toll: its pilot's stat is taken away from the chain. Put your weakest pilot there (0 pays nothing); the x2 bonus after it is the reward. Toll pilots are never wounded."),
            ("World 4-1 Pay the toll", TutorialTrigger.TurnStart, 2, "The enemy hunkers down: a big guard and no attack. Haze's No Guard halves both sides' defense this turn, which costs you nothing while the enemy is not attacking."),
            ("World 4-2 Divide and conquer", TutorialTrigger.BattleStart, 1, "A divide slot divides the chain by its pilot's stat. Dividing by less than 1 multiplies! Dividing by 0 is a very bad idea."),
            ("World 4-3 The ram (boss)", TutorialTrigger.BattleStart, 1, "Defense can multiply too: the x slot on the blue chain multiplies everything before it."),
            ("World 4-3 The ram (boss)", TutorialTrigger.EnemyCharges, 1, "The ram is charging: a big guard now and a huge Unleash next turn. Break through the whole guard this turn and it is stunned instead."),
            ("World 4-3 The ram (boss)", TutorialTrigger.ChargeBroken, 1, "Charge broken: the Unleash will not happen."),
            ("World 5-1 Two fronts", TutorialTrigger.BattleStart, 1, "Two lanes: each enemy attacks the defense on its side, and each side's attack hits its own enemy. 3 AP for both fronts: choose where to spend them."),
            ("World 5-2 The hub", TutorialTrigger.BattleStart, 1, "The hub slot feeds all four chains. Strong, but both enemies wear it down, and if it breaks every chain is cut."),
            ("World 5-3 The warden (boss)", TutorialTrigger.EnemyLocks, 1, "The warden will lock the marked pilot: it cannot be replaced or ejected for 2 turns. Swap it out now if you want it free.")
        };

        [MenuItem("Tools/Fun Game/Create Tutorial Script")]
        private static void Create()
        {
            var tutorial = AssetDatabase.LoadAssetAtPath<TutorialScript>(TutorialPath);
            if (tutorial == null)
            {
                var encounters = AssetDatabase.FindAssets("t:EncounterDefinition")
                    .Select(guid => AssetDatabase.LoadAssetAtPath<EncounterDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                    .ToDictionary(encounter => encounter.name);
                var lines = new List<TutorialLine>();
                foreach (var (name, trigger, turn, text) in Draft)
                {
                    if (!encounters.TryGetValue(name, out var encounter))
                    {
                        Debug.LogWarning($"Tutorial line for '{name}' left out: no such encounter.");
                        continue;
                    }

                    lines.Add(new TutorialLine { Encounter = encounter, Trigger = trigger, Turn = turn, Text = text });
                }

                tutorial = ScriptableObject.CreateInstance<TutorialScript>();
                tutorial.SetLines(lines);
                AssetDatabase.CreateAsset(tutorial, TutorialPath);
                AssetDatabase.SaveAssets();
                tutorial = AssetDatabase.LoadAssetAtPath<TutorialScript>(TutorialPath);
            }

            // the battle scene's session uses it
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity", OpenSceneMode.Single);
            var session = Object.FindFirstObjectByType<BattleSession>();
            var serialized = new SerializedObject(session);
            // loaded again after opening the scene: a reference loaded before it does not survive the scene change
            serialized.FindProperty("_tutorial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TutorialScript>(TutorialPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Tutorial script with {tutorial.Lines.Count} lines at {TutorialPath}, assigned to the battle scene.");
        }
    }
}
