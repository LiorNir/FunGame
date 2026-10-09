using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Utils
{
    public static class AnimationUtils
    {
        public static async UniTask PlayAnimationClipAsync(Animation animation, AnimationClip clip, CancellationToken cancellationToken)
        {
            //TODO: consider a legacy animation player component
            var duration = clip.length;
            animation.clip = clip;
            animation.Play();
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: cancellationToken);
        }
    }
}
