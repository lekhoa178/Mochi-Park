using System;
using System.Collections;
using System.Collections.Generic;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Data;
using MochiPark.Runtime.Level;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    public sealed class DraftView : MonoBehaviour
    {
        private const string DraftPanelName = "Draft";
        private const string DraftRemainTextName = "Text_DraftRemain";
        private const string DraftItemsContainerName = "Layout_DraftItems";
        private const string SkipButtonName = "Button_Skip";
        private const string RerollButtonName = "Button_Reroll";
        private const string RerollCountTextName = "Text_RerollCount";
        private const string CollapseButtonName = "Button_Collapse";
        private const string UnitNameTextName = "Text_UnitName";
        private const string UnitIconImageName = "Image_UnitIcon";
        private const string BackgroundImageName = "Image_BG";
        private const string HighlightImageName = "Image_Highlight";

        [Header("References")]
        [SerializeField] private RectTransform draftPanel;
        [SerializeField] private TextMeshProUGUI draftRemainText;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button rerollButton;
        [SerializeField] private TextMeshProUGUI rerollCountText;
        [SerializeField] private Button collapseButton;
        [SerializeField] private RectTransform collapseArrowIcon;
        [SerializeField] private RectTransform draftItemsContainer;
        [SerializeField] private GameObject draftItemPrefab;

        [Header("Rarity Backgrounds")]
        [SerializeField] private Sprite blueRarityBackground;
        [SerializeField] private Sprite purpleRarityBackground;
        [SerializeField] private Sprite goldRarityBackground;

        [Header("Motion")]
        [SerializeField] private float tweenDuration = 0.28f;
        [SerializeField] private float offscreenPadding = 80f;

        [Header("Selection")]
        [SerializeField] private Color selectedHighlightColor = Color.yellow;

        private readonly List<DraftItemBinding> itemBindings = new List<DraftItemBinding>();

        private LevelDirector director;
        private EventBus eventBus;
        private RuntimeLevelState state;
        private CanvasGroup draftCanvasGroup;
        private Coroutine tweenCoroutine;
        private Vector2 shownAnchoredPosition;
        private Quaternion expandedArrowRotation = Quaternion.identity;
        private int selectedCardIndex = -1;
        private int draggedCardIndex = -1;
        private int draggedCardPointerId;
        private bool hasCachedShownPosition;
        private bool hasCachedExpandedArrowRotation;
        private bool isOpen;
        private bool isCollapsed;
        private bool isSuppressed;
        private bool hasDraftCardDrag;
        private bool hasStartedDraftCardPlacement;

        public event Action<int> DraftCardSelected;
        public event Action<int> DraftCardActivated;
        public event Action<int, Vector2, int> DraftCardDragStarted;
        public event Action<int, Vector2, int> DraftCardDragged;
        public event Action<int, Vector2, int> DraftCardDragEnded;
        public event Action SkipRequested;
        public event Action RerollRequested;

        public void Bind(LevelDirector boundDirector, EventBus boundEventBus)
        {
            if (eventBus != null)
            {
                Unsubscribe();
            }

            director = boundDirector;
            eventBus = boundEventBus;
            state = director != null ? director.State : null;

            ResolveReferences();
            CacheShownPosition();
            Subscribe();
            RefreshFromState();
        }

        public void Unbind()
        {
            Unsubscribe();
            director = null;
            eventBus = null;
            state = null;
            DraftCardSelected = null;
            DraftCardActivated = null;
            DraftCardDragStarted = null;
            DraftCardDragged = null;
            DraftCardDragEnded = null;
            SkipRequested = null;
            RerollRequested = null;
            ClearDraftCardDrag();
            ClearDraftCardSelection();
        }

        public void SelectCard(int cardIndex)
        {
            DraftOfferState offer = state != null && state.Draft != null
                ? state.Draft.ActiveOffer
                : null;
            if (offer == null || cardIndex < 0 || cardIndex >= offer.UnitTypeIds.Count)
            {
                return;
            }

            if (selectedCardIndex == cardIndex)
            {
                DraftCardActivated?.Invoke(cardIndex);
                return;
            }

            selectedCardIndex = cardIndex;
            RefreshCardSelectionVisuals();
            DraftCardSelected?.Invoke(cardIndex);
        }

        public void Close()
        {
            ResolveReferences();
            ClearDraftCardSelection();
            if (draftPanel == null)
            {
                return;
            }

            isCollapsed = false;
            TweenTo(GetHiddenPosition(), false);
        }

        public void SetSuppressed(bool suppressed)
        {
            ResolveReferences();
            if (isSuppressed == suppressed)
            {
                return;
            }

            isSuppressed = suppressed;
            if (isSuppressed)
            {
                Close();
                return;
            }

            RefreshFromState();
        }

        private void Awake()
        {
            ResolveReferences();
            CacheShownPosition();
            SetHiddenImmediate();
        }

        private void OnEnable()
        {
            if (skipButton != null)
            {
                skipButton.onClick.RemoveListener(OnSkipClicked);
                skipButton.onClick.AddListener(OnSkipClicked);
            }

            if (rerollButton != null)
            {
                rerollButton.onClick.RemoveListener(OnRerollClicked);
                rerollButton.onClick.AddListener(OnRerollClicked);
            }

            if (collapseButton != null)
            {
                collapseButton.onClick.RemoveListener(OnCollapseClicked);
                collapseButton.onClick.AddListener(OnCollapseClicked);
            }
        }

        private void OnDisable()
        {
            if (skipButton != null)
            {
                skipButton.onClick.RemoveListener(OnSkipClicked);
            }

            if (rerollButton != null)
            {
                rerollButton.onClick.RemoveListener(OnRerollClicked);
            }

            if (collapseButton != null)
            {
                collapseButton.onClick.RemoveListener(OnCollapseClicked);
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Subscribe()
        {
            if (eventBus == null)
            {
                return;
            }

            eventBus.LevelStarted += OnLevelStarted;
            eventBus.DayPrepared += OnDayPrepared;
            eventBus.DraftOffered += OnDraftOffered;
            eventBus.DraftCompletedForDay += OnDraftCompletedForDay;
            eventBus.ParkOpened += OnParkOpened;
            eventBus.DayEnded += OnDayEnded;
        }

        private void Unsubscribe()
        {
            if (eventBus == null)
            {
                return;
            }

            eventBus.LevelStarted -= OnLevelStarted;
            eventBus.DayPrepared -= OnDayPrepared;
            eventBus.DraftOffered -= OnDraftOffered;
            eventBus.DraftCompletedForDay -= OnDraftCompletedForDay;
            eventBus.ParkOpened -= OnParkOpened;
            eventBus.DayEnded -= OnDayEnded;
        }

        private void OnLevelStarted(LevelStartedEvent evt)
        {
            state = evt.State;
            SetHiddenImmediate();
        }

        private void OnDayPrepared(DayPreparedEvent evt)
        {
            if (!IsCurrentState(evt.State))
            {
                return;
            }

            RefreshDraftRemain();
            RefreshRerollRemain();
            if (evt.State.Draft == null || evt.State.Draft.TotalRollsToday <= 0)
            {
                SetHiddenImmediate();
            }
        }

        private void OnDraftOffered(DraftOfferedEvent evt)
        {
            if (!IsCurrentState(evt.State) || evt.Offer == null)
            {
                return;
            }

            RefreshOffer(evt.Offer);
            Show();
        }

        private void OnDraftCompletedForDay(DraftCompletedForDayEvent evt)
        {
            if (IsCurrentState(evt.State))
            {
                Close();
            }
        }

        private void OnParkOpened(ParkOpenedEvent evt)
        {
            if (IsCurrentState(evt.State))
            {
                Close();
            }
        }

        private void OnDayEnded(DayEndedEvent evt)
        {
            if (IsCurrentState(evt.State))
            {
                Close();
            }
        }

        private void RefreshFromState()
        {
            if (state == null || state.Draft == null)
            {
                SetHiddenImmediate();
                return;
            }

            RefreshDraftRemain();
            RefreshRerollRemain();
            if (state.Draft.HasPendingPick ||
                state.Draft.IsCompletedForDay ||
                state.Draft.ActiveOffer == null ||
                GetRemainingDraftCount() <= 0)
            {
                SetHiddenImmediate();
                return;
            }

            RefreshOffer(state.Draft.ActiveOffer);
            Show();
        }

        private void RefreshOffer(DraftOfferState offer)
        {
            ClearDraftCardSelection();
            RefreshDraftRemain();
            RefreshRerollRemain();
            EnsureItemPool(offer.UnitTypeIds.Count);

            for (int i = 0; i < itemBindings.Count; i++)
            {
                DraftItemBinding binding = itemBindings[i];
                bool visible = i < offer.UnitTypeIds.Count;
                binding.SetVisible(visible);
                if (!visible)
                {
                    continue;
                }

                UnitDefinitionSO unitDefinition = ResolveUnit(offer.UnitTypeIds[i]);
                binding.SetUnit(unitDefinition, offer.UnitTypeIds[i], GetRarityBackground(unitDefinition));
                binding.BindClick(
                    i,
                    OnDraftItemClicked,
                    OnDraftItemDragStarted,
                    OnDraftItemDragged,
                    OnDraftItemDragEnded);
            }
        }

        private void RefreshDraftRemain()
        {
            if (draftRemainText != null)
            {
                draftRemainText.SetText("{0}", GetRemainingDraftCount());
            }
        }

        private void RefreshRerollRemain()
        {
            int remainingRerolls = state != null && state.Draft != null
                ? state.Draft.RemainingRerolls
                : 0;

            if (rerollCountText != null)
            {
                rerollCountText.SetText("{0}", remainingRerolls);
            }

            if (rerollButton != null)
            {
                rerollButton.interactable = remainingRerolls > 0 &&
                    state != null &&
                    state.Draft != null &&
                    !state.Draft.IsCompletedForDay &&
                    !state.Draft.HasPendingPick &&
                    state.Draft.ActiveOffer != null;
            }
        }

        private int GetRemainingDraftCount()
        {
            if (state == null || state.Draft == null)
            {
                return 0;
            }

            return Mathf.Max(0, state.Draft.TotalRollsToday - state.Draft.CurrentRollIndex);
        }

        private UnitDefinitionSO ResolveUnit(string unitTypeId)
        {
            if (director != null && director.TryGetUnitDefinition(unitTypeId, out UnitDefinitionSO unit))
            {
                return unit;
            }

            return null;
        }

        private Sprite GetRarityBackground(UnitDefinitionSO unitDefinition)
        {
            if (unitDefinition == null)
            {
                return null;
            }

            switch (unitDefinition.Rarity)
            {
                case UnitRarity.Purple:
                    return purpleRarityBackground;
                case UnitRarity.Gold:
                    return goldRarityBackground;
                default:
                    return blueRarityBackground;
            }
        }

        private void Show()
        {
            if (isSuppressed || draftPanel == null || GetRemainingDraftCount() <= 0)
            {
                return;
            }

            CacheShownPosition();
            if (isOpen)
            {
                return;
            }

            isCollapsed = false;
            if (tweenCoroutine == null)
            {
                draftPanel.anchoredPosition = GetHiddenPosition();
            }

            TweenTo(shownAnchoredPosition, true, expandedArrowRotation);
        }

        private void SetHiddenImmediate()
        {
            ClearDraftCardSelection();
            if (draftPanel == null)
            {
                return;
            }

            if (tweenCoroutine != null)
            {
                StopCoroutine(tweenCoroutine);
                tweenCoroutine = null;
            }

            CacheShownPosition();
            draftPanel.anchoredPosition = GetHiddenPosition();
            SetArrowRotationImmediate(expandedArrowRotation);
            SetInteractable(false);
            isOpen = false;
            isCollapsed = false;
        }

        private void TweenTo(Vector2 targetPosition, bool openAtEnd)
        {
            TweenTo(targetPosition, openAtEnd, expandedArrowRotation);
        }

        private void TweenTo(Vector2 targetPosition, bool openAtEnd, Quaternion targetArrowRotation)
        {
            if (draftPanel == null)
            {
                return;
            }

            if (tweenCoroutine != null)
            {
                StopCoroutine(tweenCoroutine);
            }

            isOpen = openAtEnd;
            SetInteractable(openAtEnd);
            tweenCoroutine = StartCoroutine(TweenPanelRoutine(targetPosition, targetArrowRotation, openAtEnd));
        }

        private IEnumerator TweenPanelRoutine(
            Vector2 targetPosition,
            Quaternion targetArrowRotation,
            bool openAtEnd)
        {
            Vector2 startPosition = draftPanel.anchoredPosition;
            Quaternion startArrowRotation = collapseArrowIcon != null
                ? collapseArrowIcon.localRotation
                : Quaternion.identity;
            float duration = Mathf.Max(0.01f, tweenDuration);
            float elapsed = 0f;
            bool movesUp = targetPosition.y >= startPosition.y;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = movesUp
                    ? 1f - Mathf.Pow(1f - t, 3f)
                    : t * t;
                draftPanel.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
                if (collapseArrowIcon != null)
                {
                    collapseArrowIcon.localRotation = Quaternion.LerpUnclamped(
                        startArrowRotation,
                        targetArrowRotation,
                        eased);
                }
                yield return null;
            }

            draftPanel.anchoredPosition = targetPosition;
            SetArrowRotationImmediate(targetArrowRotation);
            isOpen = openAtEnd;
            SetInteractable(openAtEnd);
            tweenCoroutine = null;
        }

        private Vector2 GetHiddenPosition()
        {
            RectTransform canvasRect = transform as RectTransform;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvasRect = canvas.transform as RectTransform;
            }

            float canvasHeight = canvasRect != null ? canvasRect.rect.height : Screen.height;
            float panelHeight = Mathf.Max(draftPanel.rect.height, draftPanel.sizeDelta.y);
            float hiddenDistance = Mathf.Max(1f, canvasHeight + panelHeight + offscreenPadding);
            return shownAnchoredPosition - Vector2.up * hiddenDistance;
        }

        private Vector2 GetCollapsedPosition()
        {
            RectTransform collapseButtonRect = collapseButton != null
                ? collapseButton.transform as RectTransform
                : null;
            RectTransform parentRect = draftPanel != null ? draftPanel.parent as RectTransform : null;
            if (collapseButtonRect == null || parentRect == null)
            {
                float fallbackDistance = Mathf.Max(
                    0f,
                    draftPanel.rect.height - (collapseButtonRect != null ? collapseButtonRect.rect.height : 0f));
                return shownAnchoredPosition - Vector2.up * fallbackDistance;
            }

            float buttonAnchorY = Mathf.Lerp(
                draftPanel.rect.yMin,
                draftPanel.rect.yMax,
                (collapseButtonRect.anchorMin.y + collapseButtonRect.anchorMax.y) * 0.5f);
            float buttonBottomY = buttonAnchorY +
                collapseButtonRect.anchoredPosition.y +
                collapseButtonRect.rect.yMin;
            float panelAnchorY = Mathf.Lerp(
                parentRect.rect.yMin,
                parentRect.rect.yMax,
                (draftPanel.anchorMin.y + draftPanel.anchorMax.y) * 0.5f);

            Vector2 collapsedPosition = shownAnchoredPosition;
            collapsedPosition.y = Mathf.Min(
                shownAnchoredPosition.y,
                parentRect.rect.yMin - panelAnchorY - buttonBottomY);
            return collapsedPosition;
        }

        private void SetInteractable(bool interactable)
        {
            if (draftCanvasGroup == null && draftPanel != null)
            {
                draftCanvasGroup = draftPanel.GetComponent<CanvasGroup>();
                if (draftCanvasGroup == null)
                {
                    draftCanvasGroup = draftPanel.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (draftCanvasGroup != null)
            {
                draftCanvasGroup.interactable = interactable;
                draftCanvasGroup.blocksRaycasts = interactable;
            }
        }

        private void OnSkipClicked()
        {
            SkipRequested?.Invoke();
        }

        private void OnRerollClicked()
        {
            if (rerollButton != null && rerollButton.interactable)
            {
                RerollRequested?.Invoke();
            }
        }

        private void OnCollapseClicked()
        {
            if (!isOpen || draftPanel == null)
            {
                return;
            }

            isCollapsed = !isCollapsed;
            Vector2 targetPosition = isCollapsed ? GetCollapsedPosition() : shownAnchoredPosition;
            Quaternion targetArrowRotation = isCollapsed
                ? expandedArrowRotation * Quaternion.Euler(0f, 0f, 180f)
                : expandedArrowRotation;
            TweenTo(targetPosition, true, targetArrowRotation);
        }

        private void OnDraftItemClicked(int cardIndex)
        {
            SelectCard(cardIndex);
        }

        private void OnDraftItemDragStarted(int cardIndex, Vector2 screenPosition, int pointerId)
        {
            draggedCardIndex = cardIndex;
            draggedCardPointerId = pointerId;
            hasDraftCardDrag = true;
            hasStartedDraftCardPlacement = false;
        }

        private void OnDraftItemDragged(int cardIndex, Vector2 screenPosition, int pointerId)
        {
            if (!IsCurrentDraftCardDrag(cardIndex, pointerId))
            {
                return;
            }

            if (!hasStartedDraftCardPlacement)
            {
                if (IsInsideDraftPanel(screenPosition))
                {
                    return;
                }

                hasStartedDraftCardPlacement = true;
                ClearDraftCardSelection();
                DraftCardDragStarted?.Invoke(cardIndex, screenPosition, pointerId);
            }

            DraftCardDragged?.Invoke(cardIndex, screenPosition, pointerId);
        }

        private void OnDraftItemDragEnded(int cardIndex, Vector2 screenPosition, int pointerId)
        {
            if (!IsCurrentDraftCardDrag(cardIndex, pointerId))
            {
                return;
            }

            if (!hasStartedDraftCardPlacement && !IsInsideDraftPanel(screenPosition))
            {
                hasStartedDraftCardPlacement = true;
                ClearDraftCardSelection();
                DraftCardDragStarted?.Invoke(cardIndex, screenPosition, pointerId);
            }

            if (hasStartedDraftCardPlacement)
            {
                DraftCardDragEnded?.Invoke(cardIndex, screenPosition, pointerId);
            }

            ClearDraftCardDrag();
        }

        private bool IsCurrentDraftCardDrag(int cardIndex, int pointerId)
        {
            return hasDraftCardDrag &&
                draggedCardIndex == cardIndex &&
                draggedCardPointerId == pointerId;
        }

        private bool IsInsideDraftPanel(Vector2 screenPosition)
        {
            if (draftPanel == null)
            {
                return false;
            }

            Canvas canvas = draftPanel.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(draftPanel, screenPosition, eventCamera);
        }

        private void ClearDraftCardSelection()
        {
            selectedCardIndex = -1;
            RefreshCardSelectionVisuals();
        }

        private void RefreshCardSelectionVisuals()
        {
            for (int i = 0; i < itemBindings.Count; i++)
            {
                itemBindings[i].SetSelected(i == selectedCardIndex, selectedHighlightColor);
            }
        }

        private void ClearDraftCardDrag()
        {
            draggedCardIndex = -1;
            draggedCardPointerId = 0;
            hasDraftCardDrag = false;
            hasStartedDraftCardPlacement = false;
        }

        private void EnsureItemPool(int count)
        {
            if (draftItemsContainer == null)
            {
                return;
            }

            CacheExistingItems();
            if (itemBindings.Count == 0 && draftItemPrefab == null)
            {
                Debug.LogError($"{nameof(DraftView)} cannot create draft items because no item prefab or template child is assigned.", this);
                return;
            }

            while (itemBindings.Count < count)
            {
                GameObject source = draftItemPrefab != null ? draftItemPrefab : itemBindings[0].Root;
                GameObject instance = Instantiate(source, draftItemsContainer);
                instance.name = source.name;
                itemBindings.Add(new DraftItemBinding(instance));
            }
        }

        private void CacheExistingItems()
        {
            if (itemBindings.Count > 0 || draftItemsContainer == null)
            {
                return;
            }

            for (int i = 0; i < draftItemsContainer.childCount; i++)
            {
                Transform child = draftItemsContainer.GetChild(i);
                if (child != null)
                {
                    itemBindings.Add(new DraftItemBinding(child.gameObject));
                }
            }
        }

        private void ResolveReferences()
        {
            if (draftPanel == null)
            {
                draftPanel = FindDeep<RectTransform>(transform, DraftPanelName);
            }

            if (draftRemainText == null)
            {
                draftRemainText = FindDeep<TextMeshProUGUI>(transform, DraftRemainTextName);
            }

            if (draftItemsContainer == null)
            {
                draftItemsContainer = FindDeep<RectTransform>(transform, DraftItemsContainerName);
            }

            if (skipButton == null)
            {
                skipButton = FindDeep<Button>(transform, SkipButtonName);
            }

            if (rerollButton == null)
            {
                rerollButton = FindDeep<Button>(transform, RerollButtonName);
            }

            if (rerollCountText == null)
            {
                rerollCountText = FindDeep<TextMeshProUGUI>(transform, RerollCountTextName);
            }

            if (collapseButton == null)
            {
                collapseButton = FindDeep<Button>(transform, CollapseButtonName);
            }

            if (collapseArrowIcon == null && collapseButton != null && collapseButton.transform.childCount > 0)
            {
                collapseArrowIcon = collapseButton.transform.GetChild(0) as RectTransform;
            }

            if (draftCanvasGroup == null && draftPanel != null)
            {
                draftCanvasGroup = draftPanel.GetComponent<CanvasGroup>();
                if (draftCanvasGroup == null)
                {
                    draftCanvasGroup = draftPanel.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        private void CacheShownPosition()
        {
            if (hasCachedShownPosition || draftPanel == null)
            {
                return;
            }

            shownAnchoredPosition = draftPanel.anchoredPosition;
            hasCachedShownPosition = true;

            if (!hasCachedExpandedArrowRotation && collapseArrowIcon != null)
            {
                expandedArrowRotation = collapseArrowIcon.localRotation;
                hasCachedExpandedArrowRotation = true;
            }
        }

        private void SetArrowRotationImmediate(Quaternion rotation)
        {
            if (collapseArrowIcon != null)
            {
                collapseArrowIcon.localRotation = rotation;
            }
        }

        private bool IsCurrentState(RuntimeLevelState candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            if (state == null)
            {
                state = candidate;
            }

            return candidate == state;
        }

        private static T FindDeep<T>(Transform root, string objectName)
            where T : Component
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == objectName && root.TryGetComponent(out T component))
            {
                return component;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                T found = FindDeep<T>(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private sealed class DraftItemBinding
        {
            private readonly TextMeshProUGUI unitNameText;
            private readonly Image unitIconImage;
            private readonly Image backgroundImage;
            private readonly Image highlightImage;
            private readonly Button button;
            private readonly DraftItemHoverRelay hoverRelay;
            private readonly Sprite defaultBackgroundSprite;
            private UnityEngine.Events.UnityAction clickAction;

            public DraftItemBinding(GameObject root)
            {
                Root = root;
                unitNameText = FindDeep<TextMeshProUGUI>(root.transform, UnitNameTextName);
                unitIconImage = FindDeep<Image>(root.transform, UnitIconImageName);
                backgroundImage = FindDeep<Image>(root.transform, BackgroundImageName);
                highlightImage = FindDeep<Image>(root.transform, HighlightImageName);
                button = root.GetComponentInChildren<Button>(true);
                defaultBackgroundSprite = backgroundImage != null ? backgroundImage.sprite : null;

                if (highlightImage != null)
                {
                    highlightImage.raycastTarget = false;
                    highlightImage.gameObject.SetActive(false);
                }

                if (button != null)
                {
                    hoverRelay = button.GetComponent<DraftItemHoverRelay>();
                    if (hoverRelay == null)
                    {
                        hoverRelay = button.gameObject.AddComponent<DraftItemHoverRelay>();
                    }

                    hoverRelay.SetHighlight(highlightImage);
                }
            }

            public GameObject Root { get; }

            public void SetVisible(bool visible)
            {
                if (Root != null)
                {
                    Root.SetActive(visible);
                }

                if (!visible && highlightImage != null)
                {
                    highlightImage.gameObject.SetActive(false);
                }
            }

            public void SetSelected(bool selected, Color selectedColor)
            {
                if (hoverRelay != null)
                {
                    hoverRelay.SetSelected(selected, selectedColor);
                }
            }

            public void SetUnit(UnitDefinitionSO unitDefinition, string fallbackUnitTypeId, Sprite rarityBackground)
            {
                string displayName = unitDefinition != null && !string.IsNullOrWhiteSpace(unitDefinition.DisplayName)
                    ? unitDefinition.DisplayName
                    : fallbackUnitTypeId;

                if (unitNameText != null)
                {
                    unitNameText.text = displayName;
                }

                if (unitIconImage != null)
                {
                    Sprite icon = unitDefinition != null ? unitDefinition.Icon : null;
                    unitIconImage.sprite = icon;
                    unitIconImage.enabled = icon != null;
                }

                if (backgroundImage != null)
                {
                    backgroundImage.sprite = rarityBackground != null ? rarityBackground : defaultBackgroundSprite;
                    backgroundImage.enabled = backgroundImage.sprite != null;
                }
            }

            public void BindClick(
                int cardIndex,
                Action<int> onClicked,
                Action<int, Vector2, int> onDragStarted,
                Action<int, Vector2, int> onDragged,
                Action<int, Vector2, int> onDragEnded)
            {
                if (button == null)
                {
                    return;
                }

                if (clickAction != null)
                {
                    button.onClick.RemoveListener(clickAction);
                }

                clickAction = () => onClicked?.Invoke(cardIndex);
                button.onClick.AddListener(clickAction);
                button.interactable = true;

                if (hoverRelay != null)
                {
                    hoverRelay.BindDrag(cardIndex, onDragStarted, onDragged, onDragEnded);
                }
            }
        }
    }

    public sealed class DraftItemHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private Image highlightImage;
        private int cardIndex;
        private Action<int, Vector2, int> dragStarted;
        private Action<int, Vector2, int> dragged;
        private Action<int, Vector2, int> dragEnded;
        private Color hoverHighlightColor = Color.white;
        private Color selectedHighlightColor = Color.yellow;
        private bool isHovered;
        private bool isFocused;
        private bool isSelected;

        public void SetHighlight(Image highlight)
        {
            highlightImage = highlight;
            if (highlightImage != null)
            {
                hoverHighlightColor = highlightImage.color;
            }

            RefreshHighlight();
        }

        public void SetSelected(bool selected, Color selectedColor)
        {
            isSelected = selected;
            selectedHighlightColor = selectedColor;
            RefreshHighlight();
        }

        public void BindDrag(
            int index,
            Action<int, Vector2, int> onDragStarted,
            Action<int, Vector2, int> onDragged,
            Action<int, Vector2, int> onDragEnded)
        {
            cardIndex = index;
            dragStarted = onDragStarted;
            dragged = onDragged;
            dragEnded = onDragEnded;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            RefreshHighlight();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            RefreshHighlight();
        }

        public void OnSelect(BaseEventData eventData)
        {
            isFocused = true;
            RefreshHighlight();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isFocused = false;
            RefreshHighlight();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            DispatchDrag(eventData, dragStarted);
        }

        public void OnDrag(PointerEventData eventData)
        {
            DispatchDrag(eventData, dragged);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            DispatchDrag(eventData, dragEnded);
            isHovered = false;
            RefreshHighlight();
        }

        private void DispatchDrag(PointerEventData eventData, Action<int, Vector2, int> callback)
        {
            if (eventData != null)
            {
                callback?.Invoke(cardIndex, eventData.position, eventData.pointerId);
            }
        }

        private void RefreshHighlight()
        {
            if (highlightImage != null)
            {
                highlightImage.color = isSelected ? selectedHighlightColor : hoverHighlightColor;
                highlightImage.gameObject.SetActive(isSelected || isHovered || isFocused);
            }
        }
    }
}
