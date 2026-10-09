using System.IO;
using System.Linq;
using Configurations;
using Encounters;
using FunGame.Sim;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Writes every encounter asset (with the game rules and the starter deck) to Tools/BalanceLab/encounters as plain
    /// text, so the balance lab tool can play them outside Unity. Encounters with errors are skipped and listed. Run it again
    /// whenever encounters, boards, enemies, rules or the deck change.
    /// </summary>
    public static class BalanceLabExporter
    {
        public const string OutputFolder = "Tools/BalanceLab/encounters";

        [MenuItem("Tools/Fun Game/Export Encounters For Balance Lab")]
        private static void Export()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var folder = Path.Combine(projectRoot, OutputFolder);
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.GetFiles(folder, "*.txt"))
            {
                File.Delete(old); // the folder always mirrors the assets, so a deleted encounter does not linger
            }

            var rules = AssetDatabase.FindAssets("t:GameRules")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameRules>(AssetDatabase.GUIDToAssetPath(guid))).FirstOrDefault(asset => asset != null);
            var deck = AssetDatabase.FindAssets("t:PilotDeck")
                .Select(guid => AssetDatabase.LoadAssetAtPath<PilotDeck>(AssetDatabase.GUIDToAssetPath(guid))).FirstOrDefault(asset => asset != null);
            if (deck == null)
            {
                Debug.LogError("No Pilot Deck asset was found, so there is no roster to export. Create one with Tools > Fun Game > Create Default Game Rules And Deck.");
                return;
            }

            var exported = 0;
            var skipped = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:EncounterDefinition"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
                if (encounter == null)
                {
                    continue;
                }

                if (BoardValidationGUI.HasErrors(EncounterChecks.Validate(encounter)))
                {
                    Debug.LogWarning($"Not exported (it has errors, see its inspector): {path}", encounter);
                    skipped++;
                    continue;
                }

                var setup = BattleSetupBuilder.Build(encounter, rules, deck, 1);
                var text = SetupText.Write(encounter.DisplayName, "Mid", setup, $"from {path}");
                File.WriteAllText(Path.Combine(folder, SafeFileName(Path.GetFileNameWithoutExtension(path)) + ".txt"), text);
                exported++;
            }

            Debug.Log($"Exported {exported} encounter(s) to {OutputFolder}" + (skipped > 0 ? $" ({skipped} skipped because of errors)" : string.Empty) +
                      ". Tiers default to Mid; set them in Tools/BalanceLab/tiers.txt (lines of 'encounter name|Tutorial/Early/Mid/Late').");
        }

        private static string SafeFileName(string name)
        {
            return new string(name.Select(character => char.IsLetterOrDigit(character) || character == '-' || character == '_' ? character : '_').ToArray());
        }
    }
}
