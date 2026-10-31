using UnityEngine;

// Varil düşmanı: Yerde 90° yan yatarak (yatay) durur ve hareket ettikçe
// silindirik olarak yuvarlanıp yalpalanır. Periyodik olarak hızlanıp yuvarlanma hücumu yapar.
public class BarrelEnemy : Enemy
{
    [Header("Yatay Yuvarlanma")]
    [Tooltip("Varilin temel yatış açısı (varsayılan 90 derece)")]
    [SerializeField] private float baseAngle = 90f;
    [Tooltip("Yuvarlanırken sağa sola yalpalama açısı")]
    [SerializeField] private float wobbleAngle = 7f;
    [Tooltip("Yuvarlanma dönüş hızı çarpanı")]
    [SerializeField] private float rollRotationSpeed = 360f;
    [Tooltip("Yerde yuvarlanırken sekme / titreşim miktarı")]
    [SerializeField] private float groundBounceAmount = 0.04f;
    [Tooltip("Silindirik dönme hissi için ezilme/büzülme miktarı")]
    [SerializeField] private float cylindricalSquash = 0.07f;
    [Tooltip("Yuvarlanırken metal yüzey ışık yansıma yoğunluğu")]
    [SerializeField] private float surfaceShineIntensity = 0.2f;

    [Header("Yuvarlanma Hücumu (Roll Rush)")]
    [Tooltip("Özel hücum saldırısı aktif mi?")]
    [SerializeField] private bool canCharge = true;
    [Tooltip("Saldırılar arasındaki süre (saniye)")]
    [SerializeField] private float chargeCooldown = 4f;
    [Tooltip("Saldırıyı başlatmak için oyuncuya olan maksimum mesafe")]
    [SerializeField] private float chargeTriggerDistance = 7f;
    [Tooltip("Saldırı öncesi hazırlanma/titreme süresi")]
    [SerializeField] private float windupDuration = 0.45f;
    [Tooltip("Yuvarlanarak hızlı hücum etme süresi")]
    [SerializeField] private float chargeDuration = 1f;
    [Tooltip("Hücum sırasındaki hız")]
    [SerializeField] private float chargeSpeed = 6f;

    private enum State { Normal, Windup, Charging, Cooldown }
    private State currentState = State.Normal;

    private float stateTimer;
    private float chargeCooldownTimer;
    private Vector2 chargeDirection;
    private float rollPhase;
    private Transform visualTransform;
    private Vector3 baseScale = Vector3.one;

    protected override void Awake()
    {
        base.Awake();
        visualTransform = spriteRenderer != null ? spriteRenderer.transform : transform;
        if (visualTransform != null)
            baseScale = visualTransform.localScale;

        chargeCooldownTimer = Random.Range(1.5f, chargeCooldown);
    }

    protected override void UpdateMovement()
    {
        if (target == null)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        chargeCooldownTimer -= Time.fixedDeltaTime;

        switch (currentState)
        {
            case State.Normal:
                HandleNormalMovement();
                break;
            case State.Windup:
                HandleWindup();
                break;
            case State.Charging:
                HandleCharging();
                break;
            case State.Cooldown:
                HandleCooldown();
                break;
        }

        UpdateHorizontalRollVisual();
    }

    private void HandleNormalMovement()
    {
        Vector2 toTarget = ((Vector2)target.position - rb.position);
        float distance = toTarget.magnitude;
        Vector2 direction = distance > 0.001f ? toTarget / distance : Vector2.zero;

        rb.velocity = direction * moveSpeed;

        if (canCharge && chargeCooldownTimer <= 0f && distance <= chargeTriggerDistance)
        {
            currentState = State.Windup;
            stateTimer = windupDuration;
            chargeDirection = direction;
            rb.velocity = Vector2.zero;
        }
    }

    private void HandleWindup()
    {
        stateTimer -= Time.fixedDeltaTime;
        rb.velocity = Vector2.zero;

        if (target != null)
        {
            Vector2 toTarget = ((Vector2)target.position - rb.position);
            if (toTarget.sqrMagnitude > 0.001f)
                chargeDirection = toTarget.normalized;
        }

        // Hücum öncesi yatay titreme efekti
        float jitter = Mathf.Sin(Time.time * 60f) * 0.07f;
        visualTransform.localPosition = new Vector3(0f, jitter, 0f);

        if (stateTimer <= 0f)
        {
            currentState = State.Charging;
            stateTimer = chargeDuration;
            visualTransform.localPosition = Vector3.zero;
        }
    }

    private void HandleCharging()
    {
        stateTimer -= Time.fixedDeltaTime;
        rb.velocity = chargeDirection * chargeSpeed;

        if (stateTimer <= 0f)
        {
            currentState = State.Cooldown;
            stateTimer = 0.5f;
            chargeCooldownTimer = chargeCooldown;
        }
    }

    private void HandleCooldown()
    {
        stateTimer -= Time.fixedDeltaTime;
        rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, Time.fixedDeltaTime * 6f);

        if (stateTimer <= 0f)
            currentState = State.Normal;
    }

    private void UpdateHorizontalRollVisual()
    {
        if (visualTransform == null) return;

        float speed = rb.velocity.magnitude;
        if (speed > 0.05f)
        {
            float stepDistance = speed * Time.fixedDeltaTime;
            rollPhase += stepDistance * (rollRotationSpeed * Mathf.Deg2Rad);
        }

        // 1. Temel Açı: Varil her zaman yerde 90° yatay yatar
        // Hareket yönüne göre sola/sağa bakış
        float facingAngle = baseAngle;
        if (rb.velocity.x < -0.05f)
            facingAngle = -baseAngle;

        // 2. Yalpalama (Wobble): Yerde yuvarlanırken yüzey eğimine göre hafif salınım
        float wobble = Mathf.Sin(rollPhase * 2f) * wobbleAngle;
        visualTransform.localRotation = Quaternion.Euler(0f, 0f, facingAngle + wobble);

        // 3. Yerde Sekme / Titreşim (Ground Bounce)
        if (currentState != State.Windup)
        {
            float bounce = Mathf.Abs(Mathf.Sin(rollPhase)) * groundBounceAmount;
            visualTransform.localPosition = new Vector3(0f, bounce, 0f);
        }

        // 4. Silindirik Yüzey Ezilme / Dönüş Hissi (Cylindrical Squash)
        float squash = Mathf.Sin(rollPhase * 2f) * cylindricalSquash;
        visualTransform.localScale = new Vector3(
            baseScale.x * (1f + squash),
            baseScale.y * (1f - squash),
            baseScale.z
        );

        // 5. Metal Yüzey Işık Yansıması (Yuvarlanırken parıldayan ışık akışı)
        if (spriteRenderer != null && flashRoutine == null)
        {
            float shine = (Mathf.Sin(rollPhase) + 1f) * 0.5f * surfaceShineIntensity;
            spriteRenderer.color = Color.Lerp(baseColor, Color.white, shine);
        }
    }
}
