using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// Sets the colours of the lerp-colour shader (LerpColorShader: _DarkColor and _BrightColor) on one sprite renderer
    /// without making a copy of its material, so many slots can share one material and still each have their own colours.
    /// A renderer whose material has no such properties is left alone. Colours can fade from the current ones instead of
    /// changing at once.
    /// </summary>
    public static class LerpColorTint
    {
        private static readonly int DarkId = Shader.PropertyToID("_DarkColor");
        private static readonly int BrightId = Shader.PropertyToID("_BrightColor");
        private static MaterialPropertyBlock _block;

        // the running fade of each renderer, so a new colour replaces it instead of fighting it
        private static readonly Dictionary<SpriteRenderer, Tween> Fades = new Dictionary<SpriteRenderer, Tween>();

        /// <param name="seconds">Fade from the current colours over this time; 0 (or no colours yet) sets them at once.</param>
        public static void Apply(SpriteRenderer renderer, ColorPair colors, float seconds = 0f)
        {
            if (renderer == null)
            {
                return;
            }

            if (Fades.TryGetValue(renderer, out var running))
            {
                running.Kill();
                Fades.Remove(renderer);
            }

            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            if (seconds <= 0f || !_block.HasColor(DarkId) || !Application.isPlaying)
            {
                Set(renderer, colors.Dark, colors.Bright);
                return;
            }

            var fromDark = _block.GetColor(DarkId);
            var fromBright = _block.GetColor(BrightId);
            Fades[renderer] = DOVirtual.Float(0f, 1f, seconds, t =>
                {
                    if (renderer != null)
                    {
                        Set(renderer, Color.Lerp(fromDark, colors.Dark, t), Color.Lerp(fromBright, colors.Bright, t));
                    }
                })
                .SetEase(Ease.OutQuad)
                .SetLink(renderer.gameObject)
                .OnKill(() => Fades.Remove(renderer));
        }

        private static void Set(SpriteRenderer renderer, Color dark, Color bright)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor(DarkId, dark);
            _block.SetColor(BrightId, bright);
            renderer.SetPropertyBlock(_block);
        }
    }
}
