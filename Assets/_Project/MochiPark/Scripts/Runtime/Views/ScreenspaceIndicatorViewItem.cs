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
        public enum ScreenSizeMode
        {
            FixedToScreen,
            CameraDistanceResponsive
        }

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
        [SerializeField] private ScreenSizeMode screenSizeMode = ScreenSizeMode.FixedToScreen;
        [SerializeField, Min(0.01f), Tooltip("World-space tile width represented by Reference Screen Size.")]
        private float referenceWorldSize = 2f;
        [SerializeField, Min(0.01f), Tooltip("UI width that Reference World Size occupies when this item has scale 1.")]
        private float referenceScreenSize = 200f;
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
        private float screenSizeScale = 1f;

        public ScreenSizeMode SizingMode => screenSizeMode;
        public float ReferenceWorldSize => Mathf.Max(0.01f, referenceWorldSize);
        public float ReferenceScreenSize => Mathf.Max(0.01f, referenceScreenSize);

        public void SetText(string text, Vector2 anchoredPosition)
        {
            SetText(text, anchoredPosition, 1f);
        }

        public void SetText(string text, Vector2 anchoredPosition, float sizeScale)
        {
            if (!ValidateReferences())
            {
                return;
            }

            screenSizeScale = Mathf.Max(0f, sizeScale);
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
                ApplyScale(0f);
                scaleAnimationKind = ScaleAnimationKind.Showing;
                scaleCoroutine = StartCoroutine(TweenScale(addScaleCurve, AddScaleDuration, null));
                return;
            }

            ApplyScale(1f);
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
                ApplyScale(0f);
            }

            gameObject.SetActive(false);
        }

        private System.Collections.IEnumerator TweenScale(AnimationCurve scaleCurve, float duration, System.Action completed)
        {
            AnimationCurve resolvedCurve = ResolveScaleCurve(scaleCurve);
            if (duration <= Mathf.Epsilon)
            {
                ApplyScale(resolvedCurve.Evaluate(1f));
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
                ApplyScale(resolvedCurve.Evaluate(t));
                yield return null;
            }

            ApplyScale(resolvedCurve.Evaluate(1f));
            scaleCoroutine = null;
            scaleAnimationKind = ScaleAnimationKind.None;
            completed?.Invoke();
        }

        private void ApplyScale(float animationScale)
        {
            indicatorRoot.localScale = Vector3.one * (screenSizeScale * Mathf.Max(0f, animationScale));
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
            referenceWorldSize = Mathf.Max(0.01f, referenceWorldSize);
            referenceScreenSize = Mathf.Max(0.01f, referenceScreenSize);
            ValidateReferences();
        }
    }
}
