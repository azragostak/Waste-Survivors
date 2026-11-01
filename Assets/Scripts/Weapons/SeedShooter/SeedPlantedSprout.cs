using System;
using System.Collections.Generic;
using UnityEngine;

// Tohum isabet etmiş bir düşman öldüğünde, öldüğü noktaya 001-008 PNG'lerinden rastgele bir bitki eker.
// Her 64x128 PNG (2 sütun x 4 satır = 8 kare, 32x32) çalışma zamanında 1 kez dilimlenip önbelleğe alınır.
// Büyüme animasyonu tamamlandığında bileşen devre dışı bırakılır (Update maliyeti = 0), çiçek sahnede kalır.
public class SeedPlantedSprout : MonoBehaviour
{
    private const int Columns = 2;
    private const int Rows = 4;
    private const int SortingOrderOnGround = -8; // Ground (-10) üstünde, RecyclePickup (-5) ve karakterlerin (0) altında
    private const int MaxActivePlants = 300;
    private const float PixelsPerUnit = 100f;

    // 2x4 sprite sheet içindeki 8 karenin doğal büyüme sırası:
    // 0: minik filiz -> 1: genç filiz -> 2: orta fide -> 7: tam yapraklı gövde (tomurcuk öncesi) ->
    // 3: ilk tomurcuk -> 4: gonca -> 5: açılan çiçek/meyve -> 6: tam çiçek açmış / olgun meyveli son hal.
    private static readonly int[] GrowthFrameOrder = { 0, 1, 2, 7, 3, 4, 5, 6 };

    private static readonly Dictionary<Texture2D, Sprite[]> frameCache = new();
    private static readonly HashSet<Enemy> seededEnemies = new();
    private static readonly Predicate<Enemy> isNullEnemy = e => e == null;
    private static readonly Queue<SeedPlantedSprout> activeRing = new();
    private static readonly Stack<SeedPlantedSprout> inactivePool = new();

