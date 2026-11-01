using UnityEngine;

// Oyun müziği: MusicPlaylist'teki şarkıları sırayla çalar, biten şarkıdan sonrakine geçer.
// Sahneler arası yaşar (yeniden başlatmada müzik kesilmez). Durdurma menüsü buradan kontrol eder.
// Finalde çalan şarkı yumuşakça kısılıp outro'ya geçilir.
public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance { get; private set; }

    private const float FadeDuration = 1.5f;
    // Önceki tuşu: şarkının bu saniyesinden sonra basılırsa şarkıyı başa sarar
    private const float RestartThreshold = 3f;

    private AudioSource source;
    private MusicPlaylist playlist;
    private int index = -1;
    private bool playingOutro;
    private bool switchingToOutro;
    private float fade = 1f;
    // Akışla çalınan şarkı başlarken isPlaying bir an false olabilir; o anda "bitti" sanılmasın
    private float startedAt;

    public bool HasTracks => playlist != null && playlist.tracks.Count > 0;
    public bool IsPaused { get; private set; }
    public string CurrentTitle => source != null && source.clip != null ? source.clip.name : "";
    public float CurrentTime => source != null ? source.time : 0f;
    public float CurrentLength => source != null && source.clip != null ? source.clip.length : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (Instance != null) return;

        var musicPlaylist = Resources.Load<MusicPlaylist>(MusicPlaylist.ResourcePath);
        if (musicPlaylist == null || musicPlaylist.tracks.Count == 0) return;

        var go = new GameObject("MusicPlayer");
        DontDestroyOnLoad(go);
        go.AddComponent<MusicPlayer>().Initialize(musicPlaylist);
    }

    private void Initialize(MusicPlaylist musicPlaylist)
    {
        Instance = this;
        playlist = musicPlaylist;

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = playlist.volume;
        GameAudio.EnsureInitialized();
        source.mute = GameAudio.IsMusicMuted;

        PlayIndex(Random.Range(0, playlist.tracks.Count));
    }

    void Update()
    {
        if (source == null) return;

        if (switchingToOutro)
        {
            fade = Mathf.MoveTowards(fade, 0f, Time.unscaledDeltaTime / FadeDuration);
            if (fade <= 0f)
            {
                switchingToOutro = false;
                playingOutro = true;
                fade = 1f;
                source.clip = playlist.outro;
                source.time = 0f;
                source.Play();
                startedAt = Time.unscaledTime;
            }
        }
        source.volume = playlist.volume * fade * GameAudio.MusicVolume;

        // Şarkı bittiyse (kullanıcı durdurmadıysa) sıradakine geç; outro bitince listeye dönülür
        if (!IsPaused && !switchingToOutro && !source.isPlaying && source.clip != null &&
            Time.unscaledTime - startedAt > 0.5f)
            Next();
    }

    public void Next()
    {
        if (!HasTracks) return;
        PlayIndex((index + 1) % playlist.tracks.Count);
    }

    public void Previous()
    {
        if (!HasTracks) return;

        if (source.time > RestartThreshold && !playingOutro)
        {
            source.time = 0f;
            return;
        }
        PlayIndex((index - 1 + playlist.tracks.Count) % playlist.tracks.Count);
    }

    public void TogglePause()
    {
        if (source == null || source.clip == null) return;

        IsPaused = !IsPaused;
        if (IsPaused) source.Pause();
        else source.UnPause();
    }

    // İleri/geri sarma (saniye)
    public void Seek(float seconds)
    {
        if (source == null || source.clip == null) return;
        source.time = Mathf.Clamp(source.time + seconds, 0f, Mathf.Max(0f, source.clip.length - 0.1f));
    }

    // Final: çalan şarkı kısılıp outro başlar
    public void PlayOutro()
    {
        if (playlist.outro == null || playingOutro || switchingToOutro) return;

        if (IsPaused)
        {
            IsPaused = false;
            source.UnPause();
        }
        switchingToOutro = true;
    }

    // Yeni oyun başladı: outro çalıyorsa listeye geri dön
    public void OnRunStarted()
    {
        if (playingOutro || switchingToOutro)
            Next();
    }

    private void PlayIndex(int newIndex)
    {
        index = newIndex;
        playingOutro = false;
        switchingToOutro = false;
        IsPaused = false;
        fade = 1f;

        source.clip = playlist.tracks[index];
        source.time = 0f;
        source.Play();
        startedAt = Time.unscaledTime;
    }
}
