using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    public sealed class DiscardView : MonoBehaviour
    {
        private const string DragOverlayCameraName = "Dragged Unit Overlay Camera";
        private const string DragOverlayLayerName = "DraggedUnitOverlay";

        [SerializeField] private RectTransform discardPanel;
        [SerializeField, Min(1f)] private float highlightedScale = 1.12f;
        [SerializeField, Min(0f)] private float highlightDuration = 0.2f;
        [SerializeField, Min(0f)] private float resetDuration = 0.12f;

        [Header("Panel Motion")]
        [SerializeField, Min(0f)] private float hiddenOffsetY = 280f;
        [SerializeField, Min(0f)] private float showDuration = 0.28f;
        [SerializeField, Min(0f)] private float hideDuration = 0.2f;

        private readonly Dictionary<Transform, int> originalLayers = new Dictionary<Transform, int>();
        private Canvas canvas;
        private Camera baseCamera;
        private Camera dragOverlayCamera;
        private UniversalAdditionalCameraData baseCameraData;
        private Vector3 panelBaseScale = Vector3.one;
        private Vector2 panelShownPosition;
        private Vector2 panelHiddenPosition;
        private Tween panelMoveTween;
        private Tween panelScaleTween;
        private bool hasPanelBaseScale;
        private bool hasPanelPositions;
        private bool isHighlighted;
        private bool isPanelVisible;
        private bool ownsCameraStackEntry;
        private int dragOverlayLayer = -1;

        private void Awake()
        {
            ResolveReferences();
            SetPanelVisible(false, true);
        }

        private void LateUpdate()
        {
            if (dragOverlayCamera == null || baseCamera == null || originalLayers.Count == 0)
            {
                return;
            }

            SyncOverlayCamera();
        }

        private void OnDisable()
        {
            RestoreUnitLayers();
            SetHighlighted(false, true);
            SetPanelVisible(false, true);
        }

        private void OnDestroy()
        {
            RestoreUnitLayers();
            panelMoveTween?.Kill();
            panelScaleTween?.Kill();
            RemoveOverlayCameraFromStack();
            if (dragOverlayCamera != null)
            {
                Destroy(dragOverlayCamera.gameObject);
            }
        }

        public void Bind(Camera targetCamera)
        {
            ResolveReferences();
            baseCamera = targetCamera != null ? targetCamera : Camera.main;
            if (canvas == null || baseCamera == null)
            {
                return;
            }

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = baseCamera;
            canvas.planeDistance = Mathf.Min(
                baseCamera.farClipPlane - 0.01f,
                baseCamera.nearClipPlane + 0.5f);

            dragOverlayLayer = LayerMask.NameToLayer(DragOverlayLayerName);
            if (dragOverlayLayer < 0)
            {
                Debug.LogWarning(
                    $"{nameof(DiscardView)} needs a '{DragOverlayLayerName}' layer to render the dragged unit above the discard UI.",
                    this);
                return;
            }

            EnsureOverlayCamera();
        }

        public bool ContainsScreenPoint(Vector2 screenPosition)
        {
            ResolveReferences();
            if (discardPanel == null || !discardPanel.gameObject.activeInHierarchy)
            {
                return false;
            }

            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(discardPanel, screenPosition, eventCamera);
        }

        public void BeginUnitDrag(GameObject unitView)
        {
            RestoreUnitLayers();
            SetPanelVisible(true);
            if (unitView == null)
            {
                return;
            }

            if (baseCamera == null)
            {
                Bind(Camera.main);
            }

            if (dragOverlayCamera == null || dragOverlayLayer < 0)
            {
                return;
            }

            CacheAndSetLayer(unitView.transform);
            SyncOverlayCamera();
            dragOverlayCamera.enabled = true;
        }

        public void EndUnitDrag()
        {
            RestoreUnitLayers();
            SetHighlighted(false);
            SetPanelVisible(false);
        }

        private void RestoreUnitLayers()
        {
            foreach (KeyValuePair<Transform, int> entry in originalLayers)
            {
                if (entry.Key != null)
                {
                    entry.Key.gameObject.layer = entry.Value;
                }
            }

            originalLayers.Clear();
            if (dragOverlayCamera != null)
            {
                dragOverlayCamera.enabled = false;
            }
        }

        public void SetHighlighted(bool highlighted)
        {
            SetHighlighted(highlighted, false);
        }

        private void SetHighlighted(bool highlighted, bool immediate)
        {
            ResolveReferences();
            if (discardPanel == null || (isHighlighted == highlighted && !immediate))
            {
                return;
            }

            isHighlighted = highlighted;
            panelScaleTween?.Kill();
            panelScaleTween = null;
            Vector3 targetScale = highlighted ? panelBaseScale * highlightedScale : panelBaseScale;
            if (immediate)
            {
                discardPanel.localScale = targetScale;
                return;
            }

            float duration = highlighted ? highlightDuration : resetDuration;
            panelScaleTween = discardPanel.DOScale(targetScale, duration)
                .SetEase(highlighted ? Ease.OutBack : Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void SetPanelVisible(bool visible, bool immediate = false)
        {
            ResolveReferences();
            if (discardPanel == null || !hasPanelPositions || (isPanelVisible == visible && !immediate))
            {
                return;
            }

            isPanelVisible = visible;
            panelMoveTween?.Kill();
            panelMoveTween = null;
            Vector2 targetPosition = visible ? panelShownPosition : panelHiddenPosition;
            if (immediate)
            {
                discardPanel.anchoredPosition = targetPosition;
                return;
            }

            float duration = visible ? showDuration : hideDuration;
            panelMoveTween = discardPanel.DOAnchorPos(targetPosition, duration)
                .SetEase(visible ? Ease.OutBack : Ease.InBack)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void ResolveReferences()
        {
            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
            }

            if (discardPanel == null)
            {
                Transform panel = transform.Find("DiscardPanel");
                discardPanel = panel != null ? panel as RectTransform : null;
            }

            if (discardPanel != null && !hasPanelBaseScale)
            {
                panelBaseScale = discardPanel.localScale;
                hasPanelBaseScale = true;
            }

            if (discardPanel != null && !hasPanelPositions)
            {
                panelShownPosition = discardPanel.anchoredPosition;
                panelHiddenPosition = panelShownPosition + Vector2.down * hiddenOffsetY;
                hasPanelPositions = true;
            }
        }

        private void EnsureOverlayCamera()
        {
            if (dragOverlayCamera == null)
            {
                Transform existing = transform.Find(DragOverlayCameraName);
                if (existing != null)
                {
                    dragOverlayCamera = existing.GetComponent<Camera>();
                }
            }

            if (dragOverlayCamera == null)
            {
                GameObject cameraObject = new GameObject(DragOverlayCameraName);
                dragOverlayCamera = cameraObject.AddComponent<Camera>();
            }

            UniversalAdditionalCameraData overlayData =
                dragOverlayCamera.GetComponent<UniversalAdditionalCameraData>();
            if (overlayData == null)
            {
                overlayData = dragOverlayCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            overlayData.renderType = CameraRenderType.Overlay;
            dragOverlayCamera.tag = "Untagged";
            dragOverlayCamera.enabled = false;
            AddOverlayCameraToStack();
        }

        private void SyncOverlayCamera()
        {
            if (dragOverlayCamera == null || baseCamera == null || dragOverlayLayer < 0)
            {
                return;
            }

            dragOverlayCamera.CopyFrom(baseCamera);
            dragOverlayCamera.transform.SetPositionAndRotation(
                baseCamera.transform.position,
                baseCamera.transform.rotation);
            dragOverlayCamera.transform.localScale = Vector3.one;
            dragOverlayCamera.clearFlags = CameraClearFlags.Nothing;
            dragOverlayCamera.cullingMask = 1 << dragOverlayLayer;
            dragOverlayCamera.enabled = true;

            UniversalAdditionalCameraData overlayData =
                dragOverlayCamera.GetComponent<UniversalAdditionalCameraData>();
            overlayData.renderType = CameraRenderType.Overlay;
        }

        private void AddOverlayCameraToStack()
        {
            if (baseCamera == null || dragOverlayCamera == null)
            {
                return;
            }

            baseCameraData = baseCamera.GetComponent<UniversalAdditionalCameraData>();
            if (baseCameraData == null)
            {
                baseCameraData = baseCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            baseCameraData.renderType = CameraRenderType.Base;
            List<Camera> cameraStack = baseCameraData.cameraStack;
            if (cameraStack == null || cameraStack.Contains(dragOverlayCamera))
            {
                return;
            }

            cameraStack.Add(dragOverlayCamera);
            ownsCameraStackEntry = true;
        }

        private void RemoveOverlayCameraFromStack()
        {
            if (!ownsCameraStackEntry || baseCameraData == null || dragOverlayCamera == null)
            {
                return;
            }

            baseCameraData.cameraStack?.Remove(dragOverlayCamera);
            ownsCameraStackEntry = false;
        }

        private void CacheAndSetLayer(Transform target)
        {
            if (target == null)
            {
                return;
            }

            originalLayers[target] = target.gameObject.layer;
            target.gameObject.layer = dragOverlayLayer;
            for (int i = 0; i < target.childCount; i++)
            {
                CacheAndSetLayer(target.GetChild(i));
            }
        }
    }
}
