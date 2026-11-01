using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bir oyunun (run) akışı: süre, öldürme sayısı, 10. dakikadaki final ve oyuncu ölünce oyun sonu.
// Sahneye eklemeye gerek yok; sahne yüklenince kendini oluşturur.
//
// Debug tuşları (editörde ve development build'de):
//   F9 = finali hemen tetikle
//   F8 = süreyi 1 dakika ileri sar (zorluk eğrisini denemek için)
//   F7 = evrimi hazırla: evrimi olan ilk silahı maksimum seviyeye çıkarır, gereken pasifi verir ve
//        bir level atlatır; level-up ekranında EVRİM kartı çıkar
//   R  = oyun bittiyse yeniden başlat
public class GameSession : MonoBehaviour
{
    public const float RunDuration = 600f; // 10 dakika

    public static GameSession Instance { get; private set; }

    // Level-up ekranı açıkken oyun durduğu için ölçekli zamanla ilerler
    public float Elapsed { get; private set; }
    public float Remaining => Mathf.Max(0f, RunDuration - Elapsed);
    public int Kills { get; private set; }
    public bool IsFinished { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsOver => IsFinished || IsGameOver;

    // Final başladığında (dünya temizlenirken)
    public event Action Finished;
    // Oyuncu öldüğünde
    public event Action GameOver;

    private static bool DebugKeysEnabled => Application.isEditor || Debug.isDebugBuild;

    // Her sahne yüklenişinde (yeniden başlatma dahil) bir GameSession olsun
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterBootstrap()
    {
        Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindObjectOfType<GameSession>() == null)
            new GameObject("GameSession").AddComponent<GameSession>();
    }

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    void OnEnable() => Enemy.Killed += OnEnemyKilled;

    void OnDisable() => Enemy.Killed -= OnEnemyKilled;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        var health = FindObjectOfType<PlayerHealth>();
        if (health != null)
            health.Died += TriggerGameOver;
    }

    void Update()
    {
        if (!IsOver)
        {
            Elapsed += Time.deltaTime;
            if (Elapsed >= RunDuration)
                TriggerFinale();
        }

        if (DebugKeysEnabled && !IsOver)
        {
            if (Input.GetKeyDown(KeyCode.F9))
                TriggerFinale();
            else if (Input.GetKeyDown(KeyCode.F8))
            {
                Elapsed = Mathf.Min(RunDuration - 1f, Elapsed + 60f);
                Debug.Log($"Süre ileri sarıldı: {FormatTime(Elapsed)}");
            }
            else if (Input.GetKeyDown(KeyCode.F7))
                DebugPrepareEvolution();
        }

        if (IsOver && Input.GetKeyDown(KeyCode.R))
            Restart();
    }

    private static void DebugPrepareEvolution()
    {
        var pool = Resources.Load<UpgradePool>(UpgradePool.ResourcePath);
        var weapons = FindObjectOfType<PlayerWeapons>();
        var passives = FindObjectOfType<PlayerPassives>();
        var experience = FindObjectOfType<PlayerExperience>();
        if (pool == null || weapons == null || experience == null) return;

        foreach (var data in pool.weapons)
        {
            if (data == null || data.evolution == null || weapons.HasEvolved(data)) continue;

            if (weapons.GetWeapon(data) == null && !weapons.HasFreeSlot)
            {
                Debug.LogWarning($"Evrim testi: {data.displayName} için boş silah slotu yok.");
                continue;
            }
            for (int i = 0; i < data.MaxLevel && (weapons.GetWeapon(data) == null || !weapons.GetWeapon(data).IsMaxLevel); i++)
                weapons.AddOrUpgrade(data);

            var requirement = data.evolutionRequirement;
            if (requirement != null && passives != null && passives.GetItem(requirement) == null)
            {
                if (passives.HasFreeSlot)
                    passives.AddOrUpgrade(requirement);
                else
                    Debug.LogWarning($"Evrim testi: {requirement.displayName} için boş item slotu yok.");
            }

            Debug.Log($"Evrim testi: {data.displayName} maksimum seviyede, level atlatılıyor.");
            experience.AddExperience(experience.ExperienceToNextLevel - experience.Experience);
            return;
        }

        Debug.LogWarning("Evrim testi: evrimleşebilecek silah bulunamadı.");
    }

    public void TriggerFinale()
    {
        if (IsOver) return;

        IsFinished = true;
        gameObject.AddComponent<RestorationFinale>();
        Finished?.Invoke();
    }

    private void TriggerGameOver()
    {
        if (IsOver) return;

        IsGameOver = true;
        GameOver?.Invoke();

        var experience = FindObjectOfType<PlayerExperience>();
        string stats = $"Süre {FormatTime(Elapsed)}" +
                       $"     Seviye {(experience != null ? experience.Level : 1)}" +
                       $"     Temizlenen kirlilik {Kills}";
        EndScreen.Show("KİRLİLİK KAZANDI", "Dünya hâlâ temizlenmeyi bekliyor.", new Color(1f, 0.45f, 0.4f), stats);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnEnemyKilled(Enemy enemy) => Kills++;

    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }
}
