using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Tools > Waste Survivors > Zemin Oluştur: kirli ve temiz tile aseprite'larının her frame'inden bir Tile asset'i yapar,
// açık sahnede Grid altında iki tilemap kurar ve orijin merkezli Size x Size alanı boyar:
//   Ground      (üstte) kirli dünya - oyuncu yürüdükçe GroundCleaner bunu siler
//   GroundClean (altta) temiz dünya - kirli tile silinince ortaya çıkar
// Frame_0 ana zemindir; diğer frame'ler Perlin noise ile doğal görünen lekeler/bölgeler halinde dağılır.
// İki katman aynı deseni kullanır: çöp yığınının altından çalılık, kirli suyun altından temiz su çıkar.
// Her çalıştırmada bölgeler yeniden rastgele dağılır. Var olan Grid/tilemap'leri kullanır, Ctrl+Z ile geri alınabilir.
public static class GroundTilemapBuilder
{
    private const string DirtyTileGuid = "ca5c832eda33c8348b1d1bd8b4fcc139"; // Assets/Sprites/tile.aseprite
    private const string CleanTilePath = "Assets/Sprites/tile_clean.aseprite";
    private const string TileFolder = "Assets/Tiles";
    private const string DirtyTilemapName = "Ground";
    private const string CleanTilemapName = "GroundClean";
    private const int Size = 100;
    private const int DirtySortingOrder = -10; // karakterlerin ve asit sıçramalarının altında
    private const int CleanSortingOrder = -11; // kirli zeminin altında

    // Küçüldükçe bölgeler büyür (0.08 ≈ 10-15 tile genişliğinde lekeler)
    private const float RegionScale = 0.08f;
    // Frame_1, Frame_2... için: noise bu değerin üstündeyse o frame kullanılır. Büyüdükçe bölgeler seyrekleşir.
    private const float RegionThreshold = 0.62f;

