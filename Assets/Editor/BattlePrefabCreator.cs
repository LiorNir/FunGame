using System.Collections.Generic;
using System.IO;
using BattleView;
using BoardAuthoring;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FunGame.EditorTools
{
    /// <summary>
    /// Creates the default prefabs of the playable battle (pilot slot, operator, enemy, card, chain label, popup and the
    /// whole HUD) and assigns them to the Battle Visuals asset. They are only starting points: once they exist, edit them
    /// freely. Existing prefabs are never overwritten unless you ask for a reset.
    /// </summary>
    public static class BattlePrefabCreator
    {
        public const string Folder = "Assets/BattleViews/Prefabs";
        private const string PilotSlotName = "PilotSlot";
        private const string OperatorSlotName = "OperatorSlot";

        private static readonly Color DarkBackground = new Color(0f, 0f, 0f, 0.55f);

        // Starting look of the operator slot, written into the prefab when it is created and nowhere else is a copy kept:
        // change the colour on the prefab. (The pilot slot's starting looks are in PilotSlotParts.)
        private static readonly Color OperatorBaseColor = new Color(0.95f, 0.8f, 0.3f);

        [MenuItem("Tools/Fun Game/Create Battle Prefabs")]
        private static void CreateMissing()
        {
            var visuals = BattleSceneCreator.EnsureVisuals();
            EnsurePrefabs(visuals, overwrite: false);
            Selection.activeObject = visuals;
        }

        [MenuItem("Tools/Fun Game/Reset Battle Prefabs (overwrites your edits)")]
        private static void ResetAll()
        {
            if (!EditorUtility.DisplayDialog("Reset battle prefabs",
                    "This recreates every battle prefab from the defaults and replaces any changes you made to them. Continue?",
                    "Reset", "Cancel"))
            {
                return;
            }

            var visuals = BattleSceneCreator.EnsureVisuals();
            EnsurePrefabs(visuals, overwrite: true);
        }

        /// <summary>
        /// Adds parts that newer code expects to the existing card and enemy prefabs (the card's stat bars, the enemy's
        /// queue line). Only missing parts are added; everything already on the prefabs is kept.
        /// </summary>
        [MenuItem("Tools/Fun Game/Add Missing Parts To Battle Prefabs")]
        private static void AddMissingParts()
        {
            var changed = new List<string>();
            AddParts($"{Folder}/CardView.prefab", EnsureCardStatBars, changed);
            AddParts($"{Folder}/CardView.prefab", EnsureStatBarPreviews, changed);
            AddParts($"{Folder}/CardView.prefab", EnsureCardAbilityParts, changed);
            AddParts($"{Folder}/EnemyView.prefab", EnsureEnemyQueueText, changed);
            AddParts($"{Folder}/BattleHud.prefab", EnsureHudGiveUp, changed);
            Debug.Log(changed.Count == 0 ? "The battle prefabs already have every part." : $"Parts added to: {string.Join(", ", changed)}. Nothing else on them was changed.");
        }

        private static void AddParts(string path, System.Func<GameObject, bool> ensure, List<string> changed)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (ensure(contents))
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    changed.Add(path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>The Give Up button, above Play Turn. Returns true if it was added.</summary>
        private static bool EnsureHudGiveUp(GameObject hud)
        {
            var view = hud.GetComponent<HudView>();
            if (view == null || hud.transform.Find("GiveUp") != null)
            {
                return false;
            }

            var rect = ViewFactory.CreateUiRoot(hud.transform, "GiveUp", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 180f), new Vector2(330f, 60f), new Vector2(1f, 0f));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.45f, 0.18f, 0.18f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            button.colors = colors;
            ViewFactory.CreateUiText(rect, "Label", "Give up", 30f, Color.white, new Vector2(330f, 60f), Vector2.zero);

            // under the end-of-battle panel, which dims everything behind it
            var playTurn = hud.transform.Find("PlayTurn");
            if (playTurn != null)
            {
                rect.SetSiblingIndex(playTurn.GetSiblingIndex() + 1);
            }

            Assign(view, "_giveUp", button);
            return true;
        }

        /// <summary>Attack and defense bars where the card's stats text sits. Returns true if anything was added.</summary>
        private static bool EnsureCardStatBars(GameObject card)
        {
            var view = card.GetComponent<CardView>();
            if (view == null || card.transform.Find("AttackBar") != null)
            {
                return false;
            }

            var attack = CreateStatBar(card.transform, "AttackBar", new Vector2(0f, -72f));
            var defense = CreateStatBar(card.transform, "DefenseBar", new Vector2(0f, -108f));
            Assign(view, "_attackBar", attack);
            Assign(view, "_defenseBar", defense);
            EnsureStatBarPreviews(card);
            EnsureCardAbilityParts(card);
            return true;
        }

        /// <summary>The card's ability line (over the top of the picture) and its ability frame (an outline on the card).</summary>
        private static bool EnsureCardAbilityParts(GameObject card)
        {
            var view = card.GetComponent<CardView>();
            if (view == null || card.transform.Find("AbilityText") != null)
            {
                return false;
            }

            var text = ViewFactory.CreateUiText(card.transform, "AbilityText", "Surge · 2 AP", 20f, new Color(1f, 0.82f, 0.29f), new Vector2(160f, 26f), new Vector2(0f, 100f));
            text.raycastTarget = false;
            text.fontStyle = FontStyles.Bold;
            var frame = card.GetComponent<Outline>() ?? card.AddComponent<Outline>();
            frame.effectColor = new Color(1f, 0.82f, 0.29f, 1f);
            frame.effectDistance = new Vector2(4f, -4f);
            frame.enabled = false;
            Assign(view, "_abilityText", text);
            Assign(view, "_abilityFrame", frame);
            return true;
        }

        /// <summary>A recovery preview behind each card stat bar's fill (bars made before the preview existed get one).</summary>
        private static bool EnsureStatBarPreviews(GameObject card)
        {
            var added = false;
            foreach (var bar in card.GetComponentsInChildren<CardStatBar>(true))
            {
                if (bar.transform.Find("Preview") != null)
                {
                    continue;
                }

                var preview = ViewFactory.CreateImage(bar.transform, "Preview", Color.white, Vector2.zero, Vector2.zero);
                preview.raycastTarget = false;
                preview.rectTransform.anchorMin = Vector2.zero;
                preview.rectTransform.anchorMax = Vector2.one;
                preview.rectTransform.pivot = new Vector2(0f, 0.5f);
                preview.rectTransform.offsetMin = new Vector2(2f, 2f);
                preview.rectTransform.offsetMax = new Vector2(-2f, -2f);
                preview.enabled = false;
                // drawn after the background, before the fill and the label
                var fill = bar.transform.Find("Fill");
                preview.transform.SetSiblingIndex(fill != null ? fill.GetSiblingIndex() : 0);
                Assign(bar, "_preview", preview);
                added = true;
            }

            return added;
        }

        private static CardStatBar CreateStatBar(Transform parent, string name, Vector2 position)
        {
            var size = new Vector2(150f, 30f);
            var rect = ViewFactory.CreateUiRoot(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            var back = rect.gameObject.AddComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0.55f);
            back.raycastTarget = false;

            var fill = ViewFactory.CreateImage(rect, "Fill", Color.white, size, Vector2.zero);
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);

            var label = ViewFactory.CreateUiText(rect, "Label", "ATK 3", 22f, Color.white, size, Vector2.zero);
            label.raycastTarget = false;

            var bar = rect.gameObject.AddComponent<CardStatBar>();
            Assign(bar, "_fill", fill);
            Assign(bar, "_label", label);
            return bar;
        }

        /// <summary>A line under the enemy's health: which enemy of the lane it is and who comes next.</summary>
        private static bool EnsureEnemyQueueText(GameObject enemy)
        {
            var view = enemy.GetComponent<EnemyView>();
            if (view == null || enemy.transform.Find("QueueText") != null)
            {
                return false;
            }

            var health = enemy.transform.Find("HealthText");
            var position = (health != null ? health.localPosition : new Vector3(0f, -1.63f, 0f)) + new Vector3(0f, -0.3f, 0f);
            var queue = WorldText(enemy.transform, "QueueText", 0.22f, new Color(0.75f, 0.75f, 0.8f), 6, position);
            queue.text = "Enemy 1/2   next: Grinder";
            Assign(view, "_queueText", queue);
            return true;
        }

        [MenuItem("Tools/Fun Game/Upgrade Pilot Slot Prefab")]
        private static void UpgradePilotSlot()
        {
            var path = $"{Folder}/{PilotSlotName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"There is no pilot slot prefab at {path}. Use Create Battle Prefabs to make one.");
                return;
            }

            var visuals = BattleSceneCreator.EnsureVisuals();

            // the stat box first: the slot is loaded with the version of it that exists at that moment
            PilotSlotParts.EnsureStatBoxPrefab(visuals, false);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                PilotSlotParts.EnsureParts(contents, visuals);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Debug.Log($"{path} now has the current parts (see PilotSlotParts). Anything you had already changed on it was kept.");
        }

        /// <summary>Creates the prefabs that are missing (or all of them) and assigns them to the visuals asset.</summary>
        public static void EnsurePrefabs(BattleVisuals visuals, bool overwrite)
        {
            EnsureFolder();

            var option = Ensure("MenuOption", overwrite, CreateOption);
            PilotSlotParts.EnsureStatBoxPrefab(visuals, overwrite);
            // the slot prefabs are used by the board layouts themselves, not through the visuals asset
            EnsureComponent<BoardSlotAuthoring>(PilotSlotName, overwrite, visuals, CreatePilotSlot);
            EnsureComponent<BoardOperatorSlotAuthoring>(OperatorSlotName, overwrite, visuals, CreateOperator);
            visuals.EnemyPrefab = EnsureComponent<EnemyView>("EnemyView", overwrite, visuals, CreateEnemy);
            visuals.CardPrefab = EnsureComponent<CardView>("CardView", overwrite, visuals, CreateCard);
            visuals.ChainLabelPrefab = EnsureComponent<TextMeshPro>("ChainLabel", overwrite, visuals, CreateChainLabel);
            visuals.PopupPrefab = EnsureComponent<TextMeshPro>("Popup", overwrite, visuals, CreatePopup);
            visuals.HudPrefab = EnsureComponent<HudView>("BattleHud", overwrite, visuals,
                v => CreateHud(v, option.GetComponent<Button>()));

            EditorUtility.SetDirty(visuals);
            AssetDatabase.SaveAssets();
            Debug.Log($"Battle prefabs are in {Folder} and assigned in the Battle Visuals asset.");
        }

        /// <summary>The pilot slot prefab: the slot of a board layout and its playable look in one object.</summary>
        public static BoardSlotAuthoring GetPilotSlotPrefab()
        {
            EnsureFolder();
            return EnsureComponent<BoardSlotAuthoring>(PilotSlotName, false, BattleSceneCreator.EnsureVisuals(), CreatePilotSlot);
        }

        /// <summary>The operator slot prefab: the operator slot of a board layout and its playable look in one object.</summary>
        public static BoardOperatorSlotAuthoring GetOperatorSlotPrefab()
        {
            EnsureFolder();
            return EnsureComponent<BoardOperatorSlotAuthoring>(OperatorSlotName, false, BattleSceneCreator.EnsureVisuals(), CreateOperator);
        }

        // ---------- helpers ----------

        public static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/BattleViews"))
            {
                AssetDatabase.CreateFolder("Assets", "BattleViews");
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/BattleViews", "Prefabs");
            }
        }

        private static GameObject Ensure(string name, bool overwrite, System.Func<GameObject> create)
        {
            var path = $"{Folder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !overwrite)
            {
                return existing;
            }

            var root = create();
            root.name = name;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static T EnsureComponent<T>(string name, bool overwrite, BattleVisuals visuals, System.Func<BattleVisuals, GameObject> create) where T : Component
        {
            return Ensure(name, overwrite, () => create(visuals)).GetComponent<T>();
        }

        private static void Assign(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite BarSpriteOf(BattleVisuals visuals)
        {
            return visuals.BarSprite != null ? visuals.BarSprite : ViewFactory.WhiteSprite;
        }

        private static TextMeshPro WorldText(Transform parent, string name, float height, Color color, int order, Vector3 position)
        {
            return ViewFactory.CreateWorldText(parent, name, string.Empty, height, color, order, position);
        }

        // ---------- world prefabs ----------

        private static GameObject CreatePilotSlot(BattleVisuals visuals)
        {
            var root = new GameObject(PilotSlotName);
            root.AddComponent<BoardSlotAuthoring>();
            root.AddComponent<CircleCollider2D>();
            root.AddComponent<SlotView>();

            // the parts, and what each is for, are defined in one place
            PilotSlotParts.CreateScaler(root);
            PilotSlotParts.EnsureParts(root, visuals);
            return root;
        }

        private static GameObject CreateOperator(BattleVisuals visuals)
        {
            var root = new GameObject(OperatorSlotName);
            root.AddComponent<BoardOperatorSlotAuthoring>();
            var view = root.AddComponent<OperatorView>();
            var coin = ViewFactory.CreateSprite(root.transform, "Base", visuals.SlotBase, OperatorBaseColor, 2, Vector3.zero);
            ViewFactory.FitSprite(coin, 0.4f);
            var symbol = ViewFactory.CreateSprite(root.transform, "Symbol", null, Color.white, 3, Vector3.zero);
            var symbolText = WorldText(root.transform, "SymbolText", 0.4f, Color.black, 3, Vector3.zero);

            symbolText.text = "+";

            Assign(view, "_coin", coin);
            Assign(view, "_symbol", symbol);
            Assign(view, "_symbolText", symbolText);
            return root;
        }

        private static GameObject CreateEnemy(BattleVisuals visuals)
        {
            const float size = 2.3f;
            var root = new GameObject("EnemyView");
            var view = root.AddComponent<EnemyView>();

            var picture = ViewFactory.CreateSprite(root.transform, "Picture", ViewFactory.WhiteSprite, new Color(0.8f, 0.3f, 0.3f), 2, Vector3.zero);
            var barPosition = new Vector3(0f, -size * 0.5f - 0.2f, 0f);
            var barScale = new Vector3(2.2f, 0.18f, 1f);
            var barBack = ViewFactory.CreateSprite(root.transform, "BarBack", BarSpriteOf(visuals), new Color(0f, 0f, 0f, 0.75f), 3, barPosition);
            barBack.transform.localScale = barScale;
            var barFill = ViewFactory.CreateSprite(root.transform, "BarFill", BarSpriteOf(visuals), new Color(0.85f, 0.2f, 0.2f), 4, barPosition);
            barFill.transform.localScale = barScale;
            var title = WorldText(root.transform, "Title", 0.34f, Color.white, 5, new Vector3(0f, size * 0.5f + 0.35f, 0f));
            var intent = WorldText(root.transform, "Intent", 0.3f, Color.white, 5, new Vector3(0f, size * 0.5f + 0.75f, 0f));
            var health = WorldText(root.transform, "HealthText", 0.26f, Color.white, 6, barPosition + new Vector3(0f, -0.28f, 0f));

            Assign(view, "_picture", picture);
            Assign(view, "_title", title);
            Assign(view, "_intent", intent);
            Assign(view, "_barBack", barBack);
            Assign(view, "_barFill", barFill);
            Assign(view, "_healthText", health);
            EnsureEnemyQueueText(root);
            return root;
        }

        private static GameObject CreateChainLabel(BattleVisuals visuals)
        {
            var root = new GameObject("ChainLabel");
            var text = root.AddComponent<TextMeshPro>();
            text.fontSize = 3f;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.sortingOrder = 9;
            text.rectTransform.sizeDelta = new Vector2(6f, 1f);
            return root;
        }

        private static GameObject CreatePopup(BattleVisuals visuals)
        {
            var root = new GameObject("Popup");
            var text = root.AddComponent<TextMeshPro>();
            text.fontSize = 4.5f;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.sortingOrder = 100;
            text.rectTransform.sizeDelta = new Vector2(8f, 1f);
            return root;
        }

        // ---------- UI prefabs ----------

        private static GameObject CreateCard(BattleVisuals visuals)
        {
            var size = new Vector2(170f, 230f);
            var rect = ViewFactory.CreateUiRoot(null, "CardView", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, size, new Vector2(0.5f, 0f));
            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.14f, 0.16f, 0.28f, 0.96f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var view = rect.gameObject.AddComponent<CardView>();

            var picture = ViewFactory.CreateImage(rect, "Picture", Color.white, new Vector2(130f, 120f), new Vector2(0f, 38f));
            picture.raycastTarget = false;
            picture.preserveAspect = true;
            var name = ViewFactory.CreateUiText(rect, "Name", string.Empty, 30f, Color.white, new Vector2(160f, 40f), new Vector2(0f, -40f));
            var stats = ViewFactory.CreateUiText(rect, "Stats", string.Empty, 28f, Color.white, new Vector2(160f, 80f), new Vector2(0f, -92f));

            Assign(view, "_button", button);
            Assign(view, "_background", background);
            Assign(view, "_picture", picture);
            Assign(view, "_name", name);
            Assign(view, "_stats", stats);
            EnsureCardStatBars(rect.gameObject);
            return rect.gameObject;
        }

        private static GameObject CreateOption()
        {
            var size = new Vector2(420f, 74f);
            var rect = ViewFactory.CreateUiRoot(null, "MenuOption", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.35f, 0.65f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            button.colors = colors;
            ViewFactory.CreateUiText(rect, "Label", string.Empty, 30f, Color.white, size, Vector2.zero);
            return rect.gameObject;
        }

        private static GameObject CreateHud(BattleVisuals visuals, Button optionPrefab)
        {
            var root = ViewFactory.CreateUiRoot(null, "BattleHud", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var view = root.gameObject.AddComponent<HudView>();

            var health = CornerText(root, "Health", new Vector2(0f, 1f), new Vector2(30f, -30f), TextAlignmentOptions.TopLeft, 46f);
            var turn = CornerText(root, "Turn", new Vector2(1f, 1f), new Vector2(-30f, -30f), TextAlignmentOptions.TopRight, 46f);
            var actionPoints = CornerText(root, "ActionPoints", new Vector2(0f, 0f), new Vector2(40f, 40f), TextAlignmentOptions.BottomLeft, 60f);

            var hintRect = ViewFactory.CreateUiRoot(root, "Hint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(1200f, 60f), new Vector2(0.5f, 0f));
            var hint = hintRect.gameObject.AddComponent<TextMeshProUGUI>();
            hint.fontSize = 34f;
            hint.alignment = TextAlignmentOptions.Center;
            hint.color = new Color(1f, 1f, 0.6f);
            hint.raycastTarget = false;

            var playRect = ViewFactory.CreateUiRoot(root, "PlayTurn", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(330f, 120f), new Vector2(1f, 0f));
            var playImage = playRect.gameObject.AddComponent<Image>();
            playImage.color = new Color(0.15f, 0.4f, 0.8f);
            var playButton = playRect.gameObject.AddComponent<Button>();
            playButton.targetGraphic = playImage;
            var playColors = playButton.colors;
            playColors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            playButton.colors = playColors;
            ViewFactory.CreateUiText(playRect, "Label", "Play Turn", 44f, Color.white, new Vector2(330f, 120f), Vector2.zero);

            var handArea = ViewFactory.CreateUiRoot(root, "HandArea", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1100f, 290f), new Vector2(0.5f, 0f));
            var hand = handArea.gameObject.AddComponent<HandView>();
            Assign(hand, "_area", handArea);

            var testPanel = CreateTestPanel(root);
            var menu = CreateActionMenu(root, optionPrefab);

            var outcome = ViewFactory.CreateUiRoot(root, "Outcome", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dim = outcome.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);
            var outcomeText = ViewFactory.CreateUiText(outcome, "Text", string.Empty, 54f, Color.white, new Vector2(1100f, 420f), new Vector2(0f, 80f));
            var restart = ViewFactory.CreateButton(outcome, "Restart", "Play again", new Vector2(420f, 110f), new Vector2(0f, -190f), new Color(0.15f, 0.4f, 0.8f), 44f);

            Assign(view, "_health", health);
            Assign(view, "_actionPoints", actionPoints);
            Assign(view, "_turn", turn);
            Assign(view, "_hint", hint);
            Assign(view, "_playTurn", playButton);
            Assign(view, "_outcomePanel", outcome.gameObject);
            Assign(view, "_outcomeText", outcomeText);
            Assign(view, "_restart", restart);
            EnsureHudGiveUp(root.gameObject);
            return root.gameObject;
        }

        private static TextMeshProUGUI CornerText(Transform parent, string name, Vector2 anchor, Vector2 offset, TextAlignmentOptions alignment, float fontSize)
        {
            var rect = ViewFactory.CreateUiRoot(parent, name, anchor, anchor, offset, new Vector2(500f, 90f), anchor);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static TestPanelView CreateTestPanel(Transform parent)
        {
            var root = ViewFactory.CreateUiRoot(parent, "TestPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -100f), new Vector2(380f, 230f), new Vector2(0f, 1f));
            var background = root.gameObject.AddComponent<Image>();
            background.color = DarkBackground;
            var panel = root.gameObject.AddComponent<TestPanelView>();

            var label = ViewFactory.CreateUiText(root, "Encounter", string.Empty, 26f, Color.white, new Vector2(360f, 54f), new Vector2(0f, 82f));
            var previous = ViewFactory.CreateButton(root, "Previous", "<", new Vector2(80f, 54f), new Vector2(-130f, 14f), new Color(0.25f, 0.3f, 0.45f), 34f);
            var next = ViewFactory.CreateButton(root, "Next", ">", new Vector2(80f, 54f), new Vector2(130f, 14f), new Color(0.25f, 0.3f, 0.45f), 34f);
            var position = ViewFactory.CreateUiText(root, "Position", string.Empty, 24f, new Color(1f, 1f, 1f, 0.7f), new Vector2(120f, 54f), new Vector2(0f, 14f));
            var start = ViewFactory.CreateButton(root, "Start", "Start", new Vector2(165f, 60f), new Vector2(-92f, -66f), new Color(0.2f, 0.55f, 0.3f), 30f);
            var stop = ViewFactory.CreateButton(root, "Stop", "Stop", new Vector2(165f, 60f), new Vector2(92f, -66f), new Color(0.6f, 0.25f, 0.25f), 30f);

            Assign(panel, "_label", label);
            Assign(panel, "_position", position);
            Assign(panel, "_start", start);
            Assign(panel, "_stop", stop);
            Assign(panel, "_previous", previous);
            Assign(panel, "_next", next);
            return panel;
        }

        private static ActionMenuView CreateActionMenu(Transform parent, Button optionPrefab)
        {
            var root = ViewFactory.CreateUiRoot(parent, "ActionMenu", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var menu = root.gameObject.AddComponent<ActionMenuView>();

            var blockerRect = ViewFactory.CreateUiRoot(root, "Blocker", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var blockerImage = blockerRect.gameObject.AddComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.001f);
            var blocker = blockerRect.gameObject.AddComponent<Button>();
            blocker.targetGraphic = blockerImage;
            blocker.transition = Selectable.Transition.None;

            var panel = ViewFactory.CreateUiRoot(blockerRect, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 100f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.08f, 0.15f, 0.97f);

            Assign(menu, "_blocker", blocker);
            Assign(menu, "_panel", panel);
            Assign(menu, "_optionPrefab", optionPrefab);
            return menu;
        }
    }
}
