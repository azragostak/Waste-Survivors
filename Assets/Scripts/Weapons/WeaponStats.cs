using System;
using UnityEngine;

// Tüm silahların ortak sayısal değerleri. Her silah işine yarayanları kullanır.
// Yeni bir stat gerekirse buraya eklenir.
[Serializable]
public struct WeaponStats
{
    [Tooltip("Vuruş başına hasar")]
    public float damage;

    [Tooltip("İki saldırı arası süre (sn)")]
    public float cooldown;

    [Tooltip("Etki alanı yarıçapı")]
    public float area;

    [Tooltip("Saldırı başına mermi/obje sayısı")]
    public int amount;

    [Tooltip("Yerde kalan objelerin ömrü (sn)")]
    public float duration;

    [Tooltip("Mermi/fırlatma hızı")]
    public float speed;

    [Tooltip("Kalıcı objelerin iki vuruşu arası süre (sn)")]
    public float hitInterval;

    [Tooltip("Hedef arama menzili")]
    public float range;

    public static WeaponStats Default => new WeaponStats
    {
        damage = 5f,
        cooldown = 2f,
        area = 1f,
        amount = 1,
        duration = 3f,
        speed = 8f,
        hitInterval = 1f,
        range = 8f,
    };
}
