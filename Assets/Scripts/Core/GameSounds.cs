using UnityEngine;

// Sahnedeki objelere bağlı olmayan genel efekt sesleri (oyuncu hasar sesi vb.).
// Assets/Resources/GameSounds.asset otomatik bulunur; sahneye bir şey bağlamak gerekmez.
[CreateAssetMenu(fileName = "GameSounds", menuName = "Waste Survivors/Game Sounds")]
public class GameSounds : ScriptableObject
{
    public const string ResourcePath = "GameSounds";

    private static GameSounds instance;
    private static bool loaded;

    [Header("Oyuncu")]
    [Tooltip("Oyuncu hasar aldığında")]
    public AudioClip playerHurt;
    [Range(0f, 1f)] public float playerHurtVolume = 0.5f;

    public static GameSounds Instance
    {
        get
        {
            if (!loaded || instance == null)
            {
                loaded = true;
                instance = Resources.Load<GameSounds>(ResourcePath);
            }
            return instance;
        }
    }
}
