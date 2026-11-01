using UnityEngine;

// Pasif itemlerin etkilediği oyuncu değerleri
public enum PassiveStat
{
    Damage,            // tüm silahların hasarı (%)
    CooldownReduction, // tüm silahların saldırı hızı artar (bekleme süresi kısalır, türbin hızlı döner)
    Area,              // tüm silahların etki alanı (%)
    MoveSpeed,         // hareket hızı (%)
    MaxHealth,         // maksimum can (düz sayı)
    PickupRadius,      // recycle toplama alanı (%)
    CleanRadius,       // zemin temizleme alanı (%)
}

// Bir pasif itemin verisi: isim, ikon ve her seviyede verdiği bonus.
// Yeni item = yeni PassiveItemData asset'i + UpgradePool'a eklemek.
[CreateAssetMenu(fileName = "NewPassiveItem", menuName = "Waste Survivors/Passive Item Data")]
public class PassiveItemData : ScriptableObject
{
    [Header("Bilgi (level-up ekranında gösterilecek)")]
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Etki")]
    public PassiveStat stat;
    [Tooltip("Her seviyede eklenen bonus. Yüzdeler 0.1 = %10, Max Health düz sayı (20 = +20 can)")]
    public float valuePerLevel = 0.1f;
    [Min(1)] public int maxLevel = 5;

    public float GetTotalValue(int level) => valuePerLevel * Mathf.Clamp(level, 0, maxLevel);

    // Örn. "+%30 hasar" veya "+40 maksimum can"
    public string GetEffectText(int level)
    {
        float value = GetTotalValue(level);
        return stat switch
        {
            PassiveStat.Damage => $"+%{value * 100f:0} hasar",
            PassiveStat.CooldownReduction => $"+%{(1f / Mathf.Max(0.3f, 1f - value) - 1f) * 100f:0} saldırı hızı",
            PassiveStat.Area => $"+%{value * 100f:0} silah alanı",
            PassiveStat.MoveSpeed => $"+%{value * 100f:0} hareket hızı",
            PassiveStat.MaxHealth => $"+{value:0} maksimum can",
            PassiveStat.PickupRadius => $"+%{value * 100f:0} toplama alanı",
            PassiveStat.CleanRadius => $"+%{value * 100f:0} temizleme alanı",
            _ => "",
        };
    }
}
