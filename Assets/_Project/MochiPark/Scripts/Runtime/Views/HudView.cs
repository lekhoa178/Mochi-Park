using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Data;
using MochiPark.Runtime.Level;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    public sealed class HudView : MonoBehaviour
    {
        private const string UnitDetailPanelName = "UnitDetail";
        private const string UnitDetailBackgroundName = "Image_BG";
        private const string UnitDetailIconName = "Image_UnitIcon";
        private const string UnitDetailNameTextName = "Text_UnitName";
        private const string UnitDetailDescriptionTextName = "Text_UnitDescription";
        private const string OpenParkButtonName = "Button_OpenPark";
        private const string ExitButtonName = "Button_Exit";
        private const string SpeedButtonName = "Button_SpeedUp";
        private const string SpeedTextName = "Text_Speed";
        private const string LeaderboardLayoutName = "Layout_Units";
        private const string LeaderboardResourcePath = "MochiPark/UI/HUD/UnitLeaderboard";
        private const int MaxLeaderboardRows = 5;

        private const string EditModeText = "X\u00e2y d\u1ef1ng";
        private const string GameplayModeText = "\u0110\u00f3n kh\u00e1ch";

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI visitorRemainingText;
        [SerializeField] private TextMeshProUGUI dayModeText;
        [SerializeField] private Image gameModeIcon;
        [SerializeField] private Sprite editModeSprite;
        [SerializeField] private Sprite gameplayModeSprite;
        [SerializeField] private Color visitorDefaultColor = Color.white;
        [SerializeField] private Color visitorEmptyColor = new Color(1f, 0.18f, 0.18f, 1f);

        [Header("Actions")]
        [SerializeField] private Button openParkButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button speedButton;
        [SerializeField] private TextMeshProUGUI speedText;
        [SerializeField] private GameObject speedControlRoot;

        [Header("Button Cosmetics")]
        [SerializeField] private UIButtonCosmeticService buttonCosmeticService;
        [SerializeField] private RectTransform openParkButtonCosmeticTarget;
        [SerializeField] private RectTransform exitButtonCosmeticTarget;
        [SerializeField] private RectTransform shovelButtonCosmeticTarget;
        [SerializeField] private RectTransform speedButtonCosmeticTarget;

        [Header("Stars")]
        [SerializeField] private RectTransform joyIconTransform;
        [SerializeField] private TextMeshProUGUI joyText;
        [SerializeField] private TextMeshProUGUI oneStarTargetText;
        [SerializeField] private TextMeshProUGUI twoStarTargetText;
        [SerializeField] private TextMeshProUGUI threeStarTargetText;
        [SerializeField] private Slider starProgressSlider;
        [SerializeField] private Image[] starImages = new Image[3];
        [SerializeField] private RectTransform[] starTransforms = new RectTransform[3];
        [SerializeField] private Sprite starLockedSprite;
        [SerializeField] private Sprite starUnlockedSprite;

        [Header("Rumble")]
        [SerializeField] private RectTransform rumbleRoot;
        [SerializeField] private TextMeshProUGUI rumbleText;
        [SerializeField] private Slider rumbleProgressSlider;
        [SerializeField] private RectTransform rumbleIconTransform;

        [Header("Shovel")]
        [SerializeField] private Button shovelButton;
        [SerializeField] private Image shovelIconImage;
        [SerializeField] private Image shovelLockImage;
        [SerializeField] private TextMeshProUGUI shovelRemainingText;
        [SerializeField] private Sprite shovelInactiveSprite;
        [SerializeField] private Sprite shovelActiveSprite;
        [SerializeField] private Texture2D shovelCursorTexture;
        [SerializeField] private Vector2 shovelCursorHotspot = new Vector2(0.5f, 0.5f);

        [Header("Unit Detail")]
        [SerializeField] private RectTransform unitDetailPanel;
        [SerializeField] private Image unitDetailBackgroundImage;
        [SerializeField] private Image unitDetailIconImage;
        [SerializeField] private TextMeshProUGUI unitDetailNameText;
        [SerializeField] private TextMeshProUGUI unitDetailDescriptionText;
        [SerializeField] private ScrollRect unitDetailDescriptionScrollRect;
        [SerializeField] private Sprite blueRarityDetailBackground;
        [SerializeField] private Sprite purpleRarityDetailBackground;
        [SerializeField] private Sprite goldRarityDetailBackground;

        [Header("Unit Leaderboard")]
        [SerializeField] private RectTransform leaderboardLayout;
        [SerializeField] private UnitLeaderboardView leaderboardRowTemplate;
        [SerializeField] private Sprite[] leaderboardBadgeSprites = new Sprite[3];
        [SerializeField] private Color[] leaderboardTopIndexColors =
        {
            new Color(0.95f, 0.78f, 0.28f, 1f),
            new Color(0.78f, 0.86f, 0.92f, 1f),
            new Color(0.86f, 0.54f, 0.32f, 1f)
        };
        [SerializeField] private Color leaderboardOtherIndexColor = new Color(0.7764706f, 0.7764706f, 0.7764706f, 1f);

        [Header("Motion")]
        [SerializeField] private float sliderTweenDuration = 0.24f;
        [SerializeField] private float starPopDuration = 0.28f;
        [SerializeField] private float starPopScale = 1.28f;
        [SerializeField] private float starPopRotationDegrees = 14f;
        [SerializeField] private float joyIconPunchDuration = 0.22f;
        [SerializeField] private float joyIconPunchScale = 1.22f;
        [SerializeField] private float rumbleShakeDegrees = 7f;
        [SerializeField] private float rumbleShakeFrequency = 34f;
        [SerializeField] private float unitDetailTweenDuration = 0.28f;
        [SerializeField] private float unitDetailOffscreenPadding = 80f;

        private readonly Coroutine[] starPopCoroutines = new Coroutine[3];
        private readonly Vector3[] starBaseScales = new Vector3[3];
        private readonly Quaternion[] starBaseRotations = new Quaternion[3];
        private readonly List<UnitTypeScoreSummary> leaderboardScratch = new List<UnitTypeScoreSummary>();
        private readonly List<UnitLeaderboardView> leaderboardRows = new List<UnitLeaderboardView>(MaxLeaderboardRows);

        private LevelDirector director;
        private EventBus eventBus;
        private RuntimeLevelState state;
        private Vector3 rumbleIconBasePosition;
        private Quaternion rumbleIconBaseRotation = Quaternion.identity;
        private Vector3 joyIconBaseScale = Vector3.one;
        private float targetStarProgress;
        private float targetRumbleProgress;
        private int displayedJoy;
        private int displayedStarCount = -1;
        private bool hasSeenRumbleThisDay;
        private bool delayJoyScoreChanged;
        private Coroutine joyIconPunchCoroutine;
        private CanvasGroup unitDetailCanvasGroup;
        private Coroutine unitDetailTweenCoroutine;
        private Vector2 unitDetailShownAnchoredPosition;
        private Sprite unitDetailDefaultBackgroundSprite;
        private bool hasCachedUnitDetailShownPosition;
        private bool isUnitDetailOpen;

        public RectTransform JoyIconTransform => joyIconTransform;
        public Texture2D ShovelCursorTexture => shovelCursorTexture != null
            ? shovelCursorTexture
            : shovelActiveSprite != null
                ? shovelActiveSprite.texture
                : null;
        public Vector2 ShovelCursorHotspot => new Vector2(
            Mathf.Clamp01(shovelCursorHotspot.x),
            Mathf.Clamp01(shovelCursorHotspot.y));

        public event Action ShovelToggleRequested;
        public event Action OpenParkRequested;
        public event Action ExitRequested;

        public void Bind(LevelDirector boundDirector, EventBus boundEventBus)
        {
            if (eventBus != null)
            {
                Unsubscribe();
            }

            director = boundDirector;
            eventBus = boundEventBus;
            state = director != null ? director.State : null;
            ResolveUnitDetailReferences();
            ResolveActionButtonReferences();
            ResolveLeaderboardReferences();
            CacheBaseTransforms();
            CacheUnitDetailShownPosition();
            displayedStarCount = -1;
            Subscribe();
            RefreshAll(false);
        }

        public void Unbind()
        {
            Unsubscribe();
            director = null;
            eventBus = null;
            state = null;
            delayJoyScoreChanged = false;
            HideUnitDetail(true);
            RefreshLeaderboard();
        }

        public void SetDelayJoyScoreChanged(bool delay)
        {
            delayJoyScoreChanged = delay;
        }

        public void SetShovelState(int remainingUses, bool isActive, bool canToggle)
        {
            int safeRemaining = Mathf.Max(0, remainingUses);
            bool hasUses = safeRemaining > 0;

            if (shovelRemainingText != null)
            {
                shovelRemainingText.SetText("{0}", safeRemaining);
            }

            if (shovelButton != null)
            {
                shovelButton.interactable = hasUses && canToggle;
            }

            if (shovelLockImage != null)
            {
                shovelLockImage.gameObject.SetActive(!hasUses);
            }

            if (shovelIconImage != null)
            {
                Sprite sprite = isActive ? shovelActiveSprite : shovelInactiveSprite;
                if (sprite != null)
                {
                    shovelIconImage.sprite = sprite;
                }
            }
        }

        public void ShowUnitDetail(UnitDefinitionSO unitDefinition)
        {
            ResolveUnitDetailReferences();
            if (unitDetailPanel == null || unitDefinition == null)
            {
                HideUnitDetail(true);
                return;
            }

            CacheUnitDetailShownPosition();
            ApplyUnitDetail(unitDefinition);

            if (!isUnitDetailOpen)
            {
                unitDetailPanel.anchoredPosition = GetUnitDetailHiddenPosition();
            }

            unitDetailPanel.gameObject.SetActive(true);
            TweenUnitDetailTo(unitDetailShownAnchoredPosition, true);
        }

        public void HideUnitDetail(bool immediate = false)
        {
            ResolveUnitDetailReferences();
            if (unitDetailPanel == null)
            {
                return;
            }

            CacheUnitDetailShownPosition();
            if (immediate)
            {
                SetUnitDetailHiddenImmediate();
                return;
            }

            TweenUnitDetailTo(GetUnitDetailHiddenPosition(), false);
        }

        public void ApplyJoyArrival(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            int totalJoy = state != null ? state.TotalJoy : displayedJoy + amount;
            int targetJoy = Mathf.Min(totalJoy, displayedJoy + amount);
            RefreshStarProgress(true, targetJoy);
        }

        public void PlayJoyIconPunch()
        {
            if (joyIconTransform == null)
            {
                return;
            }

            if (joyIconPunchCoroutine != null)
            {
                StopCoroutine(joyIconPunchCoroutine);
            }

            joyIconPunchCoroutine = StartCoroutine(PlayJoyIconPunchRoutine());
        }

        private void Awake()
        {
            if (shovelIconImage != null && shovelInactiveSprite == null)
            {
                shovelInactiveSprite = shovelIconImage.sprite;
            }

            if (visitorRemainingText != null)
            {
                visitorDefaultColor = visitorRemainingText.color;
            }

            CacheBaseTransforms();
            ResolveUnitDetailReferences();
            ResolveActionButtonReferences();
            ResolveLeaderboardReferences();
            CacheUnitDetailShownPosition();
            SetUnitDetailHiddenImmediate();
            SetSliderValue(starProgressSlider, 0f);
            SetSliderValue(rumbleProgressSlider, 0f);
            RefreshStarProgress(false);
            SetRumbleVisible(false);
            RefreshOpenParkButton();
            RefreshLeaderboard();
            SetShovelState(0, false, false);
            RefreshSpeed();
        }

        private void OnEnable()
        {
            ResolveActionButtonReferences();
            if (shovelButton != null)
            {
                shovelButton.onClick.RemoveListener(OnShovelButtonClicked);
                shovelButton.onClick.AddListener(OnShovelButtonClicked);
                RegisterButtonCosmetic(shovelButton, shovelButtonCosmeticTarget);
            }

            if (openParkButton != null)
            {
                openParkButton.onClick.RemoveListener(OnOpenParkButtonClicked);
                openParkButton.onClick.AddListener(OnOpenParkButtonClicked);
                RegisterButtonCosmetic(openParkButton, openParkButtonCosmeticTarget);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitButtonClicked);
                exitButton.onClick.AddListener(OnExitButtonClicked);
                RegisterButtonCosmetic(exitButton, exitButtonCosmeticTarget);
            }

            if (speedButton != null)
            {
                speedButton.onClick.RemoveListener(OnSpeedButtonClicked);
                speedButton.onClick.AddListener(OnSpeedButtonClicked);
                RegisterButtonCosmetic(speedButton, speedButtonCosmeticTarget);
            }
        }

        private void OnDisable()
        {
            if (shovelButton != null)
            {
                shovelButton.onClick.RemoveListener(OnShovelButtonClicked);
            }

            if (openParkButton != null)
            {
                openParkButton.onClick.RemoveListener(OnOpenParkButtonClicked);
            }

            if (buttonCosmeticService != null && openParkButton != null)
            {
                buttonCosmeticService.Unregister(openParkButton);
            }

            if (buttonCosmeticService != null && shovelButton != null)
            {
                buttonCosmeticService.Unregister(shovelButton);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitButtonClicked);
            }

            if (buttonCosmeticService != null && exitButton != null)
            {
                buttonCosmeticService.Unregister(exitButton);
            }

            if (speedButton != null)
            {
                speedButton.onClick.RemoveListener(OnSpeedButtonClicked);
            }

            if (buttonCosmeticService != null && speedButton != null)
            {
                buttonCosmeticService.Unregister(speedButton);
            }
        }

        private void Update()
        {
            if (state == null)
            {
                return;
            }

            UpdateFrenzyProgressTarget();
            TweenSlider(starProgressSlider, targetStarProgress);
            TweenSlider(rumbleProgressSlider, targetRumbleProgress);
            UpdateRumbleIconMotion();
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
            eventBus.ScoreChanged += OnScoreChanged;
            eventBus.LeaderboardUpdated += OnLeaderboardUpdated;
            eventBus.RumbleAdded += OnRumbleAdded;
            eventBus.VisitorProgressChanged += OnVisitorProgressChanged;
            eventBus.BoardCellUnlocked += OnBoardCellUnlocked;
            eventBus.FrenzyStarted += OnFrenzyStarted;
            eventBus.FrenzyEnded += OnFrenzyEnded;
            eventBus.DayEnded += OnDayEnded;
            eventBus.GameSpeedChanged += OnGameSpeedChanged;
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
            eventBus.ScoreChanged -= OnScoreChanged;
            eventBus.LeaderboardUpdated -= OnLeaderboardUpdated;
            eventBus.RumbleAdded -= OnRumbleAdded;
            eventBus.VisitorProgressChanged -= OnVisitorProgressChanged;
            eventBus.BoardCellUnlocked -= OnBoardCellUnlocked;
            eventBus.FrenzyStarted -= OnFrenzyStarted;
            eventBus.FrenzyEnded -= OnFrenzyEnded;
            eventBus.DayEnded -= OnDayEnded;
            eventBus.GameSpeedChanged -= OnGameSpeedChanged;
        }

        private void OnLevelStarted(LevelStartedEvent evt)
        {
            state = evt.State;
            displayedStarCount = -1;
            RefreshAll(false);
        }

        private void OnDayPrepared(DayPreparedEvent evt)
        {
            if (!IsCurrentState(evt.State))
            {
                return;
            }

            hasSeenRumbleThisDay = evt.State.CurrentRumble > 0;
            RefreshAll(false);
        }

        private void OnDraftOffered(DraftOfferedEvent evt)
        {
            if (IsCurrentState(evt.State))
            {
                RefreshOpenParkButton();
            }
        }

        private void OnDraftCompletedForDay(DraftCompletedForDayEvent evt)
        {
            if (IsCurrentState(evt.State))
            {
                RefreshOpenParkButton();
            }
        }

        private void OnParkOpened(ParkOpenedEvent evt)
        {
            if (IsCurrentState(evt.State))
            {
                RefreshMode();
                RefreshRumble();
                RefreshShovel(false);
                RefreshOpenParkButton();
                RefreshSpeed();
            }
        }

        private void OnGameSpeedChanged(GameSpeedChangedEvent evt)
        {
            RefreshSpeed();
        }

        private void OnBoardCellUnlocked(BoardCellUnlockedEvent evt)
        {
            if (IsCurrentState(evt.State))
            {
                RefreshShovel(false);
            }
        }

        private void OnScoreChanged(ScoreChangedEvent evt)
        {
            if (IsCurrentState(evt.State))
            {
                if (delayJoyScoreChanged && evt.SourceEvent.Amount > 0)
                {
                    return;
                }

                RefreshStarProgress(true);
            }
        }

        private void OnLeaderboardUpdated(LeaderboardUpdatedEvent evt)
        {
            if (IsCurrentState(evt.State))
            {
                RefreshLeaderboard();
            }
        }

        private void OnRumbleAdded(RumbleAddedEvent evt)
        {
            if (state == null)
            {
                return;
            }

            hasSeenRumbleThisDay |= evt.CurrentRumble > 0 || evt.Amount > 0;
            RefreshRumble();
        }

        private void OnVisitorProgressChanged(VisitorProgressChangedEvent evt)
        {
            if (IsCurrentState(evt.State))
            {
                RefreshVisitors(evt.RemainingVisitors);
            }
        }

        private void OnFrenzyStarted(FrenzyStartedEvent evt)
        {
            if (!IsCurrentState(evt.State))
            {
                return;
            }

            hasSeenRumbleThisDay = true;
            targetRumbleProgress = 1f;
            SetSliderValue(rumbleProgressSlider, 1f);
            RefreshRumble();
        }

        private void OnFrenzyEnded(FrenzyEndedEvent evt)
        {
            if (IsCurrentState(evt.State))
            {
                RefreshRumble();
            }
        }

        private void OnDayEnded(DayEndedEvent evt)
        {
            if (!IsCurrentState(evt.State))
            {
                return;
            }

            RefreshMode();
            RefreshVisitors(evt.State.RemainingMochiVisitorsToday);
            RefreshRumble();
            RefreshShovel(false);
            RefreshOpenParkButton();
            RefreshLeaderboard();
            RefreshSpeed();
        }

        private void RefreshAll(bool animateStars)
        {
            if (director != null)
            {
                state = director.State;
            }

            RefreshMode();
            RefreshVisitors(state != null ? state.RemainingMochiVisitorsToday : 0);
            RefreshStarProgress(animateStars);
            RefreshRumble();
            RefreshShovel(false);
            RefreshOpenParkButton();
            RefreshLeaderboard();
            RefreshSpeed();
        }

        private void RefreshSpeed()
        {
            float multiplier = state != null && state.GameSpeed != null
                ? state.GameSpeed.EffectiveMultiplier
                : 1f;
            if (speedText != null)
            {
                speedText.text = "x" + multiplier.ToString("0.##", CultureInfo.InvariantCulture);
            }

            ResolvedDayConfig config = state != null ? state.CurrentDayConfig : null;
            bool shouldShow = config != null &&
                              config.SpeedControlEnabled &&
                              state.Phase == GamePhase.Running;
            speedControlRoot?.SetActive(shouldShow);

            if (speedButton == null)
            {
                return;
            }

            speedButton.interactable = shouldShow &&
                                       config.AllowedGameSpeedMultipliers != null &&
                                       config.AllowedGameSpeedMultipliers.Count > 1;
        }

        private void ApplyUnitDetail(UnitDefinitionSO unitDefinition)
        {
            string displayName = !string.IsNullOrWhiteSpace(unitDefinition.DisplayName)
                ? unitDefinition.DisplayName
                : unitDefinition.UnitTypeId;
            string description = !string.IsNullOrWhiteSpace(unitDefinition.Description)
                ? unitDefinition.Description
                : string.Empty;

            if (unitDetailNameText != null)
            {
                unitDetailNameText.text = displayName;
            }

            if (unitDetailDescriptionText != null)
            {
                unitDetailDescriptionText.text = description;
            }

            if (unitDetailDescriptionScrollRect != null)
            {
                unitDetailDescriptionScrollRect.verticalNormalizedPosition = 1f;
            }

            if (unitDetailIconImage != null)
            {
                Sprite icon = unitDefinition.Icon;
                unitDetailIconImage.sprite = icon;
                unitDetailIconImage.enabled = icon != null;
            }

            if (unitDetailBackgroundImage != null)
            {
                Sprite background = GetUnitDetailRarityBackground(unitDefinition.Rarity);
                unitDetailBackgroundImage.sprite = background != null ? background : unitDetailDefaultBackgroundSprite;
                unitDetailBackgroundImage.enabled = unitDetailBackgroundImage.sprite != null;
            }
        }

        private Sprite GetUnitDetailRarityBackground(UnitRarity rarity)
        {
            switch (rarity)
            {
                case UnitRarity.Purple:
                    return purpleRarityDetailBackground;
                case UnitRarity.Gold:
                    return goldRarityDetailBackground;
                default:
                    return blueRarityDetailBackground;
            }
        }

        private void TweenUnitDetailTo(Vector2 targetPosition, bool openAtEnd)
        {
            if (unitDetailPanel == null)
            {
                return;
            }

            if (unitDetailTweenCoroutine != null)
            {
                StopCoroutine(unitDetailTweenCoroutine);
            }

            isUnitDetailOpen = openAtEnd;
            SetUnitDetailInteractable(openAtEnd);
            unitDetailTweenCoroutine = StartCoroutine(TweenUnitDetailRoutine(targetPosition, openAtEnd));
        }

        private IEnumerator TweenUnitDetailRoutine(Vector2 targetPosition, bool openAtEnd)
        {
            Vector2 startPosition = unitDetailPanel.anchoredPosition;
            float duration = Mathf.Max(0.01f, unitDetailTweenDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = openAtEnd
                    ? 1f - Mathf.Pow(1f - t, 3f)
                    : t * t;
                unitDetailPanel.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
                yield return null;
            }

            unitDetailPanel.anchoredPosition = targetPosition;
            isUnitDetailOpen = openAtEnd;
            SetUnitDetailInteractable(openAtEnd);
            if (!openAtEnd)
            {
                unitDetailPanel.gameObject.SetActive(false);
            }

            unitDetailTweenCoroutine = null;
        }

        private void SetUnitDetailHiddenImmediate()
        {
            if (unitDetailPanel == null)
            {
                return;
            }

            if (unitDetailTweenCoroutine != null)
            {
                StopCoroutine(unitDetailTweenCoroutine);
                unitDetailTweenCoroutine = null;
            }

            unitDetailPanel.anchoredPosition = GetUnitDetailHiddenPosition();
            SetUnitDetailInteractable(false);
            unitDetailPanel.gameObject.SetActive(false);
            isUnitDetailOpen = false;
        }

        private Vector2 GetUnitDetailHiddenPosition()
        {
            RectTransform canvasRect = transform as RectTransform;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvasRect = canvas.transform as RectTransform;
            }

            float canvasWidth = canvasRect != null ? canvasRect.rect.width : Screen.width;
            float panelWidth = unitDetailPanel != null
                ? Mathf.Max(unitDetailPanel.rect.width, unitDetailPanel.sizeDelta.x)
                : 0f;
            float hiddenDistance = Mathf.Max(1f, canvasWidth + panelWidth + unitDetailOffscreenPadding);
            return unitDetailShownAnchoredPosition + Vector2.right * hiddenDistance;
        }

        private void SetUnitDetailInteractable(bool interactable)
        {
            if (unitDetailCanvasGroup == null && unitDetailPanel != null)
            {
                unitDetailCanvasGroup = unitDetailPanel.GetComponent<CanvasGroup>();
                if (unitDetailCanvasGroup == null)
                {
                    unitDetailCanvasGroup = unitDetailPanel.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (unitDetailCanvasGroup != null)
            {
                unitDetailCanvasGroup.interactable = interactable;
                unitDetailCanvasGroup.blocksRaycasts = interactable;
            }
        }

        private void RefreshMode()
        {
            if (state == null)
            {
                SetModeText(1, 1, EditModeText);
                SetModeIcon(editModeSprite);
                return;
            }

            bool isGameplay = state.Phase == GamePhase.Running;
            SetModeText(state.CurrentDayIndex + 1, GetMaxDayNumber(), isGameplay ? GameplayModeText : EditModeText);
            SetModeIcon(isGameplay ? gameplayModeSprite : editModeSprite);
        }

        private void SetModeText(int dayNumber, int maxDayNumber, string modeText)
        {
            if (dayModeText != null)
            {
                dayModeText.text = $"Ngày {Mathf.Max(1, dayNumber)}/{Mathf.Max(1, maxDayNumber)} -{modeText}";
            }
        }

        private int GetMaxDayNumber()
        {
            return state != null && state.Definition != null
                ? state.Definition.TotalDayCount
                : 1;
        }

        private void SetModeIcon(Sprite sprite)
        {
            if (gameModeIcon != null && sprite != null)
            {
                gameModeIcon.sprite = sprite;
            }
        }

        private void RefreshVisitors(int remainingVisitors)
        {
            int safeRemaining = Mathf.Max(0, remainingVisitors);
            if (visitorRemainingText == null)
            {
                return;
            }

            visitorRemainingText.SetText("{0}", safeRemaining);
            visitorRemainingText.color = safeRemaining <= 0 ? visitorEmptyColor : visitorDefaultColor;
        }

        private void RefreshStarProgress(bool animateStars)
        {
            int totalJoy = state != null ? state.TotalJoy : 0;
            RefreshStarProgress(animateStars, totalJoy);
        }

        private void RefreshStarProgress(bool animateStars, int visualTotalJoy)
        {
            int totalJoy = Mathf.Max(0, visualTotalJoy);
            StarThresholds thresholds = state != null && state.Definition != null
                ? state.Definition.StarThresholds
                : null;
            int oneStar = thresholds != null ? thresholds.OneStar : 0;
            int twoStar = thresholds != null ? thresholds.TwoStar : 0;
            int threeStar = thresholds != null ? thresholds.ThreeStar : 0;
            int starCount = thresholds != null ? thresholds.CalculateStars(totalJoy) : 0;

            if (joyText != null)
            {
                joyText.SetText("{0}", totalJoy);
            }

            displayedJoy = totalJoy;
            SetTargetText(oneStarTargetText, oneStar);
            SetTargetText(twoStarTargetText, twoStar);
            SetTargetText(threeStarTargetText, threeStar);
            targetStarProgress = GetStarProgress(totalJoy, oneStar, twoStar, threeStar);

            if (displayedStarCount < 0)
            {
                displayedStarCount = starCount;
                ApplyStarSprites(starCount);
                return;
            }

            if (starCount > displayedStarCount)
            {
                for (int i = displayedStarCount; i < starCount; i++)
                {
                    SetStarUnlocked(i, true);
                    if (animateStars)
                    {
                        PlayStarPop(i);
                    }
                }
            }
            else
            {
                ApplyStarSprites(starCount);
            }

            displayedStarCount = starCount;
        }

        private float GetStarProgress(int totalJoy, int oneStar, int twoStar, int threeStar)
        {
            int maxProgress = Mathf.Max(1, threeStar);
            float oneStarPosition = GetStarSliderPosition(0, oneStar / (float)maxProgress);
            float twoStarPosition = GetStarSliderPosition(1, twoStar / (float)maxProgress);
            float threeStarPosition = GetStarSliderPosition(2, threeStar / (float)maxProgress);

            if (totalJoy < oneStar)
            {
                return Mathf.Lerp(0f, oneStarPosition, Mathf.InverseLerp(0, oneStar, totalJoy));
            }

            if (totalJoy < twoStar)
            {
                return Mathf.Lerp(oneStarPosition, twoStarPosition, Mathf.InverseLerp(oneStar, twoStar, totalJoy));
            }

            if (totalJoy < threeStar)
            {
                return Mathf.Lerp(twoStarPosition, threeStarPosition, Mathf.InverseLerp(twoStar, threeStar, totalJoy));
            }

            return threeStarPosition;
        }

        private float GetStarSliderPosition(int starIndex, float fallback)
        {
            RectTransform starTransform = GetArrayItem(starTransforms, starIndex);
            RectTransform fillRect = starProgressSlider != null ? starProgressSlider.fillRect : null;
            if (starTransform == null || fillRect == null)
            {
                return Mathf.Clamp01(fallback);
            }

            Image fillImage = fillRect.GetComponent<Image>();
            RectTransform progressRect = fillImage != null && fillImage.type == Image.Type.Filled
                ? fillRect
                : fillRect.parent as RectTransform;
            if (progressRect == null)
            {
                return Mathf.Clamp01(fallback);
            }

            Vector3 starCenter = progressRect.InverseTransformPoint(
                starTransform.TransformPoint(starTransform.rect.center));
            Rect progressBounds = progressRect.rect;
            switch (starProgressSlider.direction)
            {
                case Slider.Direction.LeftToRight:
                    return Mathf.InverseLerp(progressBounds.xMin, progressBounds.xMax, starCenter.x);
                case Slider.Direction.RightToLeft:
                    return Mathf.InverseLerp(progressBounds.xMax, progressBounds.xMin, starCenter.x);
                case Slider.Direction.BottomToTop:
                    return Mathf.InverseLerp(progressBounds.yMin, progressBounds.yMax, starCenter.y);
                case Slider.Direction.TopToBottom:
                    return Mathf.InverseLerp(progressBounds.yMax, progressBounds.yMin, starCenter.y);
                default:
                    return Mathf.Clamp01(fallback);
            }
        }

        private static void SetTargetText(TextMeshProUGUI text, int amount)
        {
            if (text != null)
            {
                text.SetText("{0}", amount);
            }
        }

        private void ApplyStarSprites(int starCount)
        {
            for (int i = 0; i < 3; i++)
            {
                SetStarUnlocked(i, i < starCount);
            }
        }

        private void SetStarUnlocked(int index, bool unlocked)
        {
            Image starImage = GetArrayItem(starImages, index);
            if (starImage == null)
            {
                return;
            }

            Sprite sprite = unlocked ? starUnlockedSprite : starLockedSprite;
            if (sprite != null)
            {
                starImage.enabled = true;
                starImage.sprite = sprite;
                return;
            }

            if (!unlocked)
            {
                starImage.enabled = false;
                return;
            }

            starImage.enabled = true;
        }

        private void RefreshRumble()
        {
            int currentRumble = state != null ? state.CurrentRumble : 0;
            int maxRumble = GetMaxRumble();
            hasSeenRumbleThisDay |= currentRumble > 0;

            if (rumbleText != null)
            {
                rumbleText.SetText("{0}/{1}", currentRumble, maxRumble);
            }

            bool isGameplay = state != null && state.Phase == GamePhase.Running;
            SetRumbleVisible(isGameplay && hasSeenRumbleThisDay);
            targetRumbleProgress = state != null && state.Frenzy.IsActive
                ? GetFrenzyProgress()
                : Mathf.Clamp01(currentRumble / (float)Mathf.Max(1, maxRumble));
        }

        private void RefreshShovel(bool isActive)
        {
            int remainingUses = state != null ? state.RemainingShovelUses : 0;
            bool canToggle = state != null && state.Phase == GamePhase.Placement;
            SetShovelState(remainingUses, isActive && canToggle && remainingUses > 0, canToggle);
        }

        private void RefreshOpenParkButton()
        {
            if (openParkButton == null)
            {
                return;
            }

            bool canOpenPark = state != null &&
                state.Phase == GamePhase.Placement &&
                state.Draft != null &&
                state.Draft.IsCompletedForDay &&
                !state.Draft.HasPendingPick;

            openParkButton.gameObject.SetActive(canOpenPark);
            openParkButton.interactable = canOpenPark;
        }

        private void RefreshLeaderboard()
        {
            ResolveLeaderboardReferences();
            leaderboardScratch.Clear();

            if (state != null && state.Scoring != null)
            {
                foreach (KeyValuePair<string, UnitTypeScoreSummary> pair in state.Scoring.UnitTypeSummaries)
                {
                    UnitTypeScoreSummary summary = pair.Value;
                    if (summary != null && summary.FinalJoy > 0)
                    {
                        leaderboardScratch.Add(summary);
                    }
                }
            }

            leaderboardScratch.Sort(CompareLeaderboardSummaries);

            int rowCount = Mathf.Min(MaxLeaderboardRows, leaderboardScratch.Count);
            for (int i = 0; i < rowCount; i++)
            {
                UnitLeaderboardView row = GetOrCreateLeaderboardRow(i);
                if (row == null)
                {
                    continue;
                }

                UnitTypeScoreSummary summary = leaderboardScratch[i];
                Sprite icon = null;
                if (director != null &&
                    !string.IsNullOrWhiteSpace(summary.UnitTypeId) &&
                    director.TryGetUnitDefinition(summary.UnitTypeId, out UnitDefinitionSO unitDefinition) &&
                    unitDefinition != null)
                {
                    icon = unitDefinition.Icon;
                }

                row.Apply(
                    i,
                    icon,
                    GetLeaderboardBadgeSprite(i, row.DefaultBadgeSprite),
                    GetLeaderboardIndexColor(i),
                    summary.FinalJoy);
            }

            for (int i = rowCount; i < leaderboardRows.Count; i++)
            {
                leaderboardRows[i].SetVisible(false);
            }
        }

        private UnitLeaderboardView GetOrCreateLeaderboardRow(int index)
        {
            if (index < 0 || index >= MaxLeaderboardRows || leaderboardLayout == null)
            {
                return null;
            }

            while (leaderboardRows.Count <= index)
            {
                UnitLeaderboardView rowView = CreateLeaderboardRowObject();
                if (rowView == null)
                {
                    return null;
                }

                leaderboardRows.Add(rowView);
            }

            UnitLeaderboardView row = leaderboardRows[index];
            row.transform.SetSiblingIndex(index + GetTemplateSiblingOffset());
            return row;
        }

        private UnitLeaderboardView CreateLeaderboardRowObject()
        {
            if (leaderboardLayout == null || leaderboardRowTemplate == null)
            {
                return null;
            }

            UnitLeaderboardView rowView = Instantiate(leaderboardRowTemplate, leaderboardLayout);
            rowView.name = leaderboardRowTemplate.name;
            rowView.SetVisible(true);
            return rowView;
        }

        private int GetTemplateSiblingOffset()
        {
            return leaderboardRowTemplate != null &&
                leaderboardRowTemplate.transform.parent == leaderboardLayout &&
                !leaderboardRowTemplate.gameObject.activeSelf
                ? 1
                : 0;
        }

        private Sprite GetLeaderboardBadgeSprite(int index, Sprite fallback)
        {
            Sprite sprite = GetArrayItem(leaderboardBadgeSprites, index);
            return sprite != null ? sprite : fallback;
        }

        private Color GetLeaderboardIndexColor(int index)
        {
            Color color = GetArrayItem(leaderboardTopIndexColors, index);
            if (index < 3 && color.a > 0f)
            {
                return color;
            }

            return leaderboardOtherIndexColor;
        }

        private static int CompareLeaderboardSummaries(UnitTypeScoreSummary left, UnitTypeScoreSummary right)
        {
            int joyComparison = right.FinalJoy.CompareTo(left.FinalJoy);
            if (joyComparison != 0)
            {
                return joyComparison;
            }

            int triggerComparison = right.TriggerCount.CompareTo(left.TriggerCount);
            if (triggerComparison != 0)
            {
                return triggerComparison;
            }

            return string.Compare(left.UnitTypeId, right.UnitTypeId, StringComparison.Ordinal);
        }

        private void OnShovelButtonClicked()
        {
            ShovelToggleRequested?.Invoke();
        }

        private void OnOpenParkButtonClicked()
        {
            OpenParkRequested?.Invoke();
        }

        private void OnExitButtonClicked()
        {
            ExitRequested?.Invoke();
        }

        private void OnSpeedButtonClicked()
        {
            if (director == null || state == null || state.Phase != GamePhase.Running)
            {
                return;
            }

            ResolvedDayConfig config = state.CurrentDayConfig;
            IReadOnlyList<float> allowedMultipliers = config != null
                ? config.AllowedGameSpeedMultipliers
                : null;
            if (config == null ||
                !config.SpeedControlEnabled ||
                allowedMultipliers == null ||
                allowedMultipliers.Count <= 1)
            {
                return;
            }

            float currentMultiplier = state.GameSpeed.RequestedMultiplier;
            int currentIndex = 0;
            float closestDistance = Mathf.Abs(allowedMultipliers[0] - currentMultiplier);
            for (int i = 1; i < allowedMultipliers.Count; i++)
            {
                float distance = Mathf.Abs(allowedMultipliers[i] - currentMultiplier);
                if (distance < closestDistance)
                {
                    currentIndex = i;
                    closestDistance = distance;
                }
            }

            int nextIndex = (currentIndex + 1) % allowedMultipliers.Count;
            director.ChangeGameSpeed(allowedMultipliers[nextIndex]);
        }

        private void UpdateFrenzyProgressTarget()
        {
            if (state == null || !state.Frenzy.IsActive)
            {
                return;
            }

            targetRumbleProgress = GetFrenzyProgress();
            if (rumbleText != null)
            {
                rumbleText.SetText("{0}/{1}", state.CurrentRumble, GetMaxRumble());
            }
        }

        private float GetFrenzyProgress()
        {
            if (state == null || state.CurrentDayConfig == null || state.CurrentDayConfig.FrenzyConfig == null)
            {
                return 0f;
            }

            float duration = Mathf.Max(0.01f, state.CurrentDayConfig.FrenzyConfig.DurationSeconds);
            return Mathf.Clamp01(state.Frenzy.TimeRemaining / duration);
        }

        private int GetMaxRumble()
        {
            if (state == null || state.CurrentDayConfig == null || state.CurrentDayConfig.FrenzyConfig == null)
            {
                return 1;
            }

            return state.CurrentDayConfig.FrenzyConfig.RumbleThreshold;
        }

        private void SetRumbleVisible(bool visible)
        {
            if (rumbleRoot != null)
            {
                rumbleRoot.gameObject.SetActive(visible);
            }
        }

        private void TweenSlider(Slider slider, float target)
        {
            if (slider == null)
            {
                return;
            }

            float duration = Mathf.Max(0.01f, sliderTweenDuration);
            float step = Time.unscaledDeltaTime / duration;
            slider.value = Mathf.MoveTowards(slider.value, Mathf.Clamp01(target), step);
        }

        private static void SetSliderValue(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            }
        }

        private void PlayStarPop(int index)
        {
            RectTransform starTransform = GetArrayItem(starTransforms, index);
            if (starTransform == null)
            {
                return;
            }

            if (starPopCoroutines[index] != null)
            {
                StopCoroutine(starPopCoroutines[index]);
            }

            starPopCoroutines[index] = StartCoroutine(PlayStarPopRoutine(index, starTransform));
        }

        private IEnumerator PlayStarPopRoutine(int index, RectTransform starTransform)
        {
            Vector3 baseScale = starBaseScales[index] == Vector3.zero ? starTransform.localScale : starBaseScales[index];
            Quaternion baseRotation = starBaseRotations[index];
            float halfDuration = Mathf.Max(0.01f, starPopDuration) * 0.5f;
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float eased = 1f - (1f - t) * (1f - t);
                starTransform.localScale = Vector3.LerpUnclamped(baseScale, baseScale * starPopScale, eased);
                starTransform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, starPopRotationDegrees, eased));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float eased = t * t;
                starTransform.localScale = Vector3.LerpUnclamped(baseScale * starPopScale, baseScale, eased);
                starTransform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, Mathf.Lerp(starPopRotationDegrees, 0f, eased));
                yield return null;
            }

            starTransform.localScale = baseScale;
            starTransform.localRotation = baseRotation;
            starPopCoroutines[index] = null;
        }

        private IEnumerator PlayJoyIconPunchRoutine()
        {
            Vector3 baseScale = joyIconBaseScale == Vector3.zero ? joyIconTransform.localScale : joyIconBaseScale;
            float halfDuration = Mathf.Max(0.01f, joyIconPunchDuration) * 0.5f;
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float eased = 1f - (1f - t) * (1f - t);
                joyIconTransform.localScale = Vector3.LerpUnclamped(baseScale, baseScale * joyIconPunchScale, eased);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float eased = t * t;
                joyIconTransform.localScale = Vector3.LerpUnclamped(baseScale * joyIconPunchScale, baseScale, eased);
                yield return null;
            }

            joyIconTransform.localScale = baseScale;
            joyIconPunchCoroutine = null;
        }

        private void UpdateRumbleIconMotion()
        {
            if (rumbleIconTransform == null)
            {
                return;
            }

            if (state != null && state.Frenzy.IsActive)
            {
                float wave = Mathf.Sin(Time.unscaledTime * rumbleShakeFrequency);
                rumbleIconTransform.localRotation =
                    rumbleIconBaseRotation * Quaternion.Euler(0f, 0f, wave * rumbleShakeDegrees);
                rumbleIconTransform.localPosition =
                    rumbleIconBasePosition + new Vector3(wave * 2f, Mathf.Cos(Time.unscaledTime * rumbleShakeFrequency * 0.73f) * 1.2f, 0f);
                return;
            }

            rumbleIconTransform.localRotation = rumbleIconBaseRotation;
            rumbleIconTransform.localPosition = rumbleIconBasePosition;
        }

        private void CacheBaseTransforms()
        {
            for (int i = 0; i < 3; i++)
            {
                RectTransform starTransform = GetArrayItem(starTransforms, i);
                if (starTransform == null)
                {
                    continue;
                }

                starBaseScales[i] = starTransform.localScale;
                starBaseRotations[i] = starTransform.localRotation;
            }

            if (rumbleIconTransform != null)
            {
                rumbleIconBasePosition = rumbleIconTransform.localPosition;
                rumbleIconBaseRotation = rumbleIconTransform.localRotation;
            }

            if (joyIconTransform != null)
            {
                joyIconBaseScale = joyIconTransform.localScale;
            }
        }

        private void ResolveUnitDetailReferences()
        {
            if (unitDetailPanel == null)
            {
                unitDetailPanel = FindDeep<RectTransform>(transform, UnitDetailPanelName);
            }

            if (unitDetailPanel != null)
            {
                if (unitDetailBackgroundImage == null)
                {
                    unitDetailBackgroundImage = FindDeep<Image>(unitDetailPanel, UnitDetailBackgroundName);
                }

                if (unitDetailIconImage == null)
                {
                    unitDetailIconImage = FindDeep<Image>(unitDetailPanel, UnitDetailIconName);
                }

                if (unitDetailNameText == null)
                {
                    unitDetailNameText = FindDeep<TextMeshProUGUI>(unitDetailPanel, UnitDetailNameTextName);
                }

                if (unitDetailDescriptionText == null)
                {
                    unitDetailDescriptionText = FindDeep<TextMeshProUGUI>(unitDetailPanel, UnitDetailDescriptionTextName);
                }

                if (unitDetailDescriptionScrollRect == null)
                {
                    unitDetailDescriptionScrollRect = unitDetailPanel.GetComponentInChildren<ScrollRect>(true);
                }

                if (unitDetailCanvasGroup == null)
                {
                    unitDetailCanvasGroup = unitDetailPanel.GetComponent<CanvasGroup>();
                    if (unitDetailCanvasGroup == null)
                    {
                        unitDetailCanvasGroup = unitDetailPanel.gameObject.AddComponent<CanvasGroup>();
                    }
                }
            }

            if (unitDetailDefaultBackgroundSprite == null && unitDetailBackgroundImage != null)
            {
                unitDetailDefaultBackgroundSprite = unitDetailBackgroundImage.sprite;
            }
        }

        private void ResolveActionButtonReferences()
        {
            if (openParkButton == null)
            {
                openParkButton = FindDeep<Button>(transform, OpenParkButtonName);
            }

            if (openParkButtonCosmeticTarget == null && openParkButton != null)
            {
                openParkButtonCosmeticTarget = openParkButton.transform as RectTransform;
            }

            if (exitButton == null)
            {
                exitButton = FindDeep<Button>(transform, ExitButtonName);
            }

            if (exitButtonCosmeticTarget == null && exitButton != null)
            {
                exitButtonCosmeticTarget = exitButton.transform as RectTransform;
            }

            if (speedButton == null)
            {
                speedButton = FindDeep<Button>(transform, SpeedButtonName);
            }

            if (speedText == null)
            {
                speedText = FindDeep<TextMeshProUGUI>(transform, SpeedTextName);
            }

            if (speedControlRoot == null && speedButton != null && speedButton.transform.parent != null)
            {
                speedControlRoot = speedButton.transform.parent.gameObject;
            }

            if (speedButtonCosmeticTarget == null && speedButton != null)
            {
                speedButtonCosmeticTarget = speedButton.transform as RectTransform;
            }

            if (shovelButtonCosmeticTarget == null && shovelButton != null)
            {
                shovelButtonCosmeticTarget = shovelButton.transform as RectTransform;
            }

            ResolveButtonCosmeticService();
        }

        private void ResolveButtonCosmeticService()
        {
            if (buttonCosmeticService != null)
            {
                return;
            }

            buttonCosmeticService = GetComponentInParent<UIButtonCosmeticService>(true);
            if (buttonCosmeticService != null)
            {
                return;
            }

            buttonCosmeticService = GetComponentInChildren<UIButtonCosmeticService>(true);
            if (buttonCosmeticService != null)
            {
                return;
            }

            buttonCosmeticService = gameObject.AddComponent<UIButtonCosmeticService>();
        }

        private void RegisterButtonCosmetic(Button button, RectTransform cosmeticTarget)
        {
            if (buttonCosmeticService == null || button == null)
            {
                return;
            }

            RectTransform target = cosmeticTarget != null
                ? cosmeticTarget
                : button.transform as RectTransform;
            buttonCosmeticService.Register(button, UICosmeticType.Bouncy, target);
        }

        private void ResolveLeaderboardReferences()
        {
            if (leaderboardLayout == null)
            {
                leaderboardLayout = FindDeep<RectTransform>(transform, LeaderboardLayoutName);
            }

            if (leaderboardRowTemplate == null && leaderboardLayout != null)
            {
                leaderboardRowTemplate = leaderboardLayout.GetComponentInChildren<UnitLeaderboardView>(true);
            }

            if (leaderboardRowTemplate == null)
            {
                GameObject rowPrefab = Resources.Load<GameObject>(LeaderboardResourcePath);
                if (rowPrefab != null)
                {
                    rowPrefab.TryGetComponent(out leaderboardRowTemplate);
                }
            }

            if (leaderboardRowTemplate != null && leaderboardRowTemplate.transform.parent == leaderboardLayout)
            {
                leaderboardRowTemplate.SetVisible(false);
            }
        }

        private void CacheUnitDetailShownPosition()
        {
            if (hasCachedUnitDetailShownPosition || unitDetailPanel == null)
            {
                return;
            }

            unitDetailShownAnchoredPosition = unitDetailPanel.anchoredPosition;
            hasCachedUnitDetailShownPosition = true;
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

        private static T GetArrayItem<T>(T[] items, int index)
            where T : Object
        {
            return items != null && index >= 0 && index < items.Length ? items[index] : null;
        }

        private static Color GetArrayItem(Color[] items, int index)
        {
            return items != null && index >= 0 && index < items.Length ? items[index] : default(Color);
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

    }
}
