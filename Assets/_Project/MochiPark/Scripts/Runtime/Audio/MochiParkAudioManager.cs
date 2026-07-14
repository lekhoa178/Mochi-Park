using System.Collections;
using MochiPark.Runtime.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MochiPark.Runtime.Audio
{
    public sealed class MochiParkAudioManager : MonoBehaviour
    {
        private const string AudioLibraryResourcePath = "MochiPark/Audio/mochi_park_audio_library";
        private const string LobbySceneName = "Lobby";
        public const string UiButtonClickSfxCueId = "ui_button_click";
        private const string GameResultSfxCueId = "game_result";
        private const float ResultMusicVolumeScale = 0.35f;

        private static MochiParkAudioManager instance;

        [SerializeField] private AudioLibrarySO library;

        private AudioSource musicSource;
        private AudioSource frenzyMusicSource;
        private AudioSource sfxSource;
        private EventBus eventBus;
        private int lastGameplayMusicIndex = -1;
        private bool gameplayMusicRequested;
        private bool frenzyActive;
        private bool musicPausedForFrenzy;
        private bool applicationPaused;
        private bool applicationUnfocused;
        private bool resumeMusicAfterSuspension;
        private bool resumeFrenzyMusicAfterSuspension;
        private Coroutine musicTransitionCoroutine;
        private Coroutine resultSfxCoroutine;

        public static MochiParkAudioManager Instance => instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeBootstrap()
        {
            EnsureInstance();
        }

        public static MochiParkAudioManager EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            MochiParkAudioManager existing = FindFirstObjectByType<MochiParkAudioManager>();
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            GameObject audioObject = new GameObject("Mochi Park Audio Manager");
            instance = audioObject.AddComponent<MochiParkAudioManager>();
            DontDestroyOnLoad(audioObject);
            return instance;
        }

        public static void BindEventBus(EventBus boundEventBus)
        {
            EnsureInstance().SetEventBus(boundEventBus);
        }

        public static void PlaySfx(string cueId, float volumeScale = 1f)
        {
            EnsureInstance().PlaySfxInternal(cueId, volumeScale);
        }

        public static void PlayUiButtonClickSfx(float volumeScale = 1f)
        {
            PlaySfx(UiButtonClickSfxCueId, volumeScale);
        }

        public static void PlayLobbyMusic()
        {
            EnsureInstance().PlayLobbyMusicInternal();
        }

        public static void PlayGameplayMusic()
        {
            EnsureInstance().PlayGameplayMusicInternal();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadLibraryIfNeeded();
            CreateSources();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            PlayMusicForScene(SceneManager.GetActiveScene());
        }

        private void Update()
        {
            if (!gameplayMusicRequested ||
                frenzyActive ||
                IsApplicationSuspended ||
                musicTransitionCoroutine != null ||
                resultSfxCoroutine != null ||
                musicSource == null ||
                musicSource.isPlaying)
            {
                return;
            }

            PlayNextGameplayMusicClip();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            applicationPaused = pauseStatus;
            HandleApplicationSuspensionChanged();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            applicationUnfocused = !hasFocus;
            HandleApplicationSuspensionChanged();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SetEventBus(null);
            if (instance == this)
            {
                instance = null;
            }
        }

        private bool IsApplicationSuspended => applicationPaused || applicationUnfocused;

        private void HandleApplicationSuspensionChanged()
        {
            if (IsApplicationSuspended)
            {
                PauseMusicForApplicationSuspension();
                return;
            }

            ResumeMusicAfterApplicationSuspension();
        }

        private void PauseMusicForApplicationSuspension()
        {
            if (musicSource != null && musicSource.isPlaying)
            {
                resumeMusicAfterSuspension = true;
                musicSource.Pause();
            }

            if (frenzyMusicSource != null && frenzyMusicSource.isPlaying)
            {
                resumeFrenzyMusicAfterSuspension = true;
                frenzyMusicSource.Pause();
            }
        }

        private void ResumeMusicAfterApplicationSuspension()
        {
            if (resumeMusicAfterSuspension && musicSource != null && musicSource.clip != null)
            {
                musicSource.UnPause();
            }

            if (resumeFrenzyMusicAfterSuspension && frenzyMusicSource != null && frenzyMusicSource.clip != null)
            {
                frenzyMusicSource.UnPause();
            }

            resumeMusicAfterSuspension = false;
            resumeFrenzyMusicAfterSuspension = false;
        }

        private void SetEventBus(EventBus boundEventBus)
        {
            if (eventBus == boundEventBus)
            {
                return;
            }

            if (eventBus != null)
            {
                eventBus.LevelStarted -= OnLevelStarted;
                eventBus.FrenzyStarted -= OnFrenzyStarted;
                eventBus.FrenzyEnded -= OnFrenzyEnded;
            }

            eventBus = boundEventBus;
            if (eventBus == null)
            {
                return;
            }

            eventBus.LevelStarted += OnLevelStarted;
            eventBus.FrenzyStarted += OnFrenzyStarted;
            eventBus.FrenzyEnded += OnFrenzyEnded;
        }

        private void OnLevelStarted(LevelStartedEvent evt)
        {
            PlayGameplayMusicInternal();
        }

        private void OnFrenzyStarted(FrenzyStartedEvent evt)
        {
            LoadLibraryIfNeeded();
            CreateSources();
            frenzyActive = true;
            PlayFrenzyStartSfx();

            if (library == null || library.FrenzyRunningMusic == null || frenzyMusicSource == null)
            {
                return;
            }

            StartMusicTransition(FadeIntoFrenzy());
        }

        private void OnFrenzyEnded(FrenzyEndedEvent evt)
        {
            LoadLibraryIfNeeded();
            CreateSources();
            frenzyActive = false;

            if (!gameplayMusicRequested || musicSource == null || library == null)
            {
                StartMusicTransition(FadeOutFrenzyOnly());
                return;
            }

            StartMusicTransition(FadeOutFrenzyAndResumeMusic());
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            PlayMusicForScene(scene);
        }

        private void PlayMusicForScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            if (scene.name == LobbySceneName)
            {
                SetEventBus(null);
                PlayLobbyMusicInternal();
            }
        }

        private void PlayLobbyMusicInternal()
        {
            LoadLibraryIfNeeded();
            CreateSources();
            StopMusicTransition();
            StopResultSfxRoutine(true);
            gameplayMusicRequested = false;
            frenzyActive = false;
            musicPausedForFrenzy = false;

            if (frenzyMusicSource != null)
            {
                frenzyMusicSource.Stop();
                frenzyMusicSource.volume = 0f;
            }

            if (library == null || library.LobbyMusic == null || musicSource == null)
            {
                return;
            }

            if (musicSource.clip == library.LobbyMusic && musicSource.isPlaying)
            {
                return;
            }

            musicSource.Stop();
            musicSource.clip = library.LobbyMusic;
            musicSource.volume = library.MusicVolume;
            musicSource.loop = true;
            musicSource.Play();
        }

        private void PlayGameplayMusicInternal()
        {
            LoadLibraryIfNeeded();
            CreateSources();
            StopResultSfxRoutine(true);
            gameplayMusicRequested = true;

            if (frenzyActive || musicSource == null)
            {
                return;
            }

            if (library == null)
            {
                return;
            }

            if (musicSource.isPlaying && musicSource.clip != library.LobbyMusic)
            {
                musicSource.volume = library.MusicVolume;
                return;
            }

            PlayNextGameplayMusicClip();
        }

        private void PlayNextGameplayMusicClip()
        {
            if (library == null || musicSource == null)
            {
                return;
            }

            AudioClip nextClip = SelectNextGameplayMusicClip();
            if (nextClip == null)
            {
                return;
            }

            musicSource.Stop();
            musicSource.clip = nextClip;
            musicSource.volume = frenzyActive ? 0f : library.MusicVolume;
            musicSource.loop = false;
            musicSource.Play();
        }

        private void StartMusicTransition(IEnumerator transition)
        {
            StopMusicTransition();
            musicTransitionCoroutine = StartCoroutine(transition);
        }

        private void StopMusicTransition()
        {
            if (musicTransitionCoroutine == null)
            {
                return;
            }

            StopCoroutine(musicTransitionCoroutine);
            musicTransitionCoroutine = null;
        }

        private IEnumerator FadeIntoFrenzy()
        {
            float duration = library != null ? library.FrenzyFadeInSeconds : 0f;
            float targetFrenzyVolume = library != null ? library.FrenzyMusicVolume : 1f;
            float musicStartVolume = musicSource != null ? musicSource.volume : 0f;

            musicPausedForFrenzy = musicSource != null && musicSource.isPlaying;

            if (frenzyMusicSource != null)
            {
                frenzyMusicSource.Stop();
                frenzyMusicSource.clip = library.FrenzyRunningMusic;
                frenzyMusicSource.volume = 0f;
                frenzyMusicSource.loop = true;
                frenzyMusicSource.Play();
            }

            yield return FadeSources(
                duration,
                musicSource,
                musicStartVolume,
                0f,
                frenzyMusicSource,
                frenzyMusicSource != null ? frenzyMusicSource.volume : 0f,
                targetFrenzyVolume);

            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Pause();
                musicSource.volume = 0f;
            }

            if (frenzyMusicSource != null)
            {
                frenzyMusicSource.volume = targetFrenzyVolume;
            }

            musicTransitionCoroutine = null;
        }

        private IEnumerator FadeOutFrenzyAndResumeMusic()
        {
            float duration = library != null ? library.FrenzyFadeOutSeconds : 0f;
            float targetMusicVolume = library != null ? library.MusicVolume : 1f;
            float frenzyStartVolume = frenzyMusicSource != null ? frenzyMusicSource.volume : 0f;

            if (musicSource != null && musicSource.clip != null)
            {
                if (musicPausedForFrenzy)
                {
                    musicSource.UnPause();
                }
                else if (!musicSource.isPlaying)
                {
                    musicSource.Play();
                }

                musicSource.volume = Mathf.Min(musicSource.volume, targetMusicVolume);
            }
            else if (musicSource != null)
            {
                PlayNextGameplayMusicClip();
            }

            yield return FadeSources(
                duration,
                musicSource,
                musicSource != null ? musicSource.volume : 0f,
                targetMusicVolume,
                frenzyMusicSource,
                frenzyStartVolume,
                0f);

            if (frenzyMusicSource != null)
            {
                frenzyMusicSource.Stop();
                frenzyMusicSource.volume = 0f;
            }

            if (musicSource != null && !musicSource.isPlaying)
            {
                PlayNextGameplayMusicClip();
            }

            musicPausedForFrenzy = false;
            musicTransitionCoroutine = null;
        }

        private IEnumerator FadeOutFrenzyOnly()
        {
            float duration = library != null ? library.FrenzyFadeOutSeconds : 0f;
            yield return FadeSources(
                duration,
                null,
                0f,
                0f,
                frenzyMusicSource,
                frenzyMusicSource != null ? frenzyMusicSource.volume : 0f,
                0f);

            if (frenzyMusicSource != null)
            {
                frenzyMusicSource.Stop();
                frenzyMusicSource.volume = 0f;
            }

            musicPausedForFrenzy = false;
            musicTransitionCoroutine = null;
        }

        private static IEnumerator FadeSources(
            float duration,
            AudioSource firstSource,
            float firstStartVolume,
            float firstEndVolume,
            AudioSource secondSource,
            float secondStartVolume,
            float secondEndVolume)
        {
            if (duration <= 0f)
            {
                SetVolume(firstSource, firstEndVolume);
                SetVolume(secondSource, secondEndVolume);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetVolume(firstSource, Mathf.Lerp(firstStartVolume, firstEndVolume, t));
                SetVolume(secondSource, Mathf.Lerp(secondStartVolume, secondEndVolume, t));
                yield return null;
            }

            SetVolume(firstSource, firstEndVolume);
            SetVolume(secondSource, secondEndVolume);
        }

        private static void SetVolume(AudioSource source, float volume)
        {
            if (source != null)
            {
                source.volume = Mathf.Clamp01(volume);
            }
        }

        private AudioClip SelectNextGameplayMusicClip()
        {
            if (library == null || library.GameplayMusic == null || library.GameplayMusic.Count == 0)
            {
                return null;
            }

            int playableCount = 0;
            for (int i = 0; i < library.GameplayMusic.Count; i++)
            {
                if (library.GameplayMusic[i] != null)
                {
                    playableCount++;
                }
            }

            if (playableCount == 0)
            {
                return null;
            }

            int selectedIndex = -1;
            if (playableCount == 1)
            {
                for (int i = 0; i < library.GameplayMusic.Count; i++)
                {
                    if (library.GameplayMusic[i] != null)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                do
                {
                    selectedIndex = Random.Range(0, library.GameplayMusic.Count);
                }
                while (selectedIndex == lastGameplayMusicIndex || library.GameplayMusic[selectedIndex] == null);
            }

            lastGameplayMusicIndex = selectedIndex;
            return selectedIndex >= 0 ? library.GameplayMusic[selectedIndex] : null;
        }

        private void PlayFrenzyStartSfx()
        {
            if (library == null || library.FrenzyStartSfx == null || sfxSource == null)
            {
                return;
            }

            sfxSource.PlayOneShot(library.FrenzyStartSfx, library.SfxVolume);
        }

        private void PlaySfxInternal(string cueId, float volumeScale)
        {
            LoadLibraryIfNeeded();
            CreateSources();
            if (library == null || sfxSource == null || !library.TryGetSfxCue(cueId, out AudioCue cue))
            {
                return;
            }

            float volume = library.SfxVolume * cue.Volume * Mathf.Clamp01(volumeScale);
            if (cue.Id == GameResultSfxCueId)
            {
                PlayResultSfx(cue, volume);
                return;
            }

            sfxSource.PlayOneShot(cue.Clip, volume);
        }

        private void PlayResultSfx(AudioCue cue, float volume)
        {
            StopResultSfxRoutine(true);
            resultSfxCoroutine = StartCoroutine(PlayResultSfxRoutine(cue, volume));
        }

        private IEnumerator PlayResultSfxRoutine(AudioCue cue, float volume)
        {
            bool shouldResumeMusic = musicSource != null && musicSource.clip != null && musicSource.isPlaying;

            StopMusicTransition();

            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Pause();
            }

            sfxSource.PlayOneShot(cue.Clip, volume);

            if (cue.Clip != null)
            {
                yield return new WaitForSecondsRealtime(cue.Clip.length);
            }

            if (shouldResumeMusic && musicSource != null && musicSource.clip != null)
            {
                musicSource.volume = GetResultMusicVolume();
                musicSource.UnPause();
            }

            resultSfxCoroutine = null;
        }

        private void StopResultSfxRoutine(bool stopPlayback)
        {
            if (resultSfxCoroutine == null)
            {
                return;
            }

            StopCoroutine(resultSfxCoroutine);
            resultSfxCoroutine = null;

            if (stopPlayback && sfxSource != null)
            {
                sfxSource.Stop();
            }
        }

        private float GetResultMusicVolume()
        {
            float baseVolume = library != null ? library.MusicVolume : 1f;
            return Mathf.Clamp01(baseVolume * ResultMusicVolumeScale);
        }

        private void LoadLibraryIfNeeded()
        {
            if (library != null)
            {
                return;
            }

            library = Resources.Load<AudioLibrarySO>(AudioLibraryResourcePath);
            if (library == null)
            {
                Debug.LogWarning($"Missing Mochi Park audio library at Resources/{AudioLibraryResourcePath}.");
            }
        }

        private void CreateSources()
        {
            if (musicSource == null)
            {
                musicSource = CreateSource("Music Source");
            }

            if (frenzyMusicSource == null)
            {
                frenzyMusicSource = CreateSource("Frenzy Music Source");
            }

            if (sfxSource == null)
            {
                sfxSource = CreateSource("SFX Source");
            }
        }

        private AudioSource CreateSource(string sourceName)
        {
            GameObject sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = false;
            return source;
        }
    }
}
