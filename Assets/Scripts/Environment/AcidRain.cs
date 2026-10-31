using UnityEngine;

// Art hazır olana kadar kodla kurulan asit yağmuru:
// kamerayı takip eden yeşil damlalar, yere düşünce sıçrama halkası + damlacıklar ve ekrana hafif zehirli bir ton.
// Sahnede boş bir objeye eklemek yeterli, her şeyi kendisi oluşturur.
public class AcidRain : MonoBehaviour
{
    [Header("Yağmur")]
    [SerializeField, Range(0f, 1f)] private float intensity = 0.7f;
    [SerializeField] private float maxDropsPerSecond = 450f;
    [SerializeField] private Color dropColor = new Color(0.55f, 1f, 0.15f, 0.55f);
    [SerializeField] private float fallSpeed = 16f;
    [Tooltip("Yatay rüzgar (+ sağa, - sola)")]
    [SerializeField] private float wind = 3f;
    [SerializeField] private float dropWidth = 0.05f;

    [Header("Sıçrama")]
    [SerializeField, Range(0f, 1f)] private float splashChance = 0.6f;
    [SerializeField] private Color splashColor = new Color(0.6f, 1f, 0.2f, 0.8f);
    [SerializeField] private float splashSize = 0.35f;

    [Header("Ekran tonu")]
    [SerializeField] private Color tintColor = new Color(0.35f, 0.9f, 0.1f, 0.12f);

    [Header("Çizim sırası")]
    [SerializeField] private int dropSortingOrder = 100;
    [Tooltip("Karakterlerin altında, zeminin üstünde kalmalı")]
    [SerializeField] private int splashSortingOrder = -1;

    [SerializeField] private Camera targetCamera;

    private const float MinDropLifetime = 0.35f;
    private const float MaxDropLifetime = 0.65f;

    private ParticleSystem drops;
    private SpriteRenderer tint;
    private Material dropMaterial;
    private Material ringMaterial;

    // Hava durumu sistemi vs. için (0 = durdu, 1 = sağanak)
    public float Intensity
    {
        get => intensity;
        set => intensity = Mathf.Clamp01(value);
    }

    void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var dotTexture = CreateCircleTexture(16, ring: false);
        dropMaterial = new Material(shader) { mainTexture = dotTexture };
        ringMaterial = new Material(shader) { mainTexture = CreateCircleTexture(32, ring: true) };

        drops = CreateDrops();
        AddSplashRing(drops);
        AddSplashDroplets(drops);
        tint = CreateTint(shader);

        UpdateArea();
        drops.Play();
    }

    void LateUpdate()
    {
        if (drops == null) return;

        UpdateArea();

        var emission = drops.emission;
        emission.rateOverTime = maxDropsPerSecond * intensity;

        if (tint != null)
        {
            var color = tintColor;
            color.a *= intensity;
            tint.color = color;
        }
    }

    // Emitter kameranın görüş alanını kaplasın; damlalar world space'te olduğu için kamera kayınca sürüklenmezler
    private void UpdateArea()
    {
        if (targetCamera == null) return;

        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;
        Vector3 camPos = targetCamera.transform.position;

        // Damla yere inene kadar kat ettiği yol kadar yukarıdan (ve rüzgarın tersinden) doğsun ki iniş noktaları ekranı kaplasın
        float avgLifetime = (MinDropLifetime + MaxDropLifetime) * 0.5f;
        float maxFall = fallSpeed * MaxDropLifetime;
        transform.position = new Vector3(camPos.x - wind * avgLifetime, camPos.y + maxFall * 0.5f, 0f);

        var shape = drops.shape;
        shape.scale = new Vector3(halfWidth * 2f + Mathf.Abs(wind) + 2f, halfHeight * 2f + maxFall, 0f);

        if (tint != null)
            tint.transform.localScale = new Vector3(halfWidth * 2f + 1f, halfHeight * 2f + 1f, 1f);
    }

    private ParticleSystem CreateDrops()
    {
        var go = new GameObject("Drops");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(MinDropLifetime, MaxDropLifetime);
        main.startSpeed = 0f;
        main.startSize = dropWidth;
        main.startColor = dropColor;
        main.maxParticles = 3000;

        var emission = ps.emission;
        emission.rateOverTime = maxDropsPerSecond * intensity;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Rectangle;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = wind;
        velocity.y = -fallSpeed;
        velocity.z = 0f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.035f;
        renderer.lengthScale = 1f;
        renderer.sharedMaterial = dropMaterial;
        renderer.sortingOrder = dropSortingOrder;

        return ps;
    }

    // Damla öldüğü yerde (= yere değdiği yerde) büyüyüp sönen bir halka
    private void AddSplashRing(ParticleSystem parent)
    {
        var ps = CreateSubEmitter(parent, "SplashRing", ringMaterial, burstCount: 1);

        var main = ps.main;
        main.startLifetime = 0.25f;
        main.startSpeed = 0f;
        main.startSize = splashSize;
        main.startColor = splashColor;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f));
    }

    // Halkadan etrafa saçılan birkaç minik damlacık
    private void AddSplashDroplets(ParticleSystem parent)
    {
        var ps = CreateSubEmitter(parent, "SplashDroplets", dropMaterial, burstCount: 3);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startColor = splashColor;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;
    }

    private ParticleSystem CreateSubEmitter(ParticleSystem parent, string name, Material material, short burstCount)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1500;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burstCount) });

        var shape = ps.shape;
        shape.enabled = false;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = splashSortingOrder;

        var subEmitters = parent.subEmitters;
        subEmitters.enabled = true;
        subEmitters.AddSubEmitter(ps, ParticleSystemSubEmitterType.Death,
            ParticleSystemSubEmitterProperties.InheritNothing, splashChance);

        return ps;
    }

    // Kameraya yapışık, tüm ekranı kaplayan yarı saydam yeşil katman
    private SpriteRenderer CreateTint(Shader shader)
    {
        if (targetCamera == null) return null;

        var go = new GameObject("AcidTint");
        go.transform.SetParent(targetCamera.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 1f);

        var texture = Texture2D.whiteTexture;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
        sr.sharedMaterial = new Material(shader);
        sr.sortingOrder = dropSortingOrder - 1;
        return sr;
    }

    private static Texture2D CreateCircleTexture(int size, bool ring)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float center = (size - 1) * 0.5f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center; // 0 merkez, 1 kenar
                float alpha = ring
                    ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.15f)
                    : Mathf.Clamp01(1f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    void OnDestroy()
    {
        if (tint != null) Destroy(tint.gameObject);
    }
}
