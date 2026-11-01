using UnityEngine;

// Tüm silahların temel sınıfı. Cooldown, seviye ve stat okuma burada;
// alt sınıflar sadece "saldırınca ne olur"u (TryAttack) yazar.
public abstract class Weapon : MonoBehaviour
{
    public WeaponData Data { get; private set; }
    public int Level { get; private set; } = 1;
    public bool IsMaxLevel => Level >= Data.MaxLevel;

    protected Transform Owner { get; private set; }

    // Stat okumanın tek noktası: pasif item bonusları (hasar %, alan %, saldırı hızı) burada uygulanır.
    // Her okumada o anki itemlere bakılır; item silahtan önce ya da sonra alınmış olsun, etkisi hep geçerlidir.
    protected WeaponStats Stats
    {
        get
        {
            var stats = Data.GetStats(Level);
            stats.damage *= 1f + PlayerPassives.GetBonus(Owner, PassiveStat.Damage);
            stats.area *= 1f + PlayerPassives.GetBonus(Owner, PassiveStat.Area);
            // Saldırı hızı: bekleme süresi ve aynı düşmana tekrar vurma aralığı birlikte kısalır
            float attackInterval = 1f / AttackSpeedMultiplier;
            stats.cooldown *= attackInterval;
            stats.hitInterval *= attackInterval;
            return stats;
        }
    }

    // Güneş Paneli gibi itemlerden gelen saldırı hızı çarpanı (1 = bonus yok). Bekleme süresi en fazla %70 kısalabilir.
    protected float AttackSpeedMultiplier =>
        1f / Mathf.Max(0.3f, 1f - PlayerPassives.GetBonus(Owner, PassiveStat.CooldownReduction));

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