    private static Texture2D[] registeredSheets;
    private static float registeredFps = 8f;
    private static float registeredBaseScale = 1.6f;

    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private int frameIndex;
    private float frameTimer;
    private float frameDuration;
    private float targetScale;
    private float popTimer;
    private const float PopDuration = 0.14f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        frameCache.Clear();
        seededEnemies.Clear();
        activeRing.Clear();
        inactivePool.Clear();
        registeredSheets = null;
    }

    public static void MarkEnemy(Enemy enemy, Texture2D[] plantSheets, float fps, float baseScale)
    {
        if (enemy == null || !enemy.IsAlive || plantSheets == null || plantSheets.Length == 0)
            return;

        registeredSheets = plantSheets;
        registeredFps = Mathf.Max(1f, fps);
        registeredBaseScale = Mathf.Max(0.1f, baseScale);

        if (seededEnemies.Count > 64)
            seededEnemies.RemoveWhere(isNullEnemy);

        if (seededEnemies.Add(enemy))
            enemy.Died += HandleSeededEnemyDied;
    }

    private static void HandleSeededEnemyDied(Enemy enemy)
    {
        seededEnemies.Remove(enemy);

        if (enemy == null || !enemy.gameObject.scene.isLoaded)
            return;

        Texture2D chosenSheet = PickRandomSheet(registeredSheets);
        if (chosenSheet == null)
            return;

        Sprite[] orderedFrames = GetOrCreateOrderedFrames(chosenSheet);
        if (orderedFrames == null || orderedFrames.Length == 0)
            return;

        Vector2 jitter = UnityEngine.Random.insideUnitCircle * 0.08f;
        Vector2 plantPos = (Vector2)enemy.transform.position + jitter;
        SpawnPlant(plantPos, orderedFrames, registeredFps, registeredBaseScale);
    }

    private static Texture2D PickRandomSheet(Texture2D[] sheets)
    {
        if (sheets == null || sheets.Length == 0)
            return null;

        int start = UnityEngine.Random.Range(0, sheets.Length);
        for (int i = 0; i < sheets.Length; i++)
        {
            var candidate = sheets[(start + i) % sheets.Length];
            if (candidate != null)
                return candidate;
        }
        return null;
    }

    private static Sprite[] GetOrCreateOrderedFrames(Texture2D sheet)
    {
        if (frameCache.TryGetValue(sheet, out var cached) && cached != null && cached.Length > 0 && cached[0] != null)
            return cached;

        // Piksel sanatının bulanıklaşmaması için Point filtreleme
        if (sheet.filterMode != FilterMode.Point)
            sheet.filterMode = FilterMode.Point;

        int cellWidth = sheet.width / Columns;
        int cellHeight = sheet.height / Rows;
        if (cellWidth <= 0 || cellHeight <= 0)
            return null;

        var rawCells = new Sprite[Columns * Rows];
        var rootPivot = new Vector2(0.5f, 0.12f);

        for (int rowFromTop = 0; rowFromTop < Rows; rowFromTop++)
        {
            int y = sheet.height - (rowFromTop + 1) * cellHeight;
            for (int col = 0; col < Columns; col++)
            {
                int x = col * cellWidth;
                int index = rowFromTop * Columns + col;
                rawCells[index] = Sprite.Create(
                    sheet,
                    new Rect(x, y, cellWidth, cellHeight),
                    rootPivot,
                    PixelsPerUnit
                );
            }
        }

        var ordered = new Sprite[GrowthFrameOrder.Length];
        for (int i = 0; i < GrowthFrameOrder.Length; i++)
            ordered[i] = rawCells[GrowthFrameOrder[i]];

        frameCache[sheet] = ordered;
        return ordered;
    }

    private static void SpawnPlant(Vector2 position, Sprite[] orderedFrames, float fps, float baseScale)
    {
        while (activeRing.Count > 0 && activeRing.Peek() == null)
            activeRing.Dequeue();

        SeedPlantedSprout sprout = null;
        while (inactivePool.Count > 0 && sprout == null)
            sprout = inactivePool.Pop();

        if (sprout == null && activeRing.Count >= MaxActivePlants)
            sprout = activeRing.Dequeue();

        if (sprout == null)
        {
            var go = new GameObject("PlantedSprout");
            sprout = go.AddComponent<SeedPlantedSprout>();
            sprout.InitializeRenderer();
        }

        activeRing.Enqueue(sprout);
        sprout.PlayGrowth(position, orderedFrames, fps, baseScale);
    }

    private void InitializeRenderer()
    {
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = SortingOrderOnGround;
    }

    private void PlayGrowth(Vector2 position, Sprite[] orderedFrames, float fps, float baseScale)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        frames = orderedFrames;
        frameIndex = 0;
        frameTimer = 0f;
        frameDuration = 1f / Mathf.Max(1f, fps);
        targetScale = baseScale * UnityEngine.Random.Range(0.9f, 1.12f);
        popTimer = 0f;

        transform.position = new Vector3(position.x, position.y, 0f);
        transform.localScale = Vector3.one * (targetScale * 0.65f);

        spriteRenderer.flipX = UnityEngine.Random.value < 0.5f;
        spriteRenderer.sprite = frames[0];

        gameObject.SetActive(true);
        enabled = true;
    }

    void Update()
    {
        if (frames == null || frames.Length == 0)
        {
            enabled = false;
            return;
        }

        float dt = Time.deltaTime;

        if (popTimer < PopDuration)
        {
            popTimer += dt;
            float p = Mathf.Clamp01(popTimer / PopDuration);
            // Hafif elastik filizlenme ölçeği
            float scaleFactor = Mathf.Lerp(0.75f, 1.08f, p);
            if (p >= 1f) scaleFactor = 1f;
            transform.localScale = Vector3.one * (targetScale * scaleFactor);
        }

        frameTimer += dt;
        while (frameTimer >= frameDuration && frameIndex < frames.Length - 1)
        {
            frameTimer -= frameDuration;
            frameIndex++;
            spriteRenderer.sprite = frames[frameIndex];

            // Son çiçeklenme karesine ulaştığında ufak bir açılma efekti ver
            if (frameIndex == frames.Length - 1)
                popTimer = 0f;
        }

        // Son kareye ulaşıp açılma animasyonu bittiğinde Update'i kapat (performans için 0 CPU yükü)
        if (frameIndex >= frames.Length - 1 && popTimer >= PopDuration)
        {
            transform.localScale = Vector3.one * targetScale;
            enabled = false;
        }
    }
}

