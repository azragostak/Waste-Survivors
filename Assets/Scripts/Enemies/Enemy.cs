using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour, IDamageable
{
    // Sahnedeki tüm düşmanlar. Silahlar hedef bulmak için bunu kullanır (layer ayarı gerekmez).
    private static readonly List<Enemy> active = new();
    public static IReadOnlyList<Enemy> Active => active;

    [SerializeField] protected float maxHealth = 10f;
    [SerializeField] protected float moveSpeed = 1.5f;
    [Tooltip("Esinti, çapraz hareket vs. dahil hiçbir durumda geçemeyeceği hız (oyuncu hızının altında tut)")]
    [SerializeField] protected float maxSpeed = 1.9f;
    [Tooltip("Spawner'da seçilme ağırlığı (2 = 1'e göre iki kat sık çıkar)")]
    [SerializeField, Min(0f)] protected float spawnWeight = 1f;
    [SerializeField] protected Color hitFlashColor = new Color(1f, 0.4f, 0.4f);
    [SerializeField] protected float hitFlashDuration = 0.1f;

    [Tooltip("Ölünce düşen geri dönüşüm parçası (XP)")]
    [SerializeField] protected RecyclePickup recycleDrop;
    [SerializeField, Min(0)] protected int experienceReward = 1;

    // XP gem düşürme, skor vs. için
    public event Action<Enemy> Died;

    public bool IsAlive => health > 0f;
    public float SpawnWeight => spawnWeight;

    protected float health;
    protected Transform target;
    protected Rigidbody2D rb;
    protected SpriteRenderer spriteRenderer;
    protected Color baseColor;
    protected Coroutine flashRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            baseColor = spriteRenderer.color;
    }

    protected virtual void OnEnable()
    {
        health = maxHealth;
        active.Add(this);
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

    protected virtual void FixedUpdate()
    {
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
        Destroy(gameObject);
    }

    protected virtual void DropRecycle()
    {
        if (recycleDrop == null || experienceReward <= 0) return;

        var drop = Instantiate(recycleDrop, transform.position, Quaternion.identity);
        drop.Experience = experienceReward;
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
        float radiusSqr = radius * radius;
        foreach (var enemy in active)
        {
            if (enemy.IsAlive && SqrDistance(enemy, position) <= radiusSqr)
                results.Add(enemy);
        }
    }

    private static float SqrDistance(Enemy enemy, Vector2 position) =>
        ((Vector2)enemy.transform.position - position).sqrMagnitude;
}
