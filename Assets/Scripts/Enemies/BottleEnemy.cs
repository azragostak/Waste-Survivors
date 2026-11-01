using UnityEngine;

// Şişe düşmanı: sürüler halinde gelen, canı az ve kalabalık düşman.
// Her şişe doğarken Variants listesindeki görsellerden birini rastgele alır (cam, gazoz, su şişesi); özellikleri aynıdır.
// Yürürken dibinin köşeleri üzerinde sağa sola sallanır.
// Sprite "Visual" child objesinde olmalı: sallanma sadece görsele uygulanır, fizik gövdesi etkilenmez.
public class BottleEnemy : Enemy
{
    [Header("Görsel Varyantlar")]
    [Tooltip("Doğarken bunlardan biri rastgele seçilir")]
    [SerializeField] private Sprite[] variants;

    [Header("Sallanarak Yürüme")]
    [Tooltip("Sağa sola sallanma hızı")]
    [SerializeField] private float waddleFrequency = 9f;
    [Tooltip("Sallanma açısı (derece)")]
    [SerializeField] private float waddleAngle = 12f;
    [Tooltip("Her adımda hafif zıplama yüksekliği")]
    [SerializeField] private float stepHop = 0.03f;

    private Transform visualTransform;
    private Vector3 visualBasePosition;
    private float phase;

    protected override void Awake()
    {
        base.Awake();

        if (spriteRenderer != null && variants != null && variants.Length > 0)
        {
            var variant = variants[Random.Range(0, variants.Length)];
            if (variant != null)
                spriteRenderer.sprite = variant;
        }

        if (spriteRenderer != null && spriteRenderer.transform != transform)
        {
            visualTransform = spriteRenderer.transform;
            visualBasePosition = visualTransform.localPosition;
        }
        else
        {
            Debug.LogWarning($"{name}: Sprite bir child objede olmalı, sallanma animasyonu kapalı.", this);
        }

        // Sürüdeki şişeler aynı anda sallanmasın
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    // Görsel her karede güncellensin (FixedUpdate'te kesik kesik oynar)
    void Update()
    {
        if (visualTransform == null) return;

        // Dururken sallanma söner
        float moving = Mathf.Clamp01(rb.velocity.magnitude / Mathf.Max(0.01f, moveSpeed));
        float t = Time.time * waddleFrequency + phase;

        visualTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t) * waddleAngle * moving);
        visualTransform.localPosition = visualBasePosition + Vector3.up * (Mathf.Abs(Mathf.Cos(t)) * stepHop * moving);
    }
}
