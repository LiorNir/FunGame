using BoardAuthoring;
using FunGame.Board;
using TMPro;
using UnityEngine;

namespace BattleView
{
    /// <summary>The look of one operator slot, as a prefab: a base and the operator's symbol on top of it.</summary>
    public class OperatorView : MonoBehaviour, ISlotFootprint
    {
        [SerializeField] private SpriteRenderer _coin;
        [SerializeField] private SpriteRenderer _symbol;
        [Tooltip("Shown instead of the symbol picture when the Battle Visuals has no sprite for the operator.")]
        [SerializeField] private TextMeshPro _symbolText;
        [Tooltip("The symbol picture is scaled to fit in a square of this size (world units).")]
        [SerializeField] private float _symbolSize = 0.34f;

        /// <summary>The size of the slot is the size of its base, so it is changed on the prefab's "Base" child.</summary>
        public float Radius => ViewFactory.SpriteRadius(_coin, transform);

        public void Show(Operation operation, BattleVisuals visuals)
        {
            if (_coin != null && _coin.sprite == null)
            {
                _coin.sprite = visuals.SlotBase;
            }

            var sprite = visuals.GetOperatorSprite(operation);
            _symbol.sprite = sprite;
            _symbol.enabled = sprite != null;
            ViewFactory.FitSprite(_symbol, _symbolSize);

            if (_symbolText != null)
            {
                _symbolText.text = sprite == null ? BattleVisuals.OperatorSymbol(operation) : string.Empty;
            }
        }
    }
}
