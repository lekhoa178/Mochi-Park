using System;
using System.Collections.Generic;
using UnityEngine;

namespace MochiPark.Runtime.Audio
{
    [Serializable]
    public sealed class AudioCue
    {
        [SerializeField] private string id;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        public string Id => id;
        public AudioClip Clip => clip;
        public float Volume => Mathf.Clamp01(volume);
    }

    [CreateAssetMenu(
        fileName = "mochi_park_audio_library",
        menuName = "Mochi Park/Audio/Audio Library")]
    public sealed class AudioLibrarySO : ScriptableObject
    {
        [Header("Music")]
        [SerializeField] private AudioClip lobbyMusic;
        [SerializeField] private List<AudioClip> gameplayMusic = new List<AudioClip>();
        [SerializeField] private AudioClip frenzyRunningMusic;

        [Header("SFX")]
        [SerializeField] private AudioClip frenzyStartSfx;
        [SerializeField] private List<AudioCue> sfxCues = new List<AudioCue>();

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float frenzyMusicVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("Transitions")]
        [SerializeField, Min(0f)] private float frenzyFadeInSeconds = 0.75f;
        [SerializeField, Min(0f)] private float frenzyFadeOutSeconds = 0.75f;

        public AudioClip LobbyMusic => lobbyMusic;
        public IReadOnlyList<AudioClip> GameplayMusic => gameplayMusic;
        public AudioClip FrenzyRunningMusic => frenzyRunningMusic;
        public AudioClip FrenzyStartSfx => frenzyStartSfx;
        public float MusicVolume => Mathf.Clamp01(musicVolume);
        public float FrenzyMusicVolume => Mathf.Clamp01(frenzyMusicVolume);
        public float SfxVolume => Mathf.Clamp01(sfxVolume);
        public float FrenzyFadeInSeconds => Mathf.Max(0f, frenzyFadeInSeconds);
        public float FrenzyFadeOutSeconds => Mathf.Max(0f, frenzyFadeOutSeconds);

        public bool TryGetSfxCue(string cueId, out AudioCue cue)
        {
            if (string.IsNullOrWhiteSpace(cueId))
            {
                cue = null;
                return false;
            }

            for (int i = 0; i < sfxCues.Count; i++)
            {
                AudioCue candidate = sfxCues[i];
                if (candidate != null && candidate.Id == cueId && candidate.Clip != null)
                {
                    cue = candidate;
                    return true;
                }
            }

            cue = null;
            return false;
        }
    }
}
