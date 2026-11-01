using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// Çöp Tepesi boss savaşı. Saldırıların hepsi önce kırmızı uyarı verir; doğru konumlanan oyuncu hepsinden kaçabilir.
//
// Faz 1 (can %100-50): Garbage Slam ve Stampede.
// Faz 2 (can %50 altı): + Trash Volley; bütün saldırılar daha sık ve hızlı (uyarılar yine kaçınılabilir kalır).
// Her iki fazda belirli aralıklarla küçük düşman grupları çağırır.
//
//   Garbage Slam: oyuncunun olduğu yerde (faz 2'de çevresinde de) daire şeklinde kırmızı uyarı, dolunca çöp çöker.
//   Stampede:     yatay ve dikey şeritlerde kırmızı uyarı, sonra şerit boyunca oyuncuyu takip etmeyen,
//                 yolunu yüksek hızda tamamlayan düşman sürüsü geçer. Sürü tek türdendir, türü rastgele seçilir.
//   Trash Volley: boss turuncuya dönüp titrer, sonra çevresine boşluklu çöp halkaları fırlatır.
//
// Debug: F5 = boss'u hemen faz 2'ye geçir (editörde ve development build'de).
[RequireComponent(typeof(BossEnemy))]
public class BossFight : MonoBehaviour
{
    [Header("Genel")]
    [Tooltip("Ekrana girdikten sonra ilk saldırıya kadar geçen süre")]
    [SerializeField] private float entranceDelay = 2.5f;
    [Tooltip("İki saldırı arası bekleme (faz 1)")]
    [SerializeField] private float pauseBetweenAttacks = 2.4f;
    [Tooltip("Canının bu oranının altında faz 2 başlar")]
    [SerializeField, Range(0.1f, 0.9f)] private float phase2HealthRatio = 0.5f;
    [Tooltip("Faz 2'de saldırılar bu kat hızlanır (uyarılar, aralar, mermi ve sürü hızı)")]
    [SerializeField, Min(1f)] private float phase2Speed = 1.35f;
    [Tooltip("Uyarı süresi bundan kısa olamaz: saldırılar hep kaçınılabilir kalsın")]
    [SerializeField] private float minTelegraphTime = 0.8f;

    [Header("Garbage Slam (daire)")]
    [SerializeField] private float slamRadius = 2.4f;
    [SerializeField] private float slamTelegraph = 1.4f;
    [SerializeField] private float slamDamage = 35f;
    [Tooltip("Faz 2'de aynı anda kaç daire (biri hep oyuncunun üstünde)")]
    [SerializeField, Min(1)] private int phase2SlamCount = 3;
    [Tooltip("Faz 2'de ek dairelerin oyuncuya uzaklığı")]
    [SerializeField] private float slamSpread = 3.5f;
    [SerializeField] private Color slamImpactColor = new Color(0.95f, 0.35f, 0.2f);
    [Tooltip("Her çöküşte bunlardan biri rastgele çalar")]
    [SerializeField] private AudioClip[] slamSounds;
    [SerializeField, Range(0f, 1f)] private float slamVolume = 0.6f;

    [Header("Stampede (şeritler)")]
    [SerializeField] private float laneWidth = 1.3f;
    [SerializeField] private float laneTelegraph = 1.4f;
    [SerializeField] private float stampedeSpeed = 12f;
    [SerializeField, Min(1)] private int stampedeCountPerLane = 25;
    [Tooltip("Sürüdeki düşmanlar arası mesafe")]
    [SerializeField] private float stampedeSpacing = 0.3f;
    [Tooltip("Faz 2'de yatay ve dikeyde kaçar şerit (biri hep oyuncunun hizasında)")]
    [SerializeField, Min(1)] private int phase2LanesPerAxis = 2;
    [Tooltip("Ek şeritlerin oyuncu hizasına uzaklığı")]
    [SerializeField] private float extraLaneOffset = 3f;

    [Header("Trash Volley (faz 2)")]
    [SerializeField] private Sprite[] volleySprites;
    [Tooltip("Fırlatmadan önce turuncuya dönüp titreme süresi")]
    [SerializeField] private float volleyWindup = 0.8f;
    [SerializeField, Min(1)] private int volleyWaves = 3;
    [Tooltip("Bir halkadaki parça sayısı (aralarındaki boşluktan geçilebilmeli)")]
    [SerializeField, Min(3)] private int volleyCount = 14;
    [SerializeField] private float volleySpeed = 3.6f;
    [SerializeField] private float volleyDamage = 15f;
    [SerializeField] private float volleyWaveInterval = 0.5f;
    [SerializeField] private float volleyProjectileSize = 0.5f;
    [SerializeField] private Color windupColor = new Color(1f, 0.55f, 0.2f);

