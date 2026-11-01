using UnityEngine;

// Boss: Çöp Tepesi. Süre dolunca (10:00) gelir; ölünce dünya temizlenme finali başlar.
// Balçık gibi sürünerek gelir: gövdesi ritmik olarak hafifçe uzayıp kısalır; uzarken öne doğru akar,
// toparlanırken yavaşlar. Üstüne yerinde jöle gibi hafif bir titreme eklenir.
// Sprite "Visual" child objesinde olmalı (pivot altta): uzama/kısalma tabandan olur, fizik gövdesi etkilenmez.
// Saldırılar ve fazlar BossFight'ta.
public class BossEnemy : Enemy
{
    [Header("Boss")]
    [SerializeField] private string bossName = "TRASH MOUNTAIN";
    [Tooltip("Gövde görseli (boss_cop_tepesi.aseprite; AsepriteSpriteLinker bağlar)")]
    [SerializeField] private Sprite bodySprite;

    [Header("Sürünme (balçık)")]
    [Tooltip("Ortalama ilerleme hızı (oyuncu 2)")]
    [SerializeField] private float crawlSpeed = 0.9f;
    [Tooltip("Bir uzayıp kısalma döngüsünün süresi (sn)")]
    [SerializeField, Min(0.2f)] private float crawlCycle = 1.2f;
    [Tooltip("Uzayıp kısalma miktarı")]
    [SerializeField, Range(0f, 0.3f)] private float stretchAmount = 0.08f;
    [Tooltip("Toparlanırken hızın ne kadarı kalsın (0 = durur, 1 = hiç yavaşlamaz)")]
    [SerializeField, Range(0f, 1f)] private float contractedSpeedRatio = 0.35f;
    [Tooltip("Yerinde jöle gibi titreme miktarı ve hızı")]
    [SerializeField] private float wobbleAmount = 0.015f;
    [SerializeField] private float wobbleFrequency = 3f;

    [Header("Toz (tabanında, önde çizilir; arkada iz bırakır)")]
    [Tooltip("Saniyede ortalama toz parçacığı; uzayıp öne akarken daha çok, toparlanırken daha az")]
    [SerializeField, Min(0f)] private float dustPerSecond = 22f;
    [SerializeField] private Color dustColor = new Color(0.62f, 0.55f, 0.45f, 0.55f);
    [Tooltip("Tozun çıktığı taban genişliği (yarısı)")]
    [SerializeField] private float dustHalfWidth = 1.5f;
    [Tooltip("Tabanın gövde merkezine göre yüksekliği")]
    [SerializeField] private float dustFeetOffset = -1.35f;

    [Tooltip("Faz 2'de (öfkeli) sürünme hızı bu kat artar")]
    [SerializeField, Min(1f)] private float enragedCrawlMultiplier = 1.25f;

    [Header("Ses")]
    [SerializeField] private AudioClip deathSound;
    [SerializeField, Range(0f, 1f)] private float deathVolume = 0.85f;

    public string BossName
    {
        get
        {
            string baseName = GameLanguage.IsTurkish && bossName == "TRASH MOUNTAIN" ? "ÇÖP TEPESİ" : bossName;
            return Enraged ? baseName + GameLanguage.Tr(" - ENRAGED", " - ÖFKELİ") : baseName;
        }
    }
    // BossFight faz 2'de açar
    public bool Enraged { get; set; }

    private ParticleSystem dust;
    private float dustBudget;
    private Vector2 moveDirection;

    private Transform visual;
    private Vector3 visualBaseScale;
    private float crawlPhase;

    // 0 = tamamen toparlanmış, 1 = en uzamış
    private float Stretch01 => (Mathf.Sin(crawlPhase * Mathf.PI * 2f) + 1f) * 0.5f;

    protected override void Awake()
    {
        base.Awake();

        if (spriteRenderer != null && bodySprite != null)
            spriteRenderer.sprite = bodySprite;

        if (spriteRenderer != null && spriteRenderer.transform != transform)
        {
            visual = spriteRenderer.transform;
            visualBaseScale = visual.localScale;
        }
        else
        {
            Debug.LogWarning($"{name}: Sprite bir child objede olmalı, balçık animasyonu kapalı.", this);
        }

        crawlPhase = Random.value;
        dust = CreateDust();
    }

