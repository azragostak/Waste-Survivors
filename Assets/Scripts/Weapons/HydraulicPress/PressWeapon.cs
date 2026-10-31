using System.Collections.Generic;
using UnityEngine;

// Hidrolik Pres: en yakın düşmanların tepesinden ağır bir pres indirir.
// Yere çarptığı anda etki alanındaki tüm düşmanları ezer ve kaybolur (bkz. PressDrop).
//
// Stat kullanımı:
//   cooldown = iki saldırı arası süre      amount   = saldırı başına düşen pres sayısı
//   range    = hedef arama menzili         speed    = gökten düşüş hızı
//   damage   = ezme (alan) hasarı          area     = ezme / şok dalgası yarıçapı
//   duration = çarptıktan sonra kalma ve kaybolma süresi
public class PressWeapon : Weapon
{
    [Tooltip("Gökten düşürülecek hidrolik pres prefab'ı.")]
    [SerializeField] private PressDrop pressPrefab;

    [Tooltip("Birden fazla pres düşerken aralarındaki gecikme (sn).")]
    [SerializeField] private float dropStagger = 0.1f;

    [Tooltip("Hareketli düşmanların gidiş yönünü ne oranda öngörsün (0 = olduğu yer, 1 = tam tahmin).")]
    [Range(0f, 1f)]
    [SerializeField] private float targetLeadFactor = 0.6f;

    private readonly List<Enemy> targets = new();

    protected override bool TryAttack()
    {
        if (pressPrefab == null) return false;

        var stats = Stats;
        Enemy.GetNearest(Owner.position, stats.range, stats.amount, targets);
        if (targets.Count == 0) return false;

        float estimatedFallTime = pressPrefab.GetEstimatedFallTime(stats.speed);

        for (int i = 0; i < stats.amount; i++)
        {
            Enemy targetEnemy = targets[i % targets.Count];
            Vector2 targetPosition = targetEnemy.transform.position;

            // Hareketli düşmanlar (özellikle yuvarlanan variller) için hafif önleme hesabı
            if (targetLeadFactor > 0f && targetEnemy.TryGetComponent<Rigidbody2D>(out var targetRb))
            {
                float delay = i * dropStagger;
                targetPosition += targetRb.velocity * ((estimatedFallTime + delay) * targetLeadFactor);
            }

            // Pres sayısı sahnedeki hedef sayısından fazlaysa aynı noktaya üst üste binmesinler
            if (i >= targets.Count)
                targetPosition += Random.insideUnitCircle * (stats.area * 0.8f);

            var drop = Instantiate(pressPrefab, targetPosition, Quaternion.identity);
            drop.Drop(targetPosition, stats, i * dropStagger);
        }

        return true;
    }
}

