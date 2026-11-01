using System.Collections.Generic;
using UnityEngine;

// Düşmanları DifficultyTimeline'a göre doğurur: süre ilerledikçe spawn hızı, düşman canı/hasarı ve karışım değişir.
// - Tekli düşmanlar ekranın hemen dışında, oyuncunun etrafında rastgele doğar.
// - Sürüler (şişeler vs.) belirli aralıklarla tek bir noktada toplu doğar.
// - Çember olaylarında düşmanlar oyuncunun etrafını sarar.
// - Oyuncudan çok uzakta kalan düşmanlar oyuncunun gittiği yöne, ekranın hemen dışına taşınır;
//   böylece oyuncu kaçsa da baskı sürer ve geride kalanlar düşman sınırını boş yere doldurmaz.
public class EnemySpawner : MonoBehaviour
{
    [Tooltip("Boşsa Resources/DifficultyTimeline kullanılır")]
    [SerializeField] private DifficultyTimeline timeline;
    [SerializeField] private Transform player;

    [Tooltip("Ekran kenarının ne kadar dışında doğsunlar")]
    [SerializeField] private float offscreenPadding = 1.5f;
    [Tooltip("Spawn mesafesinin kaç katından uzaktaki düşmanlar oyuncunun önüne taşınır")]
    [SerializeField, Min(1.1f)] private float relocateDistanceFactor = 1.7f;
    [Tooltip("Sürüler düşman sınırını en fazla bu oranda aşabilir (sürü yarım kalmasın)")]
    [SerializeField, Min(1f)] private float swarmCapAllowance = 1.25f;

    private const float RelocateCheckInterval = 1f;
    // Sınır doluyken biriken spawn'lar sınır açılınca tek seferde patlamasın
    private const float MaxSpawnBudget = 3f;

    private Camera cam;
    private PlayerController playerController;
    private int currentPhase = -1;
    private float spawnBudget;
    private float swarmTimer;
    private float relocateTimer;
    private readonly HashSet<DifficultyTimeline.RingEvent> firedRings = new();

    private float Elapsed => GameSession.Instance != null ? GameSession.Instance.Elapsed : Time.timeSinceLevelLoad;

    void Start()
    {
        cam = Camera.main;
        playerController = FindObjectOfType<PlayerController>();
        if (player == null && playerController != null)
            player = playerController.transform;

        if (timeline == null)
            timeline = Resources.Load<DifficultyTimeline>(DifficultyTimeline.ResourcePath);
        if (timeline == null || timeline.phases.Count == 0)
            Debug.LogWarning("EnemySpawner: DifficultyTimeline bulunamadı, düşman doğmayacak.", this);
    }

    void Update()
    {
        if (player == null || timeline == null || timeline.phases.Count == 0) return;
        if (GameSession.Instance != null && GameSession.Instance.IsOver) return;

        float t = Elapsed;
        int phaseIndex = timeline.GetPhaseIndex(t);
        var phase = timeline.phases[phaseIndex];
        if (phaseIndex != currentPhase)
        {
            currentPhase = phaseIndex;
            swarmTimer = phase.swarm.interval * 0.5f;
        }

        float healthScale = timeline.GetHealthMultiplier(t);
        float damageScale = timeline.GetDamageMultiplier(t);
        int maxEnemies = timeline.GetMaxEnemies(t);

        UpdateSingleSpawns(phase, t, healthScale, damageScale, maxEnemies);
        UpdateSwarm(phase, healthScale, damageScale, maxEnemies);
        UpdateRings(t, healthScale, damageScale);
        RelocateStragglers();
    }

    private void UpdateSingleSpawns(DifficultyTimeline.Phase phase, float t, float healthScale, float damageScale, int maxEnemies)
    {
        spawnBudget = Mathf.Min(MaxSpawnBudget, spawnBudget + timeline.GetSpawnsPerSecond(t) * Time.deltaTime);

        while (spawnBudget >= 1f && Enemy.Active.Count < maxEnemies)
        {
            spawnBudget -= 1f;
            Spawn(PickPrefab(phase), GetSpawnPosition(), healthScale, damageScale);
        }
    }

