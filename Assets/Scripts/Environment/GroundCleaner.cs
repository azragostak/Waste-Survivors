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
            Debug.LogWarning("GroundCleaner: 'Ground' tilemap'i bulunamadı. Tools > Waste Survivors > Zemin Oluştur ile kurabilirsin.", this);
    }

    void Update()
    {
        if (dirtyTilemap == null) return;

        MarkCellsInRadius();
        UpdateFades();
    }

    private void MarkCellsInRadius()
    {
        Vector2 position = transform.position;
        Vector3Int center = dirtyTilemap.WorldToCell(position);
        Vector3 cellSize = dirtyTilemap.layoutGrid.cellSize;
        float radius = cleanRadius * (1f + PlayerPassives.GetBonus(this, PassiveStat.CleanRadius));
        int rangeX = Mathf.CeilToInt(radius / Mathf.Max(0.01f, cellSize.x)) + 1;
        int rangeY = Mathf.CeilToInt(radius / Mathf.Max(0.01f, cellSize.y)) + 1;
        float radiusSqr = radius * radius;

        for (int y = -rangeY; y <= rangeY; y++)
        {
            for (int x = -rangeX; x <= rangeX; x++)
            {
                var cell = new Vector3Int(center.x + x, center.y + y, center.z);
                if (fading.ContainsKey(cell) || !dirtyTilemap.HasTile(cell)) continue;

                Vector2 cellCenter = dirtyTilemap.GetCellCenterWorld(cell);
                if ((cellCenter - position).sqrMagnitude > radiusSqr) continue;

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