    [Header("Düşman Çağırma")]
    [Tooltip("Çağrılan grupların ve Stampede sürülerinin türleri")]
    [SerializeField] private Enemy[] minionPrefabs;
    [SerializeField] private float minionInterval = 14f;
    [SerializeField, Min(1)] private int minionCount = 12;
    [Tooltip("Çağrılan düşmanların can ve hasar çarpanı (10. dakikanın zorluğu)")]
    [SerializeField] private float minionHealthMultiplier = 3.6f;
    [SerializeField] private float minionDamageMultiplier = 1.4f;

    private BossEnemy boss;
    private Transform player;
    private Camera cam;
    private bool phase2;
    private float minionTimer;
    private Attack? lastAttack;

    private float Speed => phase2 ? phase2Speed : 1f;
    private float Telegraph(float baseTime) => Mathf.Max(minTelegraphTime, baseTime / Speed);
    private static bool DebugKeysEnabled => Application.isEditor || Debug.isDebugBuild;

    void Awake()
    {
        boss = GetComponent<BossEnemy>();
        cam = Camera.main;
        minionTimer = minionInterval * 0.5f;
    }

    void Start()
    {
        var controller = FindObjectOfType<PlayerController>();
        player = controller != null ? controller.transform : null;
        StartCoroutine(FightLoop());
    }

    void Update()
    {
        if (Time.deltaTime <= 0f || boss == null || !boss.IsAlive || player == null) return;

        if (!phase2 && boss.CurrentHealth <= boss.MaxHealth * phase2HealthRatio)
            EnterPhase2();

        if (DebugKeysEnabled && !phase2 && Input.GetKeyDown(KeyCode.F5))
            boss.TakeDamage(boss.CurrentHealth - boss.MaxHealth * phase2HealthRatio + 1f);

        minionTimer -= Time.deltaTime;
        if (minionTimer <= 0f)
        {
            minionTimer = minionInterval / Speed;
            SpawnMinionGroup();
        }
    }

    private IEnumerator FightLoop()
    {
        yield return new WaitForSeconds(entranceDelay);

        while (boss != null && boss.IsAlive)
        {
            if (player == null) yield break;

            yield return StartCoroutine(RunAttack(PickAttack()));
            yield return new WaitForSeconds(pauseBetweenAttacks / (Speed * Speed));
        }
    }

    private enum Attack { Slam, Stampede, Volley }

    // Aynı saldırı art arda gelmesin
    private Attack PickAttack()
    {
        var attacks = new List<Attack> { Attack.Slam, Attack.Stampede };
        if (phase2) attacks.Add(Attack.Volley);
        if (lastAttack.HasValue && attacks.Count > 1)
            attacks.Remove(lastAttack.Value);

        var pick = attacks[Random.Range(0, attacks.Count)];
        lastAttack = pick;
        return pick;
    }

    private IEnumerator RunAttack(Attack attack) => attack switch
    {
        Attack.Slam => GarbageSlam(),
        Attack.Stampede => Stampede(),
        _ => TrashVolley(),
    };

    private void EnterPhase2()
    {
        phase2 = true;
        boss.Enraged = true;
        PressShockwaveEffect.Spawn(transform.position, 3.5f, new Color(1f, 0.25f, 0.2f), 0.5f);
        SpawnMinionGroup();
        minionTimer = minionInterval / Speed;
    }

    // ---------------------------------------------------------------- Garbage Slam
    private IEnumerator GarbageSlam()
    {
        var centers = new List<Vector2> { player.position };
        if (phase2)
        {
            float startAngle = Random.value * 360f;
            for (int i = 1; i < phase2SlamCount; i++)
            {
                float angle = (startAngle + i * 360f / (phase2SlamCount - 1)) * Mathf.Deg2Rad;
                centers.Add((Vector2)player.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * slamSpread);
            }
        }

        float telegraph = Telegraph(slamTelegraph);
        foreach (var center in centers)
            TelegraphZone.Circle(center, slamRadius, telegraph);
        yield return new WaitForSeconds(telegraph);

        bool hit = false;
        foreach (var center in centers)
        {
            PressShockwaveEffect.Spawn(center, slamRadius, slamImpactColor, 0.35f);
            if (!hit && player != null && ((Vector2)player.position - center).sqrMagnitude <= slamRadius * slamRadius)
            {
                hit = true;
                if (player.TryGetComponent(out PlayerHealth health))
                    health.TakeDamage(slamDamage);
            }
        }
        if (slamSounds != null && slamSounds.Length > 0)
            SfxPlayer.Play(slamSounds[Random.Range(0, slamSounds.Length)], slamVolume, minInterval: 0.05f, maxVoices: 3);
    }

