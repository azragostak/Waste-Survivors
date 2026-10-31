using UnityEngine;

// Plastik Poşet düşmanı: Havada rüzgarla süzülerek, dalgalı ve hafif hareketlerle uçar.
// Düz bir çizgide gitmez; rüzgarda salınır, havada hafifçe aşağı yukarı yükselip alçalır ve kanatlanır.
// Sprite "Visual" child objesinde olmalı: yatma/şişme sadece görsele uygulanır, fizik gövdesi etkilenmez.
public class PlasticBagEnemy : Enemy
{
    [Header("Uçuş ve Rüzgar Hareketi")]
    [Tooltip("Rüzgarda sağa sola salınma frekansı")]
    [SerializeField] private float swayFrequency = 1.2f;
    [Tooltip("Rüzgarda sağa sola salınma genliği/genişliği")]
    [SerializeField] private float swayAmplitude = 0.45f;
    [Tooltip("Havadaki dikey dalgalanma (bobbing) hızı")]
    [SerializeField] private float bobFrequency = 1.5f;
    [Tooltip("Havadaki dikey dalgalanma yüksekliği")]
    [SerializeField] private float bobAmplitude = 0.2f;
    [Tooltip("Rüzgarla yön değiştirirken ivmelenme yumuşaklığı")]
    [SerializeField] private float acceleration = 3.5f;

    [Header("Görsel Süzülme")]
    [Tooltip("Rüzgar yönüne göre yana yatma açısı (tilt)")]
    [SerializeField] private float maxTiltAngle = 12f;
    [Tooltip("Hava akımıyla şişip sönme (flutter) hızı")]
    [SerializeField] private float flutterSpeed = 4f;
    [Tooltip("Hava akımıyla şişip sönme miktarı")]
    [SerializeField] private float flutterAmount = 0.05f;

    [Header("Rüzgar Esintisi (Gust)")]
    [Tooltip("Zaman zaman rüzgar yakalayıp hızlanma özelliği")]
    [SerializeField] private bool canCatchGust = true;
    [Tooltip("Esintiler arası süre")]
    [SerializeField] private float gustInterval = 5f;
    [Tooltip("Esinti hızı çarpanı")]
    [SerializeField] private float gustSpeedMultiplier = 1.4f;
    [Tooltip("Esinti süresi")]
    [SerializeField] private float gustDuration = 1.2f;

    private float randomPhase;
    private float gustTimer;
    private float currentGustTime;
    private Transform visualTransform;
    private Vector3 baseScale = Vector3.one;
    private bool facingLeft;

    protected override void Awake()
    {
        base.Awake();
        if (spriteRenderer != null && spriteRenderer.transform != transform)
        {
            visualTransform = spriteRenderer.transform;
            baseScale = visualTransform.localScale;
        }
        else
        {
            Debug.LogWarning($"{name}: Sprite bir child objede olmalı, süzülme animasyonu kapalı.", this);
        }

        // Her poşetin hareketi farklı olsun diye rastgele faz kaydırması
        randomPhase = Random.Range(0f, 100f);
        gustTimer = Random.Range(1.5f, gustInterval);
    }

    protected override void UpdateMovement()
    {
        if (target == null)
        {
            rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, Time.fixedDeltaTime * 2f);
            return;
        }

        Vector2 toTarget = (Vector2)target.position - rb.position;
        float distance = toTarget.magnitude;
        Vector2 directDir = distance > 0.001f ? toTarget / distance : Vector2.zero;

        // Rüzgar esintisi (Gust) kontrolü
        float currentSpeed = moveSpeed;
        if (canCatchGust)
        {
            gustTimer -= Time.fixedDeltaTime;
            if (gustTimer <= 0f && currentGustTime <= 0f)
            {
                currentGustTime = gustDuration;
                gustTimer = gustInterval + Random.Range(-1f, 1.5f);
            }

            if (currentGustTime > 0f)
            {
                currentGustTime -= Time.fixedDeltaTime;
                currentSpeed *= gustSpeedMultiplier;
            }
        }

        // Rüzgarda salınım: hedefe giden yöne dik eksende sinüs dalgası
        Vector2 perp = new Vector2(-directDir.y, directDir.x);
        float sway = Mathf.Sin((Time.time + randomPhase) * swayFrequency);
        float bob = Mathf.Sin((Time.time + randomPhase) * bobFrequency);

        // Hedefe doğru git ama rüzgarın salınımı yönü eğsin
        Vector2 flightDir = (directDir + perp * (sway * swayAmplitude) + Vector2.up * (bob * bobAmplitude * 0.5f)).normalized;

        // Havadaki hafif ve yumuşak ivmelenme
        Vector2 targetVelocity = flightDir * currentSpeed;
        rb.velocity = Vector2.Lerp(rb.velocity, targetVelocity, Time.fixedDeltaTime * acceleration);

        // Salınımda hız x'i sürekli işaret değiştirip sprite'ı titretiyordu; oyuncunun hangi tarafta olduğuna bak
        if (Mathf.Abs(directDir.x) > 0.3f)
            facingLeft = directDir.x < 0f;
    }

    // Görsel her karede güncellensin (FixedUpdate'te kesik kesik oynuyordu)
    void Update()
    {
        UpdateFlightVisual();
    }

    private void UpdateFlightVisual()
    {
        if (visualTransform == null) return;

        float sway = Mathf.Sin((Time.time + randomPhase) * swayFrequency);

        // Rüzgarın estiği yöne doğru kanatlanma / yana yatma
        float tilt = -sway * maxTiltAngle;
        visualTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        // Uçarken hava dolup boşalma (flutter) efekti
        float flutter = Mathf.Sin(Time.time * flutterSpeed + randomPhase) * flutterAmount;
        visualTransform.localScale = new Vector3(baseScale.x * (1f + flutter), baseScale.y * (1f - flutter), baseScale.z);

        spriteRenderer.flipX = facingLeft;
    }
}
