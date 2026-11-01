using System.Collections.Generic;
using UnityEngine;

// Kısa efekt sesleri için ortak çalar. Sahneye eklemeye gerek yok, ilk kullanımda kendini kurar.
// Bullet heaven'da aynı ses saniyede onlarca kez tetiklenebilir; bu yüzden aynı ses
// minInterval'dan sık çalınmaz ve aynı anda en fazla maxVoices kopyası duyulur.
public static class SfxPlayer
{
    private const int VoiceCount = 24;

    private static AudioSource[] voices;
    private static int nextSteal;
    private static readonly Dictionary<AudioClip, float> lastPlayed = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        voices = null;
        nextSteal = 0;
        lastPlayed.Clear();
    }

    public static void Play(AudioClip clip, float volume = 1f, float pitchVariation = 0.08f,
        float minInterval = 0.05f, int maxVoices = 4, float pitch = 1f)
    {
        if (clip == null || volume <= 0f) return;
        EnsureVoices();

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(clip, out float last) && now - last < minInterval) return;

        int playingSame = 0;
        AudioSource free = null;
        foreach (var voice in voices)
        {
            if (voice.isPlaying)
            {
                if (voice.clip == clip) playingSame++;
            }
            else if (free == null)
            {
                free = voice;
            }
        }
        if (playingSame >= maxVoices) return;

        // Hepsi doluysa sırayla en eski kanalı devral
        if (free == null)
        {
            free = voices[nextSteal];
            nextSteal = (nextSteal + 1) % VoiceCount;
        }

        lastPlayed[clip] = now;
        free.clip = clip;
        free.volume = volume;
        // Aynı sesin tekrarı daha az yapay duyulsun
        free.pitch = pitch + Random.Range(-pitchVariation, pitchVariation);
        free.Play();
    }

    private static void EnsureVoices()
    {
        if (voices != null && voices[0] != null) return;

        var go = new GameObject("SfxPlayer");
        Object.DontDestroyOnLoad(go);
        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D oyun: konumdan bağımsız
            voices[i] = source;
        }
    }
}
