using System;
using System.Collections.Generic;
using UnityEngine;

// Bir silahın tüm ayarlanabilir verisi: isim, ikon, seviye tablosu ve davranış prefab'ı.
// Yeni silah = yeni WeaponData asset'i + Weapon'dan türeyen bir script'li prefab.
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Waste Survivors/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Bilgi (level-up ekranında gösterilecek)")]
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Davranış")]
    [Tooltip("Oyuncuya eklenecek, Weapon component'i olan prefab")]
    public Weapon weaponPrefab;

    [Header("Seviyeler (1. eleman = Level 1)")]
    public List<WeaponLevel> levels = new() { new WeaponLevel() };

    public int MaxLevel => levels.Count;

    public WeaponStats GetStats(int level) => GetLevel(level).stats;

    public string GetUpgradeDescription(int level) => GetLevel(level).upgradeDescription;

    private WeaponLevel GetLevel(int level) => levels[Mathf.Clamp(level - 1, 0, levels.Count - 1)];
}

[Serializable]
public class WeaponLevel
{
    [Tooltip("Bu seviyeye geçerken level-up kartında yazacak metin")]
    [TextArea] public string upgradeDescription;
    public WeaponStats stats = WeaponStats.Default;
}
