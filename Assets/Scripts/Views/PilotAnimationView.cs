using System;
using System.Threading;
using Configurations;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Views
{
    /// <summary>
    /// Plays a pilot's Idle / Attack / Defeated sprite sequences on a sprite renderer (board) or a UI image (hand card); a
    /// new play call cancels the running one. The sequences are set in the Inspector or, in battle, from the pilot's asset
    /// (<see cref="SetSequences"/>). A missing or empty sequence is skipped: whatever is showing keeps showing. Idle loops; Attack
    /// and Defeated play once (Attack then goes back to Idle, Defeated stays on its last frame). Idle starts at the frame the
    /// clock says, so a picture that is rebuilt (the hand is redrawn on every change)
    /// carries on smoothly instead of restarting.
    /// </summary>
    public class PilotAnimationView : MonoBehaviour
    {
        // a frame time of 0 would make a looping sequence spin without ever waiting
        private const float MinFrameTime = 0.01f;

        [SerializeField] private Animation _animation;
        [Tooltip("The picture to animate on the board. Set this or Image.")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [Tooltip("The picture to animate in the UI (a hand card). Set this or Sprite Renderer.")]
        [SerializeField] private Image _image;

        [Header("Idle")]
        [SerializeField] private SpriteSequence _idle;

        [Header("Attack")]
        [Tooltip("Played once, then back to the Idle loop.")]
        [SerializeField] private SpriteSequence _attack;

        [Header("Defeated")]
        [SerializeField] private SpriteSequence _defeated;

        private CancellationTokenSource _sequenceCts;

        /// <summary>True from the first frame shown until <see cref="Stop"/>: the renderer shows this view's frames, not a still picture.</summary>
        public bool IsShowingFrames { get; private set; }

        public bool HasIdle => _idle != null && _idle.HasFrames;
        public bool HasAttack => _attack != null && _attack.HasFrames;
        public bool HasDefeated => _defeated != null && _defeated.HasFrames;

        /// <summary>Replaces the sequences (any may be null) and stops what is playing.</summary>
        public void SetSequences(SpriteSequence idle, SpriteSequence attack, SpriteSequence defeated)
        {
            Stop();
            _idle = idle;
            _attack = attack;
            _defeated = defeated;
        }

        public UniTask PlayIdleAnimation() => PlaySequence(_idle, true).AsUniTask();

        public async UniTask PlayAttackAnimation()
        {
            if (!HasAttack)
            {
                return;
            }

            var finished = await PlaySequence(_attack, false);
            if (finished)
            {
                PlayIdleAnimation().Forget();
            }
        }

        /// <summary>Plays the Defeated frames once (the last one stays); the task ends when they have played.</summary>
        public UniTask PlayDefeatedAnimation()
        {
            if (!HasDefeated || !HasTarget)
            {
                return UniTask.CompletedTask;
            }

            return PlaySequence(_defeated, false).AsUniTask();
        }

        private bool HasTarget => _spriteRenderer != null || _image != null;

        private static float FrameTime(SpriteSequence sequence) => Mathf.Max(sequence.SpriteChangeTime, MinFrameTime);


        /// <summary>Stops the running sequence; the renderer keeps the last frame shown until someone changes it.</summary>
        public void Stop()
        {
            StopCurrentSequence();
            IsShowingFrames = false;
        }

        /// <summary>Plays a sequence, looping or once; true if a one-time sequence ran to its end, false if it was skipped or cancelled.</summary>
        private async UniTask<bool> PlaySequence(SpriteSequence sequence, bool loop)
        {
            if (sequence == null || !sequence.HasFrames || !HasTarget)
            {
                return false;
            }

            StopCurrentSequence();
            _sequenceCts = new CancellationTokenSource();
            var token = _sequenceCts.Token;
            if (_animation != null)
            {
                _animation.Stop();
            }

            var frameTime = FrameTime(sequence);
            var frames = sequence.Sprites.FindAll(sprite => sprite != null);
            var start = loop ? (int)(Time.unscaledTime / frameTime) % frames.Count : 0;
            while (true)
            {
                for (var step = 0; step < frames.Count; step++)
                {
                    if (!ShowFrame(frames[(start + step) % frames.Count]))
                    {
                        return false;
                    }

                    IsShowingFrames = true;
                    var wasCancelled = await UniTask.Delay(TimeSpan.FromSeconds(frameTime), cancellationToken: token)
                        .SuppressCancellationThrow();
                    if (wasCancelled)
                    {
                        return false;
                    }
                }

                if (!loop)
                {
                    return true;
                }
            }
        }

        // false once the picture is gone (its object destroyed)
        private bool ShowFrame(Sprite sprite)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = sprite;
                return true;
            }

            if (_image != null)
            {
                _image.sprite = sprite;
                return true;
            }

            return false;
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
