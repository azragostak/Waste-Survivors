using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 10. dakikada (ya da F9 ile) çalışan final. GameSession ekler.
//  1. Oyuncudan dışarı doğru yeşil bir temizlik dalgası yayılır: dalganın geçtiği yerde düşmanlar ve
//     yerdeki recycle'lar kaybolur, kirli zemin temizlenir. Asit yağmuru diner.
//  2. Ekran dışı da dahil her yer temiz olur (sonsuz zeminin yeni yüklenen parçaları da).
//  3. Etrafta bitkisi az olan yerlere, dalga gibi içten dışa tohumlar filizlenir.
//  4. Zafer ekranı açılır. Oyuncu temizlenen dünyada dolaşmaya devam edebilir.
public class RestorationFinale : MonoBehaviour
{
    private const float WaveSpeed = 9f;            // birim/sn
    private const float WaveScreenMargin = 3f;     // dalga ekran kenarının ne kadar dışına kadar gitsin
    private const float RainFadeDuration = 2.5f;
    private const float PlantCellSize = 1.1f;      // bitki ızgarası
    private const float PlantCheckRadius = 0.9f;   // bu yarıçapta bitki varsa oraya ekilmez
    private const float PlantWaveSpeed = 7f;
    private const float PlantViewMargin = 1.5f;
    private const string CleanTilemapName = "GroundClean";
    private const string WaterTileName = "GroundClean_2"; // suya bitki ekilmez

    private struct PendingPlant
    {
        public Vector2 position;
        public float delay;
    }

    private IEnumerator Start()
    {
        var player = FindObjectOfType<PlayerController>();
        Vector2 center = player != null ? (Vector2)player.transform.position : Vector2.zero;

        if (player != null && player.TryGetComponent(out PlayerHealth health))
            health.IsInvulnerable = true;

        var rain = FindObjectOfType<AcidRain>();
        if (rain != null)
            StartCoroutine(FadeRain(rain));

        if (MusicPlayer.Instance != null)
            MusicPlayer.Instance.PlayOutro();

        yield return Wave(center);

        // Ekran dışında kalanlar da gitsin, sonsuz zeminin geri kalanı temiz yüklensin
        foreach (var enemy in new List<Enemy>(Enemy.Active))
            enemy.Vanish();
        ClearPickups(center, float.MaxValue);

        var cleaner = FindObjectOfType<GroundCleaner>();
        yield return new WaitForSeconds(0.5f); // son temizlenen karelerin solması bitsin
        var looper = FindObjectOfType<InfiniteGroundLooper>();
        if (looper != null)
            looper.SetAllClean();
        if (cleaner != null)
            cleaner.enabled = false;

        yield return SpreadSeeds(player != null ? (Vector2)player.transform.position : center);
        yield return new WaitForSeconds(0.8f);

        ShowVictory();
    }

    private IEnumerator Wave(Vector2 center)
    {
        var cam = Camera.main;
        float halfHeight = cam != null ? cam.orthographicSize : 5f;
        float halfWidth = cam != null ? halfHeight * cam.aspect : 9f;
        float maxRadius = new Vector2(halfWidth, halfHeight).magnitude + WaveScreenMargin;

        var cleaner = FindObjectOfType<GroundCleaner>();
        var ring = CreateWaveRing();
        var enemies = new List<Enemy>();

        float radius = 0f;
        while (radius < maxRadius)
        {
            float previous = radius;
            radius = Mathf.Min(maxRadius, radius + WaveSpeed * Time.deltaTime);

            if (cleaner != null)
                cleaner.CleanRing(center, previous, radius);

            Enemy.GetInRadius(center, radius, enemies);
            foreach (var enemy in enemies)
                enemy.Vanish();

            ClearPickups(center, radius);
            UpdateWaveRing(ring, center, radius, 1f - radius / maxRadius);
            yield return null;
        }

        Destroy(ring.gameObject);
    }

    // Recycle'lar toplanmadan kaybolur (toplanırsa level-up ekranı açılıp finali durdururdu)
    private static void ClearPickups(Vector2 center, float radius)
    {
        float radiusSqr = radius * radius;
        for (int i = RecyclePickup.Active.Count - 1; i >= 0; i--)
        {
            var pickup = RecyclePickup.Active[i];
            if (pickup != null && ((Vector2)pickup.transform.position - center).sqrMagnitude <= radiusSqr)
                Destroy(pickup.gameObject);
        }
    }

