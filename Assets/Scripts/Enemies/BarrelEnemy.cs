using UnityEngine;

// Varil düşmanı: Rastgele yerlerden çıkar, oyuncuya doğru yatay eksende
// fırıl fırıl yuvarlanarak gelir ve saldırır.
public class BarrelEnemy : Enemy
{
    [Header("Yatay Yuvarlanma Hareketi")]
    [Tooltip("Yatay eksendeki yuvarlanma hızı")]
    [SerializeField] private float horizontalSpeed = 4f;

    [Tooltip("Oyuncunun dikey (Y) hizasına yönelme hızı")]
    [SerializeField] private float verticalSteerSpeed = 1.8f;

    [Tooltip("Maksimum dikey hız")]
    [SerializeField] private float maxVerticalSpeed = 2f;

    [Tooltip("Oyuncuyu geçtikten sonra geri dönme mesafesi")]
    [SerializeField] private float overshootDistance = 2.5f;

    [Header("Yuvarlanma Dönüş Görseli")]
    [Tooltip("Yuvarlanma yarıçapı (dönüş hızını belirler)")]
    [SerializeField] private float rollRadius = 0.32f;

    [Tooltip("Yerde yuvarlanırken hafif sekme/ritim miktarı")]
    [SerializeField] private float groundBounce = 0.04f;

    private float currentRollAngle;
    private float moveDirectionX = 1f;
    private Transform visualTransform;

    protected override void Awake()
    {
        base.Awake();
        visualTransform = spriteRenderer != null ? spriteRenderer.transform : transform;
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
    }

    protected override void UpdateMovement()
    {
        if (target == null)
        {
            rb.velocity = new Vector2(moveDirectionX * horizontalSpeed, 0f);
            UpdateRollVisual();
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

        // 3. Hızı Uygula: Daima güçlü bir yatay hız ile yuvarlanır
        rb.velocity = new Vector2(moveDirectionX * horizontalSpeed, targetVy);

        // 4. Görsel Yuvarlanma: Sürekli 360 derece döner
        UpdateRollVisual();
    }

    private void UpdateRollVisual()
    {
        if (visualTransform == null) return;

        float speed = rb.velocity.magnitude;
        if (speed > 0.05f)
        {
            // Sağa gidiyorsa saat yönünde (-), sola gidiyorsa saat yönünün tersine (+) döner
            float dirSign = moveDirectionX > 0f ? -1f : 1f;
            float stepDistance = speed * Time.fixedDeltaTime;
            float angleDelta = dirSign * (stepDistance / Mathf.Max(0.05f, rollRadius)) * Mathf.Rad2Deg;

            currentRollAngle = (currentRollAngle + angleDelta) % 360f;
            visualTransform.localRotation = Quaternion.Euler(0f, 0f, currentRollAngle);

            // Yerde yuvarlanırken hafif ritmik sekme
            float bounce = Mathf.Abs(Mathf.Sin(currentRollAngle * Mathf.Deg2Rad * 2f)) * groundBounce;
            visualTransform.localPosition = new Vector3(0f, bounce, 0f);
        }
    }
}
