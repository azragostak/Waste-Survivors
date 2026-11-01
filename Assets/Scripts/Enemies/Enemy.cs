using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour, IDamageable, IHealth
{
    // Sahnedeki tüm düşmanlar. Silahlar hedef bulmak için bunu kullanır (layer ayarı gerekmez).
    private static readonly List<Enemy> active = new();
    public static IReadOnlyList<Enemy> Active => active;

    // UI için: GameHUD bunlarla can barı takar ve hasar sayısı gösterir
    public static event Action<Enemy> Spawned;
    public static event Action<Enemy, float> Damaged;
    // Öldürülen her düşman (kaybolanlar sayılmaz); skor için
    public static event Action<Enemy> Killed;

    [SerializeField] protected float maxHealth = 50f;
    [Tooltip("Zamanla artan düşman canından ne kadar etkilensin (0 = can hep sabit, 1 = tam)")]
    [SerializeField, Range(0f, 1f)] protected float healthScaling = 1f;
    [SerializeField] protected float moveSpeed = 1.5f;
    [Tooltip("Esinti, çapraz hareket vs. dahil hiçbir durumda geçemeyeceği hız (oyuncu hızının altında tut)")]
    [SerializeField] protected float maxSpeed = 1.9f;
    [SerializeField] protected Color hitFlashColor = new Color(1f, 0.4f, 0.4f);
    [SerializeField] protected float hitFlashDuration = 0.1f;
    [Tooltip("Oyuncuya değince verdiği hasar")]
    [SerializeField, Min(0f)] protected float contactDamage = 10f;
    [Tooltip("Büyük düşmanlar için: silahlar merkezden bu kadar uzağa da isabet eder (0 = nokta gibi küçük düşman)")]
    [SerializeField, Min(0f)] protected float bodyRadius;
    [Tooltip("Boss: sersemlemez, mıknatısla çekilmez, can barı ekranın üstünde gösterilir")]
    [SerializeField] protected bool isBoss;

    [Tooltip("Ölünce düşen geri dönüşüm parçası (XP)")]
    [SerializeField] protected RecyclePickup recycleDrop;
    [SerializeField, Min(0)] protected int experienceReward = 1;

    // XP gem düşürme, skor vs. için
    public event Action<Enemy> Died;

    public bool IsAlive => health > 0f && !vanishing;
    public float ContactDamage => Mathf.Max(1f, Mathf.Round(contactDamage * damageMultiplier));
    public float CurrentHealth => Mathf.Max(0f, health);
    public float MaxHealth => RoundToFive(maxHealth * healthMultiplier);

    private static float RoundToFive(float value) => Mathf.Max(5f, Mathf.Round(value / 5f) * 5f);

    // Ölünce düşürdüğü recycle (Manyetik Pres öldürdüklerinin XP'sini oyuncuya çeker)
    public RecyclePickup LastDrop { get; private set; }

    // Sersemleyen düşman yerinden kıpırdamaz ve oyuncuya temas hasarı vermez
    public bool IsStunned => Time.time < stunnedUntil;
    // Elektrik çarpma efekti gibi görseller için
    public SpriteRenderer BodyRenderer => spriteRenderer;
    public Color BaseColor => baseColor;
    public float BodyRadius => bodyRadius;
    public bool IsBoss => isBoss;
    private float stunnedUntil;
    private float stunImmuneUntil;

    // Boss Stampede saldırısı: oyuncuyu takip etmeden düz bir yolda yüksek hızla koşup kaybolur
    public bool IsDashing { get; private set; }
    private float dashUntil;
    private Vector2 dashVelocity;

    protected float health;
    // Zaman ilerledikçe spawner tarafından artırılır (DifficultyTimeline)
    private float healthMultiplier = 1f;
    private float damageMultiplier = 1f;
    private bool vanishing;
    protected Transform target;
    protected Rigidbody2D rb;
    protected SpriteRenderer spriteRenderer;
    protected Color baseColor;
    protected Coroutine flashRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active.Clear();
        Spawned = null;
        Damaged = null;
        Killed = null;
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        if (TryGetComponent<Collider2D>(out var col) && col.sharedMaterial == null)
            col.sharedMaterial = PlayerController.FrictionlessMaterial;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            baseColor = spriteRenderer.color;
    }

    protected virtual void OnEnable()
    {
        health = MaxHealth;
        active.Add(this);
        Spawned?.Invoke(this);
    }

    protected virtual void OnDisable() => active.Remove(this);

    protected virtual void Start()
    {
        if (target == null)
        {
            var player = FindObjectOfType<PlayerController>();
            if (player != null) target = player.transform;
        }
    }

    public void SetTarget(Transform newTarget) => target = newTarget;

    // Spawner doğurduktan hemen sonra çağırır: oyun ilerledikçe düşmanlar daha dayanıklı ve acıtıcı olur
    public void ApplyDifficulty(float healthScale, float damageScale)
    {
        healthMultiplier = Mathf.Max(0.1f, 1f + (healthScale - 1f) * healthScaling);
        damageMultiplier = Mathf.Max(0.1f, damageScale);
        health = MaxHealth;
    }

    // Final: ödül düşürmeden küçülüp kaybolur
    public void Vanish(float duration = 0.35f)
    {
        if (vanishing) return;
        vanishing = true;

        rb.velocity = Vector2.zero;
        rb.simulated = false;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        StartCoroutine(VanishRoutine(duration));
    }

    private IEnumerator VanishRoutine(float duration)
    {
        Vector3 startScale = transform.localScale;
        Color startColor = spriteRenderer != null ? baseColor : Color.white;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float p = t / duration;
            // Önce hafifçe şişip beyazlaşır, sonra küçülerek söner
            float scale = p < 0.3f ? Mathf.Lerp(1f, 1.2f, p / 0.3f) : Mathf.Lerp(1.2f, 0f, (p - 0.3f) / 0.7f);
            transform.localScale = startScale * scale;
            if (spriteRenderer != null)
            {
                Color color = Color.Lerp(startColor, Color.white, Mathf.Min(1f, p * 3f));
                color.a = 1f - p;
                spriteRenderer.color = color;
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    // Sersemlet. Bitince 'immunity' sn boyunca tekrar sersemlemez (sürekli donup kalmasın).
    // Sersemlediyse true döner.
    public bool TryStun(float duration, float immunity)
    {
        if (!IsAlive || isBoss || Time.time < stunImmuneUntil) return false;

        stunnedUntil = Time.time + duration;
        stunImmuneUntil = stunnedUntil + immunity;
        return true;
    }

    // Verilen hızla 'duration' sn düz koşar, sonra ödül düşürmeden kaybolur.
    // Koşarken diğer düşmanlara ve oyuncuya takılmaz (trigger), değdiği oyuncuya yine hasar verir.
    public void ForceDash(Vector2 velocity, float duration)
    {
        IsDashing = true;
        dashVelocity = velocity;
        dashUntil = Time.time + duration;
        if (TryGetComponent(out Collider2D col))
            col.isTrigger = true;
        if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.01f)
            spriteRenderer.flipX = velocity.x < 0f;
    }

    protected virtual void FixedUpdate()
    {
        if (vanishing) return;
        if (IsDashing)
        {
            if (Time.time >= dashUntil)
            {
                IsDashing = false;
                Vanish();
                return;
            }
            rb.velocity = dashVelocity;
            return;
        }
        if (IsStunned)
        {
            rb.velocity = Vector2.zero;
            return;
        }
        UpdateMovement();

        // Alt sınıf ne hesaplarsa hesaplasın oyuncudan hızlı olamasın
        if (rb.velocity.sqrMagnitude > maxSpeed * maxSpeed)
            rb.velocity = rb.velocity.normalized * maxSpeed;
    }

    protected virtual void UpdateMovement()
    {
        if (target == null)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        Vector2 direction = ((Vector2)target.position - rb.position).normalized;
        rb.velocity = direction * moveSpeed;

        if (spriteRenderer != null && direction.x != 0f)
            spriteRenderer.flipX = direction.x < 0f;
    }

    public virtual void TakeDamage(float amount)
    {
        if (!IsAlive) return;

        health -= amount;
        Damaged?.Invoke(this, amount);

        if (health <= 0f)
        {
            Die();
            return;
        }

        if (spriteRenderer != null)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(HitFlash());
        }
    }

    protected virtual IEnumerator HitFlash()
    {
        spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        spriteRenderer.color = baseColor;
        flashRoutine = null;
    }

    protected virtual void Die()
    {
        DropRecycle();
        Died?.Invoke(this);
        Killed?.Invoke(this);
        Destroy(gameObject);
    }

    protected virtual void DropRecycle()
    {
        if (recycleDrop == null || experienceReward <= 0) return;

        LastDrop = RecyclePickup.Drop(recycleDrop, transform.position, experienceReward);
    }

    // position'a en yakın en fazla 'count' düşmanı (range içinde) yakından uzağa sıralı döndürür
    public static void GetNearest(Vector2 position, float range, int count, List<Enemy> results)
    {
        GetInRadius(position, range, results);
        results.Sort((a, b) => SqrDistance(a, position).CompareTo(SqrDistance(b, position)));
        if (results.Count > count)
            results.RemoveRange(count, results.Count - count);
    }

    public static void GetInRadius(Vector2 position, float radius, List<Enemy> results)
    {
        results.Clear();
        foreach (var enemy in active)
        {
            // Büyük düşmanlara kenarından da isabet edilsin
            float reach = radius + enemy.bodyRadius;
            if (enemy.IsAlive && SqrDistance(enemy, position) <= reach * reach)
                results.Add(enemy);
        }
    }

    private static float SqrDistance(Enemy enemy, Vector2 position) =>
        ((Vector2)enemy.transform.position - position).sqrMagnitude;
}
