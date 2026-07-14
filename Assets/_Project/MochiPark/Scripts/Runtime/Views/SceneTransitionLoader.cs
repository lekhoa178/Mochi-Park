using System;
using System.Collections;
using TransitionScreenPackage;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MochiPark.Runtime.Views
{
    [DisallowMultipleComponent]
    public sealed class SceneTransitionLoader : MonoBehaviour
    {
        private const float SceneReadyProgress = 0.9f;
        private const string DefaultTransitionSourcePath = "MochiPark/UI/Lobby/Canvas_Lobby";

        public static bool IsLoading { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsLoading = false;
        }

        public static bool TryLoadScene(
            string sceneName,
            GameObject transitionScreenPrefab,
            float minimumLoadingSeconds,
            float transitionEventTimeoutSeconds,
            Action afterSceneLoaded = null,
            Action onComplete = null,
            Action onFailed = null)
        {
            if (IsLoading)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("Cannot load a scene because the scene name is empty.");
                return false;
            }

            IsLoading = true;
            GameObject host = new GameObject(nameof(SceneTransitionLoader));
            DontDestroyOnLoad(host);
            SceneTransitionLoader loader = host.AddComponent<SceneTransitionLoader>();
            loader.StartCoroutine(loader.LoadSceneRoutine(
                sceneName,
                transitionScreenPrefab,
                minimumLoadingSeconds,
                transitionEventTimeoutSeconds,
                afterSceneLoaded,
                onComplete,
                onFailed));
            return true;
        }

        private IEnumerator LoadSceneRoutine(
            string sceneName,
            GameObject transitionScreenPrefab,
            float minimumLoadingSeconds,
            float transitionEventTimeoutSeconds,
            Action afterSceneLoaded,
            Action onComplete,
            Action onFailed)
        {
            TransitionScreenManager transitionScreen = CreateTransitionScreen(transitionScreenPrefab);

            if (transitionScreen != null)
            {
                bool finishedReveal = false;
                transitionScreen.FinishedRevealEvent += OnFinishedReveal;
                transitionScreen.Reveal();

                yield return WaitForTransitionEvent(() => finishedReveal, transitionEventTimeoutSeconds);
                transitionScreen.FinishedRevealEvent -= OnFinishedReveal;

                void OnFinishedReveal()
                {
                    finishedReveal = true;
                }
            }

            float loadStartedAt = Time.unscaledTime;
            AsyncOperation loadOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (loadOperation == null)
            {
                Debug.LogWarning($"Could not start loading scene '{sceneName}'.");
                CleanupTransitionScreen(transitionScreen);
                IsLoading = false;
                onFailed?.Invoke();
                Destroy(gameObject);
                yield break;
            }

            loadOperation.allowSceneActivation = false;
            while (loadOperation.progress < SceneReadyProgress)
            {
                yield return null;
            }

            float elapsed = Time.unscaledTime - loadStartedAt;
            if (elapsed < minimumLoadingSeconds)
            {
                yield return new WaitForSecondsRealtime(minimumLoadingSeconds - elapsed);
            }

            loadOperation.allowSceneActivation = true;
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            afterSceneLoaded?.Invoke();
            yield return null;

            if (transitionScreen != null)
            {
                bool finishedHide = false;
                transitionScreen.FinishedHideEvent += OnFinishedHide;
                transitionScreen.Hide();

                yield return WaitForTransitionEvent(() => finishedHide, transitionEventTimeoutSeconds);
                transitionScreen.FinishedHideEvent -= OnFinishedHide;

                void OnFinishedHide()
                {
                    finishedHide = true;
                }
            }

            CleanupTransitionScreen(transitionScreen);
            IsLoading = false;
            onComplete?.Invoke();
            Destroy(gameObject);
        }

        private TransitionScreenManager CreateTransitionScreen(GameObject transitionScreenPrefab)
        {
            GameObject prefab = transitionScreenPrefab != null
                ? transitionScreenPrefab
                : ResolveDefaultTransitionScreenPrefab();

            if (prefab == null)
            {
                Debug.LogWarning("No transition screen prefab is assigned; loading without transition.");
                return null;
            }

            GameObject instance = Instantiate(prefab);
            DontDestroyOnLoad(instance);

            Canvas canvas = instance.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = short.MaxValue;
            }

            TransitionScreenManager manager = instance.GetComponent<TransitionScreenManager>();
            if (manager == null)
            {
                Debug.LogWarning("Assigned transition prefab does not contain a TransitionScreenManager.");
                Destroy(instance);
            }

            return manager;
        }

        private static GameObject ResolveDefaultTransitionScreenPrefab()
        {
            GameObject lobbyCanvasPrefab = Resources.Load<GameObject>(DefaultTransitionSourcePath);
            if (lobbyCanvasPrefab == null)
            {
                return null;
            }

            LobbyController lobbyController = lobbyCanvasPrefab.GetComponent<LobbyController>();
            return lobbyController != null ? lobbyController.TransitionScreenPrefab : null;
        }

        private static IEnumerator WaitForTransitionEvent(Func<bool> isFinished, float timeoutSeconds)
        {
            float startTime = Time.unscaledTime;
            float safeTimeout = Mathf.Max(0.01f, timeoutSeconds);
            while (!isFinished() && Time.unscaledTime - startTime < safeTimeout)
            {
                yield return null;
            }
        }

        private static void CleanupTransitionScreen(TransitionScreenManager transitionScreen)
        {
            if (transitionScreen != null)
            {
                Destroy(transitionScreen.gameObject);
            }
        }
    }
}
