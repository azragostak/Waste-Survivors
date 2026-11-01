using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Gökten hızla düşüp hedef noktadaki düşmanları ezen tek bir hidrolik pres.
public class PressDrop : MonoBehaviour
{
    [Header("Düşüş Ayarları")]
    [Tooltip("Hedef noktanın kaç birim yukarısından düşmeye başlasın")]
    [SerializeField] private float dropHeight = 6f;

    [Tooltip("Pres tabanının hedef merkezine tam oturması için dikey görsel ofset")]
    [SerializeField] private float impactYOffset = 0.25f;

    [Tooltip("Pres görselinin 1x boyutta kabul edildiği referans alan yarıçapı (alan büyüdükçe pres de büyür)")]
    [SerializeField, Min(0.1f)] private float referenceArea = 1.3f;

    [Header("Ezme ve Gölge Efektleri")]
    [Tooltip("Opsiyonel: kendi çarpma efekt prefab'ın. Boşsa kodla çizilen şok dalgası kullanılır.")]
    [SerializeField] private GameObject impactVisualPrefab;
    [SerializeField] private float shockwaveLifetime = 0.3f;
    [SerializeField] private Color shockwaveColor = new Color(1f, 0.78f, 0.2f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.38f);

    [Header("Ses")]
    [Tooltip("Pres yere çarptığında")]
    [SerializeField] private AudioClip impactSound;
    [SerializeField, Range(0f, 1f)] private float impactVolume = 0.28f;

    private static Sprite cachedShadowSprite;

    private WeaponStats stats;
    private Vector2 impactPoint;
    private bool impacted;
    private SpriteRenderer spriteRenderer;
    private Vector3 baseScale;
    private GameObject shadowObject;
    private SpriteRenderer shadowRenderer;
    private readonly List<Enemy> hits = new();

