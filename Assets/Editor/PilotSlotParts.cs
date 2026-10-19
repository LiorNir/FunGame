using BattleView;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// The one definition of what a pilot slot prefab is made of. It is used to build a fresh prefab and to upgrade an
    /// existing one: every part is looked up by name and only the ones that are missing are created, so changes made to
    /// the prefab by hand (sizes, sprites, positions, colours) are kept. Old names are renamed and parts that no longer
    /// exist in the design are removed. The result is wired to the slot view.
    ///
    /// <code>
    /// PilotSlot                      slot data, slot view, click collider
    ///   GeneralScaler                scales the whole look
    ///     PilotBackground            disc behind the pilot; its size is the slot's size
    ///     Pilot                      the placed pilot's picture
    ///     Ring                       decorative frame with its own material (not touched by code)
    ///     StatsBox                   groups the two boxes; shown while a pilot stands in the slot
    ///       AttackBox                instance of the StatBox prefab (the number is coloured by code)
    ///       DefenseBox               instance of the StatBox prefab (its fill is the wound bar)
    ///     PilotNameText              name of a pilot that has no picture yet
    ///     LockIcon / LockText        shown while locked; the text says how it unlocks
    ///     BonusBadge / BonusText     shown when the slot has a bonus
    /// </code>
    ///
    /// The StatBox prefab (one box: StatBorder, StatBackground, StatFill and the number, StatText) is its own asset so that
    /// the look of a stat box is changed in one place and both boxes follow.
    /// </summary>
    public static class PilotSlotParts
    {
        public const string Scaler = "GeneralScaler";
        public const string StatBoxPath = BattlePrefabCreator.Folder + "/StatBox.prefab";

        private const string StatsBoxName = "StatsBox";
        private const string AttackBoxName = "AttackBox";
        private const string DefenseBoxName = "DefenseBox";

        // the two boxes sit one above the other to the left of the pilot (attack on top), so nothing is drawn under the
        // slot, where the operator of the row below and, on the root row, the hand would hide it
        private static readonly Vector3 StatsBoxPosition = new Vector3(-1.07f, 0f, 0f);
        private const float StatBoxOffsetY = 0.2f;

        // names used before the parts were reorganised
        private const string LegacyBackground = "Base";
        private const string LegacyNameText = "CenterText";
        private const string InactiveTextName = "InactiveText"; // only used by the switched-off INACTIVE label

        // Starting looks, written into a part only when it is created. After that the prefab is the only place they live.
        private const float BackgroundDiameter = 0.9f;
        private const float ScalerScale = 1.4f;
        private static readonly Color BackgroundColor = new Color(0.12f, 0.12f, 0.2f);
        private static readonly Color BonusTextColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color StatsBorderColor = new Color(0.6f, 0.6f, 0.65f);
        private static readonly Color StatsBackgroundColor = new Color(0f, 0f, 0f, 0.92f);
        private static readonly Color StatFillColor = new Color(0.15f, 0.25f, 0.4f); // only a preview: the slot colours it

        // draw order, back to front (the operator slot's base uses 2; board lines use 0)
        private const int OrderBackground = 2;
        private const int OrderPilot = 3;
        private const int OrderRing = 4;
        private const int OrderLockedOverlay = 5;
        private const int OrderStatsBorder = 7;
        private const int OrderStatsBackground = 8;
        private const int OrderStatFill = 9;
        private const int OrderStats = 10;
        private const int OrderBadge = 10;
        private const int OrderIcon = 10;
        private const int OrderTexts = 11;

        /// <summary>Creates the scaler object under a new root. Call before <see cref="EnsureParts"/> when building from nothing.</summary>
        public static Transform CreateScaler(GameObject root)
        {
            var scaler = new GameObject(Scaler).transform;
            scaler.SetParent(root.transform, false);
            scaler.localScale = Vector3.one * ScalerScale;
            return scaler;
        }

        public static void EnsureParts(GameObject root, BattleVisuals visuals)
        {
            var view = root.GetComponent<SlotView>();
            var scaler = root.transform.Find(Scaler);
            var parent = scaler != null ? scaler : root.transform;

            var background = EnsureSprite(parent, "PilotBackground", LegacyBackground, visuals.SlotBase, BackgroundColor, OrderBackground, Vector3.zero, out var created);
            if (created)
            {
                ViewFactory.FitSprite(background, BackgroundDiameter);
            }

            // the pulsing ability glow was replaced by the Ability Ready particle effect
            var oldGlow = parent.Find("AbilityGlow");
            if (oldGlow != null)
            {
                Object.DestroyImmediate(oldGlow.gameObject);
            }

            var pilot = EnsureSprite(parent, "Pilot", null, null, Color.white, OrderPilot, Vector3.zero, out _);
            // plays the pilot's Idle / Attack / Defeated clips (from its animation prefab) on the picture
            var pilotAnimation = AnimationParts.Ensure(pilot.gameObject);

            // the ring is decoration with its own graphic and material; an old placeholder disc would hide the pilot
            var ring = EnsureSprite(parent, "Ring", null, null, Color.white, OrderRing, Vector3.zero, out _);
            if (ring.sprite == AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"))
            {
                ring.sprite = null;
            }

            ring.enabled = true;

            // the wound bar is gone: the defense box's fill shows what is left of the defense
            foreach (var legacyBar in new[] { parent.Find("WoundBack"), parent.Find("WoundFill") })
            {
                if (legacyBar != null)
                {
                    Object.DestroyImmediate(legacyBar.gameObject);
                }
            }

            // (the StatBox prefab is brought up to date before the slot is loaded; this covers a slot built from scratch)
            EnsureStatBoxPrefab(visuals, false);
            var statsBox = EnsureStatsBox(parent, visuals, out var attackBox, out var defenseBox);
            var nameText = EnsureText(parent, "PilotNameText", LegacyNameText, 0.2f, Color.white, OrderTexts, Vector3.zero, out _);

            var lockIcon = EnsureSprite(parent, "LockIcon", null, null, Color.white, OrderIcon, new Vector3(0f, 0.1f, 0f), out _);
            var lockText = EnsureText(parent, "LockText", null, 0.14f, new Color(1f, 0.85f, 0.4f), OrderTexts, new Vector3(0f, -0.22f, 0f), out created);
            if (created)
            {
                lockText.text = "turn 3";
            }

            var bonusBadge = EnsureSprite(parent, "BonusBadge", null, null, Color.white, OrderBadge, new Vector3(0.43f, 0.43f, 0f), out _);
            var bonusText = EnsureText(parent, "BonusText", null, 0.3f, BonusTextColor, OrderTexts, new Vector3(0.43f, 0.43f, 0f), out created);
            if (created)
            {
                bonusText.fontStyle = FontStyles.Bold;
                bonusText.text = "x2";
            }

            // INACTIVE LABEL (switched off: an unpowered pilot is shown greyed instead). To bring it back, uncomment the
            // creation below and the Assign line further down, uncomment the matching lines in SlotView, and run
            // Tools > Fun Game > Upgrade Pilot Slot Prefab. The object was removed from the prefab by the first upgrade, so
            // it is created again. (Its old look: height 0.2, colour (1, 0.85, 0.4), above the slot, text "<b>INACTIVE</b>".)
            // var inactiveText = EnsureText(parent, InactiveTextName, null, 0.2f, new Color(1f, 0.85f, 0.4f), OrderTexts, new Vector3(0f, 0.65f, 0f), out created);
            // if (created)
            // {
            //     inactiveText.text = "<b>INACTIVE</b>";
            // }

            // beside the pilot, one line per status: above it the line would cross the operator icon between two slots
            var statusText = EnsureText(parent, "PilotStatusText", null, 0.15f, new Color(1f, 0.55f, 0.35f), OrderTexts, new Vector3(0.6f, 0.12f, 0f), out created);
            if (created)
            {
                statusText.fontStyle = FontStyles.Bold;
                statusText.rectTransform.pivot = new Vector2(0f, 0.5f);
                statusText.alignment = TextAlignmentOptions.MidlineLeft;
                statusText.rectTransform.localPosition = new Vector3(0.6f, 0.12f, 0f);
                statusText.text = "SURGE: ATK x2\nnext: SNIPE -50%";
            }

            // drawn over the pilot while an enemy has locked it (assign a picture, for example chains)
            var lockedOverlay = EnsureSprite(parent, "LockedOverlay", null, null, Color.white, OrderLockedOverlay, Vector3.zero, out _);
            lockedOverlay.enabled = false;
            var effects = PilotEffectParts.Ensure(view.gameObject, parent);
            // the circle look (these round parts) and the rectangle look; the Battle Visuals' Slot Shape picks one in battle
            SlotShapeParts.Ensure(view.gameObject, parent, background, ring);

            var serialized = new SerializedObject(view);
            Assign(serialized, "_pilotBackground", background);
            Assign(serialized, "_pilotRenderer", pilot);
            Assign(serialized, "_pilotAnimation", pilotAnimation);
            Assign(serialized, "_effects", effects);
            Assign(serialized, "_lockedOverlay", lockedOverlay);
            Assign(serialized, "_attackBox", attackBox);
            Assign(serialized, "_defenseBox", defenseBox);
            Assign(serialized, "_ring", ring);
            Assign(serialized, "_bonusText", bonusText);
            Assign(serialized, "_statsBox", statsBox.gameObject);
            Assign(serialized, "_lockIcon", lockIcon);
            Assign(serialized, "_lockText", lockText);
            Assign(serialized, "_bonusBadge", bonusBadge);
            Assign(serialized, "_pilotNameText", nameText);
            Assign(serialized, "_pilotStatusText", statusText);
            // INACTIVE LABEL: Assign(serialized, "_inactiveText", inactiveText);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // the click area follows the background (the slot view also does this when a battle starts)
            var collider = root.GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                collider.radius = view.Radius;
            }
        }

        // ---------- helpers ----------

        /// <summary>
        /// Creates the StatBox prefab if it is missing (or always, when <paramref name="overwrite"/> is set): a border, a
        /// background and a number, sized for values up to three digits with one decimal.
        /// </summary>
        public static GameObject EnsureStatBoxPrefab(BattleVisuals visuals, bool overwrite)
        {
            BattlePrefabCreator.EnsureFolder();
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatBoxPath);
            if (existing != null && !overwrite)
            {
                // a StatBox made before it had its view component or its fill gets them, everything else on it is kept
                if (existing.GetComponent<StatBoxView>() == null || existing.transform.Find("StatFill") == null)
                {
                    var contents = PrefabUtility.LoadPrefabContents(StatBoxPath);
                    try
                    {
                        WireStatBoxView(contents, visuals);
                        PrefabUtility.SaveAsPrefabAsset(contents, StatBoxPath);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(contents);
                    }

                    existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatBoxPath);
                }

                return existing;
            }

            var root = new GameObject("StatBox");
            var border = ViewFactory.CreateSprite(root.transform, "StatBorder", BarSpriteOf(visuals), StatsBorderColor, OrderStatsBorder, Vector3.zero);
            border.transform.localScale = new Vector3(0.7f, 0.36f, 1f);
            var background = ViewFactory.CreateSprite(root.transform, "StatBackground", BarSpriteOf(visuals), StatsBackgroundColor, OrderStatsBackground, Vector3.zero);
            background.transform.localScale = new Vector3(0.64f, 0.3f, 1f);
            var text = ViewFactory.CreateWorldText(root.transform, "StatText", "12.5", 0.24f, Color.white, OrderStats, Vector3.zero);
            text.name = "StatText";
            WireStatBoxView(root, visuals);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, StatBoxPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>
        /// Brings a StatBox root up to date: the view component, the fill (made the size of the background when missing) and
        /// the draw order of the parts, all wired to the view. What is there already is kept.
        /// </summary>
        private static void WireStatBoxView(GameObject root, BattleVisuals visuals)
        {
            var view = root.GetComponent<StatBoxView>();
            if (view == null)
            {
                view = root.AddComponent<StatBoxView>();
            }

            var border = root.transform.Find("StatBorder").GetComponent<SpriteRenderer>();
            var background = root.transform.Find("StatBackground").GetComponent<SpriteRenderer>();
            var text = root.transform.Find("StatText").GetComponent<TextMeshPro>();
            var fillTransform = root.transform.Find("StatFill");
            SpriteRenderer fill;
            if (fillTransform == null)
            {
                fill = ViewFactory.CreateSprite(root.transform, "StatFill", BarSpriteOf(visuals), StatFillColor, OrderStatFill, background.transform.localPosition);
                fill.transform.localScale = background.transform.localScale;
            }
            else
            {
                fill = fillTransform.GetComponent<SpriteRenderer>();
            }

            border.sortingOrder = OrderStatsBorder;
            background.sortingOrder = OrderStatsBackground;
            fill.sortingOrder = OrderStatFill;
            text.sortingOrder = OrderStats;

            var serialized = new SerializedObject(view);
            Assign(serialized, "_border", border);
            Assign(serialized, "_fill", fill);
            Assign(serialized, "_text", text);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform EnsureStatsBox(Transform parent, BattleVisuals visuals, out StatBoxView attackBox, out StatBoxView defenseBox)
        {
            var group = parent.Find(StatsBoxName);
            if (group == null)
            {
                group = new GameObject(StatsBoxName).transform;
                group.SetParent(parent, false);
                group.localPosition = StatsBoxPosition;
            }

            // the single combined box (and, before that, the loose numbers) is replaced by the two boxes
            foreach (var legacy in new[] { group.Find("StatsBorder"), group.Find("StatsBackground"), group.Find("Stats"), parent.Find("Stats") })
            {
                if (legacy != null)
                {
                    Object.DestroyImmediate(legacy.gameObject);
                }
            }

            attackBox = EnsureStatBox(group, AttackBoxName, StatBoxOffsetY, visuals);
            defenseBox = EnsureStatBox(group, DefenseBoxName, -StatBoxOffsetY, visuals);
            return group;
        }

        /// <summary>Finds a stat box in the group or adds an instance of the StatBox prefab; returns its view.</summary>
        private static StatBoxView EnsureStatBox(Transform group, string name, float offsetY, BattleVisuals visuals)
        {
            var box = group.Find(name);
            if (box == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(EnsureStatBoxPrefab(visuals, false), group);
                instance.name = name;
                instance.transform.localPosition = new Vector3(0f, offsetY, 0f);
                box = instance.transform;
            }

            var view = box.GetComponent<StatBoxView>();
            if (view == null)
            {
                Debug.LogError($"'{name}' in the pilot slot is an instance of {StatBoxPath}, which has no StatBoxView. Run the upgrade again.");
            }

            return view;
        }

        private static Transform FindPart(Transform parent, string name, string legacyName)
        {
            var found = parent.Find(name);
            if (found == null && legacyName != null)
            {
                found = parent.Find(legacyName);
                if (found != null)
                {
                    found.name = name;
                }
            }

            return found;
        }

        private static SpriteRenderer EnsureSprite(Transform parent, string name, string legacyName, Sprite sprite, Color color, int order, Vector3 position, out bool created)
        {
            var existing = FindPart(parent, name, legacyName);
            created = existing == null;
            var renderer = created
                ? ViewFactory.CreateSprite(parent, name, sprite, color, order, position)
                : existing.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            return renderer;
        }

        private static TextMeshPro EnsureText(Transform parent, string name, string legacyName, float height, Color color, int order, Vector3 position, out bool created)
        {
            var existing = FindPart(parent, name, legacyName);
            created = existing == null;
            var text = created
                ? ViewFactory.CreateWorldText(parent, name, string.Empty, height, color, order, position)
                : existing.GetComponent<TextMeshPro>();
            text.sortingOrder = order;
            return text;
        }

        private static Sprite BarSpriteOf(BattleVisuals visuals)
        {
            return visuals.BarSprite != null ? visuals.BarSprite : ViewFactory.WhiteSprite;
        }

        private static void Assign(SerializedObject target, string field, Object value)
        {
            var property = target.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"The slot view has no field '{field}'.");
                return;
            }

            property.objectReferenceValue = value;
        }
    }
}
