using UnityEngine;

// Varil düşmanı: Rastgele yerlerden çıkar, oyuncuya doğru yatay eksende
// fırıl fırıl yuvarlanarak gelir ve saldırır.
// Yuvarlanma, varil_roll.png karelerinin hıza göre oynatılmasıyla gösterilir.
// Sprite "Visual" child objesinde olmalı: sekme sadece görsele uygulanır, fizik gövdesi etkilenmez.
public class BarrelEnemy : Enemy
{
    [Header("Yatay Yuvarlanma Hareketi")]
    [Tooltip("Yatay eksendeki yuvarlanma hızı")]
    [SerializeField] private float horizontalSpeed = 1.4f;

    [Tooltip("Yön değiştirirken yatay hızın değişme ivmesi (düşük = daha geniş, yavaş dönüş)")]
    [SerializeField] private float horizontalAcceleration = 3f;

    [Tooltip("Oyuncunun dikey (Y) hizasına yönelme hızı")]
    [SerializeField] private float verticalSteerSpeed = 1.8f;

    [Tooltip("Maksimum dikey hız")]
    [SerializeField] private float maxVerticalSpeed = 0.8f;

    [Tooltip("Oyuncuyu geçtikten sonra geri dönme mesafesi")]
    [SerializeField] private float overshootDistance = 2.5f;

    [Header("Yuvarlanma Animasyonu")]
    [Tooltip("Yuvarlanma kareleri (varil_roll.png). Sağa giderken ileri, sola giderken geri oynar.")]
    [SerializeField] private Sprite[] rollFrames;

    [Tooltip("Animasyonun bir tam turu için yuvarlanılan mesafe (dünya birimi). Küçüldükçe daha hızlı döner.")]
    [SerializeField, Min(0.1f)] private float rollDistancePerCycle = 1.2f;

    [Tooltip("Yerde yuvarlanırken hafif sekme/ritim miktarı")]
    [SerializeField] private float groundBounce = 0.03f;

    // Animasyon turunun neresinde olduğu (0-1); hız ne olursa olsun kareler yol ile senkron kalır
    private float rollPhase;
    private float moveDirectionX = 1f;
    private float currentVelocityX;
    private Transform visualTransform;

    protected override void Awake()
    {
        base.Awake();

        if (spriteRenderer != null && spriteRenderer.transform != transform)
            visualTransform = spriteRenderer.transform;
        else
            Debug.LogWarning($"{name}: Sprite bir child objede olmalı, yuvarlanma animasyonu kapalı.", this);
    }

    protected override void Start()
    {
        base.Start();

        // Doğduğu anda oyuncunun solunda mı sağında mı olduğuna göre yatay yönünü belirle
        if (target != null)
        {
            moveDirectionX = transform.position.x < target.position.x ? 1f : -1f;
        }
        else
        {
            moveDirectionX = Random.value > 0.5f ? 1f : -1f;
        }

        // Doğar doğmaz yuvarlanmaya başlasın
        currentVelocityX = moveDirectionX * horizontalSpeed;
    }

    protected override void UpdateMovement()
    {
        if (target == null)
        {
            UpdateHorizontalVelocity();
            rb.velocity = new Vector2(currentVelocityX, 0f);
            return;
        }

        Vector2 targetPos = target.position;
        Vector2 currentPos = rb.position;

        // 1. Yatay Yön Kontrolü:
        // Eğer sağa gidiyorsa ve oyuncuyu overshootDistance kadar geçtiyse sola dön
        if (moveDirectionX > 0f && currentPos.x > targetPos.x + overshootDistance)
        {
            moveDirectionX = -1f;
        }
        // Eğer sola gidiyorsa ve oyuncuyu geçtiyse sağa dön
        else if (moveDirectionX < 0f && currentPos.x < targetPos.x - overshootDistance)
        {
            moveDirectionX = 1f;
        }

        // 2. Dikey Takip: Yatay yuvarlanırken oyuncunun Y hizasına doğru yumuşakça yaklaş
        float diffY = targetPos.y - currentPos.y;
        float targetVy = Mathf.Clamp(diffY * verticalSteerSpeed, -maxVerticalSpeed, maxVerticalSpeed);

        // 3. Hızı Uygula: Yön değişince anında geri dönmez, yavaşlayıp ters yöne hızlanır
        UpdateHorizontalVelocity();
        rb.velocity = new Vector2(currentVelocityX, targetVy);
    }

    private void UpdateHorizontalVelocity()
    {
        currentVelocityX = Mathf.MoveTowards(currentVelocityX, moveDirectionX * horizontalSpeed,
            horizontalAcceleration * Time.fixedDeltaTime);
    }

    // Görsel her karede güncellensin (FixedUpdate'te kesik kesik oynar)
    void Update()
    {
        UpdateRollVisual();
    }

    private void UpdateRollVisual()
    {
        // Sadece yatay hız animasyonu ilerletir: sağa giderken ileri, sola giderken geri.
        // Yön değiştirirken hız düştükçe animasyon da yavaşlar, durur ve ters yöne başlar.
        rollPhase = Mathf.Repeat(rollPhase + currentVelocityX * Time.deltaTime / rollDistancePerCycle, 1f);

        if (spriteRenderer != null && rollFrames != null && rollFrames.Length > 0)
        {
            int frame = Mathf.Min(rollFrames.Length - 1, (int)(rollPhase * rollFrames.Length));
            if (rollFrames[frame] != null)
                spriteRenderer.sprite = rollFrames[frame];
        }

        if (visualTransform == null) return;

        // Yerde yuvarlanırken hafif ritmik sekme (yavaşken azalır)
        float speedFactor = Mathf.Clamp01(Mathf.Abs(currentVelocityX) / Mathf.Max(0.01f, horizontalSpeed));
        float bounce = Mathf.Abs(Mathf.Sin(rollPhase * Mathf.PI * 4f)) * groundBounce * speedFactor;
        visualTransform.localPosition = new Vector3(0f, bounce, 0f);
    }
}
