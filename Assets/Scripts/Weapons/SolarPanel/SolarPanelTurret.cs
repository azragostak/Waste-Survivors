using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Fırlatılıp yere kurulan tek bir güneş paneli. Panel prefab'larının köküne eklenir.
// Farklı görünümler = aynı script, farklı sprite'lı prefab'lar.
public class SolarPanelTurret : MonoBehaviour
{
    [Header("Fırlatma")]
    [Tooltip("Uçuş sırasındaki kavis yüksekliği")]
    [SerializeField] private float arcHeight = 1.5f;

    [Header("Elektrik Efekti")]
    [Tooltip("Opsiyonel: kendi efekt prefab'ın (1 birim çaplı olmalı, alana göre ölçeklenir). Boşsa kodla çizilen şimşek kullanılır.")]
    [SerializeField] private GameObject zapVisualPrefab;
    [SerializeField] private float zapVisualLifetime = 0.25f;
    [SerializeField] private Color zapColor = new Color(0.4f, 0.85f, 1f);

    private WeaponStats stats;
    private bool landed;
    private readonly List<Enemy> hits = new();

    public void Launch(Vector2 targetPosition, WeaponStats weaponStats)
    {
        stats = weaponStats;
        StartCoroutine(Run(targetPosition));
    }

    private IEnumerator Run(Vector2 target)
    {
        // Kavisli uçuş
        Vector2 start = transform.position;
        float flightTime = Mathf.Max(0.1f, Vector2.Distance(start, target) / Mathf.Max(0.01f, stats.speed));
        for (float t = 0f; t < flightTime; t += Time.deltaTime)
        {
            float k = t / flightTime;
            transform.position = Vector2.Lerp(start, target, k) + Vector2.up * (arcHeight * 4f * k * (1f - k));
            yield return null;
        }
        transform.position = target;
        landed = true;

        // Yerde: ömrü boyunca belirli aralıklarla elektrik at
        float endTime = Time.time + stats.duration;
        while (Time.time < endTime)
        {
            Zap();
            yield return new WaitForSeconds(stats.hitInterval);
        }

        Destroy(gameObject);
    }

    private void Zap()
    {
        Vector2 center = transform.position;
        Enemy.GetInRadius(center, stats.area, hits);
        foreach (var enemy in hits)
            enemy.TakeDamage(stats.damage);

        if (zapVisualPrefab != null)
        {
            var visual = Instantiate(zapVisualPrefab, center, Quaternion.identity);
            visual.transform.localScale = Vector3.one * (stats.area * 2f);
            Destroy(visual, zapVisualLifetime);
        }
        else
        {
            ElectricZapEffect.Spawn(center, stats.area, hits, zapColor, zapVisualLifetime);
        }
    }

    void OnDrawGizmos()
    {
        if (!landed) return;
        Gizmos.color = zapColor;
        Gizmos.DrawWireSphere(transform.position, stats.area);
    }
}