    private void UpdateSwarm(DifficultyTimeline.Phase phase, float healthScale, float damageScale, int maxEnemies)
    {
        var swarm = phase.swarm;
        if (swarm == null || swarm.prefab == null) return;

        swarmTimer -= Time.deltaTime;
        if (swarmTimer > 0f) return;
        swarmTimer = swarm.interval;

        // Hepsi ekran dışında aynı noktanın etrafında doğar, birlikte gelir
        Vector2 center = GetSpawnPosition();
        int cap = Mathf.RoundToInt(maxEnemies * swarmCapAllowance);
        float spread = 0.4f + Mathf.Sqrt(swarm.count) * 0.22f;
        for (int i = 0; i < swarm.count && Enemy.Active.Count < cap; i++)
            Spawn(swarm.prefab, center + Random.insideUnitCircle * spread, healthScale, damageScale);
    }

    // Çember olayları düşman sınırını saymaz: kısa süreli gerilim zirveleri
    private void UpdateRings(float t, float healthScale, float damageScale)
    {
        foreach (var ring in timeline.ringEvents)
        {
            if (ring.prefab == null || firedRings.Contains(ring) || t < ring.minute * 60f) continue;
            firedRings.Add(ring);

            float radius = GetSpawnRadius() * 0.85f;
            for (int i = 0; i < ring.count; i++)
            {
                float angle = i * Mathf.PI * 2f / ring.count;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Spawn(ring.prefab, (Vector2)player.position + offset, healthScale, damageScale);
            }
        }
    }

    private void RelocateStragglers()
    {
        relocateTimer -= Time.deltaTime;
        if (relocateTimer > 0f) return;
        relocateTimer = RelocateCheckInterval;

        float limit = GetSpawnRadius() * relocateDistanceFactor;
        float limitSqr = limit * limit;
        Vector2 playerPos = player.position;

        foreach (var enemy in Enemy.Active)
        {
            if (!enemy.IsAlive) continue;
            if (((Vector2)enemy.transform.position - playerPos).sqrMagnitude <= limitSqr) continue;

            Vector2 position = GetSpawnPosition(aheadOfPlayer: true);
            if (enemy.TryGetComponent(out Rigidbody2D body))
                body.position = position;
            enemy.transform.position = position;
        }
    }

    private void Spawn(Enemy prefab, Vector2 position, float healthScale, float damageScale)
    {
        if (prefab == null) return;

        var enemy = Instantiate(prefab, position, Quaternion.identity);
        enemy.SetTarget(player);
        enemy.ApplyDifficulty(healthScale, damageScale);
    }

    // Fazın düşman ağırlıklarına göre rastgele seçim
    private static Enemy PickPrefab(DifficultyTimeline.Phase phase)
    {
        float total = 0f;
        foreach (var entry in phase.enemies)
        {
            if (entry.prefab != null) total += entry.weight;
        }
        if (total <= 0f) return null;

        float roll = Random.value * total;
        Enemy last = null;
        foreach (var entry in phase.enemies)
        {
            if (entry.prefab == null || entry.weight <= 0f) continue;
            last = entry.prefab;
            roll -= entry.weight;
            if (roll <= 0f) return entry.prefab;
        }
        return last;
    }

    // Kamera köşesine olan mesafe + pay = her zaman ekran dışı
    private float GetSpawnRadius()
    {
        float halfHeight = cam != null ? cam.orthographicSize : 5f;
        float halfWidth = cam != null ? halfHeight * cam.aspect : 9f;
        return new Vector2(halfWidth, halfHeight).magnitude + offscreenPadding;
    }

    private Vector2 GetSpawnPosition(bool aheadOfPlayer = false)
    {
        float angle;
        if (aheadOfPlayer && playerController != null)
        {
            // Oyuncunun yürüdüğü yönün ±70 derecesine
            Vector2 dir = playerController.LastMoveDirection;
            angle = Mathf.Atan2(dir.y, dir.x) + Random.Range(-70f, 70f) * Mathf.Deg2Rad;
        }
        else
        {
            angle = Random.value * Mathf.PI * 2f;
        }

        return (Vector2)player.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * GetSpawnRadius();
    }
}
