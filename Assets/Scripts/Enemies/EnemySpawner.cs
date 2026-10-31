using UnityEngine;

// Basit test spawner'ı: ekranın hemen dışında, oyuncunun etrafında rastgele düşman doğurur.
public class EnemySpawner : MonoBehaviour
{
    [Tooltip("Her spawn'da bunlardan biri, düşmanın Spawn Weight'ine göre rastgele seçilir")]
    [SerializeField] private Enemy[] enemyPrefabs;
    [SerializeField] private Transform player;
    [SerializeField] private float spawnInterval = 1f;
    [SerializeField] private int enemiesPerSpawn = 2;
    [SerializeField] private int maxEnemies = 150;

    [Tooltip("Ekran kenarının ne kadar dışında doğsunlar")]
    [SerializeField] private float offscreenPadding = 1.5f;

    private Camera cam;
    private float timer;

    void Start()
    {
        cam = Camera.main;
        if (player == null)
        {
            var playerController = FindObjectOfType<PlayerController>();
            if (playerController != null) player = playerController.transform;
        }
    }

    void Update()
    {
        if (player == null || enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = spawnInterval;

        for (int i = 0; i < enemiesPerSpawn && Enemy.Active.Count < maxEnemies; i++)
            Spawn();
    }

    private void Spawn()
    {
        var prefab = PickPrefab();
        if (prefab == null) return;

        var enemy = Instantiate(prefab, GetSpawnPosition(), Quaternion.identity);
        enemy.SetTarget(player);
    }

    // Her düşmanın Spawn Weight'ine göre ağırlıklı rastgele seçim
    private Enemy PickPrefab()
    {
        float total = 0f;
        foreach (var prefab in enemyPrefabs)
        {
            if (prefab != null) total += prefab.SpawnWeight;
        }
        if (total <= 0f) return null;

        float roll = Random.value * total;
        foreach (var prefab in enemyPrefabs)
        {
            if (prefab == null) continue;
            roll -= prefab.SpawnWeight;
            if (roll <= 0f) return prefab;
        }

        // Kayan nokta yuvarlaması için son geçerli prefab
        for (int i = enemyPrefabs.Length - 1; i >= 0; i--)
        {
            if (enemyPrefabs[i] != null && enemyPrefabs[i].SpawnWeight > 0f) return enemyPrefabs[i];
        }
        return null;
    }

    private Vector2 GetSpawnPosition()
    {
        // Kamera köşesine olan mesafe + pay = her zaman ekran dışı
        float halfHeight = cam != null ? cam.orthographicSize : 5f;
        float halfWidth = cam != null ? halfHeight * cam.aspect : 9f;
        float radius = new Vector2(halfWidth, halfHeight).magnitude + offscreenPadding;

        float angle = Random.value * Mathf.PI * 2f;
        return (Vector2)player.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }
}
