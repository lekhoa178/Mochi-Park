using System;
using UnityEngine;

namespace MochiPark.Runtime.Views
{
    [Serializable]
    public sealed class FloatingScoreAnimationSettings
    {
        [SerializeField, Min(0f)] private float visibleDuration = 1.5f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.5f;
        [SerializeField] private float risePixels = 68f;
        [SerializeField, Min(0f)] private float startScale = 1.05f;
        [SerializeField, Min(0f)] private float endScale = 0.94f;

        public float VisibleDuration => Mathf.Max(0f, visibleDuration);
        public float FadeDuration => Mathf.Max(0f, fadeDuration);
        public float RisePixels => risePixels;
        public float StartScale => Mathf.Max(0f, startScale);
        public float EndScale => Mathf.Max(0f, endScale);
        public float Lifetime => VisibleDuration + FadeDuration;
    }
}
