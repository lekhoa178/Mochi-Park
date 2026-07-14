using System.Collections.Generic;
using UnityEngine;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    public sealed class FloatingScoreView : MonoBehaviour
    {
        [SerializeField] private RectTransform floatingScoreRoot;
        [SerializeField] private FloatingScoreItemView floatingScoreItemPrefab;
        [SerializeField] private FloatingScoreAnimationSettings animationSettings =
            new FloatingScoreAnimationSettings();

        private readonly List<FloatingScoreItemView> activeItems = new List<FloatingScoreItemView>();
        private Canvas cachedCanvas;

        public void ShowScore(
            Camera worldCamera,
            Vector3 worldPosition,
            int amount)
        {
            if (!ValidateReferences())
            {
                return;
            }

            if (worldCamera == null)
            {
                Debug.LogError($"{nameof(FloatingScoreView)} on '{name}' cannot show a score because the world camera is null.", this);
                return;
            }

            Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
            if (screenPosition.z <= 0f)
            {
                Debug.LogWarning(
                    $"{nameof(FloatingScoreView)} on '{name}' skipped a score because world position {worldPosition} is behind camera '{worldCamera.name}'.",
                    this);
                return;
            }

            if (!TryConvertScreenToLocal(screenPosition, out Vector2 anchoredPosition))
            {
                return;
            }

            FloatingScoreItemView item = Instantiate(floatingScoreItemPrefab, floatingScoreRoot);
            item.gameObject.SetActive(true);
            item.Completed += OnItemCompleted;
            activeItems.Add(item);
            item.Play(amount, anchoredPosition, animationSettings);
        }

        public void Clear()
        {
            for (int i = activeItems.Count - 1; i >= 0; i--)
            {
                FloatingScoreItemView item = activeItems[i];
                if (item == null)
                {
                    continue;
                }

                item.Completed -= OnItemCompleted;
                Destroy(item.gameObject);
            }

            activeItems.Clear();
        }

        private bool TryConvertScreenToLocal(Vector3 screenPosition, out Vector2 anchoredPosition)
        {
            Camera eventCamera = GetCanvasEventCamera();
            bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                floatingScoreRoot,
                screenPosition,
                eventCamera,
                out anchoredPosition);

            if (!converted)
            {
                Debug.LogError(
                    $"{nameof(FloatingScoreView)} on '{name}' could not convert screen position {screenPosition} into '{floatingScoreRoot.name}'.",
                    this);
            }

            return converted;
        }

        private Camera GetCanvasEventCamera()
        {
            Canvas canvas = GetCachedCanvas();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            if (canvas.worldCamera != null)
            {
                return canvas.worldCamera;
            }

            Debug.LogWarning(
                $"{nameof(FloatingScoreView)} on '{name}' uses a non-overlay Canvas without a world camera. Falling back to Camera.main.",
                this);
            return Camera.main;
        }

        private Canvas GetCachedCanvas()
        {
            if (cachedCanvas == null)
            {
                cachedCanvas = GetComponentInParent<Canvas>();
            }

            return cachedCanvas;
        }

        private void OnItemCompleted(FloatingScoreItemView item)
        {
            if (item != null)
            {
                item.Completed -= OnItemCompleted;
            }

            activeItems.Remove(item);
        }

        private bool ValidateReferences()
        {
            bool valid = true;

            if (floatingScoreRoot == null)
            {
                Debug.LogError($"{nameof(FloatingScoreView)} on '{name}' is missing a floating score root reference.", this);
                valid = false;
            }

            if (floatingScoreItemPrefab == null)
            {
                Debug.LogError($"{nameof(FloatingScoreView)} on '{name}' is missing a floating score item prefab reference.", this);
                valid = false;
            }

            if (animationSettings == null)
            {
                Debug.LogError($"{nameof(FloatingScoreView)} on '{name}' is missing animation settings.", this);
                valid = false;
            }

            return valid;
        }

        private void OnValidate()
        {
            cachedCanvas = null;
            ValidateReferences();
        }
    }
}
