using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Tools > Waste Survivors > Zemin Oluştur: seçilen aseprite'ın her frame'inden bir Tile asset'i yapar,
// açık sahnede Grid/Ground tilemap'ini kurar ve orijin merkezli Size x Size alanı boyar.
// Frame_0 ana zemindir; diğer frame'ler Perlin noise ile doğal görünen lekeler/bölgeler halinde dağılır.
// Her çalıştırmada bölgeler yeniden rastgele dağılır. Var olan Grid/Ground'u kullanır, Ctrl+Z ile geri alınabilir.
public static class GroundTilemapBuilder
{
    private const string TileGuid = "ca5c832eda33c8348b1d1bd8b4fcc139"; // Assets/Sprites/tile.aseprite
    private const string TileFolder = "Assets/Tiles";
    private const string TilemapName = "Ground";
    private const int Size = 100;
    private const int SortingOrder = -10; // karakterlerin ve asit sıçramalarının altında

    // Küçüldükçe bölgeler büyür (0.08 ≈ 10-15 tile genişliğinde lekeler)
    private const float RegionScale = 0.08f;
    // Frame_1, Frame_2... için: noise bu değerin üstündeyse o frame kullanılır. Büyüdükçe bölgeler seyrekleşir.
    private const float RegionThreshold = 0.62f;

    [MenuItem("Tools/Waste Survivors/Zemin Oluştur (100x100)")]
    private static void BuildGround() => Build(TileGuid, "Ground");

    private static void Build(string spriteGuid, string tilePrefix)
    {
        var sprites = LoadSprites(spriteGuid);
        if (sprites.Length == 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(spriteGuid);
            Debug.LogError(string.IsNullOrEmpty(path)
                ? $"Zemin: {spriteGuid} GUID'li tile aseprite'ı projede bulunamadı."
                : $"Zemin: {path} içinde sprite bulunamadı.");
            return;
        }

        var tiles = sprites.Select((sprite, i) => GetOrCreateTile(sprite, $"{tilePrefix}_{i}")).ToArray();
        AssetDatabase.SaveAssets();

        var grid = GetOrCreateGrid();
        var tilemap = GetOrCreateTilemap(grid);

        // Hücre boyutu = sprite'ın dünya boyutu, böylece karakterle aynı piksel yoğunluğunda ve boşluksuz döşenir
        var reference = sprites[0];
        Vector2 worldSize = reference.rect.size / reference.pixelsPerUnit;
        Undo.RecordObject(grid, "Zemin Oluştur");
        grid.cellSize = new Vector3(worldSize.x, worldSize.y, 0f);

        Undo.RegisterCompleteObjectUndo(tilemap, "Zemin Oluştur");
        // Sprite pivot'u hücreye otursun (aseprite pivot'u alt-orta olabiliyor)
        tilemap.tileAnchor = reference.pivot / reference.rect.size;
        tilemap.ClearAllTiles();

        var block = new TileBase[Size * Size];
        var counts = new int[tiles.Length];
        var offsets = Enumerable.Range(0, tiles.Length)
            .Select(_ => new Vector2(Random.Range(0f, 10000f), Random.Range(0f, 10000f)))
            .ToArray();

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int frame = PickFrame(x, y, tiles.Length, offsets);
                block[y * Size + x] = tiles[frame];
                counts[frame]++;
            }
        }

        tilemap.SetTilesBlock(new BoundsInt(-Size / 2, -Size / 2, 0, Size, Size, 1), block);
        tilemap.CompressBounds();

        EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
        Selection.activeGameObject = tilemap.gameObject;

        string distribution = string.Join(", ", counts.Select((c, i) => $"Frame_{i}: %{c * 100f / block.Length:0}"));
        Debug.Log($"Zemin: {Size}x{Size} alan boyandı ({distribution}). Sahneyi kaydetmeyi unutma.");
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

    private static Sprite[] LoadSprites(string spriteGuid)
    {
        string path = AssetDatabase.GUIDToAssetPath(spriteGuid);
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

    private static Tilemap GetOrCreateTilemap(Grid grid)
    {
        var existing = grid.transform.Find(TilemapName);
        if (existing != null && existing.TryGetComponent(out Tilemap found))
            return found;

        var go = new GameObject(TilemapName);
        Undo.RegisterCreatedObjectUndo(go, "Zemin Oluştur");
        go.transform.SetParent(grid.transform, false);

        var tilemap = go.AddComponent<Tilemap>();
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = SortingOrder;
        return tilemap;
    }
}
