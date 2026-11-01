using System.Collections.Generic;
using UnityEngine;

// Tohumla öldürülen düşmanlardan düşen can yenileyici meyve (Object Pool destekli).
// Oyuncu yaklaştığında ona doğru çekilir ve toplandığında PlayerHealth üzerinden can yeniler.
public class FruitPickup : MonoBehaviour
{
    private const int MaxActiveFruits = 30;
    private const int FruitSortingOrder = -4;  // RecyclePickup (-5) ve bitkilerin (-8) üstünde, karakterlerin (0) altında
    private const int ShadowSortingOrder = -5;
    private const float HopDuration = 0.22f;
    private const float HopHeight = 0.28f;
    private const float AttractRadius = 1.25f;
    private const float StartSpeed = 2.6f;
    private const float Acceleration = 28f;
    private const float CollectDistance = 0.22f;

    private static readonly Queue<FruitPickup> activeRing = new();
    private static readonly Stack<FruitPickup> inactivePool = new();
    private static Sprite cachedShadowSprite;
    private static PlayerHealth cachedPlayerHealth;

    private SpriteRenderer spriteRenderer;
    private Transform shadowTransform;
    private SpriteRenderer shadowRenderer;

    private Vector2 spawnOrigin;
    private Vector2 groundPosition;
    private float healAmount;
    private float targetScale;
    private AudioClip collectSound;

