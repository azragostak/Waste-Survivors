using UnityEngine;

// Tüm silahların temel sınıfı. Cooldown, seviye ve stat okuma burada;
// alt sınıflar sadece "saldırınca ne olur"u (TryAttack) yazar.
public abstract class Weapon : MonoBehaviour
{
    public WeaponData Data { get; private set; }
    public int Level { get; private set; } = 1;
    public bool IsMaxLevel => Level >= Data.MaxLevel;

    protected Transform Owner { get; private set; }

    // Stat okumanın tek noktası: ileride pasif bonuslar (hasar %, alan % vs.) burada uygulanır.
    protected WeaponStats Stats => Data.GetStats(Level);

    private float cooldownTimer;

    public void Initialize(WeaponData data, Transform owner)
    {
        Data = data;
        Owner = owner;
        Level = 1;
        OnInitialized();
    }

    public void LevelUp()
    {
        if (IsMaxLevel) return;
        Level++;
        OnLevelUp();
    }

    protected virtual void Update()
    {
        if (Data == null) return;

        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer > 0f) return;

        // Hedef yoksa saldırı olmaz, sonraki frame tekrar denenir
        if (TryAttack())
            cooldownTimer = Stats.cooldown;
    }

    // Saldırı gerçekleştiyse true döndür (cooldown başlar)
    protected abstract bool TryAttack();

    protected virtual void OnInitialized() { }
    protected virtual void OnLevelUp() { }
}
