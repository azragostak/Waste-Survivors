using UnityEngine;

// Bir karakterin üstünde duran küçük can barı. SpriteRenderer ile çizilir, Canvas gerektirmez.
// GameHUD oyuncuya ve her yeni düşmana Attach ile otomatik takar.
public class WorldHealthBar : MonoBehaviour
{
    private const float BorderSize = 0.03f;
    private const float GapAboveSprite = 0.06f;

    private static Sprite pixelSprite;

    // Trailer (F2): bütün can barları gizli
    public static bool HideAll;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => HideAll = false;

    private Object owner;
    private IHealth health;
    private Transform fill;
    private SpriteRenderer backgroundRenderer;
    private SpriteRenderer fillRenderer;
    private float width;
    private bool hideWhenFull;

    public static WorldHealthBar Attach(Component target, IHealth health, Color fillColor,
        float width, float height, bool hideWhenFull, int sortingOrder)
    {
        var go = new GameObject("HealthBar");
        go.transform.SetParent(target.transform, false);

        // Ebeveynin ölçeği ne olursa olsun bar dünya biriminde istenen boyutta kalsın
        Vector3 parentScale = target.transform.lossyScale;
        go.transform.localScale = new Vector3(1f / SafeScale(parentScale.x), 1f / SafeScale(parentScale.y), 1f);
        go.transform.localPosition = new Vector3(0f, (GetSpriteTop(target) + GapAboveSprite + height * 0.5f) / SafeScale(parentScale.y), 0f);

        var bar = go.AddComponent<WorldHealthBar>();
        bar.owner = target;
        bar.health = health;
        bar.width = width;
        bar.hideWhenFull = hideWhenFull;

        bar.backgroundRenderer = CreatePart(go.transform, "Background", new Color(0f, 0f, 0f, 0.7f), sortingOrder);
        bar.backgroundRenderer.transform.localScale = new Vector3(width + BorderSize * 2f, height + BorderSize * 2f, 1f);

        bar.fillRenderer = CreatePart(go.transform, "Fill", fillColor, sortingOrder + 1);
        bar.fill = bar.fillRenderer.transform;
        bar.fill.localScale = new Vector3(width, height, 1f);

        bar.Refresh();
        return bar;
    }

    void LateUpdate()
    {
        // Düşman ölünce bar da onunla birlikte yok olur; sadece garanti olsun diye
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        Refresh();

        // Ebeveyn dönse bile bar düz dursun
        transform.rotation = Quaternion.identity;
    }

    private void Refresh()
    {
        float ratio = health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f;

        // Sol kenara sabitlenmiş şekilde kısalır
        Vector3 scale = fill.localScale;
        scale.x = width * ratio;
        fill.localScale = scale;
        fill.localPosition = new Vector3(-width * 0.5f * (1f - ratio), 0f, 0f);

        bool visible = !HideAll && !(hideWhenFull && ratio >= 1f) && ratio > 0f;
        backgroundRenderer.enabled = visible;
        fillRenderer.enabled = visible;
    }

    private static SpriteRenderer CreatePart(Transform parent, string name, Color color, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetPixelSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    // 1x1 birimlik beyaz kare; boyutu localScale ile verilir
    private static Sprite GetPixelSprite()
    {
        if (pixelSprite != null) return pixelSprite;

        var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        pixelSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return pixelSprite;
    }

    // Sprite'ın üst kenarının, hedefin pivot'una göre dünya birimindeki yüksekliği
    private static float GetSpriteTop(Component target)
    {
        var sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return 0.5f;
        // Görsel child'ı kaydırılmışsa (ör. şişelerde sprite ortalanmış) o ofset de hesaba katılır
        float visualOffset = sr.transform.position.y - target.transform.position.y;
        return visualOffset + sr.sprite.bounds.max.y * sr.transform.lossyScale.y;
    }

    private static float SafeScale(float value) => Mathf.Abs(value) < 0.0001f ? 1f : value;
}
