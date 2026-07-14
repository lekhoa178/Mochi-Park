using System;
using System.Collections;
using MochiPark.Runtime.Audio;
using MochiPark.Runtime.Data;
using MochiPark.Runtime.Level;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    public sealed class ResultView : MonoBehaviour
    {
        private const string PopupName = "Popup";
        private const string RetryButtonName = "Button_Retry";
        private const string ExitButtonName = "Button_Exit";
        private const string ScoreTextName = "Text_CurrentScore";
        private const string LevelTextName = "Text_Level";
        private const string StarProgressSliderName = "Slider_StarProgression";
        private const string JoyIconName = "Image_JoyIcon";
        private const string GameResultSfxCueId = "game_result";
        private static readonly string[] StarRootNames = { "Star_1", "Star_2", "Star_3" };

        [Header("References")]
        [SerializeField] private RectTransform popup;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Slider starProgressSlider;
        [SerializeField] private RectTransform joyIconTransform;
        [SerializeField] private Image[] starImages = new Image[3];
        [SerializeField] private RectTransform[] starTransforms = new RectTransform[3];

        [Header("Button Cosmetics")]
        [SerializeField] private UIButtonCosmeticService buttonCosmeticService;
        [SerializeField] private RectTransform retryButtonCosmeticTarget;
        [SerializeField] private RectTransform exitButtonCosmeticTarget;

        [Header("Star Sprites")]
        [SerializeField] private Sprite reachedStarSprite;
        [SerializeField] private Sprite unreachedStarSprite;

        [Header("Motion")]
        [SerializeField] private float popupScaleDuration = 0.46f;
        [SerializeField] private float progressDuration = 1.35f;
        [SerializeField] private float starPopDuration = 0.28f;
        [SerializeField] private float starPopScale = 1.28f;
        [SerializeField] private float starPopRotationDegrees = 14f;
        [SerializeField] private float scoreLoopDuration = 0.62f;
        [SerializeField] private float scoreLoopScale = 1.3f;
        [SerializeField] private float scoreLoopRotationDegrees = 6f;

        private readonly Coroutine[] starPopCoroutines = new Coroutine[3];
        private readonly Vector3[] starBaseScales = new Vector3[3];
        private readonly Quaternion[] starBaseRotations = new Quaternion[3];
        private readonly Sprite[] starBaseSprites = new Sprite[3];
        private RuntimeLevelState state;
        private Vector3 popupBaseScale = Vector3.one;
        private Quaternion popupBaseRotation = Quaternion.identity;
        private Vector3 scoreBaseScale = Vector3.one;
        private Quaternion scoreBaseRotation = Quaternion.identity;
        private int displayedStarCount = -1;
        private Coroutine showCoroutine;
        private Coroutine scoreLoopCoroutine;

        public event Action RetryRequested;
        public event Action ExitRequested;

        public void Show(RuntimeLevelState resultState)
        {
            state = resultState;
            ResolveReferences();
            StopMotionCoroutines();
            ResetMotionTransforms();

            gameObject.SetActive(true);
            transform.localScale = Vector3.one;
            SetButtonsInteractable(true);
            ApplyLevelText();
            ApplyProgress(0);
            MochiParkAudioManager.PlaySfx(GameResultSfxCueId);

            if (popup != null)
            {
                popup.localScale = Vector3.zero;
                popup.localRotation = popupBaseRotation;
            }

            showCoroutine = StartCoroutine(ShowRoutine());
        }

        public void HideImmediate()
        {
            StopMotionCoroutines();
            ResetMotionTransforms();
            SetButtonsInteractable(false);
            gameObject.SetActive(false);
        }

        private void Awake()
        {
            ResolveReferences();
            CacheBaseTransforms();
            ApplyProgress(0);
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(OnRetryClicked);
                retryButton.onClick.AddListener(OnRetryClicked);
                RegisterButtonCosmetic(retryButton, retryButtonCosmeticTarget);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitClicked);
                exitButton.onClick.AddListener(OnExitClicked);
                RegisterButtonCosmetic(exitButton, exitButtonCosmeticTarget);
            }
        }

        private void OnDisable()
        {
            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(OnRetryClicked);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitClicked);
            }

            if (buttonCosmeticService != null && retryButton != null)
            {
                buttonCosmeticService.Unregister(retryButton);
            }

            if (buttonCosmeticService != null && exitButton != null)
            {
                buttonCosmeticService.Unregister(exitButton);
            }
        }

        private void OnDestroy()
        {
            StopMotionCoroutines();
        }

        private IEnumerator ShowRoutine()
        {
            yield return TweenPopupIn();
            yield return PlayProgressRoutine();

            if (scoreLoopCoroutine != null)
            {
                StopCoroutine(scoreLoopCoroutine);
            }

            scoreLoopCoroutine = StartCoroutine(PlayScoreLoopRoutine());
            showCoroutine = null;
        }

        private IEnumerator TweenPopupIn()
        {
            if (popup == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0.01f, popupScaleDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOutBack(t);
                popup.localScale = Vector3.LerpUnclamped(Vector3.zero, popupBaseScale, eased);
                yield return null;
            }

            popup.localScale = popupBaseScale;
            popup.localRotation = popupBaseRotation;
        }

        private IEnumerator PlayProgressRoutine()
        {
            int targetJoy = state != null ? Mathf.Max(0, state.TotalJoy) : 0;
            int maxProgress = GetMaxProgress();
            float targetSliderProgress = Mathf.Clamp01(targetJoy / (float)maxProgress);
            float duration = Mathf.Max(0.01f, progressDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                int visualJoy = Mathf.RoundToInt(Mathf.Lerp(0, targetJoy, eased));
                float sliderProgress = Mathf.Lerp(0f, targetSliderProgress, eased);
                ApplyProgress(visualJoy, sliderProgress);
                yield return null;
            }

            ApplyProgress(targetJoy, targetSliderProgress);
        }

        private IEnumerator PlayScoreLoopRoutine()
        {
            RectTransform scoreTransform = scoreText != null ? scoreText.rectTransform : null;
            if (scoreTransform == null)
            {
                yield break;
            }

            while (true)
            {
                yield return TweenScore(scoreTransform, true);
                yield return TweenScore(scoreTransform, false);
            }
        }

        private IEnumerator TweenScore(RectTransform scoreTransform, bool outwards)
        {
            float halfDuration = Mathf.Max(0.01f, scoreLoopDuration) * 0.5f;
            float elapsed = 0f;
            Vector3 fromScale = outwards ? scoreBaseScale : scoreBaseScale * scoreLoopScale;
            Vector3 toScale = outwards ? scoreBaseScale * scoreLoopScale : scoreBaseScale;
            float fromRotation = outwards ? 0f : scoreLoopRotationDegrees;
            float toRotation = outwards ? scoreLoopRotationDegrees : 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float eased = outwards ? EaseOutBack(t) : t * t;
                scoreTransform.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
                scoreTransform.localRotation =
                    scoreBaseRotation * Quaternion.Euler(0f, 0f, Mathf.Lerp(fromRotation, toRotation, eased));
                yield return null;
            }

            scoreTransform.localScale = toScale;
            scoreTransform.localRotation = scoreBaseRotation * Quaternion.Euler(0f, 0f, toRotation);
        }

        private void ApplyProgress(int visualJoy)
        {
            ApplyProgress(visualJoy, GetSliderProgress(visualJoy));
        }

        private void ApplyProgress(int visualJoy, float sliderProgress)
        {
            int safeJoy = Mathf.Max(0, visualJoy);
            StarThresholds thresholds = state != null && state.Definition != null
                ? state.Definition.StarThresholds
                : null;
            int maxProgress = GetMaxProgress();
            int progressJoy = Mathf.RoundToInt(Mathf.Clamp01(sliderProgress) * maxProgress);
            int starCount = thresholds != null ? thresholds.CalculateStars(progressJoy) : 0;

            if (scoreText != null)
            {
                scoreText.SetText("{0}", safeJoy);
            }

            if (starProgressSlider != null)
            {
                starProgressSlider.SetValueWithoutNotify(Mathf.Clamp01(sliderProgress));
            }

            if (displayedStarCount < 0)
            {
                displayedStarCount = starCount;
                ApplyStars(starCount);
                return;
            }

            if (starCount > displayedStarCount)
            {
                for (int i = displayedStarCount; i < starCount; i++)
                {
                    SetStarUnlocked(i, true);
                    PlayStarPop(i);
                }
            }
            else if (starCount < displayedStarCount)
            {
                ApplyStars(starCount);
            }

            displayedStarCount = starCount;
        }

        private int GetMaxProgress()
        {
            StarThresholds thresholds = state != null && state.Definition != null
                ? state.Definition.StarThresholds
                : null;
            return thresholds != null ? Mathf.Max(1, thresholds.ThreeStar) : 1;
        }

        private float GetSliderProgress(int joy)
        {
            return Mathf.Clamp01(Mathf.Max(0, joy) / (float)GetMaxProgress());
        }

        private void ApplyLevelText()
        {
            if (levelText == null || state == null || state.Definition == null)
            {
                return;
            }

            string displayName = !string.IsNullOrWhiteSpace(state.Definition.DisplayName)
                ? state.Definition.DisplayName
                : state.Definition.LevelId;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                levelText.text = displayName;
            }
        }

        private void ApplyStars(int starCount)
        {
            displayedStarCount = Mathf.Clamp(starCount, 0, 3);
            for (int i = 0; i < 3; i++)
            {
                SetStarUnlocked(i, i < displayedStarCount);
            }
        }

        private void SetStarUnlocked(int index, bool unlocked)
        {
            Image starImage = GetArrayItem(starImages, index);
            if (starImage != null)
            {
                Sprite stateSprite = unlocked ? reachedStarSprite ?? starBaseSprites[index] : unreachedStarSprite;
                if (stateSprite != null)
                {
                    starImage.sprite = stateSprite;
                }

                starImage.enabled = unlocked || unreachedStarSprite != null;
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

        private void ResolveReferences()
        {
            if (popup == null)
            {
                popup = FindDeep<RectTransform>(transform, PopupName);
            }

            if (retryButton == null)
            {
                retryButton = FindDeep<Button>(transform, RetryButtonName);
            }

            if (retryButtonCosmeticTarget == null && retryButton != null)
            {
                retryButtonCosmeticTarget = retryButton.transform as RectTransform;
            }

            if (exitButton == null)
            {
                exitButton = FindDeep<Button>(transform, ExitButtonName);
            }

            if (exitButtonCosmeticTarget == null && exitButton != null)
            {
                exitButtonCosmeticTarget = exitButton.transform as RectTransform;
            }

            if (scoreText == null)
            {
                scoreText = FindDeep<TextMeshProUGUI>(transform, ScoreTextName);
            }

            if (levelText == null)
            {
                levelText = FindDeep<TextMeshProUGUI>(transform, LevelTextName);
            }

            if (starProgressSlider == null)
            {
                starProgressSlider = FindDeep<Slider>(transform, StarProgressSliderName);
            }

            if (joyIconTransform == null)
            {
                joyIconTransform = FindDeep<RectTransform>(transform, JoyIconName);
            }

            for (int i = 0; i < StarRootNames.Length; i++)
            {
                if (starTransforms[i] == null)
                {
                    starTransforms[i] = FindDeep<RectTransform>(transform, StarRootNames[i]);
                }

                if (starImages[i] == null && starTransforms[i] != null)
                {
                    starImages[i] = starTransforms[i].GetComponentInChildren<Image>(true);
                }
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

        private void CacheBaseTransforms()
        {
            if (popup != null)
            {
                popupBaseScale = popup.localScale == Vector3.zero ? Vector3.one : popup.localScale;
                popupBaseRotation = popup.localRotation;
            }

            RectTransform scoreTransform = scoreText != null ? scoreText.rectTransform : null;
            if (scoreTransform != null)
            {
                scoreBaseScale = scoreTransform.localScale == Vector3.zero ? Vector3.one : scoreTransform.localScale;
                scoreBaseRotation = scoreTransform.localRotation;
            }

            for (int i = 0; i < 3; i++)
            {
                RectTransform starTransform = GetArrayItem(starTransforms, i);
                if (starTransform == null)
                {
                    continue;
                }

                starBaseScales[i] = starTransform.localScale;
                starBaseRotations[i] = starTransform.localRotation;

                Image starImage = GetArrayItem(starImages, i);
                if (starImage != null)
                {
                    starBaseSprites[i] = starImage.sprite;
                }
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (retryButton != null)
            {
                retryButton.interactable = interactable;
            }

            if (exitButton != null)
            {
                exitButton.interactable = interactable;
            }
        }

        private void StopMotionCoroutines()
        {
            if (showCoroutine != null)
            {
                StopCoroutine(showCoroutine);
                showCoroutine = null;
            }

            if (scoreLoopCoroutine != null)
            {
                StopCoroutine(scoreLoopCoroutine);
                scoreLoopCoroutine = null;
            }

            for (int i = 0; i < starPopCoroutines.Length; i++)
            {
                if (starPopCoroutines[i] != null)
                {
                    StopCoroutine(starPopCoroutines[i]);
                    starPopCoroutines[i] = null;
                }
            }
        }

        private void ResetMotionTransforms()
        {
            if (popup != null)
            {
                popup.localScale = popupBaseScale;
                popup.localRotation = popupBaseRotation;
            }

            RectTransform scoreTransform = scoreText != null ? scoreText.rectTransform : null;
            if (scoreTransform != null)
            {
                scoreTransform.localScale = scoreBaseScale;
                scoreTransform.localRotation = scoreBaseRotation;
            }

            for (int i = 0; i < 3; i++)
            {
                RectTransform starTransform = GetArrayItem(starTransforms, i);
                if (starTransform == null)
                {
                    continue;
                }

                Vector3 baseScale = starBaseScales[i] == Vector3.zero ? starTransform.localScale : starBaseScales[i];
                starTransform.localScale = baseScale;
                starTransform.localRotation = starBaseRotations[i];
            }
        }

        private void OnRetryClicked()
        {
            RetryRequested?.Invoke();
        }

        private void OnExitClicked()
        {
            ExitRequested?.Invoke();
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        private static T GetArrayItem<T>(T[] items, int index)
            where T : Object
        {
            return items != null && index >= 0 && index < items.Length ? items[index] : null;
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
