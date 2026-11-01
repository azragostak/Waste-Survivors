using System.Collections.Generic;
using UnityEngine;

// Rüzgar Türbini: karakterin etrafında türbin kanatları döner, kanatların çarptığı her düşman hasar alır.
// Kanatlar rotor gibi dizilir: kökü karakterin yanında, ucu dışarı bakar. Saldırı beklemesi yok, sürekli döner.
//
// Stat kullanımı:
//   amount      = kanat sayısı                  damage = vuruş başına hasar
//   speed       = dönüş hızı (derece/sn)        area   = kanat boyu çarpanı (1 = sprite boyu)
//   Saldırı hızı itemleri (Güneş Paneli) dönüşü de hızlandırır; alan itemleri (Gübre) kanatları büyütür.
//   hitInterval = aynı düşmanı tekrar vurmak için geçmesi gereken süre (sn)
//   cooldown, duration, range kullanılmaz
public class WindTurbineWeapon : Weapon
{
    [Tooltip("Boşsa WeaponData ikonu kullanılır")]
    [SerializeField] private Sprite bladeSprite;
    [Tooltip("Rotor merkezi: karakterin ayak pivotundan gövde hizasına ofset")]
    [SerializeField] private Vector2 centerOffset = new Vector2(0f, 0.4f);
    [Tooltip("Kanat kökünün rotor merkezine uzaklığı")]
    [SerializeField] private float innerRadius = 0.35f;
    [Tooltip("Düşmanların yaklaşık yarıçapı (kanada bu kadar yaklaşınca vurulur)")]
    [SerializeField] private float enemyRadius = 0.25f;
    [Tooltip("Karakterlerin üstünde, can barlarının altında")]
    [SerializeField] private int sortingOrder = 3;

    [Header("Ses")]
    [Tooltip("Kanat düşmana çarptığında. Kalabalıkta saniyede onlarca vuruş olur; sık tekrarı sınırlanır.")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.35f;

    private const float CleanupInterval = 2f;

    private readonly List<Transform> blades = new();
    private readonly List<Enemy> nearby = new();
    private readonly Dictionary<Enemy, float> nextHitTime = new();
    private readonly List<Enemy> staleKeys = new();

    private Transform rotor;
    private Sprite sprite;
    private float angle;
    private float cleanupTimer;

    protected override void OnInitialized()
    {
        sprite = bladeSprite != null ? bladeSprite : Data.icon;
        if (sprite == null)
            Debug.LogWarning($"{name}: Kanat sprite'ı atanmamış, kanatlar görünmeyecek (hasar yine verilir).", this);

        rotor = new GameObject("Rotor").transform;
        rotor.SetParent(transform, false);
    }

    // Base Update cooldown'a göre saldırır; türbin ise her karede döner ve çarpışma kontrol eder
    protected override void Update()
    {
        if (Data == null || rotor == null) return;

        var stats = Stats;
        int count = Mathf.Max(1, stats.amount);
        SyncBladeCount(count);

        // Saat yönünde döner
        angle = (angle - stats.speed * AttackSpeedMultiplier * Time.deltaTime) % 360f;
        rotor.position = Owner.position + (Vector3)centerOffset;

        float scale = Mathf.Max(0.1f, stats.area);
        float length = GetBladeLength() * scale;
        float halfWidth = GetBladeWidth() * scale * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float bladeAngle = angle + i * 360f / count;
            var blade = blades[i];
            // Sprite yukarı bakıyor ve pivot'u kökte; yukarısı dışarıyı gösterecek şekilde çevir
            blade.localRotation = Quaternion.Euler(0f, 0f, bladeAngle - 90f);
            blade.localPosition = Direction(bladeAngle) * innerRadius;
            blade.localScale = new Vector3(scale, scale, 1f);
        }

        HitEnemies(stats, count, length, halfWidth);
        CleanupDeadEnemies();
    }

    private void HitEnemies(WeaponStats stats, int count, float length, float halfWidth)
    {
        Vector2 center = rotor.position;
        float outerRadius = innerRadius + length;
        float hitDistance = halfWidth + enemyRadius;

        bool hitAny = false;
        Enemy.GetInRadius(center, outerRadius + enemyRadius, nearby);
        foreach (var enemy in nearby)
        {
            if (nextHitTime.TryGetValue(enemy, out float readyAt) && Time.time < readyAt) continue;

            Vector2 position = enemy.transform.position;
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Direction(angle + i * 360f / count);
                Vector2 root = center + dir * innerRadius;
                Vector2 tip = center + dir * outerRadius;
                if (DistanceToSegment(position, root, tip) > hitDistance) continue;

                nextHitTime[enemy] = Time.time + Mathf.Max(0.05f, stats.hitInterval);
                enemy.TakeDamage(stats.damage);
                hitAny = true;
                break;
            }
        }

        if (hitAny)
            SfxPlayer.Play(hitSound, hitVolume, minInterval: 0.12f, maxVoices: 3);
    }

    // Ölen düşmanlar sözlükte birikmesin
    private void CleanupDeadEnemies()
    {
        cleanupTimer -= Time.deltaTime;
        if (cleanupTimer > 0f) return;
        cleanupTimer = CleanupInterval;

        staleKeys.Clear();
        foreach (var pair in nextHitTime)
        {
            if (pair.Key == null || Time.time >= pair.Value)
                staleKeys.Add(pair.Key);
        }
        foreach (var key in staleKeys)
            nextHitTime.Remove(key);
    }

    private void SyncBladeCount(int count)
    {
        while (blades.Count < count)
        {
            var go = new GameObject($"Blade{blades.Count + 1}");
            go.transform.SetParent(rotor, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            blades.Add(go.transform);
        }

        while (blades.Count > count)
        {
            Destroy(blades[blades.Count - 1].gameObject);
            blades.RemoveAt(blades.Count - 1);
        }
    }

    // Kanadın kökten uca uzunluğu ve genişliği (dünya birimi, ölçeksiz)
    private float GetBladeLength() => sprite != null ? sprite.bounds.size.y : 1.28f;
    private float GetBladeWidth() => sprite != null ? sprite.bounds.size.x : 0.24f;

    private static Vector2 Direction(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return Vector2.Distance(point, a + ab * t);
    }

    // Saldırı Update içinde yapılıyor
    protected override bool TryAttack() => false;
}
