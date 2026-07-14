using System.Collections.Generic;
using MochiPark.Runtime.Core;
using UnityEngine;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    public sealed class JoyFlyVFXController : MonoBehaviour
    {
        public delegate bool TryResolveWorldPosition(string unitInstanceId, out Vector3 worldPosition);

        [SerializeField] private UIVFXCanvasView canvasView;
        [SerializeField] private int preloadCount = 8;
        [SerializeField] private JoyFlyVFXView prefabView;
        [SerializeField] private GameObject arrivalVfxPrefab;
        [SerializeField] private float arrivalVfxLifetimeSeconds = 5f;

        private readonly List<JoyFlyVFXView> activeEffects = new List<JoyFlyVFXView>();
        private readonly Stack<JoyFlyVFXView> pooledEffects = new Stack<JoyFlyVFXView>();

        private EventBus eventBus;
        private Camera worldCamera;
        private HudView hudView;
        private TryResolveWorldPosition worldPositionResolver;
        private bool isBound;

        public bool IsBound => isBound;

        public bool Bind(
            EventBus boundEventBus,
            Camera boundWorldCamera,
            HudView boundHudView,
            TryResolveWorldPosition boundWorldPositionResolver)
        {
            Unbind();

            canvasView = canvasView != null ? canvasView : GetComponent<UIVFXCanvasView>();

            if (boundEventBus == null ||
                boundHudView == null ||
                boundHudView.JoyIconTransform == null ||
                boundWorldPositionResolver == null ||
                canvasView == null ||
                prefabView == null)
            {
                Debug.LogWarning(
                    $"{nameof(JoyFlyVFXController)} on '{name}' cannot bind because the event bus, HUD Joy icon, canvas view, resolver, or Joy fly VFX view prefab is missing.",
                    this);
                return false;
            }

            eventBus = boundEventBus;
            worldCamera = boundWorldCamera != null ? boundWorldCamera : Camera.main;
            hudView = boundHudView;
            worldPositionResolver = boundWorldPositionResolver;

            if (!canvasView.Initialize(worldCamera))
            {
                return false;
            }

            PreloadPool();

            eventBus.JoyCreated += OnJoyCreated;
            isBound = true;
            return true;
        }

        public void Unbind()
        {
            if (eventBus != null)
            {
                eventBus.JoyCreated -= OnJoyCreated;
            }

            Clear();
            eventBus = null;
            worldCamera = null;
            hudView = null;
            worldPositionResolver = null;
            isBound = false;
        }

        public void Clear()
        {
            Clear(false);
        }

        public void Clear(bool applyPendingJoy)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                JoyFlyVFXView effect = activeEffects[i];
                if (effect == null)
                {
                    continue;
                }

                if (applyPendingJoy && effect.JoyAmount > 0)
                {
                    hudView?.ApplyJoyArrival(effect.JoyAmount);
                }

                effect.ResetForPool();
                pooledEffects.Push(effect);
            }

            activeEffects.Clear();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void OnJoyCreated(JoyCreatedEvent evt)
        {
            if (!isBound || evt.Amount <= 0)
            {
                return;
            }

            string unitInstanceId = evt.Attribution.SourceUnitInstanceId;
            if (string.IsNullOrEmpty(unitInstanceId) ||
                worldPositionResolver == null ||
                !worldPositionResolver(unitInstanceId, out Vector3 worldPosition) ||
                canvasView == null ||
                !canvasView.TryWorldToLocal(worldCamera, worldPosition, out Vector2 sourceLocalPosition) ||
                hudView == null ||
                hudView.JoyIconTransform == null)
            {
                ApplyJoyImmediately(evt.Amount);
                return;
            }

            JoyFlyVFXView effect = GetEffect();
            if (effect == null)
            {
                ApplyJoyImmediately(evt.Amount);
                return;
            }

            Transform effectTransform = effect.transform;
            if (canvasView.EffectsRoot != null && effectTransform.parent != canvasView.EffectsRoot)
            {
                effectTransform.SetParent(canvasView.EffectsRoot, false);
            }

            effectTransform.SetAsLastSibling();
            activeEffects.Add(effect);
            effect.Play(canvasView, sourceLocalPosition, hudView.JoyIconTransform, evt.Amount, OnEffectArrived, OnEffectCompleted);
        }

        private JoyFlyVFXView GetEffect()
        {
            while (pooledEffects.Count > 0)
            {
                JoyFlyVFXView effect = pooledEffects.Pop();
                if (effect != null)
                {
                    return effect;
                }
            }

            if (prefabView == null || canvasView == null || canvasView.EffectsRoot == null)
            {
                return null;
            }

            JoyFlyVFXView created = Instantiate(prefabView, canvasView.EffectsRoot);
            created.name = prefabView.name;
            created.ResetForPool();
            return created;
        }

        private void PreloadPool()
        {
            int missingCount = Mathf.Max(0, preloadCount - pooledEffects.Count);
            for (int i = 0; i < missingCount; i++)
            {
                JoyFlyVFXView effect = GetEffect();
                if (effect == null)
                {
                    return;
                }

                effect.ResetForPool();
                pooledEffects.Push(effect);
            }
        }

        private void OnEffectArrived(JoyFlyVFXView effect, int amount)
        {
            hudView?.ApplyJoyArrival(amount);
            hudView?.PlayJoyIconPunch();
            PlayArrivalVfx(effect);
        }

        private void OnEffectCompleted(JoyFlyVFXView effect, int amount)
        {
            activeEffects.Remove(effect);

            if (effect == null)
            {
                return;
            }

            effect.ResetForPool();
            pooledEffects.Push(effect);
        }

        private void ApplyJoyImmediately(int amount)
        {
            hudView?.ApplyJoyArrival(amount);
            hudView?.PlayJoyIconPunch();
        }

        private void PlayArrivalVfx(JoyFlyVFXView effect)
        {
            if (arrivalVfxPrefab == null || effect == null || canvasView == null || canvasView.EffectsRoot == null)
            {
                return;
            }

            GameObject arrivalVfx = Instantiate(arrivalVfxPrefab, canvasView.EffectsRoot);
            arrivalVfx.name = arrivalVfxPrefab.name;
            arrivalVfx.SetActive(true);

            Transform arrivalTransform = arrivalVfx.transform;
            Transform effectTransform = effect.transform;
            arrivalTransform.SetAsLastSibling();
            arrivalTransform.localPosition = effectTransform.localPosition;
            arrivalTransform.localRotation = effectTransform.localRotation;

            Destroy(arrivalVfx, Mathf.Max(0f, arrivalVfxLifetimeSeconds));
        }
    }
}
