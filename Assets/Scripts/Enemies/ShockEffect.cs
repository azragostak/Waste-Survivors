using UnityEngine;

// Elektrik çarpma görseli: çizgi filmlerdeki "iskelet görünme" anı gibi.
// Düşmanların iskeleti olmadığı için içi siyah, dışı beyaz çizgili bir siluete dönüşür
// ve sersemleme boyunca normal haliyle hızlıca titreşir. Shader gerekmez: dış çizgi,
// sprite'ın 8 yöne kaydırılmış beyaz kopyalarıyla çizilir.
// Düşmana ilk çarpmada eklenir, sonraki çarpmalarda aynı objeler tekrar kullanılır.
[DisallowMultipleComponent]
public class ShockEffect : MonoBehaviour
{
    private const float FlickerInterval = 0.06f;
    private const float OutlineWidth = 0.025f;

    private static readonly Vector2[] OutlineDirections =
    {
        new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f),
        new(0.71f, 0.71f), new(-0.71f, 0.71f), new(0.71f, -0.71f), new(-0.71f, -0.71f),
    };

    private Enemy enemy;
    private SpriteRenderer body;
    private SpriteRenderer[] outlines;
    private float startTime;
    private float endTime;

    public static void Play(Enemy enemy, float duration)
    {
        if (enemy == null || enemy.BodyRenderer == null) return;

        if (!enemy.TryGetComponent(out ShockEffect effect))
        {
            effect = enemy.gameObject.AddComponent<ShockEffect>();
            effect.Setup(enemy);
        }
        effect.startTime = Time.time;
        effect.endTime = Time.time + duration;
        effect.enabled = true;
    }

    private void Setup(Enemy owner)
    {
        enemy = owner;
        body = owner.BodyRenderer;

        // Görselin child'ı: dönme, ölçek ve sekme animasyonlarını birebir takip eder
        outlines = new SpriteRenderer[OutlineDirections.Length];
        Vector3 scale = body.transform.lossyScale;
        for (int i = 0; i < OutlineDirections.Length; i++)
        {
            var go = new GameObject("ShockOutline");
            go.transform.SetParent(body.transform, false);
            go.transform.localPosition = new Vector3(
                OutlineDirections[i].x * OutlineWidth / Mathf.Max(0.01f, Mathf.Abs(scale.x)),
                OutlineDirections[i].y * OutlineWidth / Mathf.Max(0.01f, Mathf.Abs(scale.y)), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = Color.white;
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder - 1;
            sr.enabled = false;
            outlines[i] = sr;
        }
    }

    void LateUpdate()
    {
        // Kaybolma (final) ya da ölüm başladıysa görseli bırak
        if (Time.time >= endTime || enemy == null || !enemy.IsAlive)
        {
            Stop();
            return;
        }

        bool silhouette = ((int)((Time.time - startTime) / FlickerInterval) & 1) == 0;
        body.color = silhouette ? Color.black : enemy.BaseColor;
        foreach (var outline in outlines)
        {
            outline.enabled = silhouette;
            if (!silhouette) continue;
            // Varil gibi kare kare oynayan sprite'larda dış çizgi de aynı kareyi göstersin
            outline.sprite = body.sprite;
            outline.flipX = body.flipX;
            outline.flipY = body.flipY;
        }
    }

    private void Stop()
    {
        foreach (var outline in outlines)
            outline.enabled = false;
        if (enemy != null && enemy.IsAlive)
            body.color = enemy.BaseColor;
        enabled = false;
    }
}
