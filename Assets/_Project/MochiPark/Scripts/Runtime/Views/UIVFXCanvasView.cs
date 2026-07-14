using UnityEngine;
using UnityEngine.UI;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    public sealed class UIVFXCanvasView : MonoBehaviour
    {
        private const string UIVfxLayerName = "UI VFX";
        private const string FallbackLayerName = "ScreenOverlay";

        [SerializeField] private Canvas canvas;
        [SerializeField] private Camera overlayCamera;
        [SerializeField] private RectTransform effectsRoot;

        private Camera baseCamera;
        private bool isInitialized;

        public RectTransform EffectsRoot => effectsRoot != null ? effectsRoot : transform as RectTransform;
        public Camera OverlayCamera => overlayCamera;
        public bool IsInitialized => isInitialized;

        public bool Initialize(Camera targetBaseCamera)
        {
            isInitialized = false;
            CacheReferences();
            baseCamera = targetBaseCamera != null ? targetBaseCamera : Camera.main;

            if (canvas == null || EffectsRoot == null || baseCamera == null)
            {
                Debug.LogError(
                    $"{nameof(UIVFXCanvasView)} on '{name}' needs a Canvas, effects root, and base camera.",
                    this);
                return false;
            }

            ConfigureLayer();
            ConfigureCanvas();
            DisableOverlayCamera();
            isInitialized = true;
            return true;
        }

        public bool TryWorldToLocal(Camera worldCamera, Vector3 worldPosition, out Vector2 localPosition)
        {
            localPosition = default;
            Camera sourceCamera = worldCamera != null ? worldCamera : baseCamera;
            if (sourceCamera == null || EffectsRoot == null)
            {
                return false;
            }

            Vector3 screenPosition = sourceCamera.WorldToScreenPoint(worldPosition);
            if (screenPosition.z <= 0f)
            {
                return false;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                EffectsRoot,
                screenPosition,
                GetCanvasEventCamera(),
                out localPosition);
        }

        public bool TryRectTransformToLocal(RectTransform target, out Vector2 localPosition)
        {
            localPosition = default;
            if (target == null || EffectsRoot == null)
            {
                return false;
            }

            Camera targetCamera = GetRectTransformEventCamera(target);
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(targetCamera, target.position);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                EffectsRoot,
                screenPosition,
                GetCanvasEventCamera(),
                out localPosition);
        }

        private void CacheReferences()
        {
            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
            }

            if (overlayCamera == null)
            {
                overlayCamera = GetComponentInChildren<Camera>(true);
            }

            if (effectsRoot == null)
            {
                effectsRoot = transform as RectTransform;
            }
        }

        private void ConfigureCanvas()
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;

            GraphicRaycaster[] raycasters = GetComponentsInChildren<GraphicRaycaster>(true);
            for (int i = 0; i < raycasters.Length; i++)
            {
                raycasters[i].enabled = false;
            }

            RectTransform canvasTransform = canvas.transform as RectTransform;
            NormalizeFullscreenRect(canvasTransform);
            NormalizeFullscreenRect(EffectsRoot);
        }

        private void DisableOverlayCamera()
        {
            if (overlayCamera == null)
            {
                return;
            }

            overlayCamera.enabled = false;
        }

        private void ConfigureLayer()
        {
            int uiVfxLayer = GetUIVfxLayer();
            SetLayerRecursively(gameObject, uiVfxLayer);
        }

        private static Camera GetRectTransformEventCamera(RectTransform target)
        {
            Canvas targetCanvas = target.GetComponentInParent<Canvas>();
            if (targetCanvas == null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return targetCanvas.worldCamera;
        }

        private Camera GetCanvasEventCamera()
        {
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        private static void NormalizeFullscreenRect(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        private static int GetUIVfxLayer()
        {
            int layer = LayerMask.NameToLayer(UIVfxLayerName);
            if (layer >= 0)
            {
                return layer;
            }

            layer = LayerMask.NameToLayer(FallbackLayerName);
            return layer >= 0 ? layer : 0;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            if (target == null)
            {
                return;
            }

            target.layer = layer;
            Transform targetTransform = target.transform;
            for (int i = 0; i < targetTransform.childCount; i++)
            {
                SetLayerRecursively(targetTransform.GetChild(i).gameObject, layer);
            }
        }
    }
}
