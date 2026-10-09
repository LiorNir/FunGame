using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleView
{
    /// <summary>Small helpers that build the sprites, texts and UI pieces the battle views are made of.</summary>
    public static class ViewFactory
    {
        // World-space TextMeshPro: a font size of 10 is roughly one world unit tall.
        private const float FontSizePerWorldUnit = 10f;

        private const float DefaultPopupHeight = 0.45f;

        private static Material _spriteMaterial;

        /// <summary>The popup prefab of the Battle Visuals, set by the battle session. Without one a plain text is made.</summary>
        public static TextMeshPro PopupPrefab { get; set; }

        public static Material SpriteMaterial
        {
            get
            {
                if (_spriteMaterial == null)
                {
                    _spriteMaterial = new Material(Shader.Find("Sprites/Default"));
                }

                return _spriteMaterial;
            }
        }

        private static Sprite _whiteSprite;

        /// <summary>A one-unit white square, used for bars when no bar sprite is assigned.</summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    var texture = Texture2D.whiteTexture;
                    _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
                }

                return _whiteSprite;
            }
        }

        // ---------- world space ----------

        public static SpriteRenderer CreateSprite(Transform parent, string name, Sprite sprite, Color color, int sortingOrder, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>Scales a sprite renderer so its sprite fits in a square of the given size.</summary>
        public static void FitSprite(SpriteRenderer renderer, float size)
        {
            if (renderer.sprite == null)
            {
                return;
            }

            var bounds = renderer.sprite.bounds.size;
            var largest = Mathf.Max(bounds.x, bounds.y);
            renderer.transform.localScale = Vector3.one * (largest > 0f ? size / largest : 1f);
        }

        /// <summary>
        /// Radius of a slot's base sprite in the slot object's own units: the sprite's half size times the scales of the
        /// base and of any objects between it and <paramref name="root"/> (the root's own scale is not included).
        /// </summary>
        public static float SpriteRadius(SpriteRenderer sprite, Transform root)
        {
            if (sprite == null)
            {
                return BoardAuthoring.SlotFootprint.FallbackRadius;
            }

            var extents = sprite.sprite != null ? sprite.sprite.bounds.extents : new Vector3(0.5f, 0.5f, 0f);
            var scale = 1f;
            for (var current = sprite.transform; current != null && current != root; current = current.parent)
            {
                scale *= Mathf.Abs(current.localScale.x);
            }

            return Mathf.Max(extents.x, extents.y) * scale;
        }

        public static TextMeshPro CreateWorldText(
            Transform parent,
            string name,
            string text,
            float heightInWorldUnits,
            Color color,
            int sortingOrder,
            Vector3 localPosition,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = heightInWorldUnits * FontSizePerWorldUnit;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.sortingOrder = sortingOrder;
            tmp.rectTransform.sizeDelta = new Vector2(6f, 1f);
            return tmp;
        }

        public static void SetWorldTextHeight(TextMeshPro text, float heightInWorldUnits)
        {
            text.fontSize = heightInWorldUnits * FontSizePerWorldUnit;
        }

        /// <summary>Creates a line between two world positions (given in world space, stored relative to the parent).</summary>
        public static LineRenderer CreateLine(Transform parent, string name, Vector3 from, Vector3 to, float width, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            // local space, so the line follows its parent when the board is moved or scaled
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, parent.InverseTransformPoint(from));
            line.SetPosition(1, parent.InverseTransformPoint(to));
            line.startWidth = width;
            line.endWidth = width;
            line.material = SpriteMaterial;
            line.sortingOrder = sortingOrder;
            return line;
        }

        /// <summary>Shows a number or word that floats up from a world position and fades out.</summary>
        public static void ShowPopup(Vector3 worldPosition, string text, Color color, float height = 0.45f)
        {
            TextMeshPro tmp;
            if (PopupPrefab != null)
            {
                tmp = Object.Instantiate(PopupPrefab, worldPosition, Quaternion.identity);
                tmp.fontSize = PopupPrefab.fontSize * (height / DefaultPopupHeight);
            }
            else
            {
                var created = new GameObject("Popup");
                created.transform.position = worldPosition;
                tmp = created.AddComponent<TextMeshPro>();
                tmp.fontSize = height * FontSizePerWorldUnit;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.sortingOrder = 100;
                tmp.rectTransform.sizeDelta = new Vector2(8f, 1f);
            }

            var go = tmp.gameObject;
            tmp.text = text;
            tmp.color = color;

            var sequence = DOTween.Sequence();
            sequence.Append(go.transform.DOMoveY(worldPosition.y + 0.8f, 0.9f).SetEase(Ease.OutCubic));
            sequence.Join(DOTween.ToAlpha(() => tmp.color, value => tmp.color = value, 0f, 0.9f).SetDelay(0.3f));
            sequence.OnComplete(() => Object.Destroy(go));
        }

        // ---------- UI ----------

        public static RectTransform CreateUiRoot(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image CreateImage(Transform parent, string name, Color color, Vector2 size, Vector2 anchoredPosition)
        {
            var rect = CreateUiRoot(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI CreateUiText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            Color color,
            Vector2 size,
            Vector2 anchoredPosition,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var rect = CreateUiRoot(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 anchoredPosition, Color color, float fontSize = 34f)
        {
            var image = CreateImage(parent, name, color, size, anchoredPosition);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            button.colors = colors;
            CreateUiText(image.transform, "Label", label, fontSize, Color.white, size, Vector2.zero);
            return button;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = label;
            }
        }

        public static Tween TweenColor(SpriteRenderer renderer, Color target, float duration)
        {
            return DOTween.To(() => renderer.color, value => renderer.color = value, target, duration);
        }

        public static UniTask Wait(float seconds)
        {
            return UniTask.Delay(System.TimeSpan.FromSeconds(seconds));
        }
    }
}
