using System.Collections.Generic;
using UnityEngine;

// Tohum Atıcı: en yakın düşmanlara hızlı tohum mermileri ateşler.
// Geliştikçe saldırı hızı (cooldown azalması) ve vuruş hasarı artar; mermiler Object Pool ile yeniden kullanılır.
//
// Stat kullanımı:
//   cooldown = iki atış arası süre         damage   = tohum başına hasar
//   speed    = tohum uçuş hızı             range    = en yakın düşmanı arama menzili
//   amount   = atış başına tohum sayısı    area     = tohum boyutu ve isabet yarıçapı çarpanı
//   duration = tohumun havada kalma ömrü
public class SeedWeapon : Weapon
{
    [Tooltip("Ateşlenecek tohum mermisi prefab'ı (Object Pool ile çoğaltılır).")]
    [SerializeField] private SeedProjectile seedPrefab;

    [Tooltip("Karakterin ayak pivotundan gövde hizasına çıkış ofseti.")]
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0f, 0.45f);

    [Tooltip("Hareketli düşmanlar için hedef önleme oranı (0 = anlık konumu, 1 = tam önleme).")]
    [Range(0f, 1f)]
    [SerializeField] private float targetLeadFactor = 0.5f;

    [Tooltip("Aynı hedefe birden fazla tohum atıldığında aralarındaki yayılma açısı (derece).")]
    [SerializeField] private float multiShotSpreadAngle = 10f;

    private readonly List<Enemy> targets = new();

    protected override bool TryAttack()
    {
        if (seedPrefab == null) return false;

        var stats = Stats;
        Vector2 fireOrigin = (Vector2)Owner.position + muzzleOffset;

        Enemy.GetNearest(fireOrigin, stats.range, stats.amount, targets);
        if (targets.Count == 0) return false;

        int shotCount = Mathf.Max(1, stats.amount);
        float projectileSpeed = Mathf.Max(1f, stats.speed);

        for (int i = 0; i < shotCount; i++)
        {
            Enemy target = targets[i % targets.Count];
            Vector2 targetPos = target.transform.position;

            // Hareketli düşmanın hızına göre nişan noktasını hafif öne al
            if (targetLeadFactor > 0f && target.TryGetComponent<Rigidbody2D>(out var targetRb))
            {
                float dist = Vector2.Distance(fireOrigin, targetPos);
                float travelTime = dist / projectileSpeed;
                targetPos += targetRb.velocity * (travelTime * targetLeadFactor);
            }

            Vector2 dir = targetPos - fireOrigin;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.right;
            else
                dir.Normalize();

            // Hedef sayısından fazla tohum atılıyorsa hafif yelpaze açısı ver
            if (shotCount > targets.Count)
            {
                int groupIndex = i / targets.Count;
                float sign = (groupIndex % 2 == 1) ? 1f : -1f;
                int step = (groupIndex + 1) / 2;
                dir = RotateVector(dir, sign * step * multiShotSpreadAngle);
            }

            SeedProjectile.Spawn(seedPrefab, fireOrigin, dir, stats);
        }

        return true;
    }

    private static Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}

