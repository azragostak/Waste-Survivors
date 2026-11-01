using UnityEngine;

// Enemy.FixedUpdate (varsayılan 0) çalıştıktan hemen sonra çalışsın ki temastaki düşmanları yana ayırabilsin
[DefaultExecutionOrder(10)]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    private static PhysicsMaterial2D frictionlessMaterial;

    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("Oyuncu yürürken önündeki düşmanları iki yana ayırma hızı çarpanı")]
    [SerializeField, Range(0f, 2f)] private float enemyPartRatio = 0.85f;

    private readonly ContactPoint2D[] contacts = new ContactPoint2D[32];

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private Vector2 moveInput;

    // Silahlar vs. için son bakılan yön (oyuncu dururken de geçerli kalır)
    public Vector2 LastMoveDirection { get; private set; } = Vector2.right;

    public static PhysicsMaterial2D FrictionlessMaterial
    {
        get
        {
            if (frictionlessMaterial == null)
            {
                frictionlessMaterial = new PhysicsMaterial2D("FrictionlessCharacter")
                {
                    friction = 0f,
                    bounciness = 0f
                };
            }
            return frictionlessMaterial;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => frictionlessMaterial = null;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Top-down oyun: yerçekimi yok, karakter dönmesin, düşmanlar tarafından ittirilemesin (Kinematic)
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (playerCollider != null && playerCollider.sharedMaterial == null)
            playerCollider.sharedMaterial = FrictionlessMaterial;
    }

    void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(x, y);

        // Çaprazda daha hızlı gitmesin diye normalize et
        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();

        if (moveInput.sqrMagnitude > 0.01f)
            LastMoveDirection = moveInput.normalized;

        if (spriteRenderer != null && moveInput.x != 0f)
            spriteRenderer.flipX = moveInput.x < 0f;

        if (animator != null)
            animator.SetFloat("Speed", moveInput.magnitude);
    }

    void FixedUpdate()
    {
        Vector2 velocity = moveInput * (moveSpeed * (1f + PlayerPassives.GetBonus(this, PassiveStat.MoveSpeed)));
        rb.velocity = velocity;

        if (velocity.sqrMagnitude > 0.01f && enemyPartRatio > 0f)
            PartTouchingEnemies(velocity);
    }

    private void PartTouchingEnemies(Vector2 playerVelocity)
    {
        float speed = playerVelocity.magnitude;
        Vector2 forward = playerVelocity / speed;
        Vector2 perp = new Vector2(-forward.y, forward.x);
        Vector2 playerCenter = playerCollider != null ? (Vector2)playerCollider.bounds.center : rb.position;
        float minSideSpeed = speed * enemyPartRatio;

        int contactCount = rb.GetContacts(contacts);
        for (int i = 0; i < contactCount; i++)
        {
            Rigidbody2D otherRb = contacts[i].rigidbody != rb ? contacts[i].rigidbody : contacts[i].otherRigidbody;
            if (otherRb == null || otherRb.bodyType != RigidbodyType2D.Dynamic)
                continue;

            Vector2 toEnemy = otherRb.position - playerCenter;
            if (Vector2.Dot(toEnemy, forward) < -0.15f)
                continue;

            float sideDist = Vector2.Dot(toEnemy, perp);
            float sideSign = Mathf.Abs(sideDist) > 0.02f
                ? Mathf.Sign(sideDist)
                : ((otherRb.GetInstanceID() & 1) == 0 ? 1f : -1f);

            Vector2 sideDir = perp * sideSign;
            float currentSideSpeed = Vector2.Dot(otherRb.velocity, sideDir);
            if (currentSideSpeed < minSideSpeed)
                otherRb.velocity += sideDir * (minSideSpeed - currentSideSpeed);
        }
    }
}
