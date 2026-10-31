using System;
using UnityEngine;

// Oyuncunun canı. Player objesine eklenir.
// Düşmanlara değince onların Contact Damage'i kadar hasar alır; vurulduktan sonra kısa süre hasar almaz.
public class PlayerHealth : MonoBehaviour, IDamageable, IHealth
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [Tooltip("Hasar aldıktan sonra tekrar hasar alamadığı süre (sn)")]
    [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.5f;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.4f, 0.4f);

    private float health;
    private float invulnerableTimer;
    private SpriteRenderer spriteRenderer;
    private Color baseColor;

    public float CurrentHealth => health;
    // Pasif itemlerden gelen bonus dahil
    public float MaxHealth => maxHealth + PlayerPassives.GetBonus(this, PassiveStat.MaxHealth);
    public bool IsAlive => health > 0f;

    public event Action<PlayerHealth> HealthChanged;
    // Game over ekranı buna bağlanacak
    public event Action Died;

    void Awake()
    {
        health = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            baseColor = spriteRenderer.color;
    }

    void Update()
    {
        if (invulnerableTimer <= 0f) return;

        invulnerableTimer -= Time.deltaTime;

        // Vurulunca kırmızı yanıp yavaşça normale döner
        if (spriteRenderer != null)
        {
            float t = invulnerabilityDuration > 0f ? Mathf.Clamp01(invulnerableTimer / invulnerabilityDuration) : 0f;
            spriteRenderer.color = Color.Lerp(baseColor, hitFlashColor, t);
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (invulnerableTimer > 0f || !IsAlive) return;

        var enemy = collision.collider.GetComponentInParent<Enemy>();
        if (enemy != null && enemy.IsAlive)
            TakeDamage(enemy.ContactDamage);
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f || invulnerableTimer > 0f) return;

        health = Mathf.Max(0f, health - amount);
        invulnerableTimer = invulnerabilityDuration;
        HealthChanged?.Invoke(this);

        if (health <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f) return;

        health = Mathf.Min(MaxHealth, health + amount);
        HealthChanged?.Invoke(this);
    }

    private void Die()
    {
        Debug.Log("Oyuncu öldü!");

        // Game over ekranı gelene kadar: oyuncu hareket edemesin
        if (TryGetComponent(out PlayerController controller))
            controller.enabled = false;
        if (TryGetComponent(out Rigidbody2D rb))
            rb.velocity = Vector2.zero;

        Died?.Invoke();
    }
}
