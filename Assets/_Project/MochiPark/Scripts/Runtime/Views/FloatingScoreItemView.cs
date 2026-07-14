using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class FloatingScoreItemView : MonoBehaviour
    {
        [SerializeField] private RectTransform animatedRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private Image scoreIcon;

        private Coroutine animationCoroutine;
        private bool completionRaised;

        public event Action<FloatingScoreItemView> Completed;

        public void Play(
            int amount,
            Vector2 anchoredPosition,
            FloatingScoreAnimationSettings settings)
        {
            if (!ValidateReferences())
            {
                Destroy(gameObject);
                return;
            }

            RectTransform itemRect = (RectTransform)transform;
            itemRect.anchoredPosition = anchoredPosition;

            FloatingScoreAnimationSettings resolvedSettings = settings ?? new FloatingScoreAnimationSettings();
            amountText.SetText("+{0}", amount);
            canvasGroup.alpha = 1f;

            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }

            animationCoroutine = StartCoroutine(PlayAnimation(resolvedSettings));
        }

        private IEnumerator PlayAnimation(FloatingScoreAnimationSettings settings)
        {
            Vector2 startPosition = animatedRoot.anchoredPosition;
            float lifetime = settings.Lifetime;

            animatedRoot.localScale = Vector3.one * settings.StartScale;

            if (lifetime <= Mathf.Epsilon)
            {
                DestroyAfterCompletion();
                yield break;
            }

            float elapsed = 0f;
            while (this != null && elapsed < lifetime)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / lifetime);
                float eased = 1f - Mathf.Pow(1f - normalized, 2f);

                animatedRoot.anchoredPosition = startPosition + Vector2.up * (settings.RisePixels * eased);
                animatedRoot.localScale = Vector3.one * Mathf.Lerp(settings.StartScale, settings.EndScale, normalized);
                canvasGroup.alpha = CalculateAlpha(elapsed, settings.VisibleDuration, settings.FadeDuration);

                yield return null;
            }

            DestroyAfterCompletion();
        }

        private static float CalculateAlpha(float elapsed, float visibleDuration, float fadeDuration)
        {
            float fadeElapsed = Mathf.Max(0f, elapsed - visibleDuration);
            if (fadeDuration <= Mathf.Epsilon)
            {
                return fadeElapsed > 0f ? 0f : 1f;
            }

            float fadeNormalized = Mathf.Clamp01(fadeElapsed / fadeDuration);
            return 1f - Mathf.SmoothStep(0f, 1f, fadeNormalized);
        }

        private bool ValidateReferences()
        {
            bool valid = true;

            if (animatedRoot == null)
            {
                Debug.LogError($"{nameof(FloatingScoreItemView)} on '{name}' is missing an animated root reference.", this);
                valid = false;
            }

            if (canvasGroup == null)
            {
                Debug.LogError($"{nameof(FloatingScoreItemView)} on '{name}' is missing a CanvasGroup reference.", this);
                valid = false;
            }

            if (amountText == null)
            {
                Debug.LogError($"{nameof(FloatingScoreItemView)} on '{name}' is missing an amount text reference.", this);
                valid = false;
            }

            if (scoreIcon == null)
            {
                Debug.LogError($"{nameof(FloatingScoreItemView)} on '{name}' is missing a score icon reference.", this);
                valid = false;
            }

            return valid;
        }

        private void DestroyAfterCompletion()
        {
            RaiseCompleted();
            Destroy(gameObject);
        }

        private void RaiseCompleted()
        {
            if (completionRaised)
            {
                return;
            }

            completionRaised = true;
            Completed?.Invoke(this);
        }

        private void OnDestroy()
        {
            RaiseCompleted();
        }

        private void OnValidate()
        {
            ValidateReferences();
        }
    }
}
