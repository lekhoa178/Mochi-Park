using System;
using UnityEngine;
using UnityEngine.UI;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    public sealed class JoyFlyVFXView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform rootTransform;
        [SerializeField] private RectTransform joyIconTransform;
        [SerializeField] private RectTransform trailRibbonTransform;
        [SerializeField] private Image joyIconImage;
        [SerializeField] private Image trailRibbonImage;
        [SerializeField] private Image impactBurstImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Motion")]
        [SerializeField] private float minTravelDuration = 0.58f;
        [SerializeField] private float maxTravelDuration = 0.82f;
        [SerializeField] private float curveOffsetPixels = 150f;
        [SerializeField] private float randomCurveOffsetPixels = 90f;
        [SerializeField] private float spawnScaleDuration = 0.16f;
        [SerializeField] private float spawnScale = 0.58f;
        [SerializeField] private float spawnOvershootScale = 1.16f;
        [SerializeField] private float trailMaxLength = 160f;
        [SerializeField] private float impactDuration = 0.28f;

        private UIVFXCanvasView canvasView;
        private RectTransform destination;
        private Action<JoyFlyVFXView, int> arrived;
        private Action<JoyFlyVFXView, int> completed;
        private ParticleSystem[] particleSystems;
        private Vector2 startLocalPosition;
        private Vector2 previousLocalPosition;
        private float elapsed;
        private float travelDuration;
        private float signedCurveOffset;
        private float impactElapsed;
        private int joyAmount;
        private bool isFlying;
        private bool isImpacting;

        public bool IsActive => isFlying || isImpacting;
        public int JoyAmount => joyAmount;

        public void Play(
            UIVFXCanvasView targetCanvasView,
            Vector2 sourceLocalPosition,
            RectTransform targetDestination,
            int amount,
            Action<JoyFlyVFXView, int> arrivedCallback,
            Action<JoyFlyVFXView, int> completedCallback)
        {
            CacheReferences();

            canvasView = targetCanvasView;
            destination = targetDestination;
            arrived = arrivedCallback;
            completed = completedCallback;
            joyAmount = Mathf.Max(0, amount);
            startLocalPosition = sourceLocalPosition;
            previousLocalPosition = sourceLocalPosition;
            elapsed = 0f;
            impactElapsed = 0f;
            travelDuration = UnityEngine.Random.Range(
                Mathf.Max(0.01f, minTravelDuration),
                Mathf.Max(minTravelDuration, maxTravelDuration));
            float direction = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            signedCurveOffset = direction *
                Mathf.Max(0f, curveOffsetPixels + UnityEngine.Random.Range(-randomCurveOffsetPixels, randomCurveOffsetPixels));

            gameObject.SetActive(true);
            ResetVisualState();
            SetRootPosition(sourceLocalPosition);
            PlayParticles();
            isFlying = true;
            isImpacting = false;
        }

        public void ResetForPool()
        {
            isFlying = false;
            isImpacting = false;
            canvasView = null;
            destination = null;
            arrived = null;
            completed = null;
            joyAmount = 0;
            elapsed = 0f;
            impactElapsed = 0f;
            StopAndClearParticles();
            ResetVisualState();
            gameObject.SetActive(false);
        }

        private void Awake()
        {
            CacheReferences();
            ResetVisualState();
        }

        private void Update()
        {
            if (isFlying)
            {
                UpdateFlight();
                return;
            }

            if (isImpacting)
            {
                UpdateImpact();
            }
        }

        private void CacheReferences()
        {
            if (rootTransform == null)
            {
                rootTransform = transform as RectTransform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            particleSystems ??= GetComponentsInChildren<ParticleSystem>(true);
        }

        private void UpdateFlight()
        {
            if (canvasView == null || destination == null || rootTransform == null)
            {
                Complete();
                return;
            }

            if (!canvasView.TryRectTransformToLocal(destination, out Vector2 endLocalPosition))
            {
                Complete();
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, travelDuration));
            float eased = t * t;
            Vector2 currentLocalPosition = EvaluateCurve(startLocalPosition, endLocalPosition, eased);
            SetRootPosition(currentLocalPosition);
            UpdateSpawnScale(t);
            UpdateTrail(previousLocalPosition, currentLocalPosition, t);
            previousLocalPosition = currentLocalPosition;

            if (t >= 1f)
            {
                BeginImpact(endLocalPosition);
            }
        }

        private Vector2 EvaluateCurve(Vector2 start, Vector2 end, float t)
        {
            Vector2 delta = end - start;
            Vector2 perpendicular = delta.sqrMagnitude > Mathf.Epsilon
                ? new Vector2(-delta.y, delta.x).normalized
                : Vector2.up;
            Vector2 control = (start + end) * 0.5f + perpendicular * signedCurveOffset;
            float inverse = 1f - t;
            return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
        }

        private void BeginImpact(Vector2 endLocalPosition)
        {
            isFlying = false;
            isImpacting = true;
            impactElapsed = 0f;
            SetRootPosition(endLocalPosition);
            NotifyArrived();
            SetGraphicEnabled(joyIconImage, false);
            SetGraphicEnabled(trailRibbonImage, false);

            if (impactBurstImage != null)
            {
                impactBurstImage.enabled = true;
                SetImageAlpha(impactBurstImage, 1f);
                impactBurstImage.rectTransform.localScale = Vector3.one * 0.35f;
            }
        }

        private void UpdateImpact()
        {
            impactElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(impactElapsed / Mathf.Max(0.01f, impactDuration));
            float scale = Mathf.LerpUnclamped(0.35f, 1.65f, 1f - (1f - t) * (1f - t));
            if (impactBurstImage != null)
            {
                impactBurstImage.rectTransform.localScale = Vector3.one * scale;
                SetImageAlpha(impactBurstImage, 1f - t);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f - t;
            }

            if (t >= 1f)
            {
                Complete();
            }
        }

        private void Complete()
        {
            NotifyArrived();
            Action<JoyFlyVFXView, int> callback = completed;
            isFlying = false;
            isImpacting = false;
            arrived = null;
            completed = null;
            callback?.Invoke(this, 0);
        }

        private void NotifyArrived()
        {
            if (joyAmount <= 0)
            {
                return;
            }

            Action<JoyFlyVFXView, int> callback = arrived;
            int amount = joyAmount;
            joyAmount = 0;
            callback?.Invoke(this, amount);
        }

        private void UpdateSpawnScale(float flightT)
        {
            if (rootTransform == null)
            {
                return;
            }

            float spawnT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, spawnScaleDuration));
            float scale = spawnT < 0.5f
                ? Mathf.Lerp(spawnScale, spawnOvershootScale, spawnT * 2f)
                : Mathf.Lerp(spawnOvershootScale, 1f, (spawnT - 0.5f) * 2f);
            float sparkle = Mathf.Sin(flightT * Mathf.PI * 4f) * 0.04f;
            rootTransform.localScale = Vector3.one * Mathf.Max(0.01f, scale + sparkle);
        }

        private void UpdateTrail(Vector2 previousPosition, Vector2 currentPosition, float flightT)
        {
            if (trailRibbonTransform == null || trailRibbonImage == null)
            {
                return;
            }

            Vector2 delta = currentPosition - previousPosition;
            float distance = delta.magnitude;
            if (distance <= 0.001f)
            {
                return;
            }

            Vector2 direction = delta / distance;
            float length = Mathf.Clamp(distance * 8f, 28f, trailMaxLength);
            trailRibbonTransform.anchoredPosition = -direction * length * 0.5f;
            trailRibbonTransform.sizeDelta = new Vector2(length, trailRibbonTransform.sizeDelta.y);
            trailRibbonTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            SetImageAlpha(trailRibbonImage, Mathf.Lerp(0.85f, 0.25f, flightT));
        }

        private void ResetVisualState()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (rootTransform != null)
            {
                rootTransform.localScale = Vector3.one;
                rootTransform.localRotation = Quaternion.identity;
            }

            if (joyIconTransform != null)
            {
                joyIconTransform.localScale = Vector3.one;
                joyIconTransform.localRotation = Quaternion.identity;
            }

            SetGraphicEnabled(joyIconImage, true);
            SetGraphicEnabled(trailRibbonImage, true);
            SetGraphicEnabled(impactBurstImage, false);
            SetImageAlpha(joyIconImage, 1f);
            SetImageAlpha(trailRibbonImage, 0.85f);
            SetImageAlpha(impactBurstImage, 0f);
        }

        private void SetRootPosition(Vector2 localPosition)
        {
            if (rootTransform != null)
            {
                rootTransform.anchoredPosition = localPosition;
            }
        }

        private void PlayParticles()
        {
            if (particleSystems == null)
            {
                return;
            }

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particleSystem.main;
                main.useUnscaledTime = true;
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }
        }

        private void StopAndClearParticles()
        {
            if (particleSystems == null)
            {
                return;
            }

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Clear(true);
            }
        }

        private static void SetGraphicEnabled(Graphic graphic, bool enabled)
        {
            if (graphic != null)
            {
                graphic.enabled = enabled;
            }
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }
    }
}