    private float age;
    private bool attracted;
    private float moveSpeed;
    private float bobPhase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeRing.Clear();
        inactivePool.Clear();
        cachedPlayerHealth = null;
    }

    public static void Spawn(Vector2 origin, Sprite sprite, float heal, float baseScale, AudioClip sound = null)
    {
        if (sprite == null || heal <= 0f) return;
        if (GameSession.Instance != null && GameSession.Instance.IsOver) return;

        while (activeRing.Count > 0 && activeRing.Peek() == null)
            activeRing.Dequeue();

        FruitPickup pickup = null;
        while (inactivePool.Count > 0 && pickup == null)
            pickup = inactivePool.Pop();

        if (pickup == null && activeRing.Count >= MaxActiveFruits)
        {
            while (activeRing.Count > 0 && pickup == null)
            {
                var candidate = activeRing.Dequeue();
                if (candidate != null && candidate.gameObject.activeSelf)
                    pickup = candidate;
            }
        }

        if (pickup == null)
        {
            var go = new GameObject("FruitPickup");
            pickup = go.AddComponent<FruitPickup>();
            pickup.InitializeVisuals();
        }

        activeRing.Enqueue(pickup);
        pickup.Activate(origin, sprite, heal, baseScale, sound);
    }

    private void InitializeVisuals()
    {
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = FruitSortingOrder;

        var shadowGo = new GameObject("Shadow");
        shadowTransform = shadowGo.transform;
        shadowTransform.SetParent(transform, false);
        shadowRenderer = shadowGo.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = GetOrCreateShadowSprite();
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.32f);
        shadowRenderer.sortingOrder = ShadowSortingOrder;
    }

    private void Activate(Vector2 origin, Sprite sprite, float heal, float baseScale, AudioClip sound)
    {
        if (spriteRenderer == null)
            InitializeVisuals();

        spawnOrigin = origin;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(0.18f, 0.30f);
        groundPosition = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

        healAmount = heal;
        targetScale = Mathf.Max(0.2f, baseScale);
        collectSound = sound;

        age = 0f;
        attracted = false;
        moveSpeed = StartSpeed;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);

        spriteRenderer.sprite = sprite;
        transform.position = new Vector3(origin.x, origin.y, 0f);
        transform.localScale = Vector3.one * (targetScale * 0.6f);

        if (shadowTransform != null)
        {
            shadowTransform.gameObject.SetActive(true);
            shadowTransform.position = new Vector3(origin.x, origin.y - 0.14f, 0f);
            shadowTransform.localScale = new Vector3(0.1f, 0.05f, 1f);
        }

        gameObject.SetActive(true);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        age += dt;
        var player = GetPlayerHealth();

        if (!attracted)
        {
            if (age < HopDuration)
            {
                float t = age / HopDuration;
                Vector2 basePos = Vector2.Lerp(spawnOrigin, groundPosition, t);
                float arc = 4f * t * (1f - t) * HopHeight;
                transform.position = new Vector3(basePos.x, basePos.y + arc, 0f);
                transform.localScale = Vector3.one * Mathf.Lerp(targetScale * 0.6f, targetScale, t);
                UpdateShadow(basePos, Mathf.Lerp(0.6f, 1f, t));
            }
            else
            {
                float bob = Mathf.Sin((age - HopDuration) * 3.4f + bobPhase);
                float yOffset = (bob * 0.5f + 0.5f) * 0.06f;
                float pulse = 1f + bob * 0.04f;
                transform.position = new Vector3(groundPosition.x, groundPosition.y + yOffset, 0f);
                transform.localScale = Vector3.one * (targetScale * pulse);
                UpdateShadow(groundPosition, Mathf.Lerp(1f, 0.85f, bob * 0.5f + 0.5f));
            }

            // Meyve yere sekerken hemen görünebilsin diye kısa bir gecikmeden sonra çekime izin ver
            if (age >= 0.14f && player != null && player.IsAlive)
            {
                Vector2 toPlayer = (Vector2)player.transform.position - groundPosition;
                if (toPlayer.sqrMagnitude <= AttractRadius * AttractRadius)
                {
                    attracted = true;
                    if (shadowTransform != null)
                        shadowTransform.gameObject.SetActive(false);
                }
            }
            return;
        }

        if (player == null || !player.IsAlive)
        {
            attracted = false;
            groundPosition = transform.position;
            if (shadowTransform != null)
                shadowTransform.gameObject.SetActive(true);
            return;
        }

        moveSpeed += Acceleration * dt;
        Vector2 targetPos = player.transform.position;
        Vector2 nextPos = Vector2.MoveTowards(transform.position, targetPos, moveSpeed * dt);
        transform.position = new Vector3(nextPos.x, nextPos.y, 0f);
        transform.localScale = Vector3.one * targetScale;

        if ((targetPos - nextPos).sqrMagnitude <= CollectDistance * CollectDistance)
        {
            Collect(player);
        }
    }

    private void UpdateShadow(Vector2 groundPos, float factor)
    {
        if (shadowTransform == null) return;
        // Dünya uzayında meyvenin hemen altında sabit boyutta gölge tut
        shadowTransform.position = new Vector3(groundPos.x, groundPos.y - 0.14f, 0f);
        float parentScale = Mathf.Max(0.01f, transform.localScale.x);
        float w = (0.30f * factor) / parentScale;
        float h = (0.14f * factor) / parentScale;
        shadowTransform.localScale = new Vector3(w, h, 1f);
    }

    private void Collect(PlayerHealth player)
    {
        player.Heal(healAmount);

        if (DamageNumbers.Instance != null)
            DamageNumbers.Instance.ShowHeal(player.transform.position + Vector3.up * 0.45f, healAmount);

        SeedHitEffect.Spawn(player.transform.position, new Color(0.4f, 1f, 0.5f));

        if (collectSound != null)
            SfxPlayer.Play(collectSound, 0.65f, pitchVariation: 0.05f, minInterval: 0.04f, maxVoices: 3, pitch: 1f);

        Despawn();
    }

    private void Despawn()
    {
        gameObject.SetActive(false);
        inactivePool.Push(this);
    }

    private static PlayerHealth GetPlayerHealth()
    {
        if (cachedPlayerHealth == null)
            cachedPlayerHealth = FindObjectOfType<PlayerHealth>();
        return cachedPlayerHealth;
    }

    private static Sprite GetOrCreateShadowSprite()
    {
        if (cachedShadowSprite != null) return cachedShadowSprite;

        const int size = 24;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                pixels[y * size + x] = (dx * dx + dy * dy <= 1f) ? Color.white : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedShadowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedShadowSprite;
    }
}

