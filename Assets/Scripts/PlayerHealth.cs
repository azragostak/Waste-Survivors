using System;
using UnityEngine;

// Oyuncunun canı. Player objesine eklenir.
// Düşmanlara değince onların Contact Damage'i kadar hasar alır; vurulduktan sonra kısa süre hasar almaz.
// Bir süre hasar almazsa yavaşça iyileşir: kalabalıktan sıyrılmak ödüllendirilir, oyun affedici kalır.
public class PlayerHealth : MonoBehaviour, IDamageable, IHealth
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [Tooltip("Hasar aldıktan sonra tekrar hasar alamadığı süre (sn)")]
    [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.5f;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.4f, 0.4f);

    [Header("Yenilenme")]
    [Tooltip("Saniyede yenilenen can")]
    [SerializeField, Min(0f)] private float regenPerSecond = 1f;
    [Tooltip("Hasar aldıktan kaç sn sonra yenilenme başlasın")]
    [SerializeField, Min(0f)] private float regenDelay = 3f;

    private float health;
    private float invulnerableTimer;
    private float regenTimer;
    private float lifeStealWindowStart;
    private float lifeStealInWindow;
    private float lifeStealDisplayRemainder;
    private SpriteRenderer spriteRenderer;
    private Color baseColor;

    public float CurrentHealth => health;
    // Pasif itemlerden gelen bonus dahil
    public float MaxHealth => maxHealth + PlayerPassives.GetBonus(this, PassiveStat.MaxHealth);
    public bool IsAlive => health > 0f;
    // Gaz maskesi gibi itemler en fazla bu kadar hasar azaltabilir
    public const float MaxDamageReduction = 0.6f;
    // Finalde düşmanlar kaybolurken oyuncu hasar almasın
    public bool IsInvulnerable { get; set; }

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
        UpdateRegen();

        if (invulnerableTimer <= 0f) return;

        invulnerableTimer -= Time.deltaTime;

        // Vurulunca kırmızı yanıp yavaşça normale döner
        if (spriteRenderer != null)
        {
            float t = invulnerabilityDuration > 0f ? Mathf.Clamp01(invulnerableTimer / invulnerabilityDuration) : 0f;
            spriteRenderer.color = Color.Lerp(baseColor, hitFlashColor, t);
        }
    }

    private void UpdateRegen()
    {
        if (!IsAlive || regenPerSecond <= 0f) return;

        if (regenTimer > 0f)
        {
            regenTimer -= Time.deltaTime;
            return;
        }

        if (health < MaxHealth)
            health = Mathf.Min(MaxHealth, health + regenPerSecond * Time.deltaTime);
    }

    void OnCollisionStay2D(Collision2D collision) => TouchEnemy(collision.collider);

    // Boss Stampede'sinde koşan düşmanlar trigger olur; onlar da acıtsın
    void OnTriggerStay2D(Collider2D other) => TouchEnemy(other);

    private void TouchEnemy(Collider2D other)
    {
        if (invulnerableTimer > 0f || !IsAlive) return;

        var enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null && enemy.IsAlive && !enemy.IsStunned)
            TakeDamage(enemy.ContactDamage);
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f || invulnerableTimer > 0f || IsInvulnerable) return;

        float reduction = Mathf.Min(MaxDamageReduction, PlayerPassives.GetBonus(this, PassiveStat.DamageReduction));
        health = Mathf.Max(0f, health - amount * (1f - reduction));
        invulnerableTimer = invulnerabilityDuration;

        var sounds = GameSounds.Instance;
        if (sounds != null)
            SfxPlayer.Play(sounds.playerHurt, sounds.playerHurtVolume, pitchVariation: 0.06f, minInterval: 0.1f, maxVoices: 2);
        regenTimer = regenDelay;
        HealthChanged?.Invoke(this);

        if (health <= 0f)
            Die();
    }

    // Can çalma: hasar verdikçe iyileşir, ama 1 sn'lik pencerede en fazla maxPerSecond kadar
    // (hızlı silahlar oyuncuyu ölümsüz yapmasın)
    public void LifeSteal(float amount, float maxPerSecond)
    {
        if (!IsAlive || amount <= 0f || maxPerSecond <= 0f || health >= MaxHealth) return;

        if (Time.time - lifeStealWindowStart >= 1f)
        {
            lifeStealWindowStart = Time.time;
            lifeStealInWindow = 0f;
        }

        float allowed = Mathf.Min(amount, maxPerSecond - lifeStealInWindow);
        if (allowed <= 0f) return;

        float before = health;
        Heal(allowed);
        float healed = health - before;
        if (healed <= 0f) return;

        lifeStealInWindow += healed;
        lifeStealDisplayRemainder += healed;

        int displayAmount = Mathf.FloorToInt(lifeStealDisplayRemainder + 0.05f);
        if (displayAmount >= 1)
        {
            lifeStealDisplayRemainder = Mathf.Max(0f, lifeStealDisplayRemainder - displayAmount);
            if (DamageNumbers.Instance != null)
            {
                Vector3 jitter = new Vector3(UnityEngine.Random.Range(-0.12f, 0.12f), UnityEngine.Random.Range(0f, 0.08f), 0f);
                DamageNumbers.Instance.ShowHeal(transform.position + Vector3.up * 0.45f + jitter, displayAmount);
            }
        }
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