    // ---------------------------------------------------------------- Stampede
    private IEnumerator Stampede()
    {
        int lanesPerAxis = phase2 ? phase2LanesPerAxis : 1;
        Vector2 playerPos = player.position;
        GetViewBounds(out Vector2 min, out Vector2 max);

        var lanes = new List<(Vector2 from, Vector2 to)>();
        for (int axis = 0; axis < 2; axis++)
        {
            float sign = Random.value < 0.5f ? 1f : -1f;
            for (int i = 0; i < lanesPerAxis; i++)
            {
                float offset = i == 0 ? 0f : sign * extraLaneOffset * i;
                bool reverse = Random.value < 0.5f;
                Vector2 from, to;
                if (axis == 0) // yatay şerit
                {
                    float y = playerPos.y + offset;
                    from = new Vector2(min.x, y);
                    to = new Vector2(max.x, y);
                }
                else // dikey şerit
                {
                    float x = playerPos.x + offset;
                    from = new Vector2(x, min.y);
                    to = new Vector2(x, max.y);
                }
                lanes.Add(reverse ? (to, from) : (from, to));
            }
        }

        float telegraph = Telegraph(laneTelegraph);
        foreach (var (from, to) in lanes)
            TelegraphZone.Lane(from, to, laneWidth, telegraph);
        yield return new WaitForSeconds(telegraph);

        // Sürünün hepsi aynı tür, tür her saldırıda rastgele
        var prefab = PickMinionPrefab();
        if (prefab == null) yield break;

        float speed = stampedeSpeed * Mathf.Sqrt(Speed);
        foreach (var (from, to) in lanes)
        {
            Vector2 dir = (to - from).normalized;
            Vector2 side = new Vector2(-dir.y, dir.x);
            float length = Vector2.Distance(from, to);
            for (int k = 0; k < stampedeCountPerLane; k++)
            {
                // Şeridin girişinden geriye doğru sıralanır, sırayla akarak geçerler
                Vector2 position = from - dir * (k * stampedeSpacing + 0.5f) + side * Random.Range(-laneWidth * 0.3f, laneWidth * 0.3f);
                float duration = (length + k * stampedeSpacing + 1.5f) / speed;
                var enemy = SpawnMinion(prefab, position);
                enemy.ForceDash(dir * speed, duration);
            }
        }
    }

    // ---------------------------------------------------------------- Trash Volley
    private IEnumerator TrashVolley()
    {
        var body = boss.BodyRenderer;
        float windup = Telegraph(volleyWindup);
        for (float t = 0f; t < windup; t += Time.deltaTime)
        {
            if (body != null)
                body.color = Color.Lerp(boss.BaseColor, windupColor, Mathf.PingPong(t * 8f, 1f));
            yield return null;
        }
        if (body != null) body.color = boss.BaseColor;

        float speed = volleySpeed * Mathf.Sqrt(Speed);
        float baseAngle = Random.value * 360f;
        float step = 360f / volleyCount;
        for (int wave = 0; wave < volleyWaves; wave++)
        {
            // Her halka yarım adım kayık: boşluklar sabit kalmaz ama hep geçilebilir
            float waveAngle = baseAngle + (wave % 2) * step * 0.5f;
            Vector2 origin = transform.position;
            for (int i = 0; i < volleyCount; i++)
            {
                float angle = (waveAngle + i * step) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var sprite = volleySprites != null && volleySprites.Length > 0
                    ? volleySprites[Random.Range(0, volleySprites.Length)] : null;
                BossProjectile.Spawn(sprite, origin + dir * boss.BodyRadius, dir * speed, volleyDamage, volleyProjectileSize, 7f);
            }
            yield return new WaitForSeconds(volleyWaveInterval / Speed);
        }
    }

    // ---------------------------------------------------------------- Düşman çağırma
    private void SpawnMinionGroup()
    {
        var prefab = PickMinionPrefab();
        if (prefab == null || player == null) return;

        GetViewBounds(out Vector2 min, out Vector2 max);
        float radius = (max - min).magnitude * 0.5f;
        float angle = Random.value * Mathf.PI * 2f;
        Vector2 center = (Vector2)player.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        int count = Mathf.RoundToInt(minionCount * (phase2 ? 1.3f : 1f));
        for (int i = 0; i < count; i++)
            SpawnMinion(prefab, center + Random.insideUnitCircle * 1.5f);
    }

    private Enemy SpawnMinion(Enemy prefab, Vector2 position)
    {
        var enemy = Instantiate(prefab, position, Quaternion.identity);
        enemy.SetTarget(player);
        enemy.ApplyDifficulty(minionHealthMultiplier, minionDamageMultiplier);
        return enemy;
    }

    private Enemy PickMinionPrefab()
    {
        if (minionPrefabs == null || minionPrefabs.Length == 0) return null;
        return minionPrefabs[Random.Range(0, minionPrefabs.Length)];
    }

    // Kameranın gördüğü alan + ekran dışı pay
    private void GetViewBounds(out Vector2 min, out Vector2 max)
    {
        float halfHeight = (cam != null ? cam.orthographicSize : 5f) + 2f;
        float halfWidth = (cam != null ? cam.orthographicSize * cam.aspect : 9f) + 2f;
        Vector2 center = cam != null ? (Vector2)cam.transform.position : (Vector2)player.position;
        min = center - new Vector2(halfWidth, halfHeight);
        max = center + new Vector2(halfWidth, halfHeight);
    }
}
