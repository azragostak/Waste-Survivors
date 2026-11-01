using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bir oyunun (run) akışı: süre, öldürme sayısı, 10. dakikadaki final ve oyuncu ölünce oyun sonu.
// Sahneye eklemeye gerek yok; sahne yüklenince kendini oluşturur.
//
// Debug tuşları (editörde ve development build'de):
//   F9 = finali hemen tetikle
//   F8 = süreyi 1 dakika ileri sar (zorluk eğrisini denemek için)
//   F6 = boss'u hemen çağır (süreyi 10:00'a atlar)
//   F7 = evrimi hazırla: elindeki (yoksa havuzdaki) evrimi olan ilk silahı maksimum seviyeye çıkarır,
//        gereken pasifi verir ve bir level atlatır; level-up ekranında EVRİM kartı çıkar
//   F1 = boş dünya (trailer): düşmanlar ve XP kaybolur, yeni düşman gelmez, silahlar gizlenir, süre durur
//   F2 = HUD'u gizle/göster (trailer için temiz görüntü)
//   F3 = ölümsüzlük aç/kapa
//   F4 = level-up ekranı kapalı (trailer): seviye artar ama ekran açılmaz, oyun durmaz; silahları L ile yükselt
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
    // Süre doldu, normal düşmanlar durdu, boss geldi (final boss ölünce başlar)
    public bool IsBossPhase { get; private set; }
    // Trailer için boş dünya (F1): düşman doğmaz, silahlar gizli, süre durur
    public bool IsPeaceful { get; private set; }
    // Trailer için (F4): level-up ekranı açılmaz
    public bool SuppressLevelUps { get; private set; }

    // Final başladığında (dünya temizlenirken)
    public event Action Finished;
    // Süre dolup boss evresi başladığında
    public event Action BossPhaseStarted;
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
        if (FindObjectOfType<PlayerController>() == null) return;
        if (FindObjectOfType<GameSession>() == null)
            new GameObject("GameSession").AddComponent<GameSession>();
    }

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;

        // Önceki oyunun finalinden outro çalıyorsa listeye dön
        if (MusicPlayer.Instance != null)
            MusicPlayer.Instance.OnRunStarted();
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
            if (!IsPeaceful)
                Elapsed += Time.deltaTime;
            if (Elapsed >= RunDuration && !IsBossPhase)
                StartBossPhase();
        }

        if (DebugKeysEnabled && !IsOver && !PauseMenu.IsPaused)
        {
            if (Input.GetKeyDown(KeyCode.F9))
                TriggerFinale();
            else if (Input.GetKeyDown(KeyCode.F8))
            {
                if (!IsBossPhase)
                    Elapsed = Mathf.Min(RunDuration - 1f, Elapsed + 60f);
                Debug.Log($"Süre ileri sarıldı: {FormatTime(Elapsed)}");
            }
            else if (Input.GetKeyDown(KeyCode.F7))
                DebugPrepareEvolution();
            else if (Input.GetKeyDown(KeyCode.F6))
                StartBossPhase();
            else if (Input.GetKeyDown(KeyCode.F1))
                TogglePeaceful();
            else if (Input.GetKeyDown(KeyCode.F2))
            {
                var hud = FindObjectOfType<GameHUD>();
                if (hud != null) hud.ToggleHidden();
            }
            else if (Input.GetKeyDown(KeyCode.F4))
            {
                SuppressLevelUps = !SuppressLevelUps;
                Debug.Log($"Level-up ekranı: {(SuppressLevelUps ? "kapalı" : "açık")}");
            }
            else if (Input.GetKeyDown(KeyCode.F3))
            {
                var health = FindObjectOfType<PlayerHealth>();
                if (health != null)
                {
                    health.IsInvulnerable = !health.IsInvulnerable;
                    Debug.Log($"Ölümsüzlük: {(health.IsInvulnerable ? "açık" : "kapalı")}");
                }
            }
        }

        if (IsOver && Input.GetKeyDown(KeyCode.R))
            Restart();
    }

    private void TogglePeaceful()
    {
        IsPeaceful = !IsPeaceful;

        if (IsPeaceful)
        {
            foreach (var enemy in new System.Collections.Generic.List<Enemy>(Enemy.Active))
                enemy.Vanish();
            for (int i = RecyclePickup.Active.Count - 1; i >= 0; i--)
            {
                if (RecyclePickup.Active[i] != null)
                    Destroy(RecyclePickup.Active[i].gameObject);
            }
        }

        var weapons = FindObjectOfType<PlayerWeapons>();
        if (weapons != null)
            weapons.SetWeaponsActive(!IsPeaceful);

        Debug.Log($"Boş dünya: {(IsPeaceful ? "açık (düşman yok, silahlar gizli, süre durdu)" : "kapalı")}");
    }

    private void DebugPrepareEvolution()
    {
        var pool = Resources.Load<UpgradePool>(UpgradePool.ResourcePath);
        var weapons = FindObjectOfType<PlayerWeapons>();
        var passives = FindObjectOfType<PlayerPassives>();
        var experience = FindObjectOfType<PlayerExperience>();
        if (pool == null || weapons == null || experience == null) return;

        // Önce elde olan silahlar (sıradaki F7 bir sonrakini dener), yoksa havuzdakiler
        var candidates = new System.Collections.Generic.List<WeaponData>();
        foreach (var weapon in weapons.Weapons)
            candidates.Add(weapon.Data);
        foreach (var data in pool.weapons)
        {
            if (!candidates.Contains(data))
                candidates.Add(data);
        }

        foreach (var data in candidates)
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
            // F4 açık olsa da evrim kartı gelsin
            bool suppressed = SuppressLevelUps;
            SuppressLevelUps = false;
            experience.AddExperience(experience.ExperienceToNextLevel - experience.Experience);
            SuppressLevelUps = suppressed;
            return;
        }

        Debug.LogWarning("Evrim testi: evrimleşebilecek silah bulunamadı.");
    }

    public void StartBossPhase()
    {
        if (IsBossPhase || IsOver) return;

        IsBossPhase = true;
        Elapsed = Mathf.Max(Elapsed, RunDuration);
        BossPhaseStarted?.Invoke();
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
        Time.timeScale = 0f;

        var experience = FindObjectOfType<PlayerExperience>();
        int level = experience != null ? experience.Level : 1;
        string stats = GameLanguage.IsTurkish
            ? $"Süre {FormatTime(Elapsed)}     Seviye {level}     Temizlenen kirlilik {Kills}"
            : $"Time {FormatTime(Elapsed)}     Level {level}     Enemies defeated {Kills}";
        EndScreen.Show(
            GameLanguage.Tr("POLLUTION WINS", "KİRLİLİK KAZANDI"),
            GameLanguage.Tr("The world is still waiting to be cleaned.", "Dünya hâlâ temizlenmeyi bekliyor."),
            new Color(1f, 0.45f, 0.4f), stats);
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
