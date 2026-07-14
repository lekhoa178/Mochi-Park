using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    public sealed class ScreenspaceIndicatorView : MonoBehaviour
    {
        private const float EditPanelShowDuration = 0.18f;
        private const float EditPanelHideDuration = 0.1f;

        [FormerlySerializedAs("joyAmountRoot")]
        [SerializeField] private RectTransform indicatorRoot;
        [FormerlySerializedAs("joyAmountItemPrefab")]
        [SerializeField] private ScreenspaceIndicatorViewItem defaultItemPrefab;
        [SerializeField] private ScreenspaceIndicatorViewItem joyMultiplierIndicatorPrefab;
        [SerializeField] private ScreenspaceIndicatorViewItem dailyLimitIndicatorPrefab;
        [SerializeField] private RectTransform editPanelPrefab;
        [SerializeField] private AnimationCurve editPanelShowScaleCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.68f, 1.1f),
            new Keyframe(1f, 1f));
        [SerializeField] private AnimationCurve editPanelHideScaleCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f));

        private readonly Dictionary<string, ScreenspaceIndicatorViewItem> activeItems =
            new Dictionary<string, ScreenspaceIndicatorViewItem>();
        private Canvas cachedCanvas;
        private RectTransform activeEditPanel;
        private Coroutine editPanelScaleCoroutine;
        private System.Action editPanelCancelRequested;
        private System.Action editPanelRotateRequested;
        private System.Action editPanelConfirmRequested;
        private Button activeEditPanelConfirmButton;
        private CanvasGroup activeEditPanelConfirmCanvasGroup;
        private bool editPanelIsHiding;

        public bool SetText(
            Camera worldCamera,
            string key,
            Vector3 worldPosition,
            string text)
        {
            return SetText(worldCamera, key, worldPosition, text, defaultItemPrefab, Vector2.zero);
        }

        public bool SetJoyMultiplierText(
            Camera worldCamera,
            string key,
            Vector3 worldPosition,
            string text)
        {
            return SetJoyMultiplierText(worldCamera, key, worldPosition, text, Vector2.zero);
        }

        public bool SetJoyMultiplierText(
            Camera worldCamera,
            string key,
            Vector3 worldPosition,
            string text,
            Vector2 anchoredOffset)
        {
            return SetText(worldCamera, key, worldPosition, text, joyMultiplierIndicatorPrefab, anchoredOffset);
        }

        public bool SetDailyLimitText(
            Camera worldCamera,
            string key,
            Vector3 worldPosition,
            string text)
        {
            return SetDailyLimitText(worldCamera, key, worldPosition, text, Vector2.zero);
        }

        public bool SetDailyLimitText(
            Camera worldCamera,
            string key,
            Vector3 worldPosition,
            string text,
            Vector2 anchoredOffset)
        {
            return SetText(worldCamera, key, worldPosition, text, dailyLimitIndicatorPrefab, anchoredOffset);
        }

        public bool SetText(
            Camera worldCamera,
            string key,
            Vector3 worldPosition,
            string text,
            ScreenspaceIndicatorViewItem itemPrefab)
        {
            return SetText(worldCamera, key, worldPosition, text, itemPrefab, Vector2.zero);
        }

        public bool SetText(
            Camera worldCamera,
            string key,
            Vector3 worldPosition,
            string text,
            ScreenspaceIndicatorViewItem itemPrefab,
            Vector2 anchoredOffset)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrEmpty(text))
            {
                Hide(key);
                return false;
            }

            if (!ValidateReferences(itemPrefab))
            {
                return false;
            }

            if (worldCamera == null)
            {
                Debug.LogError($"{nameof(ScreenspaceIndicatorView)} on '{name}' cannot show indicator '{key}' because the world camera is null.", this);
                return false;
            }

            Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
            if (screenPosition.z <= 0f)
            {
                Hide(key);
                return false;
            }

            if (!TryConvertScreenToLocal(screenPosition, out Vector2 anchoredPosition))
            {
                return false;
            }

            ScreenspaceIndicatorViewItem item = GetOrCreateItem(key, itemPrefab);
            if (item == null)
            {
                return false;
            }

            float sizeScale = 1f;
            if (item.SizingMode == ScreenspaceIndicatorViewItem.ScreenSizeMode.CameraDistanceResponsive)
            {
                if (!TryCalculateCameraDistanceScale(worldCamera, worldPosition, item, out sizeScale))
                {
                    return false;
                }

                anchoredOffset *= sizeScale;
            }

            anchoredPosition += anchoredOffset;
            item.SetText(text, anchoredPosition, sizeScale);
            item.ShowAnimated();
            return true;
        }

        public bool ShowEditPanel(
            Camera worldCamera,
            Vector3 worldPosition,
            Vector2 anchoredOffset,
            System.Action cancelRequested,
            System.Action rotateRequested,
            System.Action confirmRequested,
            bool confirmInteractable)
        {
            if (!ValidateEditPanelReferences())
            {
                return false;
            }

            if (worldCamera == null)
            {
                Debug.LogError($"{nameof(ScreenspaceIndicatorView)} on '{name}' cannot show edit panel because the world camera is null.", this);
                return false;
            }

            Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
            if (screenPosition.z <= 0f)
            {
                HideEditPanel();
                return false;
            }

            if (!TryConvertScreenToLocal(screenPosition, out Vector2 anchoredPosition))
            {
                return false;
            }

            RectTransform editPanel = GetOrCreateEditPanel();
            if (editPanel == null)
            {
                return false;
            }

            editPanelCancelRequested = cancelRequested;
            editPanelRotateRequested = rotateRequested;
            editPanelConfirmRequested = confirmRequested;
            SetEditPanelConfirmInteractable(confirmInteractable);
            editPanel.anchoredPosition = anchoredPosition + anchoredOffset;
            editPanel.SetAsLastSibling();
            ShowEditPanelAnimated(editPanel);
            return true;
        }

        public void HideEditPanel()
        {
            if (activeEditPanel == null || !activeEditPanel.gameObject.activeSelf)
            {
                return;
            }

            if (editPanelIsHiding)
            {
                return;
            }

            if (editPanelScaleCoroutine != null)
            {
                StopCoroutine(editPanelScaleCoroutine);
            }

            editPanelIsHiding = true;
            editPanelScaleCoroutine = StartCoroutine(TweenEditPanelScale(
                editPanelHideScaleCurve,
                EditPanelHideDuration,
                () =>
                {
                    editPanelIsHiding = false;
                    if (activeEditPanel != null)
                    {
                        activeEditPanel.gameObject.SetActive(false);
                    }
                }));
        }

        public void HideEditPanelImmediate()
        {
            if (editPanelScaleCoroutine != null)
            {
                StopCoroutine(editPanelScaleCoroutine);
                editPanelScaleCoroutine = null;
            }

            if (activeEditPanel != null)
            {
                activeEditPanel.localScale = Vector3.zero;
                activeEditPanel.gameObject.SetActive(false);
            }

            editPanelIsHiding = false;
        }

        public void Hide(string key)
        {
            if (!string.IsNullOrWhiteSpace(key) &&
                activeItems.TryGetValue(key, out ScreenspaceIndicatorViewItem item) &&
                item != null)
            {
                item.HideAnimated();
            }
        }

        public void HideAll()
        {
            HideAll(null, false);
        }

        public void HideAll(string keyPrefix)
        {
            HideAll(keyPrefix, false);
        }

        public void HideAllImmediate()
        {
            HideAll(null, true);
        }

        public void HideAllImmediate(string keyPrefix)
        {
            HideAll(keyPrefix, true);
        }

        public void Clear()
        {
            Clear(null);
        }

        public void Clear(string keyPrefix)
        {
            if (string.IsNullOrEmpty(keyPrefix))
            {
                DestroyEditPanel();
            }

            List<string> keysToRemove = null;
            foreach (KeyValuePair<string, ScreenspaceIndicatorViewItem> pair in activeItems)
            {
                if (!MatchesPrefix(pair.Key, keyPrefix))
                {
                    continue;
                }

                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }

                if (keysToRemove == null)
                {
                    keysToRemove = new List<string>();
                }

                keysToRemove.Add(pair.Key);
            }

            if (keysToRemove == null)
            {
                return;
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                activeItems.Remove(keysToRemove[i]);
            }
        }

        private void HideAll(string keyPrefix, bool immediate)
        {
            foreach (KeyValuePair<string, ScreenspaceIndicatorViewItem> pair in activeItems)
            {
                if (!MatchesPrefix(pair.Key, keyPrefix) || pair.Value == null)
                {
                    continue;
                }

                if (immediate)
                {
                    pair.Value.HideImmediate();
                }
                else
                {
                    pair.Value.HideAnimated();
                }
            }
        }

        private ScreenspaceIndicatorViewItem GetOrCreateItem(
            string key,
            ScreenspaceIndicatorViewItem itemPrefab)
        {
            if (activeItems.TryGetValue(key, out ScreenspaceIndicatorViewItem item) && item != null)
            {
                return item;
            }

            item = Instantiate(itemPrefab, indicatorRoot);
            item.name = key;
            item.HideImmediate();
            activeItems[key] = item;
            return item;
        }

        private RectTransform GetOrCreateEditPanel()
        {
            if (activeEditPanel != null)
            {
                return activeEditPanel;
            }

            activeEditPanel = Instantiate(editPanelPrefab, indicatorRoot);
            activeEditPanel.name = editPanelPrefab.name;
            activeEditPanel.gameObject.SetActive(false);
            activeEditPanel.localScale = Vector3.zero;
            BindEditPanelButton(activeEditPanel, "Button_Cancel", InvokeEditPanelCancel);
            BindEditPanelButton(activeEditPanel, "Button_Rotate", InvokeEditPanelRotate);
            activeEditPanelConfirmButton = BindEditPanelButton(activeEditPanel, "Button_Confirm", InvokeEditPanelConfirm);
            activeEditPanelConfirmCanvasGroup = activeEditPanelConfirmButton != null
                ? activeEditPanelConfirmButton.GetComponent<CanvasGroup>()
                : null;
            if (activeEditPanelConfirmButton != null && activeEditPanelConfirmCanvasGroup == null)
            {
                activeEditPanelConfirmCanvasGroup = activeEditPanelConfirmButton.gameObject.AddComponent<CanvasGroup>();
            }

            return activeEditPanel;
        }

        private void ShowEditPanelAnimated(RectTransform editPanel)
        {
            bool shouldPlayShowAnimation = !editPanel.gameObject.activeSelf || editPanelIsHiding;
            editPanel.gameObject.SetActive(true);

            if (!shouldPlayShowAnimation)
            {
                return;
            }

            if (editPanelScaleCoroutine != null)
            {
                StopCoroutine(editPanelScaleCoroutine);
            }

            editPanel.localScale = Vector3.zero;
            editPanelIsHiding = false;
            editPanelScaleCoroutine = StartCoroutine(TweenEditPanelScale(
                editPanelShowScaleCurve,
                EditPanelShowDuration,
                null));
        }

        private System.Collections.IEnumerator TweenEditPanelScale(
            AnimationCurve scaleCurve,
            float duration,
            System.Action completed)
        {
            AnimationCurve resolvedCurve = ResolveScaleCurve(scaleCurve);
            if (duration <= Mathf.Epsilon)
            {
                activeEditPanel.localScale = Vector3.one * Mathf.Max(0f, resolvedCurve.Evaluate(1f));
                editPanelScaleCoroutine = null;
                completed?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            while (activeEditPanel != null && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                activeEditPanel.localScale = Vector3.one * Mathf.Max(0f, resolvedCurve.Evaluate(t));
                yield return null;
            }

            if (activeEditPanel != null)
            {
                activeEditPanel.localScale = Vector3.one * Mathf.Max(0f, resolvedCurve.Evaluate(1f));
            }

            editPanelScaleCoroutine = null;
            completed?.Invoke();
        }

        private void DestroyEditPanel()
        {
            if (editPanelScaleCoroutine != null)
            {
                StopCoroutine(editPanelScaleCoroutine);
                editPanelScaleCoroutine = null;
            }

            if (activeEditPanel != null)
            {
                Destroy(activeEditPanel.gameObject);
                activeEditPanel = null;
            }

            editPanelCancelRequested = null;
            editPanelRotateRequested = null;
            editPanelConfirmRequested = null;
            activeEditPanelConfirmButton = null;
            activeEditPanelConfirmCanvasGroup = null;
            editPanelIsHiding = false;
        }

        private static AnimationCurve ResolveScaleCurve(AnimationCurve scaleCurve)
        {
            return scaleCurve != null && scaleCurve.length > 0
                ? scaleCurve
                : AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        private static Button BindEditPanelButton(RectTransform editPanel, string buttonName, UnityEngine.Events.UnityAction action)
        {
            Button[] buttons = editPanel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button != null && button.name == buttonName)
                {
                    button.onClick.RemoveListener(action);
                    button.onClick.AddListener(action);
                    return button;
                }
            }

            Debug.LogWarning($"{nameof(ScreenspaceIndicatorView)} could not find '{buttonName}' in edit panel '{editPanel.name}'.", editPanel);
            return null;
        }

        private void SetEditPanelConfirmInteractable(bool interactable)
        {
            if (activeEditPanelConfirmButton != null)
            {
                activeEditPanelConfirmButton.interactable = interactable;
            }

            if (activeEditPanelConfirmCanvasGroup != null)
            {
                activeEditPanelConfirmCanvasGroup.alpha = interactable ? 1f : 0.5f;
            }
        }

        private void InvokeEditPanelCancel()
        {
            editPanelCancelRequested?.Invoke();
        }

        private void InvokeEditPanelRotate()
        {
            editPanelRotateRequested?.Invoke();
        }

        private void InvokeEditPanelConfirm()
        {
            editPanelConfirmRequested?.Invoke();
        }

        private bool TryConvertScreenToLocal(Vector3 screenPosition, out Vector2 anchoredPosition)
        {
            Camera eventCamera = GetCanvasEventCamera();
            bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                indicatorRoot,
                screenPosition,
                eventCamera,
                out anchoredPosition);

            if (!converted)
            {
                Debug.LogError(
                    $"{nameof(ScreenspaceIndicatorView)} on '{name}' could not convert screen position {screenPosition} into '{indicatorRoot.name}'.",
                    this);
            }

            return converted;
        }

        private bool TryCalculateCameraDistanceScale(
            Camera worldCamera,
            Vector3 worldPosition,
            ScreenspaceIndicatorViewItem item,
            out float sizeScale)
        {
            float halfWorldSize = item.ReferenceWorldSize * 0.5f;
            Vector3 rightOffset = Vector3.right * halfWorldSize;
            Vector3 forwardOffset = Vector3.forward * halfWorldSize;
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            if (!TryIncludeProjectedCorner(
                    worldCamera,
                    worldPosition - rightOffset - forwardOffset,
                    ref minX,
                    ref maxX) ||
                !TryIncludeProjectedCorner(
                    worldCamera,
                    worldPosition - rightOffset + forwardOffset,
                    ref minX,
                    ref maxX) ||
                !TryIncludeProjectedCorner(
                    worldCamera,
                    worldPosition + rightOffset - forwardOffset,
                    ref minX,
                    ref maxX) ||
                !TryIncludeProjectedCorner(
                    worldCamera,
                    worldPosition + rightOffset + forwardOffset,
                    ref minX,
                    ref maxX))
            {
                sizeScale = 1f;
                return false;
            }

            sizeScale = Mathf.Max(0f, maxX - minX) / item.ReferenceScreenSize;
            return sizeScale > Mathf.Epsilon;
        }

        private bool TryIncludeProjectedCorner(
            Camera worldCamera,
            Vector3 worldCorner,
            ref float minX,
            ref float maxX)
        {
            Vector3 screenCorner = worldCamera.WorldToScreenPoint(worldCorner);
            if (screenCorner.z <= 0f || !TryConvertScreenToLocal(screenCorner, out Vector2 localCorner))
            {
                return false;
            }

            minX = Mathf.Min(minX, localCorner.x);
            maxX = Mathf.Max(maxX, localCorner.x);
            return true;
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
                $"{nameof(ScreenspaceIndicatorView)} on '{name}' uses a non-overlay Canvas without a world camera. Falling back to Camera.main.",
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

        private bool ValidateReferences(ScreenspaceIndicatorViewItem itemPrefab)
        {
            bool valid = true;

            if (indicatorRoot == null)
            {
                Debug.LogError($"{nameof(ScreenspaceIndicatorView)} on '{name}' is missing an indicator root reference.", this);
                valid = false;
            }

            if (itemPrefab == null)
            {
                Debug.LogError($"{nameof(ScreenspaceIndicatorView)} on '{name}' is missing an indicator item prefab reference.", this);
                valid = false;
            }

            return valid;
        }

        private bool ValidateEditPanelReferences()
        {
            bool valid = true;

            if (indicatorRoot == null)
            {
                Debug.LogError($"{nameof(ScreenspaceIndicatorView)} on '{name}' is missing an indicator root reference.", this);
                valid = false;
            }

            if (editPanelPrefab == null)
            {
                Debug.LogError($"{nameof(ScreenspaceIndicatorView)} on '{name}' is missing an edit panel prefab reference.", this);
                valid = false;
            }

            return valid;
        }

        private static bool MatchesPrefix(string key, string keyPrefix)
        {
            return string.IsNullOrEmpty(keyPrefix) || key.StartsWith(keyPrefix, System.StringComparison.Ordinal);
        }

        private void OnValidate()
        {
            cachedCanvas = null;
            ValidateReferences(defaultItemPrefab);
            ValidateReferences(joyMultiplierIndicatorPrefab);
            ValidateReferences(dailyLimitIndicatorPrefab);
        }
    }
}
