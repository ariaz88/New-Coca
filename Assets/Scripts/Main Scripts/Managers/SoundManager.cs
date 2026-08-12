using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour
{
    private const string SoundEnabledKey = "SoundEnabled";
    private const string MusicEnabledKey = "MusicEnabled";
    private const string SodaMoveClipResourcePath = "Audio/SFX/soda_move_beep";
    private const string BoxCompleteClipResourcePath = "Audio/SFX/box_complete_pop";
    private const string BoxSpawnClipResourcePath = "Audio/SFX/box_spawn_pop";

    public static SoundManager instance;

    private bool isMuted;
    private bool isMusicMuted;

    [Header("Audio Sources")]
    public AudioSource soundEffectSource;
    public AudioSource musicSource;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip sodaMoveClip;
    [SerializeField, Range(0f, 1f)] private float sodaMoveVolume = 0.55f;
    [SerializeField] private AudioClip boxCompleteClip;
    [SerializeField, Range(0f, 1f)] private float boxCompleteVolume = 0.75f;
    [SerializeField] private AudioClip boxSpawnClip;
    [SerializeField, Range(0f, 1f)] private float boxSpawnVolume = 0.5f;

    public bool IsSoundEnabled => !isMuted;
    public bool IsMusicEnabled => !isMusicMuted;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        LoadSavedSettings();
        ApplyMuteState();
    }

    public void PlaySound(AudioClip audioClip, float volume = 1f)
    {
        if (!isMuted && soundEffectSource != null && audioClip != null)
        {
            soundEffectSource.PlayOneShot(audioClip, volume);
        }
    }

    public void PlaySodaMove()
    {
        EnsureSodaMoveClip();
        PlaySound(sodaMoveClip, sodaMoveVolume);
    }

    public void PlayBoxComplete()
    {
        if (boxCompleteClip == null)
        {
            boxCompleteClip = Resources.Load<AudioClip>(BoxCompleteClipResourcePath);
        }

        PlaySound(boxCompleteClip, boxCompleteVolume);
    }

    public void PlayBoxSpawn()
    {
        if (boxSpawnClip == null)
        {
            boxSpawnClip = Resources.Load<AudioClip>(BoxSpawnClipResourcePath);
        }

        PlaySound(boxSpawnClip, boxSpawnVolume);
    }

    /// <summary>Plays main music only in real gameplay levels (Level1 and later).</summary>
    public void PlayLevelMusic()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        const string levelPrefix = "Level";
        if (!sceneName.StartsWith(levelPrefix, StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(sceneName.Substring(levelPrefix.Length), out int levelNumber) ||
            levelNumber < 1)
        {
            return;
        }

        PlayMusic();
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    public void PlayMusic(bool loop = true)
    {
        if (musicSource == null)
        {
            return;
        }

        musicSource.loop = loop;
        if (!isMusicMuted && !musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    public void ToggleSoundMute(bool toggleSound)
    {
        isMuted = !toggleSound;
        PlayerPrefs.SetInt(SoundEnabledKey, toggleSound ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMuteState();
        Debug.Log($"Sound is now {(isMuted ? "Disabled " : "Enabled")}");
    }

    public void ToggleMusicMute(bool toggleMusic)
    {
        isMusicMuted = !toggleMusic;
        PlayerPrefs.SetInt(MusicEnabledKey, toggleMusic ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMuteState();
        Debug.Log($"Music is now {(isMusicMuted ? "Disabled " : "Enabled")}");
        PlayLevelMusic();
    }

    private void LoadSavedSettings()
    {
        isMuted = PlayerPrefs.GetInt(SoundEnabledKey, 1) == 0;
        isMusicMuted = PlayerPrefs.GetInt(MusicEnabledKey, 1) == 0;
    }

    private void ApplyMuteState()
    {
        if (soundEffectSource != null)
        {
            soundEffectSource.mute = isMuted;
        }

        if (musicSource != null)
        {
            musicSource.mute = isMusicMuted;
        }
    }

    private void EnsureSodaMoveClip()
    {
        if (sodaMoveClip == null)
        {
            sodaMoveClip = Resources.Load<AudioClip>(SodaMoveClipResourcePath);
        }
    }
}
