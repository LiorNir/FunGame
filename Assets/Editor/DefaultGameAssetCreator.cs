using System.Linq;
using Configurations;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>Creates the global rules asset and a starter pilot deck, if they do not exist yet.</summary>
    public static class DefaultGameAssetCreator
    {
        public const string Folder = "Assets/Rules";
        private const string RulesPath = Folder + "/GameRules.asset";
        private const string DeckPath = Folder + "/StarterDeck.asset";

        [MenuItem("Tools/Fun Game/Create Default Game Rules And Deck")]
        private static void Create()
        {
            var (rules, _) = EnsureAssets();
            Selection.activeObject = rules;
            Debug.Log($"Game rules and starter deck are in {Folder}.");
        }

        /// <summary>Returns the game rules and starter deck assets, creating them first if they are missing.</summary>
        public static (GameRules rules, PilotDeck deck) EnsureAssets()
        {
            EnsureFolder();

            var rules = AssetDatabase.LoadAssetAtPath<GameRules>(RulesPath);
            if (rules == null)
            {
                // The defaults in GameRules are the agreed starting rules: 20 HP, 3 AP refreshed every turn, hand of 4.
                rules = ScriptableObject.CreateInstance<GameRules>();
                AssetDatabase.CreateAsset(rules, RulesPath);
            }

            var deck = AssetDatabase.LoadAssetAtPath<PilotDeck>(DeckPath);
            if (deck == null)
            {
                var pilots = AssetDatabase.FindAssets("t:PilotTokenConfiguration")
                    .Select(guid => AssetDatabase.LoadAssetAtPath<PilotTokenConfiguration>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(pilot => pilot != null)
                    .OrderBy(pilot => pilot.name)
                    .ToList();
                deck = ScriptableObject.CreateInstance<PilotDeck>();
                deck.SetData(pilots);
                AssetDatabase.CreateAsset(deck, DeckPath);
            }

            AssetDatabase.SaveAssets();
            return (rules, deck);
        }

        public static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "Rules");
            }
        }
    }
}
