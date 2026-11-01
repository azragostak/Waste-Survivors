using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Mevcut Ground (kirli) ve GroundClean (temiz) Tilemap desenini şablon olarak alır ve
// kamera hareket ettikçe 16x16 parça (chunk) halinde her yöne dikişsiz (ayna-döngü) olarak sonsuza dek döşer.
// Oyuncunun temizlediği kareler hafızada tutulur; uzaklaşıp geri dönülse bile temiz kalır.
[DefaultExecutionOrder(-40)]
public class InfiniteGroundLooper : MonoBehaviour
{
    private const int ChunkSize = 16;

    [Tooltip("Kirli zemin katmanı (boşsa 'Ground' aranır)")]
    [SerializeField] private Tilemap dirtyTilemap;

    [Tooltip("Temiz zemin katmanı (boşsa 'GroundClean' aranır)")]
    [SerializeField] private Tilemap cleanTilemap;

    [Tooltip("Takip edilecek kamera (boşsa Camera.main kullanılır)")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Ekran kenarının ne kadar dışına kadar (dünya birimi) zemin hazır tutulsun")]
    [SerializeField, Min(2f)] private float viewPaddingWorld = 6f;

    [Tooltip("Yükleme sınırının kaç chunk dışına çıkılınca eski chunk bellekten boşaltılsın")]
    [SerializeField, Min(1)] private int unloadPaddingChunks = 2;

    private GroundCleaner cleaner;
    private TileBase[] dirtyTemplate;
    private TileBase[] cleanTemplate;
    private int templateOriginX;
    private int templateOriginY;
    private int templateWidth;
    private int templateHeight;

    private TileBase[] dirtyChunkBuffer;
    private TileBase[] cleanChunkBuffer;
    private TileBase[] emptyChunkBuffer;

    private readonly HashSet<Vector2Int> loadedChunks = new();
    private readonly List<Vector2Int> chunksToUnload = new();
    private readonly HashSet<Vector2Int> cleanedCells = new();

    private int lastMinChunkX = int.MaxValue;
    private int lastMaxChunkX = int.MinValue;
    private int lastMinChunkY = int.MaxValue;
    private int lastMaxChunkY = int.MinValue;
    private bool initialized;

    public static InfiniteGroundLooper EnsureAttached(Tilemap dirtyMap, GroundCleaner groundCleaner)
    {
        if (dirtyMap == null) return null;

        var existing = FindObjectOfType<InfiniteGroundLooper>();
        if (existing != null)
        {
            existing.Setup(dirtyMap, groundCleaner);
            return existing;
        }

        GameObject host = dirtyMap.layoutGrid != null ? dirtyMap.layoutGrid.gameObject : dirtyMap.gameObject;
        var looper = host.AddComponent<InfiniteGroundLooper>();
        looper.Setup(dirtyMap, groundCleaner);
        return looper;
    }

    private void Setup(Tilemap dirtyMap, GroundCleaner groundCleaner)
    {
        if (dirtyTilemap == null)
            dirtyTilemap = dirtyMap;

        if (cleaner != groundCleaner)
        {
            if (cleaner != null)
                cleaner.TileCleaned -= HandleTileCleaned;

            cleaner = groundCleaner;
            if (cleaner != null)
                cleaner.TileCleaned += HandleTileCleaned;
        }

        if (!initialized)
            Initialize();
    }

    void Start()
    {
        if (!initialized)
            Initialize();
    }

    void OnDestroy()
    {
        if (cleaner != null)
            cleaner.TileCleaned -= HandleTileCleaned;
    }

    private void Initialize()
    {
        if (initialized) return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (dirtyTilemap == null || cleanTilemap == null)
        {
            foreach (var tm in FindObjectsOfType<Tilemap>())
            {
                if (dirtyTilemap == null && tm.name == "Ground")
                    dirtyTilemap = tm;
                else if (cleanTilemap == null && tm.name == "GroundClean")
                    cleanTilemap = tm;
            }
        }

        if (cleaner == null)
        {
            cleaner = FindObjectOfType<GroundCleaner>();
            if (cleaner != null)
                cleaner.TileCleaned += HandleTileCleaned;
        }

        Tilemap referenceMap = cleanTilemap != null ? cleanTilemap : dirtyTilemap;
        if (referenceMap == null)
            return;

        referenceMap.CompressBounds();
        if (dirtyTilemap != null && dirtyTilemap != referenceMap)
            dirtyTilemap.CompressBounds();

        BoundsInt sourceBounds = referenceMap.cellBounds;
        if (sourceBounds.size.x <= 0 || sourceBounds.size.y <= 0)
        {
            if (dirtyTilemap != null && dirtyTilemap.cellBounds.size.x > 0 && dirtyTilemap.cellBounds.size.y > 0)
                sourceBounds = dirtyTilemap.cellBounds;
            else
                return;
        }

        templateOriginX = sourceBounds.xMin;
        templateOriginY = sourceBounds.yMin;
        templateWidth = sourceBounds.size.x;
        templateHeight = sourceBounds.size.y;

        dirtyTemplate = dirtyTilemap != null ? dirtyTilemap.GetTilesBlock(sourceBounds) : null;
        cleanTemplate = cleanTilemap != null ? cleanTilemap.GetTilesBlock(sourceBounds) : null;

        int blockLen = ChunkSize * ChunkSize;
        dirtyChunkBuffer = new TileBase[blockLen];
        cleanChunkBuffer = new TileBase[blockLen];
        emptyChunkBuffer = new TileBase[blockLen];

        // Sabit başlangıç karolarını temizleyip tüm zemini dinamik sonsuz chunk yönetimi altına al
        if (dirtyTilemap != null) dirtyTilemap.ClearAllTiles();
        if (cleanTilemap != null) cleanTilemap.ClearAllTiles();

        initialized = true;
        RefreshChunks(force: true);
    }

    void LateUpdate()
    {
        if (!initialized) return;
        RefreshChunks(force: false);
    }

    private void HandleTileCleaned(Vector3Int cell)
    {
        cleanedCells.Add(new Vector2Int(cell.x, cell.y));
    }

    private void RefreshChunks(bool force)
    {
        Tilemap refMap = dirtyTilemap != null ? dirtyTilemap : cleanTilemap;
        if (refMap == null) return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        Vector3 centerPos;
        float halfHeight;
        float halfWidth;

        if (targetCamera != null)
        {
            centerPos = targetCamera.transform.position;
            halfHeight = targetCamera.orthographicSize + viewPaddingWorld;
            halfWidth = targetCamera.orthographicSize * targetCamera.aspect + viewPaddingWorld;
        }
        else if (cleaner != null)
        {
            centerPos = cleaner.transform.position;
            halfHeight = 6f + viewPaddingWorld;
            halfWidth = 11f + viewPaddingWorld;
        }
        else
        {
            centerPos = Vector3.zero;
            halfHeight = 6f + viewPaddingWorld;
            halfWidth = 11f + viewPaddingWorld;
        }

        Vector3Int minCell = refMap.WorldToCell(new Vector3(centerPos.x - halfWidth, centerPos.y - halfHeight, 0f));
        Vector3Int maxCell = refMap.WorldToCell(new Vector3(centerPos.x + halfWidth, centerPos.y + halfHeight, 0f));

        int minChunkX = FloorDiv(minCell.x, ChunkSize);
        int maxChunkX = FloorDiv(maxCell.x, ChunkSize);
        int minChunkY = FloorDiv(minCell.y, ChunkSize);
        int maxChunkY = FloorDiv(maxCell.y, ChunkSize);

        if (!force &&
            minChunkX == lastMinChunkX && maxChunkX == lastMaxChunkX &&
            minChunkY == lastMinChunkY && maxChunkY == lastMaxChunkY)
        {
            return;
        }

        lastMinChunkX = minChunkX;
        lastMaxChunkX = maxChunkX;
        lastMinChunkY = minChunkY;
        lastMaxChunkY = maxChunkY;

        // 1. Uzakta kalan eski chunk'ları boşalt (bellek sabit kalsın)
        int unloadMinX = minChunkX - unloadPaddingChunks;
        int unloadMaxX = maxChunkX + unloadPaddingChunks;
        int unloadMinY = minChunkY - unloadPaddingChunks;
        int unloadMaxY = maxChunkY + unloadPaddingChunks;

        chunksToUnload.Clear();
        foreach (var chunk in loadedChunks)
        {
            if (chunk.x < unloadMinX || chunk.x > unloadMaxX ||
                chunk.y < unloadMinY || chunk.y > unloadMaxY)
            {
                chunksToUnload.Add(chunk);
            }
        }

        for (int i = 0; i < chunksToUnload.Count; i++)
        {
            Vector2Int chunk = chunksToUnload[i];
            UnloadChunk(chunk);
            loadedChunks.Remove(chunk);
        }

        // 2. Görüş alanına yeni giren chunk'ları dikişsiz döngüyle doldur
        for (int cy = minChunkY; cy <= maxChunkY; cy++)
        {
            for (int cx = minChunkX; cx <= maxChunkX; cx++)
            {
                var key = new Vector2Int(cx, cy);
                if (loadedChunks.Add(key))
                    LoadChunk(cx, cy);
            }
        }
    }

    private void LoadChunk(int chunkX, int chunkY)
    {
        int startX = chunkX * ChunkSize;
        int startY = chunkY * ChunkSize;
        var bounds = new BoundsInt(startX, startY, 0, ChunkSize, ChunkSize, 1);

        bool hasDirty = dirtyTilemap != null && dirtyTemplate != null && dirtyTemplate.Length == templateWidth * templateHeight;
        bool hasClean = cleanTilemap != null && cleanTemplate != null && cleanTemplate.Length == templateWidth * templateHeight;

        for (int ly = 0; ly < ChunkSize; ly++)
        {
            int wy = startY + ly;
            int ty = WrapMirrorIndex(wy - templateOriginY, templateHeight);
            int templateRowOffset = ty * templateWidth;
            int bufferRowOffset = ly * ChunkSize;

            for (int lx = 0; lx < ChunkSize; lx++)
            {
                int wx = startX + lx;
                int tx = WrapMirrorIndex(wx - templateOriginX, templateWidth);
                int templateIndex = templateRowOffset + tx;
                int bufferIndex = bufferRowOffset + lx;

                if (hasClean)
                    cleanChunkBuffer[bufferIndex] = cleanTemplate[templateIndex];

                if (hasDirty)
                {
                    bool alreadyCleaned = cleanedCells.Contains(new Vector2Int(wx, wy)) ||
                                          (cleaner != null && cleaner.IsCellFading(new Vector3Int(wx, wy, 0)));
                    dirtyChunkBuffer[bufferIndex] = alreadyCleaned ? null : dirtyTemplate[templateIndex];
                }
            }
        }

        if (hasClean)
            cleanTilemap.SetTilesBlock(bounds, cleanChunkBuffer);

        if (hasDirty)
            dirtyTilemap.SetTilesBlock(bounds, dirtyChunkBuffer);
    }

    private void UnloadChunk(Vector2Int chunk)
    {
        var bounds = new BoundsInt(chunk.x * ChunkSize, chunk.y * ChunkSize, 0, ChunkSize, ChunkSize, 1);

        if (cleanTilemap != null)
            cleanTilemap.SetTilesBlock(bounds, emptyChunkBuffer);

        if (dirtyTilemap != null)
            dirtyTilemap.SetTilesBlock(bounds, emptyChunkBuffer);
    }

    // Negatif koordinatlarda doğru taban bölme (-1 / 16 == -1)
    private static int FloorDiv(int a, int b) =>
        a >= 0 ? a / b : (a - b + 1) / b;

    // 0..N-1..N-1..0 şeklinde ayna döngüsü: harita tekrar ederken su/çalı geçişlerinde keskin çizgi oluşmaz
    private static int WrapMirrorIndex(int offset, int length)
    {
        if (length <= 1) return 0;
        int period = length * 2;
        int mod = offset % period;
        if (mod < 0) mod += period;
        return mod < length ? mod : (period - 1 - mod);
    }
}