    // Ritim her karede ilerler; fizik adımı hızı bu ritme göre uygular
    void Update()
    {
        if (!IsAlive || IsStunned) return;

        float rhythm = Enraged ? enragedCrawlMultiplier : 1f;
        crawlPhase = (crawlPhase + Time.deltaTime * rhythm / crawlCycle) % 1f;
        UpdateVisual();
        EmitDust();
    }

    protected override void UpdateMovement()
    {
        if (target == null)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        Vector2 toTarget = (Vector2)target.position - rb.position;
        Vector2 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.zero;

        // Uzarken akar, toparlanırken yavaşlar; ortalaması crawlSpeed olacak şekilde ölçeklenir
        float pulse = Mathf.Lerp(contractedSpeedRatio, 2f - contractedSpeedRatio, Stretch01);
        rb.velocity = direction * (crawlSpeed * pulse * (Enraged ? enragedCrawlMultiplier : 1f));
        moveDirection = direction;

        if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.2f)
            spriteRenderer.flipX = direction.x < 0f;
    }

    private void UpdateVisual()
    {
        if (visual == null) return;

        // Uzarken yayvanlaşır (yatayda uzar, dikeyde kısalır), toparlanırken tersi
        float stretch = (Stretch01 * 2f - 1f) * stretchAmount;
        float wobble = Mathf.Sin(Time.time * wobbleFrequency * Mathf.PI * 2f) * wobbleAmount;
        float scaleX = 1f + stretch - wobble;
        float scaleY = 1f - stretch * 0.8f + wobble;
        visual.localScale = new Vector3(visualBaseScale.x * scaleX, visualBaseScale.y * scaleY, visualBaseScale.z);
    }

    // Tabanın boyunca toz çıkar; gidiş yönünün tersine savrulur ve dünyada kalarak arkada iz bırakır
    private void EmitDust()
    {
        if (dust == null || dustPerSecond <= 0f) return;

        dustBudget += dustPerSecond * Mathf.Lerp(0.3f, 1.7f, Stretch01) * Time.deltaTime;
        if (dustBudget < 1f) return;

        Vector2 feet = (Vector2)transform.position + Vector2.up * dustFeetOffset;
        var emit = new ParticleSystem.EmitParams();
        while (dustBudget >= 1f)
        {
            dustBudget -= 1f;
            emit.position = feet + new Vector2(Random.Range(-dustHalfWidth, dustHalfWidth), Random.Range(-0.1f, 0.15f));
            emit.velocity = -moveDirection * Random.Range(0.25f, 0.7f) + new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.05f, 0.3f));
            dust.Emit(emit, 1);
        }
    }

    private ParticleSystem CreateDust()
    {
        var go = new GameObject("Dust");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        // Döngüde kalsın ki elle çıkarılan parçacıklar hep simüle edilsin (kendi kendine çıkarma kapalı)
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startColor = dustColor;
        main.maxParticles = 300;

        var emission = ps.emission;
        emission.enabled = false; // EmitDust elle çıkarır

        var shape = ps.shape;
        shape.enabled = false;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f));

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("Sprites/Default");
        renderer.sharedMaterial = new Material(shader) { mainTexture = CreateSoftCircle(32) };
        // Boss'un önünde: toz gövdenin tabanını örter
        renderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 1;

        ps.Play();
        return ps;
    }

    private static Texture2D CreateSoftCircle(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float center = (size - 1) * 0.5f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                float alpha = Mathf.Clamp01(1f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * alpha * 255));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    protected override void Die()
    {
        // Toz bulutu boss'la birlikte bir anda yok olmasın, yavaşça dağılsın
        if (dust != null)
        {
            dust.transform.SetParent(null, true);
            Destroy(dust.gameObject, 1.2f);
        }

        SfxPlayer.Play(deathSound, deathVolume, pitchVariation: 0f, minInterval: 0f, maxVoices: 1);
        base.Die();

        // Boss yenildi: dünya temizlenme finali
        if (GameSession.Instance != null)
            GameSession.Instance.TriggerFinale();
    }
}
