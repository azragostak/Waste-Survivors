using UnityEngine;

// Plastik Poşet düşmanı: Havada rüzgarla süzülerek, dalgalı ve hafif hareketlerle uçar.
// Düz bir çizgide gitmez; rüzgarda salınır, havada hafifçe aşağı yukarı yükselip alçalır ve kanatlanır.
public class PlasticBagEnemy : Enemy
{
    [Header("Uçuş ve Rüzgar Hareketi")]
    [Tooltip("Rüzgarda sağa sola salınma frekansı")]
    [SerializeField] private float swayFrequency = 2.5f;
    [Tooltip("Rüzgarda sağa sola salınma genliği/genişliği")]
    [SerializeField] private float swayAmplitude = 1.3f;
    [Tooltip("Havadaki dikey dalgalanma (bobbing) hızı")]
    [SerializeField] private float bobFrequency = 3f;
    [Tooltip("Havadaki dikey dalgalanma yüksekliği")]
    [SerializeField] private float bobAmplitude = 0.35f;
    [Tooltip("Rüzgarla yön değiştirirken ivmelenme yumuşaklığı")]
    [SerializeField] private float acceleration = 3.5f;

    [Header("Görsel Süzülme")]
    [Tooltip("Rüzgar yönüne göre yana yatma açısı (tilt)")]
    [SerializeField] private float maxTiltAngle = 20f;
    [Tooltip("Hava akımıyla şişip sönme (flutter) hızı")]
    [SerializeField] private float flutterSpeed = 6f;
    [Tooltip("Hava akımıyla şişip sönme miktarı")]
    [SerializeField] private float flutterAmount = 0.08f;

    [Header("Rüzgar Esintisi (Gust)")]
    [Tooltip("Zaman zaman rüzgar yakalayıp hızlanma özelliği")]
    [SerializeField] private bool canCatchGust = true;
    [Tooltip("Esintiler arası süre")]
    [SerializeField] private float gustInterval = 5f;
    [Tooltip("Esinti hızı çarpanı")]
    [SerializeField] private float gustSpeedMultiplier = 1.8f;
    [Tooltip("Esinti süresi")]
    [SerializeField] private float gustDuration = 1.2f;

    private float randomPhase;
    private float gustTimer;
    private float currentGustTime;
    private Transform visualTransform;
    private Vector3 baseScale = Vector3.one;

    protected override void Awake()
    {
        base.Awake();
        visualTransform = spriteRenderer != null ? spriteRenderer.transform : transform;
        if (visualTransform != null)
            baseScale = visualTransform.localScale;

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

        UpdateFlightVisual(sway);
    }

    private void UpdateFlightVisual(float sway)
    {
        if (visualTransform == null) return;

        // Rüzgarın estiği yöne doğru kanatlanma / yana yatma
        float tilt = -sway * maxTiltAngle;
        visualTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        // Uçarken hava dolup boşalma (flutter) efekti
        float flutter = Mathf.Sin(Time.time * flutterSpeed + randomPhase) * flutterAmount;
        visualTransform.localScale = new Vector3(baseScale.x * (1f + flutter), baseScale.y * (1f - flutter), baseScale.z);

        // Uçuş yönüne göre flip
        if (spriteRenderer != null && rb.velocity.x != 0f)
            spriteRenderer.flipX = rb.velocity.x < 0f;
    }
}
