using Combat;
using DG.Tweening;
using FunGame.Battle;
using TMPro;
using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// The look of one enemy, as a prefab: its picture, name, revealed action and health bar. All parts are children of
    /// the prefab, so sizes, positions and text sizes are set there. The health bar's full size is whatever the
    /// "Bar Back" child is.
    /// </summary>
    public class EnemyView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _picture;
        [SerializeField] private TextMeshPro _title;
        [SerializeField] private TextMeshPro _intent;
        [SerializeField] private SpriteRenderer _barBack;
        [SerializeField] private SpriteRenderer _barFill;
        [SerializeField] private TextMeshPro _healthText;
        [Tooltip("Optional: which enemy of the lane this is and who comes next (Enemy 1/2, next: Grinder); empty for a lane with one enemy.")]
        [SerializeField] private TextMeshPro _queueText;

        [Tooltip("The enemy's picture is scaled to fit in a square of this size (world units).")]
        [SerializeField] private float _pictureSize = 2.3f;

        private BattleVisuals _visuals;
        private float _barWidth;
        private float _barHeight;
        private Vector3 _barCenter;
        private float _displayHealth;
        private float _maxHealth;

        /// <summary>The mouse entered (true) or left (false) the enemy's picture.</summary>
        public event System.Action<EnemyView, bool> Hovered;

        public string LaneId { get; set; }

        private void OnMouseEnter()
        {
            Hovered?.Invoke(this, true);
        }

        private void OnMouseExit()
        {
            Hovered?.Invoke(this, false);
        }

        public void Init(BattleVisuals visuals)
        {
            _visuals = visuals;
            if (_barBack.sprite == null)
            {
                _barBack.sprite = visuals.BarSprite != null ? visuals.BarSprite : ViewFactory.WhiteSprite;
            }

            if (_barFill.sprite == null)
            {
                _barFill.sprite = _barBack.sprite;
            }

            _barWidth = _barBack.transform.localScale.x;
            _barHeight = _barBack.transform.localScale.y;
            _barCenter = _barBack.transform.localPosition;
        }

        public void Show(EnemySpec spec, float health)
        {
            var source = spec.Source as EnemyDefinition;
            _picture.sprite = source != null ? source.Sprite : null;
            _picture.color = _picture.sprite != null ? Color.white : new Color(0.8f, 0.3f, 0.3f);
            if (_picture.sprite == null)
            {
                _picture.sprite = ViewFactory.WhiteSprite;
            }

            // the prefab sets the standard size, each enemy asset its own scale on top
            ViewFactory.FitSprite(_picture, _pictureSize * (source != null ? source.Scale : 1f));

            // hovering the picture explains the enemy; a collider the size of the picture is added if missing
            var collider = GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<BoxCollider2D>();
            }

            var bounds = _picture.bounds;
            collider.offset = transform.InverseTransformPoint(bounds.center);
            var scale = transform.lossyScale;
            collider.size = new Vector2(bounds.size.x / Mathf.Max(0.0001f, scale.x), bounds.size.y / Mathf.Max(0.0001f, scale.y));

            _title.text = spec.Name;
            _maxHealth = spec.MaxHealth;
            _displayHealth = health;
            UpdateHealthVisual();
        }

        /// <summary>
        /// Shows the revealed action: its name and each effect (attack and defense in their colours). <paramref name="texts"/>,
        /// one per effect, replaces the effects' own descriptions (a snipe that will assassinate says so).
        /// </summary>
        public void SetIntent(EnemyActionData action, System.Collections.Generic.IReadOnlyList<string> texts = null)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (var index = 0; index < action.Effects.Count; index++)
            {
                var effect = action.Effects[index];
                var text = texts != null && index < texts.Count ? texts[index] : effect.Describe();
                parts.Add(effect is AttackEffect ? _visuals.AttackText(text)
                    : effect is DefendEffect ? _visuals.DefenseText(text)
                    : effect is UnleashEffect && text != UnleashEffect.StunnedText ? _visuals.AttackText(text)
                    : effect is ChargeEffect ? _visuals.DefenseText(text)
                    : text == SnipeEffect.AssassinateText ? $"<color=#FF5050>{text}</color>"
                    : text);
            }

            _intent.text = parts.Count == 0 ? action.Name : $"{action.Name}  {string.Join("  ", parts)}";
        }

        public void SetQueue(string text)
        {
            if (_queueText != null)
            {
                _queueText.text = text;
            }
        }

        public void PunchIntent()
        {
            _intent.transform.DOKill(true);
            _intent.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f);
        }

        public Tween SetHealthAnimated(float health, float max)
        {
            _maxHealth = max;
            return DOTween.To(() => _displayHealth, value =>
            {
                _displayHealth = value;
                UpdateHealthVisual();
            }, health, 0.4f).SetEase(Ease.OutCubic);
        }

        public void PlayHit()
        {
            transform.DOKill(true);
            transform.DOShakePosition(0.3f, 0.2f, 20);
        }

        public void PlayAttack()
        {
            transform.DOKill(true);
            transform.DOPunchPosition(new Vector3(0f, -0.8f, 0f), 0.4f, 6);
        }

        public Tween PlaySpawn()
        {
            gameObject.SetActive(true);
            transform.localScale = Vector3.zero;
            return transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        }

        public Tween PlayDefeat()
        {
            transform.DOKill(true);
            var sequence = DOTween.Sequence();
            sequence.Append(transform.DOScale(0f, 0.5f).SetEase(Ease.InBack));
            sequence.OnComplete(() => gameObject.SetActive(false));
            return sequence;
        }

        private void UpdateHealthVisual()
        {
            var fraction = _maxHealth > 0f ? Mathf.Clamp01(_displayHealth / _maxHealth) : 0f;
            _barFill.transform.localScale = new Vector3(_barWidth * fraction, _barHeight, 1f);
            _barFill.transform.localPosition = _barCenter + new Vector3(-_barWidth * 0.5f + _barWidth * fraction * 0.5f, 0f, 0f);
            _healthText.text = $"{DisplayFormat.Value(_displayHealth)} / {DisplayFormat.Value(_maxHealth)}";
        }
    }
}
