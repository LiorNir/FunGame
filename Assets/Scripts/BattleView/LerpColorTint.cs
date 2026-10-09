using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// Sets the colours of the lerp-colour shader (LerpColorShader: _DarkColor and _BrightColor) on one sprite renderer
    /// without making a copy of its material, so many slots can share one material and still each have their own colours.
    /// A renderer whose material has no such properties is left alone.
    /// </summary>
    public static class LerpColorTint
    {
        private static readonly int DarkId = Shader.PropertyToID("_DarkColor");
        private static readonly int BrightId = Shader.PropertyToID("_BrightColor");
        private static MaterialPropertyBlock _block;

        public static void Apply(SpriteRenderer renderer, ColorPair colors)
        {
            if (renderer == null)
            {
                return;
            }

            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(DarkId, colors.Dark);
            _block.SetColor(BrightId, colors.Bright);
            renderer.SetPropertyBlock(_block);
        }
    }
}
