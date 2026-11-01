using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Oyun genelinde ses kontrolü: Tüm Sesler (Ana Ses / Hoparlör) ve Arkaplan Müziği (Nota).
// Tercihler PlayerPrefs'te saklanır; sahne yeniden yüklendiğinde ("Yeni Oyun") korunur.
public static class GameAudio
{
    private const string MasterMuteKey = "WasteSurvivors_MasterMuted";
    private const string MusicMuteKey = "WasteSurvivors_MusicMuted";

    private static AudioSource musicSource;
    private static bool initialized;

    public static bool IsMasterMuted { get; private set; }
    public static bool IsMusicMuted { get; private set; }

    public static event Action StateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        musicSource = null;
        initialized = false;
        StateChanged = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        IsMasterMuted = PlayerPrefs.GetInt(MasterMuteKey, 0) == 1;
        IsMusicMuted = PlayerPrefs.GetInt(MusicMuteKey, 0) == 1;
        ApplyAll();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInitialized();
        ApplyAll();
    }

    public static void ToggleMasterMute()
    {
        EnsureInitialized();
        IsMasterMuted = !IsMasterMuted;
        PlayerPrefs.SetInt(MasterMuteKey, IsMasterMuted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyAll();
        StateChanged?.Invoke();
    }

    public static void ToggleMusicMute()
    {
        EnsureInitialized();
        IsMusicMuted = !IsMusicMuted;
        PlayerPrefs.SetInt(MusicMuteKey, IsMusicMuted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyAll();
        StateChanged?.Invoke();
    }

    // Sahneye kodla arkaplan müziği başlatmak istenirse kullanılır
    public static void SetBackgroundMusic(AudioClip clip, float volume = 0.35f)
    {
        EnsureInitialized();
        if (clip == null) return;

        if (musicSource == null)
        {
            var go = new GameObject("MusicPlayer");
            UnityEngine.Object.DontDestroyOnLoad(go);
            musicSource = go.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
        }

        musicSource.volume = Mathf.Clamp01(volume);
        musicSource.mute = IsMusicMuted;
        if (musicSource.clip != clip)
        {
            musicSource.clip = clip;
            musicSource.Play();
        }
        else if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    public static void ApplyAll()
    {
        AudioListener.volume = IsMasterMuted ? 0f : 1f;

        if (musicSource != null)
            musicSource.mute = IsMusicMuted;

        // Sahnede el ile eklenmiş arkaplan müziği AudioSource'ları varsa (SfxPlayer dışındakiler) onları da sessize al/aç
        var allSources = UnityEngine.Object.FindObjectsOfType<AudioSource>(true);
        for (int i = 0; i < allSources.Length; i++)
        {
            var src = allSources[i];
            if (src == null || SfxPlayer.IsSfxSource(src)) continue;
            src.mute = IsMusicMuted;
        }
    }
}

