using System;
using System.Collections.Generic;
using UnityEngine;

namespace Configurations
{
    /// <summary>A list of sprites shown one after another (an animation made of frames), with its frame time. Whether it loops
    /// depends on its use: a pilot's Idle loops, its Attack and Defeated play once.</summary>
    [Serializable]
    public class SpriteSequence
    {
        [SerializeField] private List<Sprite> _sprites = new List<Sprite>();
        [SerializeField] private float _spriteChangeTime = 0.1f;

        public List<Sprite> Sprites => _sprites;
        public float SpriteChangeTime => _spriteChangeTime;

        /// <summary>True when at least one frame is assigned; an empty sequence is simply not played.</summary>
        public bool HasFrames => _sprites != null && _sprites.Exists(sprite => sprite != null);
    }
}
