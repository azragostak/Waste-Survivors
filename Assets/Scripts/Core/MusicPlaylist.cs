using System.Collections.Generic;
using UnityEngine;

// Oyun müzikleri. Assets/Resources/MusicPlaylist.asset MusicPlayer tarafından otomatik bulunur.
[CreateAssetMenu(fileName = "MusicPlaylist", menuName = "Waste Survivors/Music Playlist")]
public class MusicPlaylist : ScriptableObject
{
    public const string ResourcePath = "MusicPlaylist";

    [Tooltip("Oyun boyunca sırayla çalan şarkılar (rastgele birinden başlar)")]
    public List<AudioClip> tracks = new();
    [Tooltip("Final (dünya temizlenince) çalan şarkı")]
    public AudioClip outro;
    [Range(0f, 1f)] public float volume = 0.45f;
}