    // Manyetik Pres evrimi: düşmeden önce düşmanları düşeceği noktaya çeker (0 = kapalı)
    private float magnetRadius;
    private float magnetDuration;
    private float magnetPullRate;
    private LineRenderer magnetRing;
    private static Material magnetRingMaterial;

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        baseScale = transform.localScale;
    }

    public float GetEstimatedFallTime(float speed) =>
        Mathf.Max(0.12f, dropHeight / Mathf.Max(1f, speed));

    // Drop'tan önce çağrılır. radius: çekim yarıçapı, duration: düşmeden önceki çekim süresi,
    // pullRate: saniyede kalan mesafenin ne kadarının kapanacağı (üstel; büyüdükçe daha sert çeker)
    public void EnableMagnet(float radius, float duration, float pullRate)
    {
        magnetRadius = radius;
        magnetDuration = duration;
        magnetPullRate = pullRate;
    }

    public void Drop(Vector2 targetPosition, WeaponStats weaponStats, float delay = 0f)
    {
        impactPoint = targetPosition;
        stats = weaponStats;
        StartCoroutine(RunDrop(delay));
    }

    private IEnumerator RunDrop(float delay)
    {
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        CreateShadow(impactPoint);

        float areaFactor = Mathf.Max(0.75f, stats.area / Mathf.Max(0.1f, referenceArea));
        Vector3 scaledBase = baseScale * areaFactor;
        float scaledImpactYOffset = impactYOffset * areaFactor;

        // 1. Yerçekimi hissiyle hızlanarak düşüş (quadratic ease-in)
        float fallTime = GetEstimatedFallTime(stats.speed);
        Vector2 startPos = impactPoint + Vector2.up * (scaledImpactYOffset + dropHeight);
        Vector2 endPos = impactPoint + Vector2.up * scaledImpactYOffset;

        // 0. Mıknatıs evresi: pres havada titreşerek bekler, daralan halkanın içindekiler merkeze çekilir
        if (magnetRadius > 0f)
        {
            magnetRing = CreateMagnetRing();
            transform.localScale = scaledBase;
            for (float t = 0f; t < magnetDuration; t += Time.deltaTime)
            {
                float shake = Mathf.Sin(t * 70f) * 0.04f;
                transform.position = startPos + new Vector2(shake, 0f);
                PullEnemies();
                UpdateMagnetRing(Mathf.Lerp(magnetRadius, stats.area, t / Mathf.Max(0.01f, magnetDuration)), 1f);
                yield return null;
            }
        }

        for (float t = 0f; t < fallTime; t += Time.deltaTime)
        {
            float k = t / fallTime;
            float fallCurve = k * k;

            transform.position = Vector2.Lerp(startPos, endPos, fallCurve);

            // Hızlandıkça hafif dikey uzama (stretch)
            transform.localScale = new Vector3(
                scaledBase.x * Mathf.Lerp(1f, 0.9f, fallCurve),
                scaledBase.y * Mathf.Lerp(1f, 1.15f, fallCurve),
                scaledBase.z
            );

            UpdateShadow(fallCurve);
            if (magnetRadius > 0f)
                UpdateMagnetRing(stats.area, 1f - fallCurve);
            yield return null;
        }

        if (magnetRing != null)
            Destroy(magnetRing.gameObject);

        // 2. Yere çarpma ve ezme anı
        transform.position = endPos;
        UpdateShadow(1f);
        Slam();

        // 3. Çarpma esnemesi (squash) + kısa süre yerde kalıp silinerek kaybolma
        float lingerTime = Mathf.Max(0.25f, stats.duration);
        float squashDuration = Mathf.Min(0.12f, lingerTime * 0.4f);

        for (float t = 0f; t < lingerTime; t += Time.deltaTime)
        {
            // İlk anda ezilip eski boyutuna yaylanma
            if (t < squashDuration)
            {
                float s = t / squashDuration;
                transform.localScale = new Vector3(
                    scaledBase.x * Mathf.Lerp(1.28f, 1f, s),
                    scaledBase.y * Mathf.Lerp(0.72f, 1f, s),
                    scaledBase.z
                );
            }
            else
            {
                transform.localScale = scaledBase;
            }

            // Son %45'lik sürede saydamlaşarak kaybolma
            float fadeStart = lingerTime * 0.55f;
            if (t > fadeStart)
            {
                float fade = 1f - (t - fadeStart) / Mathf.Max(0.01f, lingerTime - fadeStart);
                SetAlpha(fade);
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void PullEnemies()
    {
        float step = 1f - Mathf.Exp(-magnetPullRate * Time.deltaTime);
        Enemy.GetInRadius(impactPoint, magnetRadius, hits);
        foreach (var enemy in hits)
        {
            if (!enemy.TryGetComponent(out Rigidbody2D body)) continue;
            Vector2 toCenter = impactPoint - body.position;
            // Merkezde üst üste yığılmasınlar
            if (toCenter.sqrMagnitude < 0.3f * 0.3f) continue;
            body.position += toCenter * step;
        }
    }

    private LineRenderer CreateMagnetRing()
    {
        if (magnetRingMaterial == null)
            magnetRingMaterial = new Material(Shader.Find("Sprites/Default"));

        var go = new GameObject("MagnetRing");
        var line = go.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = true;
        line.positionCount = 48;
        line.widthMultiplier = 0.08f;
        line.sharedMaterial = magnetRingMaterial;
        line.sortingOrder = -4;
        return line;
    }

    private void UpdateMagnetRing(float radius, float alpha)
    {
        if (magnetRing == null) return;
        var color = new Color(0.4f, 0.85f, 1f, 0.75f * alpha);
        magnetRing.startColor = color;
        magnetRing.endColor = color;
        for (int i = 0; i < magnetRing.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / magnetRing.positionCount;
            magnetRing.SetPosition(i, impactPoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    private void Slam()
    {
        impacted = true;
        // Aynı anda düşen presler tek bir gümleme gibi duyulmasın diye kısa aralıkla sınırlı
        SfxPlayer.Play(impactSound, impactVolume, minInterval: 0.07f, maxVoices: 4);

        Enemy.GetInRadius(impactPoint, stats.area, hits);
        foreach (var enemy in hits)
            enemy.TakeDamage(stats.damage);

        if (impactVisualPrefab != null)
        {
            var visual = Instantiate(impactVisualPrefab, impactPoint, Quaternion.identity);
            visual.transform.localScale = Vector3.one * (stats.area * 2f);
            Destroy(visual, shockwaveLifetime);
        }
        else
        {
            PressShockwaveEffect.Spawn(impactPoint, stats.area, shockwaveColor, shockwaveLifetime);
        }
    }

    private void CreateShadow(Vector2 groundPos)
    {
        shadowObject = new GameObject("PressShadow");
        shadowObject.transform.position = groundPos;

        shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = GetOrCreateShadowSprite();
        shadowRenderer.color = shadowColor;
        shadowRenderer.sortingOrder = -1;

        UpdateShadow(0f);
    }

    private void UpdateShadow(float progress)
    {
        if (shadowObject == null) return;

        float diameter = Mathf.Max(0.8f, stats.area * 1.4f);
        float scale = Mathf.Lerp(0.35f, 1f, progress);
        shadowObject.transform.localScale = new Vector3(diameter * scale, diameter * 0.5f * scale, 1f);

        if (shadowRenderer != null)
        {
            var c = shadowColor;
            c.a = shadowColor.a * Mathf.Lerp(0.4f, 1f, progress);
            shadowRenderer.color = c;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (spriteRenderer != null)
        {
            var c = spriteRenderer.color;
            c.a = alpha;
            spriteRenderer.color = c;
        }

        if (shadowRenderer != null)
        {
            var sc = shadowColor;
            sc.a = shadowColor.a * alpha;
            shadowRenderer.color = sc;
        }
    }

    private static Sprite GetOrCreateShadowSprite()
    {
        if (cachedShadowSprite != null) return cachedShadowSprite;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                pixels[y * size + x] = (dx * dx + dy * dy <= 1f) ? Color.white : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedShadowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedShadowSprite;
    }

    void OnDestroy()
    {
        if (magnetRing != null) Destroy(magnetRing.gameObject);
        if (shadowObject != null)
            Destroy(shadowObject);
    }

    void OnDrawGizmos()
    {
        if (!impacted) return;
        Gizmos.color = shockwaveColor;
        Gizmos.DrawWireSphere(impactPoint, stats.area);
    }
}

