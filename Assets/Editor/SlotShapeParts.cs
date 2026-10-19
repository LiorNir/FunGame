using System.Linq;
using BattleView;
using UnityEditor;
using UnityEngine;

namespace FunGame.EditorTools
{
    /// <summary>
    /// The two looks of the pilot slot prefab (see <see cref="SlotShapeLook"/>): the circle look wraps the existing round
    /// parts (PilotBackground, Ring, the shadows) where they are, and the rect look is a RectLook group with the rectangle
    /// pictures (RectSlotBase_Texture as the background, RectSlotColorPart_Texture as the frame, on the ring's tint
    /// material). The rect look is hidden in the prefab, so boards are edited with circles; the Battle Visuals' Slot Shape
    /// picks the look in battle. Existing looks are kept as they are.
    /// </summary>
    public static class SlotShapeParts
    {
        private const string RectBasePath = "Assets/Textures/RectSlotBase_Texture.png";
        private const string RectFramePath = "Assets/Textures/RectSlotColorPart_Texture.png";

        // the rectangle's width in the slot's own scale (the round ring is 1.28 across)
        private const float RectWidth = 1.5f;
        private const int OrderBackground = 2;
        private const int OrderFrame = 4;

        public static void Ensure(GameObject slotRoot, Transform looks, SpriteRenderer background, SpriteRenderer ring)
        {
            var view = slotRoot.GetComponent<SlotView>();
            var serialized = new SerializedObject(view);

            var circle = EnsureLook(looks, "CircleLook", PilotSlotShape.Circle, background, ring,
                new[] { background, ring }.Where(part => part != null).Select(part => part.gameObject)
                    .Concat(looks.Cast<Transform>().Where(child => child.name.StartsWith("Shadow")).Select(child => child.gameObject))
                    .ToArray());
            serialized.FindProperty("_circleLook").objectReferenceValue = circle;

            var rectHolder = looks.Find("RectLook");
            var rect = rectHolder != null ? rectHolder.GetComponent<SlotShapeLook>() : null;
            if (rect == null)
            {
                rect = CreateRectLook(looks, ring, background);
            }
            serialized.FindProperty("_rectLook").objectReferenceValue = rect;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SlotShapeLook EnsureLook(Transform looks, string name, PilotSlotShape shape, SpriteRenderer background,
                                                SpriteRenderer frame, GameObject[] parts)
        {
            var existing = looks.Find(name);
            if (existing != null && existing.GetComponent<SlotShapeLook>() != null)
            {
                return existing.GetComponent<SlotShapeLook>();
            }

            var holder = existing != null ? existing.gameObject : new GameObject(name);
            holder.transform.SetParent(looks, false);
            var look = holder.GetComponent<SlotShapeLook>();
            if (look == null)
            {
                look = holder.AddComponent<SlotShapeLook>();
            }
            Configure(look, shape, background, frame, parts);
            return look;
        }

        private static SlotShapeLook CreateRectLook(Transform looks, SpriteRenderer ring, SpriteRenderer circleBackground)
        {
            var group = new GameObject("RectLook");
            group.transform.SetParent(looks, false);
            var baseSprite = FirstSprite(RectBasePath);
            var frameSprite = FirstSprite(RectFramePath);
            var scale = baseSprite != null ? RectWidth / (baseSprite.rect.width / baseSprite.pixelsPerUnit) : 1f;

            var background = Part(group.transform, "RectBase", baseSprite, scale, OrderBackground);
            var frame = Part(group.transform, "RectFrame", frameSprite, scale, OrderFrame);
            if (circleBackground != null)
            {
                // the base picture is light and tinted like the round background (the slot's state colours start from it)
                background.color = circleBackground.color;
            }

            if (ring != null)
            {
                // the same tint material as the ring, so the chain colours and slot states colour it the same way
                frame.sharedMaterial = ring.sharedMaterial;
            }

            var look = group.AddComponent<SlotShapeLook>();
            Configure(look, PilotSlotShape.Rectangle, background, frame, new[] { group });
            group.SetActive(false);
            return look;
        }

        /// <summary>A picture of the rectangle placed where it sits in its canvas, so the base and the frame line up.</summary>
        private static SpriteRenderer Part(Transform parent, string name, Sprite sprite, float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            go.transform.localScale = Vector3.one * scale;
            if (sprite != null)
            {
                // both pictures were cut from canvases of the same size: offset each by where its cut sits in the canvas
                var canvasCentre = new Vector2(sprite.texture.width, sprite.texture.height) * 0.5f;
                var pivotInCanvas = sprite.rect.position + sprite.pivot;
                go.transform.localPosition = (Vector3)((pivotInCanvas - canvasCentre) / sprite.pixelsPerUnit * scale);
            }

            return renderer;
        }

        private static void Configure(SlotShapeLook look, PilotSlotShape shape, SpriteRenderer background, SpriteRenderer frame, GameObject[] parts)
        {
            var serialized = new SerializedObject(look);
            serialized.FindProperty("_shape").enumValueIndex = (int)shape;
            serialized.FindProperty("_background").objectReferenceValue = background;
            serialized.FindProperty("_frame").objectReferenceValue = frame;
            var list = serialized.FindProperty("_parts");
            list.arraySize = parts.Length;
            for (var index = 0; index < parts.Length; index++)
            {
                list.GetArrayElementAtIndex(index).objectReferenceValue = parts[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite FirstSprite(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderByDescending(sprite => sprite.rect.width * sprite.rect.height).FirstOrDefault();
        }
    }
}
