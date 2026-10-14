using BattleView;
using Campaign;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Creates the campaign menu's prefabs once (temporary looks, then owned and edited freely; an existing prefab is never
    /// overwritten): MapScreen, WorldRow, EncounterButton, PilotsScreen and PilotCard, in Assets/CampaignViews/Prefabs.
    /// Arrangement uses layout groups, so sizes and spacing are changed on the prefabs.
    /// </summary>
    public static class CampaignPrefabCreator
    {
        public const string Folder = "Assets/CampaignViews/Prefabs";
        public const string MapScreenPath = Folder + "/MapScreen.prefab";
        public const string PilotsScreenPath = Folder + "/PilotsScreen.prefab";

        private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.13f, 1f);
        private static readonly Color ButtonColor = new Color(0.3f, 0.32f, 0.42f);
        private static readonly Color CardColor = new Color(0.14f, 0.16f, 0.28f);

        [MenuItem("Tools/Fun Game/Create Campaign Prefabs")]
        public static void CreateAll()
        {
            EnsureFolder();
            var encounter = LoadOrCreate(Folder + "/EncounterButton.prefab", BuildEncounterButton);
            var row = LoadOrCreate(Folder + "/WorldRow.prefab", BuildWorldRow);
            LoadOrCreate(MapScreenPath, () => BuildMapScreen(row.GetComponent<WorldRowView>(), encounter.GetComponent<EncounterButtonView>()));
            var card = LoadOrCreate(Folder + "/PilotCard.prefab", BuildPilotCard);
            LoadOrCreate(PilotsScreenPath, () => BuildPilotsScreen(card.GetComponent<PilotCardView>()));
            AssetDatabase.SaveAssets();
        }

        // ---------- prefabs ----------

        private static GameObject BuildEncounterButton()
        {
            var root = new GameObject("EncounterButton", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(330f, 120f);
            var background = root.AddComponent<Image>();
            background.color = ButtonColor;
            var button = root.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.disabledColor = Color.white; // the locked look is the locked colour
            button.colors = colors;
            var layout = root.AddComponent<LayoutElement>();
            layout.preferredWidth = 330f;
            layout.preferredHeight = 120f;
            var title = ViewFactory.CreateUiText(root.transform, "Title", "1-1: First steps", 28f, Color.white, new Vector2(310f, 70f), new Vector2(0f, 14f));
            var footer = ViewFactory.CreateUiText(root.transform, "Footer", "+1 SP", 20f, new Color(1f, 1f, 1f, 0.8f), new Vector2(310f, 30f), new Vector2(0f, -36f));

            var view = root.AddComponent<EncounterButtonView>();
            Assign(view, "_button", button);
            Assign(view, "_background", background);
            Assign(view, "_title", title);
            Assign(view, "_footer", footer);
            return root;
        }

        private static GameObject BuildWorldRow()
        {
            var root = new GameObject("WorldRow", typeof(RectTransform));
            var rowLayout = root.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 30f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            var element = root.AddComponent<LayoutElement>();
            element.preferredHeight = 130f;

            var name = ViewFactory.CreateUiText(root.transform, "Name", "World 1", 40f, Color.white, new Vector2(260f, 80f), Vector2.zero, TextAlignmentOptions.Left);
            name.fontStyle = FontStyles.Bold;
            var encounters = ViewFactory.CreateUiRoot(root.transform, "Encounters", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 120f));
            var encounterLayout = encounters.gameObject.AddComponent<HorizontalLayoutGroup>();
            encounterLayout.spacing = 30f;
            encounterLayout.childAlignment = TextAnchor.MiddleLeft;
            encounterLayout.childControlWidth = false;
            encounterLayout.childControlHeight = false;
            encounterLayout.childForceExpandWidth = false;
            encounterLayout.childForceExpandHeight = false;

            var view = root.AddComponent<WorldRowView>();
            Assign(view, "_name", name);
            Assign(view, "_encounters", encounters);
            return root;
        }

        private static GameObject BuildMapScreen(WorldRowView rowPrefab, EncounterButtonView encounterPrefab)
        {
            var root = FullScreen("MapScreen");
            var skillPoints = Corner(root, ViewFactory.CreateUiText(root, "SkillPoints", "Skill points: 0", 46f, Color.white, new Vector2(800f, 70f), Vector2.zero, TextAlignmentOptions.Left).rectTransform,
                new Vector2(0f, 1f), new Vector2(60f, -40f));
            var pilots = TopButton(root, "Pilots", "Pilots", new Vector2(-780f, -40f), new Color(0.15f, 0.4f, 0.8f));
            var unlockAll = TopButton(root, "UnlockAll", "Unlock all: off", new Vector2(-420f, -40f), ButtonColor);
            var reset = TopButton(root, "Reset", "Reset progress", new Vector2(-60f, -40f), ButtonColor);
            var openLogs = TopButton(root, "OpenLogs", "Open playtest logs", new Vector2(-60f, -120f), ButtonColor);

            var worlds = ViewFactory.CreateUiRoot(root, "Worlds", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(1500f, 800f));
            var worldsLayout = worlds.gameObject.AddComponent<VerticalLayoutGroup>();
            worldsLayout.spacing = 20f;
            worldsLayout.childAlignment = TextAnchor.UpperLeft;
            worldsLayout.childControlWidth = true;
            worldsLayout.childControlHeight = true;
            worldsLayout.childForceExpandWidth = true;
            worldsLayout.childForceExpandHeight = false;

            var view = root.gameObject.AddComponent<MapScreenView>();
            Assign(view, "_skillPoints", skillPoints.GetComponent<TextMeshProUGUI>());
            Assign(view, "_worlds", worlds);
            Assign(view, "_worldRowPrefab", rowPrefab);
            Assign(view, "_encounterPrefab", encounterPrefab);
            Assign(view, "_pilots", pilots);
            Assign(view, "_unlockAll", unlockAll);
            Assign(view, "_reset", reset);
            Assign(view, "_openLogs", openLogs);
            return root.gameObject;
        }

        private static GameObject BuildPilotCard()
        {
            var root = new GameObject("PilotCard", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(300f, 420f);
            root.AddComponent<Image>().color = CardColor;

            var picture = ViewFactory.CreateImage(root.transform, "Picture", Color.white, new Vector2(170f, 170f), new Vector2(0f, 105f));
            var name = ViewFactory.CreateUiText(root.transform, "Name", "Quill", 34f, Color.white, new Vector2(280f, 44f), new Vector2(0f, -5f));
            var level = ViewFactory.CreateUiText(root.transform, "Level", "Lv 1", 26f, new Color(1f, 0.82f, 0.29f), new Vector2(120f, 36f), new Vector2(-75f, 185f), TextAlignmentOptions.Left);
            var stats = ViewFactory.CreateUiText(root.transform, "Stats", "ATK 1   DEF 3", 28f, Color.white, new Vector2(280f, 40f), new Vector2(0f, -50f));
            var ability = ViewFactory.CreateUiText(root.transform, "Ability", string.Empty, 18f, new Color(1f, 0.82f, 0.29f), new Vector2(280f, 60f), new Vector2(0f, -100f));
            var upgradeAttack = ViewFactory.CreateButton(root.transform, "UpgradeAttack", "+1 ATK", new Vector2(130f, 50f), new Vector2(-70f, -160f), new Color(0.55f, 0.22f, 0.18f), 24f);
            var upgradeDefense = ViewFactory.CreateButton(root.transform, "UpgradeDefense", "+1 DEF", new Vector2(130f, 50f), new Vector2(70f, -160f), new Color(0.18f, 0.32f, 0.55f), 24f);
            var refund = ViewFactory.CreateButton(root.transform, "Refund", "Refund upgrades", new Vector2(200f, 34f), new Vector2(0f, -195f), ButtonColor, 18f);

            var view = root.AddComponent<PilotCardView>();
            Assign(view, "_picture", picture);
            Assign(view, "_name", name);
            Assign(view, "_level", level);
            Assign(view, "_stats", stats);
            Assign(view, "_ability", ability);
            Assign(view, "_upgradeAttack", upgradeAttack);
            Assign(view, "_upgradeDefense", upgradeDefense);
            Assign(view, "_refund", refund);
            return root;
        }

        private static GameObject BuildPilotsScreen(PilotCardView cardPrefab)
        {
            var root = FullScreen("PilotsScreen");
            var skillPoints = Corner(root, ViewFactory.CreateUiText(root, "SkillPoints", "Skill points: 0", 46f, Color.white, new Vector2(1200f, 70f), Vector2.zero, TextAlignmentOptions.Left).rectTransform,
                new Vector2(0f, 1f), new Vector2(60f, -40f));
            var back = TopButton(root, "Back", "Back to map", new Vector2(-60f, -40f), ButtonColor);

            var cards = ViewFactory.CreateUiRoot(root, "Cards", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1700f, 880f));
            var grid = cards.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300f, 420f);
            grid.spacing = new Vector2(30f, 30f);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

            var view = root.gameObject.AddComponent<PilotsScreenView>();
            Assign(view, "_skillPoints", skillPoints.GetComponent<TextMeshProUGUI>());
            Assign(view, "_cards", cards);
            Assign(view, "_cardPrefab", cardPrefab);
            Assign(view, "_back", back);
            return root.gameObject;
        }

        // ---------- helpers ----------

        private static RectTransform FullScreen(string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            root.AddComponent<Image>().color = PanelColor;
            return rect;
        }

        private static Button TopButton(RectTransform root, string name, string label, Vector2 offset, Color color)
        {
            var button = ViewFactory.CreateButton(root, name, label, new Vector2(320f, 64f), Vector2.zero, color, 28f);
            Corner(root, (RectTransform)button.transform, new Vector2(1f, 1f), offset);
            return button;
        }

        private static RectTransform Corner(RectTransform root, RectTransform rect, Vector2 corner, Vector2 offset)
        {
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.anchoredPosition = offset;
            return rect;
        }

        private static GameObject LoadOrCreate(string path, System.Func<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            var built = build();
            var prefab = PrefabUtility.SaveAsPrefabAsset(built, path);
            Object.DestroyImmediate(built);
            return prefab;
        }

        private static void Assign(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/CampaignViews"))
            {
                AssetDatabase.CreateFolder("Assets", "CampaignViews");
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CampaignViews", "Prefabs");
            }
        }
    }
}