    private IEnumerator SpreadSeeds(Vector2 center)
    {
        if (!EnsurePlantSheets()) yield break;

        var cam = Camera.main;
        float halfHeight = (cam != null ? cam.orthographicSize : 5f) + PlantViewMargin;
        float halfWidth = (cam != null ? cam.orthographicSize * cam.aspect : 9f) + PlantViewMargin;
        Vector2 viewCenter = cam != null ? (Vector2)cam.transform.position : center;
        var cleanTilemap = FindTilemap(CleanTilemapName);

        // Bitkisi az olan noktaları bul, merkezden uzaklığa göre gecikmeyle sırala
        var pending = new List<PendingPlant>();
        for (float y = -halfHeight; y <= halfHeight; y += PlantCellSize)
        {
            for (float x = -halfWidth; x <= halfWidth; x += PlantCellSize)
            {
                Vector2 point = viewCenter + new Vector2(x, y) + Random.insideUnitCircle * (PlantCellSize * 0.4f);
                if (SeedPlantedSprout.CountPlantsNear(point, PlantCheckRadius) > 0) continue;
                if (IsWater(cleanTilemap, point)) continue;

                float delay = Vector2.Distance(point, center) / PlantWaveSpeed + Random.Range(0f, 0.25f);
                pending.Add(new PendingPlant { position = point, delay = delay });
            }
        }
        pending.Sort((a, b) => a.delay.CompareTo(b.delay));

        float elapsed = 0f;
        int next = 0;
        while (next < pending.Count)
        {
            elapsed += Time.deltaTime;
            while (next < pending.Count && pending[next].delay <= elapsed)
            {
                SeedPlantedSprout.PlantAt(pending[next].position, ignoreCap: true);
                next++;
            }
            yield return null;
        }
    }

    // Oyuncu tohum silahını hiç almamışsa bitki görsellerini UpgradePool'daki tohum silahından al
    private static bool EnsurePlantSheets()
    {
        if (SeedPlantedSprout.HasPlantSheets) return true;

        var pool = Resources.Load<UpgradePool>(UpgradePool.ResourcePath);
        if (pool == null) return false;

        foreach (var weapon in pool.weapons)
        {
            if (weapon == null || weapon.weaponPrefab is not SeedWeapon seedWeapon || seedWeapon.SeedPrefab == null)
                continue;

            var projectile = seedWeapon.SeedPrefab;
            SeedPlantedSprout.RegisterSheets(projectile.PlantSheets, projectile.PlantFramesPerSecond, projectile.PlantBaseScale);
            if (SeedPlantedSprout.HasPlantSheets) return true;
        }

        Debug.LogWarning("RestorationFinale: Bitki görselleri bulunamadı, final ekimi atlandı.");
        return false;
    }

    private static bool IsWater(Tilemap tilemap, Vector2 point)
    {
        if (tilemap == null) return false;
        var tile = tilemap.GetTile(tilemap.WorldToCell(point));
        return tile != null && tile.name == WaterTileName;
    }

    private static Tilemap FindTilemap(string tilemapName)
    {
        foreach (var tilemap in FindObjectsOfType<Tilemap>())
        {
            if (tilemap.name == tilemapName) return tilemap;
        }
        return null;
    }

    private static IEnumerator FadeRain(AcidRain rain)
    {
        float start = rain.Intensity;
        for (float t = 0f; t < RainFadeDuration && rain != null; t += Time.deltaTime)
        {
            rain.Intensity = Mathf.Lerp(start, 0f, t / RainFadeDuration);
            yield return null;
        }
        if (rain != null) rain.Intensity = 0f;
    }

    // Dalganın ön cephesi: genişleyen, gittikçe silinen yeşil halka
    private static LineRenderer CreateWaveRing()
    {
        var go = new GameObject("RestorationWave");
        var line = go.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = true;
        line.positionCount = 72;
        line.widthMultiplier = 0.18f;
        line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        line.sortingOrder = 90;
        return line;
    }

    private static void UpdateWaveRing(LineRenderer line, Vector2 center, float radius, float alpha)
    {
        var color = new Color(0.55f, 1f, 0.5f, 0.85f * alpha);
        line.startColor = color;
        line.endColor = color;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    private static void ShowVictory()
    {
        var session = GameSession.Instance;
        var experience = FindObjectOfType<PlayerExperience>();
        var cleaner = FindObjectOfType<GroundCleaner>();

        string stats =
            $"Time {GameSession.FormatTime(session != null ? session.Elapsed : 0f)}" +
            $"     Level {(experience != null ? experience.Level : 1)}" +
            $"     Enemies defeated {(session != null ? session.Kills : 0)}" +
            $"     Area cleaned {(cleaner != null ? cleaner.CleanedTiles : 0)} tiles";

        EndScreen.Show("THE WORLD IS CLEAN!", "The pollution is gone. Nature is back.", new Color(0.55f, 1f, 0.5f), stats);
    }
}
