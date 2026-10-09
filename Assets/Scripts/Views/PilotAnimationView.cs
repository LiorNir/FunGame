using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Views
{
    public class PilotAnimationView : MonoBehaviour
    {
        [Serializable]
        private class SpriteSequence
        {
            [SerializeField] private List<Sprite> _sprites;
            [SerializeField] private float _spriteChangeTime = 0.1f;

            [SerializeField] private bool _loop = true;

            public List<Sprite> Sprites => _sprites;
            public float SpriteChangeTime => _spriteChangeTime;
            public bool Loop => _loop;
        }

        [SerializeField] private Animation _animation;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Header("Idle")]
        [SerializeField] private SpriteSequence _idle;

        [Header("Attack")]
        [SerializeField] private SpriteSequence _attack;

        [Header("Defeated")]
        [SerializeField] private SpriteSequence _defeated;

        private CancellationTokenSource _sequenceCts;

        public UniTask PlayIdleAnimation() => PlaySequence(_idle);

        public UniTask PlayAttackAnimation() => PlaySequence(_attack);

        public UniTask PlayDefeatedAnimation() => PlaySequence(_defeated);

        private async UniTask PlaySequence(SpriteSequence sequence)
        {
            if (sequence.Sprites == null || sequence.Sprites.Count == 0) return;

            StopCurrentSequence();
            _sequenceCts = new CancellationTokenSource();
            var token = _sequenceCts.Token;
            if (_animation != null) _animation.Stop();

            while (true)
            {
                foreach (var sprite in sequence.Sprites)
                {
                    _spriteRenderer.sprite = sprite;
                    var wasCancelled = await UniTask.Delay(TimeSpan.FromSeconds(sequence.SpriteChangeTime), cancellationToken: token)
                        .SuppressCancellationThrow();
                    if (wasCancelled) return;
                }

                if (!sequence.Loop) return;
            }
        }

        private void StopCurrentSequence()
        {
            if (_sequenceCts == null) return;

            _sequenceCts.Cancel();
            _sequenceCts.Dispose();
            _sequenceCts = null;
        }

        private void OnDestroy()
        {
            StopCurrentSequence();
        }
        
        //TODO: DELETE ALL THIS
        public bool ShouldPlayIdle;
        public bool ShouldPlayAttack;
        public bool ShouldPlayDefeated;

        private void Start()
        {
            if (ShouldPlayIdle)
            {
                PlayIdleAnimation().Forget();
                    return;
            }
            
            if (ShouldPlayAttack)
            {
                PlayAttackAnimation().Forget();
                return;
            }
            
            if (ShouldPlayDefeated)
            {
                PlayDefeatedAnimation().Forget();
            }
        }
    }
}
