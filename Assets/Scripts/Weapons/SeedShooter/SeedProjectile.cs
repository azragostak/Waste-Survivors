using System.Collections.Generic;
using UnityEngine;

// Object Pool destekli tekil tohum mermisi.
// Fizik motoru yükü oluşturmadan Enemy.Active listesi üzerinden kesintisiz (segment) çarpışma kontrolü yapar.
public class SeedProjectile : MonoBehaviour
{
    private static readonly Dictionary<SeedProjectile, Stack<SeedProjectile>> pools = new();

    [Header("Çarpışma ve Görsel")]
    [Tooltip("Temel vuruş yarıçapı (düşman yarıçapı dahil yaklaşık temas mesafesi)")]
    [SerializeField] private float baseHitRadius = 0.34f;

    [Tooltip("Sprite'ın filiz ucunu uçuş yönüne hizalamak için açı ofseti (tohum sağ-üste baktığı için -45)")]
    [SerializeField] private float spriteAngleOffset = -45f;

    [SerializeField] private Color hitSparkColor = new Color(0.55f, 0.95f, 0.35f);

    [Header("Bitki Ekimi (001-008 PNG)")]
    [Tooltip("Tohum isabet eden düşman öldüğünde rastgele seçilecek 8 karelik (2x4) bitki sprite sheet'leri")]
    [SerializeField] private Texture2D[] plantSheets;

    [Tooltip("Bitki büyüme animasyonu hızı (kare/saniye)")]
    [SerializeField, Min(1f)] private float plantFramesPerSecond = 8f;

    [Tooltip("Ekilen bitkinin sahne ölçeği")]
    [SerializeField, Min(0.1f)] private float plantBaseScale = 1.6f;

    [Header("Meyve Düşürme (Can Yenileme)")]
    [Tooltip("Tohumla öldürülen düşmandan düşecek meyve görseli (meyve.asset)")]
    [SerializeField] private Sprite fruitSprite;
    [Tooltip("Tohumun son vuruşu yaptığı düşmandan meyve düşme olasılığı (0.005 = %0.5)")]
    [SerializeField, Range(0f, 1f)] private float fruitDropChance = 0.005f;
    [Tooltip("Toplanan meyvenin yenilediği can miktarı")]
    [SerializeField, Min(1f)] private float fruitHealAmount = 25f;
    [Tooltip("Düşen meyvenin sahne ölçeği")]
    [SerializeField, Min(0.2f)] private float fruitBaseScale = 2.6f;
    [Tooltip("Meyve toplandığında çalacak ses")]
    [SerializeField] private AudioClip fruitCollectSound;

    // Final ekimi için (oyuncu tohum silahını hiç almamış olsa da bitki görselleri bulunabilsin)
    public Texture2D[] PlantSheets => plantSheets;
    public float PlantFramesPerSecond => plantFramesPerSecond;
    public float PlantBaseScale => plantBaseScale;

    private SeedProjectile sourcePrefab;
    private Vector3 initialScale;
    private bool initializedScale;

    private Vector2 direction;
    private float speed;
    private float damage;
    private float hitRadius;
    private float remainingLife;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => pools.Clear();

    public static SeedProjectile Spawn(SeedProjectile prefab, Vector2 position, Vector2 dir, WeaponStats stats)
    {
        if (!pools.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<SeedProjectile>();
            pools[prefab] = stack;
        }

        SeedProjectile instance = null;
        while (stack.Count > 0 && instance == null)
            instance = stack.Pop();

        if (instance == null)
        {
            instance = Instantiate(prefab);
            instance.sourcePrefab = prefab;
        }

        instance.Activate(position, dir, stats);
        return instance;
    }

    private void Activate(Vector2 position, Vector2 dir, WeaponStats stats)
    {
        if (!initializedScale)
        {
            initialScale = transform.localScale;
            initializedScale = true;
        }

        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        speed = Mathf.Max(1f, stats.speed);
        damage = stats.damage;

        float areaMultiplier = Mathf.Max(0.5f, stats.area);
        hitRadius = baseHitRadius * areaMultiplier;
        remainingLife = stats.duration > 0f ? stats.duration : 2f;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + spriteAngleOffset;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
        transform.localScale = initialScale * areaMultiplier;

        gameObject.SetActive(true);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        remainingLife -= dt;
        if (remainingLife <= 0f)
        {
            Despawn();
            return;
        }

        Vector2 startPos = transform.position;
        Vector2 endPos = startPos + direction * (speed * dt);

        // Hızlı mermilerin düşmanın içinden geçmemesi için doğru parçası (segment) mesafesi kontrol edilir
        Enemy hitEnemy = FindHitEnemy(startPos, endPos, out Vector2 hitPoint);
        if (hitEnemy != null)
        {
            Vector2 enemyPos = hitEnemy.transform.position;
            bool wasAlive = hitEnemy.IsAlive;
            SeedPlantedSprout.MarkEnemy(hitEnemy, plantSheets, plantFramesPerSecond, plantBaseScale);
            hitEnemy.TakeDamage(damage);
            if (wasAlive && !hitEnemy.IsAlive && fruitSprite != null && Random.value < fruitDropChance)
            {
                FruitPickup.Spawn(enemyPos, fruitSprite, fruitHealAmount, fruitBaseScale, fruitCollectSound);
            }
            SeedHitEffect.Spawn(hitPoint, hitSparkColor);
            Despawn();
            return;
        }

        transform.position = endPos;
    }

    private Enemy FindHitEnemy(Vector2 a, Vector2 b, out Vector2 hitPoint)
    {
        Enemy closest = null;
        float bestProgress = float.MaxValue;
        float radiusSqr = hitRadius * hitRadius;
        hitPoint = b;

        Vector2 ab = b - a;
        float abLenSqr = ab.sqrMagnitude;

        var activeEnemies = Enemy.Active;
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            var enemy = activeEnemies[i];
            if (enemy == null || !enemy.IsAlive) continue;

            Vector2 p = enemy.transform.position;
            float t = abLenSqr > 0.000001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSqr) : 0f;
            Vector2 closestOnSegment = a + ab * t;
            float distSqr = (p - closestOnSegment).sqrMagnitude;

            if (distSqr <= radiusSqr && t < bestProgress)
            {
                bestProgress = t;
                closest = enemy;
                hitPoint = closestOnSegment;
            }
        }

        return closest;
    }

    private void Despawn()
    {
        gameObject.SetActive(false);

        if (sourcePrefab != null && pools.TryGetValue(sourcePrefab, out var stack))
            stack.Push(this);
    }
}

