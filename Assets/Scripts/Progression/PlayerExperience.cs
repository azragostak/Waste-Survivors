using System;
using UnityEngine;

// Oyuncunun XP'si ve seviyesi. Player objesine eklenir.
// Pickup Radius içindeki recycle'lar oyuncuya çekilip toplanır, yeterli XP birikince level atlanır.
public class PlayerExperience : MonoBehaviour
{
    [Header("Toplama")]
    [Tooltip("Bu yarıçap içindeki recycle'lar oyuncuya çekilir (Scene view'da yeşil daire)")]
    [SerializeField, Min(0f)] private float pickupRadius = 1.5f;

    [Header("Seviye")]
    [Tooltip("Level 1 -> 2 için gereken XP")]
    [SerializeField, Min(1)] private int baseExperience = 5;
    [Tooltip("Her seviyede gereken XP bu kadar artar")]
    [SerializeField, Min(0)] private int experienceGrowthPerLevel = 9;

    [Header("Test")]
    [SerializeField] private bool logLevelUps = true;

    public int Level { get; private set; } = 1;
    // Mevcut seviyede biriken XP (level atlayınca artan kısım devreder)
    public int Experience { get; private set; }
    public int ExperienceToNextLevel => baseExperience + experienceGrowthPerLevel * (Level - 1);
    // XP barı için 0-1 arası
    public float Progress => (float)Experience / ExperienceToNextLevel;

    // Mıknatıs tarzı pasif itemler için
    public float PickupRadius
    {
        get => pickupRadius;
        set => pickupRadius = Mathf.Max(0f, value);
    }

    public event Action<PlayerExperience> ExperienceChanged;
    // Parametre: yeni seviye. Level-up seçim ekranı buna bağlanacak.
    public event Action<int> LeveledUp;

    void Update()
    {
        float radius = pickupRadius * (1f + PlayerPassives.GetBonus(this, PassiveStat.PickupRadius));
        float radiusSqr = radius * radius;
        Vector2 position = transform.position;

        foreach (var pickup in RecyclePickup.Active)
        {
            if (!pickup.IsAttracted && ((Vector2)pickup.transform.position - position).sqrMagnitude <= radiusSqr)
                pickup.Attract(this);
        }
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0) return;

        Experience += amount;

        // Büyük bir toplamada birden fazla seviye atlanabilir
        while (Experience >= ExperienceToNextLevel)
        {
            Experience -= ExperienceToNextLevel;
            Level++;

            if (logLevelUps)
                Debug.Log($"Level {Level}! Sonraki seviye için {ExperienceToNextLevel} XP gerekiyor.");

            LeveledUp?.Invoke(Level);
        }

        ExperienceChanged?.Invoke(this);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
