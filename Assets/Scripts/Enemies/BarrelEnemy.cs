using UnityEngine;

// Varil düşmanı: Rastgele yerlerden çıkar, oyuncuya doğru yatay eksende
// fırıl fırıl yuvarlanarak gelir ve saldırır.
// Sprite "Visual" child objesinde olmalı: dönme/sekme sadece görsele uygulanır, fizik gövdesi etkilenmez.
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

    [Header("Yuvarlanma Dönüş Görseli")]
    [Tooltip("Yuvarlanma yarıçapı (dönüş hızını belirler)")]
    [SerializeField] private float rollRadius = 0.32f;

    [Tooltip("Yerde yuvarlanırken hafif sekme/ritim miktarı")]
    [SerializeField] private float groundBounce = 0.04f;

    private float currentRollAngle;
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

    // Görsel her karede güncellensin (FixedUpdate'te kesik kesik dönüyordu)
    void Update()
    {
        UpdateRollVisual();
    }

    private void UpdateRollVisual()
    {
        if (visualTransform == null) return;

        // Sadece yatay hız döndürür: sağa giderken saat yönünde (-), sola giderken tersine (+).
        // Yön değiştirirken hız düştükçe dönüş de yavaşlar, durur ve ters yöne başlar.
        float angleDelta = -currentVelocityX * Time.deltaTime / Mathf.Max(0.05f, rollRadius) * Mathf.Rad2Deg;
        currentRollAngle = (currentRollAngle + angleDelta) % 360f;
        visualTransform.localRotation = Quaternion.Euler(0f, 0f, currentRollAngle);

        // Yerde yuvarlanırken hafif ritmik sekme (yavaşken azalır)
        float speedFactor = Mathf.Clamp01(Mathf.Abs(currentVelocityX) / Mathf.Max(0.01f, horizontalSpeed));
        float bounce = Mathf.Abs(Mathf.Sin(currentRollAngle * Mathf.Deg2Rad * 2f)) * groundBounce * speedFactor;
        visualTransform.localPosition = new Vector3(0f, bounce, 0f);
    }
}