    [MenuItem("Tools/Waste Survivors/Zemin Oluştur (100x100)")]
    private static void Build()
    {
        string dirtyPath = AssetDatabase.GUIDToAssetPath(DirtyTileGuid);
        var dirtySprites = LoadSprites(dirtyPath);
        if (dirtySprites.Length == 0)
        {
            Debug.LogError(string.IsNullOrEmpty(dirtyPath)
                ? $"Zemin: {DirtyTileGuid} GUID'li tile aseprite'ı projede bulunamadı."
                : $"Zemin: {dirtyPath} içinde sprite bulunamadı.");
            return;
        }

        var cleanSprites = LoadSprites(CleanTilePath);
        if (cleanSprites.Length == 0)
            Debug.LogWarning($"Zemin: {CleanTilePath} bulunamadı, sadece kirli zemin kuruluyor.");

        var dirtyTiles = dirtySprites.Select((sprite, i) => GetOrCreateTile(sprite, $"Ground_{i}")).ToArray();
        var cleanTiles = cleanSprites.Select((sprite, i) => GetOrCreateTile(sprite, $"GroundClean_{i}")).ToArray();
        AssetDatabase.SaveAssets();

        var grid = GetOrCreateGrid();

        // Hücre boyutu = sprite'ın dünya boyutu, böylece karakterle aynı piksel yoğunluğunda ve boşluksuz döşenir
        var reference = dirtySprites[0];
        Vector2 worldSize = reference.rect.size / reference.pixelsPerUnit;
        Undo.RecordObject(grid, "Zemin Oluştur");
        grid.cellSize = new Vector3(worldSize.x, worldSize.y, 0f);

        // Bölgeler bir kez seçilir, iki katman da aynı hücrede aynı frame'i kullanır
        var frames = new int[Size * Size];
        var counts = new int[dirtyTiles.Length];
        var offsets = Enumerable.Range(0, dirtyTiles.Length)
            .Select(_ => new Vector2(Random.Range(0f, 10000f), Random.Range(0f, 10000f)))
            .ToArray();

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int frame = PickFrame(x, y, dirtyTiles.Length, offsets);
                frames[y * Size + x] = frame;
                counts[frame]++;
            }
        }

        var dirtyTilemap = Paint(grid, DirtyTilemapName, DirtySortingOrder, dirtySprites[0], dirtyTiles, frames);
        if (cleanTiles.Length > 0)
            Paint(grid, CleanTilemapName, CleanSortingOrder, cleanSprites[0], cleanTiles, frames);

        EditorSceneManager.MarkSceneDirty(dirtyTilemap.gameObject.scene);
        Selection.activeGameObject = dirtyTilemap.gameObject;

        string distribution = string.Join(", ", counts.Select((c, i) => $"Frame_{i}: %{c * 100f / frames.Length:0}"));
        string layers = cleanTiles.Length > 0 ? $"{DirtyTilemapName} + {CleanTilemapName}" : DirtyTilemapName;
        Debug.Log($"Zemin: {Size}x{Size} alan boyandı [{layers}] ({distribution}). Sahneyi kaydetmeyi unutma.");
    }

    private static Tilemap Paint(Grid grid, string tilemapName, int sortingOrder, Sprite reference, Tile[] tiles, int[] frames)
    {
        var tilemap = GetOrCreateTilemap(grid, tilemapName);
        var renderer = tilemap.GetComponent<TilemapRenderer>();

        Undo.RegisterCompleteObjectUndo(tilemap, "Zemin Oluştur");
        Undo.RecordObject(renderer, "Zemin Oluştur");
        renderer.sortingOrder = sortingOrder;

        // Sprite pivot'u hücreye otursun (aseprite pivot'u alt-orta olabiliyor)
        tilemap.tileAnchor = reference.pivot / reference.rect.size;
        tilemap.ClearAllTiles();

        // Temiz versiyonda daha az frame varsa eksikler son frame'e düşer
        var block = frames.Select(f => (TileBase)tiles[Mathf.Min(f, tiles.Length - 1)]).ToArray();
        tilemap.SetTilesBlock(new BoundsInt(-Size / 2, -Size / 2, 0, Size, Size, 1), block);
        tilemap.CompressBounds();
        return tilemap;
    }

    // Her ek frame kendi bağımsız noise alanında bölge oluşturur; çakışırsa sonraki frame kazanır
    private static int PickFrame(int x, int y, int frameCount, Vector2[] offsets)
    {
        int frame = 0;
        for (int i = 1; i < frameCount; i++)
        {
            float noise = Mathf.PerlinNoise(x * RegionScale + offsets[i].x, y * RegionScale + offsets[i].y);
            if (noise > RegionThreshold)
                frame = i;
        }
        return frame;
    }

    private static Sprite[] LoadSprites(string path)
    {
        if (string.IsNullOrEmpty(path)) return new Sprite[0];
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(s => s.name, System.StringComparer.Ordinal) // Frame_0, Frame_1, Frame_2
            .ToArray();
    }

    private static Tile GetOrCreateTile(Sprite sprite, string tileName)
    {
        string assetPath = $"{TileFolder}/{tileName}.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
        if (tile == null)
        {
            if (!AssetDatabase.IsValidFolder(TileFolder))
                AssetDatabase.CreateFolder("Assets", "Tiles");

            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, assetPath);
        }

        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static Grid GetOrCreateGrid()
    {
        var grid = Object.FindFirstObjectByType<Grid>();
        if (grid != null) return grid;

        var go = new GameObject("Grid");
        Undo.RegisterCreatedObjectUndo(go, "Zemin Oluştur");
        return go.AddComponent<Grid>();
    }

    private static Tilemap GetOrCreateTilemap(Grid grid, string tilemapName)
    {
        var existing = grid.transform.Find(tilemapName);
        if (existing != null && existing.TryGetComponent(out Tilemap found))
            return found;

        var go = new GameObject(tilemapName);
        Undo.RegisterCreatedObjectUndo(go, "Zemin Oluştur");
        go.transform.SetParent(grid.transform, false);

        var tilemap = go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>();
        return tilemap;
    }
}
