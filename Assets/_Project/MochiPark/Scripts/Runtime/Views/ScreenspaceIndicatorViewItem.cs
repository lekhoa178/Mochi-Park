using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ScreenspaceIndicatorViewItem : MonoBehaviour
    {
        private enum ScaleAnimationKind
        {
            None,
            Showing,
            Hiding
        }

        private const float AddScaleDuration = 0.18f;
        private const float RemoveScaleDuration = 0.1f;

        [FormerlySerializedAs("amountRoot")]
        [SerializeField] private RectTransform indicatorRoot;
        [FormerlySerializedAs("amountText")]
        [SerializeField] private TMP_Text indicatorText;
        [FormerlySerializedAs("amountFormat")]
        [SerializeField] private string textFormat = "{0}";
        [SerializeField] private AnimationCurve addScaleCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.68f, 1.1f),
            new Keyframe(1f, 1f));
        [SerializeField] private AnimationCurve removeScaleCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.28f, 1.1f),
            new Keyframe(1f, 0f));

        private Coroutine scaleCoroutine;
        private ScaleAnimationKind scaleAnimationKind;

        public void SetText(string text, Vector2 anchoredPosition)
        {
            if (!ValidateReferences())
            {
                return;
            }

            indicatorRoot.anchoredPosition = anchoredPosition;
            indicatorText.text = string.Format(CultureInfo.InvariantCulture, ResolveTextFormat(), text);
        }

        public void ShowAnimated()
        {
            if (!ValidateReferences())
            {
                return;
            }

            bool shouldPlayAddAnimation = !gameObject.activeSelf || scaleAnimationKind == ScaleAnimationKind.Hiding;
            gameObject.SetActive(true);

            if (scaleAnimationKind == ScaleAnimationKind.Showing)
            {
                return;
            }

            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }

            if (shouldPlayAddAnimation)
            {
                indicatorRoot.localScale = Vector3.zero;
                scaleAnimationKind = ScaleAnimationKind.Showing;
                scaleCoroutine = StartCoroutine(TweenScale(addScaleCurve, AddScaleDuration, null));
                return;
            }

            indicatorRoot.localScale = Vector3.one;
            scaleAnimationKind = ScaleAnimationKind.None;
        }

        public void HideAnimated()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            if (!ValidateReferences())
            {
                gameObject.SetActive(false);
                return;
            }

            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }

            scaleAnimationKind = ScaleAnimationKind.Hiding;
            scaleCoroutine = StartCoroutine(TweenScale(removeScaleCurve, RemoveScaleDuration, () => gameObject.SetActive(false)));
        }

        public void HideImmediate()
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
                scaleCoroutine = null;
            }

            scaleAnimationKind = ScaleAnimationKind.None;
            if (indicatorRoot != null)
            {
                indicatorRoot.localScale = Vector3.zero;
            }

            gameObject.SetActive(false);
        }

        private System.Collections.IEnumerator TweenScale(AnimationCurve scaleCurve, float duration, System.Action completed)
        {
            AnimationCurve resolvedCurve = ResolveScaleCurve(scaleCurve);
            if (duration <= Mathf.Epsilon)
            {
                indicatorRoot.localScale = Vector3.one * resolvedCurve.Evaluate(1f);
                scaleCoroutine = null;
                scaleAnimationKind = ScaleAnimationKind.None;
                completed?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                indicatorRoot.localScale = Vector3.one * Mathf.Max(0f, resolvedCurve.Evaluate(t));
                yield return null;
            }

            indicatorRoot.localScale = Vector3.one * Mathf.Max(0f, resolvedCurve.Evaluate(1f));
            scaleCoroutine = null;
            scaleAnimationKind = ScaleAnimationKind.None;
            completed?.Invoke();
        }

        private static AnimationCurve ResolveScaleCurve(AnimationCurve scaleCurve)
        {
            return scaleCurve != null && scaleCurve.length > 0
                ? scaleCurve
                : AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        private bool ValidateReferences()
        {
            bool valid = true;

            if (indicatorRoot == null)
            {
                indicatorRoot = GetComponent<RectTransform>();
            }

            if (indicatorRoot == null)
            {
                Debug.LogError($"{nameof(ScreenspaceIndicatorViewItem)} on '{name}' is missing an indicator root reference.", this);
                valid = false;
            }

            if (indicatorText == null)
            {
                indicatorText = GetComponentInChildren<TMP_Text>(true);
            }

            if (indicatorText == null)
            {
                Debug.LogError($"{nameof(ScreenspaceIndicatorViewItem)} on '{name}' is missing an indicator text reference.", this);
                valid = false;
            }

            return valid;
        }

        private string ResolveTextFormat()
        {
            return string.IsNullOrWhiteSpace(textFormat) ? "{0}" : textFormat;
        }

        private void OnValidate()
        {
            ValidateReferences();
        }
    }
}
