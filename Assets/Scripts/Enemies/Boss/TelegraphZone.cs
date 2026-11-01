using UnityEngine;

// Boss saldırılarının kırmızı uyarı alanı. Saldırı gelmeden önce yerde belirir:
// dışta soluk alan, içte süre boyunca dolan parlak kısım. Dolduğu an saldırı gerçekleşir.
//   Circle: daire alan (Garbage Slam) - içten dışa dolar.
//   Lane:   şerit (Stampede) - sürünün geleceği uçtan diğer uca doğru dolar.
// Süresi dolunca kısa bir parlamayla kaybolur. Zemin ve bitkilerin üstünde, karakterlerin altında çizilir.
public class TelegraphZone : MonoBehaviour
{
    private const int SortingOrder = -3;
    private const float FadeOutTime = 0.15f;
    private static readonly Color AreaColor = new Color(1f, 0.15f, 0.1f, 0.18f);
    private static readonly Color FillColor = new Color(1f, 0.2f, 0.1f, 0.38f);
    private static readonly Color EdgeColor = new Color(1f, 0.3f, 0.2f, 0.85f);

    private static Sprite circleSprite;
    private static Sprite ringSprite;
    private static Sprite squareSprite;

    private SpriteRenderer area;
    private SpriteRenderer fill;
    private SpriteRenderer edge;
    private bool isLane;
    private Vector2 laneSize;
    private float duration;
    private float elapsed;

    public static TelegraphZone Circle(Vector2 center, float radius, float duration)
    {
        var zone = Create("TelegraphCircle", center, 0f, duration);
        zone.area = zone.AddPart("Area", GetCircleSprite(), AreaColor, SortingOrder, Vector2.one * radius * 2f);
        zone.fill = zone.AddPart("Fill", GetCircleSprite(), FillColor, SortingOrder + 1, Vector2.zero);
        zone.edge = zone.AddPart("Edge", GetRingSprite(), EdgeColor, SortingOrder + 2, Vector2.one * radius * 2f);
        return zone;
    }

    // from: sürünün girdiği uç, to: çıktığı uç
    public static TelegraphZone Lane(Vector2 from, Vector2 to, float width, float duration)
    {
        Vector2 delta = to - from;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        var zone = Create("TelegraphLane", (from + to) * 0.5f, angle, duration);
        zone.isLane = true;
        zone.laneSize = new Vector2(delta.magnitude, width);
        zone.area = zone.AddPart("Area", GetSquareSprite(), AreaColor, SortingOrder, zone.laneSize);
        zone.fill = zone.AddPart("Fill", GetSquareSprite(), FillColor, SortingOrder + 1, new Vector2(0f, width));
        zone.edge = zone.AddPart("Edge", GetSquareSprite(), EdgeColor, SortingOrder + 2, new Vector2(zone.laneSize.x, 0.06f));
        zone.edge.transform.localPosition = new Vector3(0f, width * 0.5f, 0f);
        var lower = zone.AddPart("Edge2", GetSquareSprite(), EdgeColor, SortingOrder + 2, new Vector2(zone.laneSize.x, 0.06f));
        lower.transform.localPosition = new Vector3(0f, -width * 0.5f, 0f);
        return zone;
    }

    private static TelegraphZone Create(string name, Vector2 position, float angle, float duration)
    {
        var go = new GameObject(name);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
        var zone = go.AddComponent<TelegraphZone>();
        zone.duration = Mathf.Max(0.05f, duration);
        return zone;
    }

    private SpriteRenderer AddPart(string partName, Sprite sprite, Color color, int order, Vector2 size)
    {
        var go = new GameObject(partName);
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        if (isLane)
        {
            // Girdiği uçtan (sol, yerel -x) çıkacağı uca doğru dolar
            float length = laneSize.x * t;
            fill.transform.localScale = new Vector3(length, laneSize.y, 1f);
            fill.transform.localPosition = new Vector3(-laneSize.x * 0.5f + length * 0.5f, 0f, 0f);
        }
        else
        {
            fill.transform.localScale = area.transform.localScale * t;
        }

        // Son anlarda yanıp söner: "şimdi geliyor"
        float blink = t > 0.75f ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(elapsed * 30f)) : 1f;
        var edgeColor = EdgeColor;
        edgeColor.a *= blink;
        edge.color = edgeColor;

        if (elapsed >= duration)
        {
            float fade = 1f - (elapsed - duration) / FadeOutTime;
            if (fade <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                var c = sr.color;
                c.a = Mathf.Min(c.a, 0.6f * fade);
                sr.color = c;
            }
        }
    }

    // ??= kullanılmaz: sahne değişince Unity dokuyu silebilir, Unity null kontrolü gerekir
    private static Sprite GetCircleSprite()
    {
        if (circleSprite == null) circleSprite = CreateCircle(64, ring: false);
        return circleSprite;
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite == null) ringSprite = CreateCircle(128, ring: true);
        return ringSprite;
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null) return squareSprite;
        var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
    }

    // 1x1 birim boyutunda daire (dolu) ya da ince halka
    private static Sprite CreateCircle(int size, bool ring)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float center = (size - 1) * 0.5f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                float alpha = ring
                    ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.97f) / 0.03f)
                    : Mathf.Clamp01((1f - d) * size * 0.25f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
