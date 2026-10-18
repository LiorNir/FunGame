using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Views
{
    /// <summary>
    /// Plays a pilot's Idle / Attack / Defeated clips (legacy Animation) on a picture: a board sprite or a hand card's UI
    /// image. The clips and frames come from the pilot's animation prefab (<see cref="SetSource"/>) and key the
    /// <see cref="SpriteFrames"/> on this object, so the same clip plays on both. Idle loops; Attack plays once and goes
    /// back to Idle; Defeated plays once and stays on its last frame. A missing or empty clip is skipped: whatever is
    /// showing keeps showing. Idle starts where the clock says, so a hand card that is redrawn carries on smoothly.
    /// </summary>
    public class PilotAnimationView : MonoBehaviour
    {
        public const string IdleName = "Idle";
        public const string AttackName = "Attack";
        public const string DefeatedName = "Defeated";

        [SerializeField] private Animation _animation;
        [SerializeField] private SpriteFrames _frames;

        private readonly List<string> _loadedClips = new List<string>();
        private CancellationTokenSource _attackCts;

        /// <summary>True while this view drives the picture (a clip is playing or holds its last frame), not a still picture.</summary>
        public bool IsShowingFrames { get; private set; }

        public bool HasIdle => Has(IdleName);
        public bool HasAttack => Has(AttackName);
        public bool HasDefeated => Has(DefeatedName);

        /// <summary>Loads the frames and clips of a pilot's animation prefab (null for none) and stops what is playing.</summary>
        public void SetSource(GameObject animationPrefab)
        {
            Stop();
            if (_animation == null || _frames == null)
            {
                return;
            }

            foreach (var name in _loadedClips)
            {
                if (_animation.GetClip(name) != null)
                {
                    _animation.RemoveClip(name);
                }
            }

            _loadedClips.Clear();
            var sourceFrames = animationPrefab != null ? animationPrefab.GetComponent<SpriteFrames>() : null;
            var sourceAnimation = animationPrefab != null ? animationPrefab.GetComponent<Animation>() : null;
            _frames.SetFrames(sourceFrames != null ? sourceFrames.Frames : null);
            if (sourceAnimation == null)
            {
                return;
            }

            foreach (AnimationState state in sourceAnimation)
            {
                if (state.clip != null && state.clip.legacy && _animation.GetClip(state.name) == null)
                {
                    _animation.AddClip(state.clip, state.name);
                    _loadedClips.Add(state.name);
                }
            }
        }

        public UniTask PlayIdleAnimation()
        {
            if (!Begin(IdleName, WrapMode.Loop))
            {
                return UniTask.CompletedTask;
            }

            // in step with the clock, so a picture that is rebuilt (a redrawn hand card) does not restart its idle
            var state = _animation[IdleName];
            state.time = state.length > 0f ? Time.time % state.length : 0f;
            return UniTask.CompletedTask;
        }

        public async UniTask PlayAttackAnimation()
        {
            if (!Begin(AttackName, WrapMode.Once))
            {
                return;
            }

            _attackCts = new CancellationTokenSource();
            var cancelled = await UniTask.Delay(TimeSpan.FromSeconds(_animation[AttackName].length), cancellationToken: _attackCts.Token)
                .SuppressCancellationThrow();
            if (!cancelled && this != null)
            {
                PlayIdleAnimation().Forget();
            }
        }

        /// <summary>Plays the Defeated clip once (its last frame stays); the task ends when it has played.</summary>
        public UniTask PlayDefeatedAnimation()
        {
            return Begin(DefeatedName, WrapMode.ClampForever)
                ? UniTask.Delay(TimeSpan.FromSeconds(_animation[DefeatedName].length))
                : UniTask.CompletedTask;
        }

        /// <summary>Stops playing; the picture keeps the last frame shown until someone changes it.</summary>
        public void Stop()
        {
            CancelAttack();
            if (_animation != null)
            {
                _animation.Stop();
            }

            if (_frames != null)
            {
                _frames.Driving = false;
            }

            IsShowingFrames = false;
        }

        private bool Has(string clipName)
        {
            if (_animation == null || _frames == null || !_frames.HasFrames)
            {
                return false;
            }

            var clip = _animation.GetClip(clipName);
            return clip != null && clip.length > 0f && !clip.empty;
        }

        private bool Begin(string clipName, WrapMode wrapMode)
        {
            if (!Has(clipName))
            {
                return false;
            }

            CancelAttack();
            _animation[clipName].wrapMode = wrapMode;
            _frames.Driving = true;
            _animation.Play(clipName);
            _animation.Sample();
            _frames.Apply();
            IsShowingFrames = true;
            return true;
        }

        private void CancelAttack()
        {
            if (_attackCts == null)
            {
                return;
            }

            _attackCts.Cancel();
            _attackCts.Dispose();
            _attackCts = null;
        }

        private void OnDestroy()
        {
            CancelAttack();
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
