using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Düşmanlar hasar alınca üstlerinde yükselip kaybolan hasar sayıları.
// GameHUD oluşturur; yazı objeleri havuzda tutulup tekrar kullanılır.
public class DamageNumbers : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.7f;
    [SerializeField] private float riseDistance = 0.45f;
    [Tooltip("Dünya biriminde yazı boyutu (3 ≈ 0.3 birim yükseklik)")]
    [SerializeField] private float fontSize = 3f;
    [SerializeField] private Color normalColor = Color.white;
    [Tooltip("Bu hasar ve üstü sarı ve biraz büyük gösterilir")]
    [SerializeField] private float bigHitThreshold = 10f;
    [SerializeField] private Color bigHitColor = new Color(1f, 0.85f, 0.25f);
    [Tooltip("Asit yağmuru damlalarının (100) üstünde kalsın")]
    [SerializeField] private int sortingOrder = 110;

    private class Number
    {
        public TextMeshPro text;
        public Vector3 start;
        public float age;
        public float scale;
    }

    public static DamageNumbers Instance { get; private set; }

    private readonly List<Number> active = new();
    private readonly Stack<Number> pool = new();
    private TMP_FontAsset font;
    private Material outlineMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    void Awake() => Instance = this;

    public void Initialize(TMP_FontAsset fontAsset)
    {
        font = fontAsset;

        // Tüm sayılar tek bir siyah kenarlı materyal kullanır; zemin ne renk olursa olsun okunur
        outlineMaterial = new Material(font.material);
        outlineMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
        outlineMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
        outlineMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        outlineMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);
    }

    void OnEnable() => Enemy.Damaged += OnEnemyDamaged;

    void OnDisable() => Enemy.Damaged -= OnEnemyDamaged;

    private void OnEnemyDamaged(Enemy enemy, float amount)
    {
        if (font == null || amount <= 0f) return;

        // Aynı anda vurulan sayılar üst üste binmesin
        Vector3 jitter = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0f, 0.1f), 0f);
        Show(enemy.transform.position + Vector3.up * 0.35f + jitter, amount);
    }

    public void Show(Vector3 position, float amount)
    {
        var number = pool.Count > 0 ? pool.Pop() : CreateNumber();
        bool bigHit = amount >= bigHitThreshold;

        number.text.text = Mathf.Max(1, Mathf.RoundToInt(amount)).ToString();
        number.text.color = bigHit ? bigHitColor : normalColor;
        number.scale = bigHit ? 1.3f : 1f;
        number.start = position;
        number.age = 0f;
        number.text.transform.position = position;
        number.text.gameObject.SetActive(true);
        active.Add(number);
    }

    public void ShowHeal(Vector3 position, float amount)
    {
        if (font == null || amount <= 0f) return;

        var number = pool.Count > 0 ? pool.Pop() : CreateNumber();
        number.text.text = $"+{Mathf.Max(1, Mathf.RoundToInt(amount))}";
        number.text.color = new Color(0.35f, 1f, 0.45f);
        number.scale = 1.2f;
        number.start = position;
        number.age = 0f;
        number.text.transform.position = position;
        number.text.gameObject.SetActive(true);
        active.Add(number);
    }

    void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var number = active[i];
            number.age += Time.deltaTime;
            float t = number.age / lifetime;

            if (t >= 1f)
            {
                number.text.gameObject.SetActive(false);
                active.RemoveAt(i);
                pool.Push(number);
                continue;
            }

            // Hızlı çıkıp yavaşlayarak yükselir, ilk anda büyüyüp oturur, sonlara doğru silinir
            float rise = 1f - (1f - t) * (1f - t);
            number.text.transform.position = number.start + Vector3.up * (riseDistance * rise);

            float pop = t < 0.15f ? Mathf.Lerp(1.6f, 1f, t / 0.15f) : 1f;
            number.text.transform.localScale = Vector3.one * (number.scale * pop);

            Color color = number.text.color;
            color.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            number.text.color = color;
        }
    }

    private Number CreateNumber()
    {
        var go = new GameObject("DamageNumber");
        go.transform.SetParent(transform, false);

        var text = go.AddComponent<TextMeshPro>();
        text.font = font;
        text.fontSharedMaterial = outlineMaterial;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.rectTransform.sizeDelta = new Vector2(2f, 1f);
        text.sortingOrder = sortingOrder;

        return new Number { text = text };
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (outlineMaterial != null) Destroy(outlineMaterial);
    }
}
