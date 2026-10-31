using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private InputAction moveAction;

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

        // Project Settings > Input System'deki InputSystem_Actions (WASD, ok tuşları, gamepad)
        moveAction = InputSystem.actions.FindAction("Player/Move");
    }

    void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();

        // Çaprazda daha hızlı gitmesin diye normalize et (gamepad'de yarım basışlar korunur)
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
        rb.linearVelocity = moveInput * moveSpeed;
    }
}
