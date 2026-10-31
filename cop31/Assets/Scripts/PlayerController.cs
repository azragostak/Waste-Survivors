using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private Vector2 moveInput;

    // Silahlar vs. için son bakılan yön (oyuncu dururken de geçerli kalır)
    public Vector2 LastMoveDirection { get; private set; } = Vector2.right;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Top-down oyun: yerçekimi yok, karakter dönmesin
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
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
        rb.velocity = moveInput * moveSpeed;
    }
}
