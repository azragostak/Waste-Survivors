using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Oyuncunun yürüdüğü yerlerdeki kirli zemini (Ground tilemap'i) silip altındaki temiz zemini (GroundClean) ortaya çıkarır.
// Player objesine eklenir. Temizlenen yer kalıcı olarak temiz kalır.
public class GroundCleaner : MonoBehaviour
{
    [Tooltip("Boşsa sahnede 'Ground' isimli Tilemap aranır")]
    [SerializeField] private Tilemap dirtyTilemap;
    [Tooltip("Bu yarıçap içindeki kirli tile'lar temizlenir (dünya birimi, 1 tile = 0.32)")]
    [SerializeField, Min(0f)] private float cleanRadius = 0.6f;
    [Tooltip("Kirli tile'ın silinirken kaybolma süresi (sn)")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.35f;

    // Hücre -> fade ilerlemesi (0-1)
    private readonly Dictionary<Vector3Int, float> fading = new();
    private readonly List<Vector3Int> fadingCells = new();

    // Temizlik yüzdesi, skor vs. için
    public int CleanedTiles { get; private set; }
    public event Action<Vector3Int> TileCleaned;

    public bool IsCellFading(Vector3Int cell) => fading.ContainsKey(cell);

    // Level-up ödülü olarak büyütülebilir
    public float CleanRadius
    {
        get => cleanRadius;
        set => cleanRadius = Mathf.Max(0f, value);
    }

    void Start()
    {
        if (dirtyTilemap == null)
        {
            foreach (var tilemap in FindObjectsOfType<Tilemap>())
            {
                if (tilemap.name == "Ground")
                {
                    dirtyTilemap = tilemap;
                    break;
                }
            }
        }

        if (dirtyTilemap == null)
        {
            Debug.LogWarning("GroundCleaner: 'Ground' tilemap'i bulunamadı. Tools > Waste Survivors > Zemin Oluştur ile kurabilirsin.", this);
            return;
        }

        // Haritanın hiç bitmemesi için sonsuz zemin döngüsünü otomatik devreye al
        InfiniteGroundLooper.EnsureAttached(dirtyTilemap, this);
    }

    void Update()
    {
        if (dirtyTilemap == null) return;

        MarkCellsInRadius();
        UpdateFades();
    }

    private void MarkCellsInRadius()
    {
        float radius = cleanRadius * (1f + PlayerPassives.GetBonus(this, PassiveStat.CleanRadius));
        MarkCells(transform.position, radius, 0f);
    }

    // Final: center etrafında innerRadius ile radius arasındaki halkayı temizler.
    // Büyüyen bir dalga için her karede önceki yarıçap innerRadius olarak verilir, içerisi tekrar taranmaz.
    public void CleanRing(Vector2 center, float innerRadius, float radius)
    {
        if (dirtyTilemap == null) return;
        MarkCells(center, radius, innerRadius);
    }

    private void MarkCells(Vector2 position, float radius, float innerRadius)
    {
        Vector3Int center = dirtyTilemap.WorldToCell(position);
        Vector3 cellSize = dirtyTilemap.layoutGrid.cellSize;
        int rangeX = Mathf.CeilToInt(radius / Mathf.Max(0.01f, cellSize.x)) + 1;
        int rangeY = Mathf.CeilToInt(radius / Mathf.Max(0.01f, cellSize.y)) + 1;
        float radiusSqr = radius * radius;
        float innerSqr = innerRadius > 0f ? innerRadius * innerRadius : -1f;

        for (int y = -rangeY; y <= rangeY; y++)
        {
            for (int x = -rangeX; x <= rangeX; x++)
            {
                var cell = new Vector3Int(center.x + x, center.y + y, center.z);

                Vector2 cellCenter = dirtyTilemap.GetCellCenterWorld(cell);
                float distanceSqr = (cellCenter - position).sqrMagnitude;
                if (distanceSqr > radiusSqr || distanceSqr < innerSqr) continue;
                if (fading.ContainsKey(cell)) continue;

                TileBase tile = dirtyTilemap.GetTile(cell);
                if (tile == null) continue;

                // Tile asset'leri rengi kilitler; hücre bazında açmazsak SetColor çalışmaz
                dirtyTilemap.SetTileFlags(cell, TileFlags.None);
                fading.Add(cell, 0f);
                fadingCells.Add(cell);
            }
        }
    }

    private void UpdateFades()
    {
        float step = fadeDuration > 0f ? Time.deltaTime / fadeDuration : 1f;

        for (int i = fadingCells.Count - 1; i >= 0; i--)
        {
            var cell = fadingCells[i];
            float t = fading[cell] + step;

            if (t < 1f)
            {
                fading[cell] = t;
                dirtyTilemap.SetColor(cell, new Color(1f, 1f, 1f, 1f - t));
                continue;
            }

            dirtyTilemap.SetTile(cell, null);
            fading.Remove(cell);
            fadingCells.RemoveAt(i);
            CleanedTiles++;
            TileCleaned?.Invoke(cell);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, cleanRadius);
    }
}
