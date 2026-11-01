using System.Collections.Generic;
using UnityEngine;

// Düşmanların düşürdüğü geri dönüşüm parçası (XP).
// Oyuncunun toplama alanına girince ona doğru çekilir, oyuncuya değince XP verir.
// Sahnede çok fazla birikirse yenileri oluşturulmaz, XP'leri uzaktaki bir parçada toplanır (o parça büyür).
public class RecyclePickup : MonoBehaviour
{
    // Bu sayıdan sonra yeni parça oluşturmak yerine birleştirilir
    private const int MaxPickups = 300;

    // Sahnedeki tüm toplanabilir recycle'lar. PlayerExperience alan kontrolü için bunu kullanır (collider/layer gerekmez).
    private static readonly List<RecyclePickup> active = new();
    public static IReadOnlyList<RecyclePickup> Active => active;

    [SerializeField, Min(0)] private int experience = 1;
    [Tooltip("Çekilmeye başladığı andaki hız")]
    [SerializeField] private float startSpeed = 2f;
    [Tooltip("Çekilirken saniyede ne kadar hızlansın")]
    [SerializeField] private float acceleration = 25f;
    [SerializeField] private float collectDistance = 0.2f;

    [Header("Ses")]
    [SerializeField] private AudioClip collectSound;
    [SerializeField, Range(0f, 1f)] private float collectVolume = 0.4f;

    // Art arda toplanan parçalarda ses perdesi tırmanır (tatmin edici "zincir" hissi)
    private const float ComboResetTime = 0.5f;
    private const float ComboPitchStep = 0.035f;
    private const int ComboMaxSteps = 14;
    private static int combo;
    private static float lastCollectTime = -10f;

    private PlayerExperience collector;
    private float speed;
    private bool consumed;
    private Vector3 baseScale;

    public int Experience
    {
        get => experience;
        set => experience = Mathf.Max(0, value);
    }

    public bool IsAttracted => collector != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active.Clear();
        combo = 0;
        lastCollectTime = -10f;
    }

    void Awake() => baseScale = transform.localScale;

    void OnEnable() => active.Add(this);

    void OnDisable() => active.Remove(this);

    // Düşmanlar ölünce bunu çağırır ve düşen parçayı döndürür.
    // Sınır doluysa kameradan en uzak parça silinir, XP'si yeni parçaya eklenir:
    // yeni düşmanlar hep XP düşürür, geride bırakılan XP de kaybolmaz.
    public static RecyclePickup Drop(RecyclePickup prefab, Vector2 position, int amount)
    {
        if (prefab == null || amount <= 0) return null;

        int carried = 0;
        if (active.Count >= MaxPickups)
        {
            var farthest = FindFarthest();
            if (farthest != null)
            {
                carried = farthest.experience;
                farthest.consumed = true;
                farthest.experience = 0;
                Destroy(farthest.gameObject);
            }
        }

        var drop = Instantiate(prefab, position, Quaternion.identity);
        drop.Experience = amount;
        if (carried > 0)
            drop.AddExperience(carried);
        return drop;
    }

    private static RecyclePickup FindFarthest()
    {
        var cam = Camera.main;
        Vector2 reference = cam != null ? (Vector2)cam.transform.position : Vector2.zero;
        RecyclePickup farthest = null;
        float farthestSqr = -1f;
        foreach (var pickup in active)
        {
            // Silinmesi kare sonuna kalan parçalar aynı karede tekrar seçilmesin (XP iki kez taşınmasın)
            if (pickup.IsAttracted || pickup.consumed) continue;
            float sqr = ((Vector2)pickup.transform.position - reference).sqrMagnitude;
            if (sqr > farthestSqr)
            {
                farthestSqr = sqr;
                farthest = pickup;
            }
        }
        return farthest;
    }

    private void AddExperience(int amount)
    {
        experience += amount;
        // Değeri arttıkça biraz büyür (en fazla 2 katı), büyük parçalar uzaktan fark edilsin
        float grow = Mathf.Min(2f, 1f + Mathf.Log(experience, 2f) * 0.12f);
        transform.localScale = baseScale * grow;
    }

    public void Attract(PlayerExperience target)
    {
        if (collector != null) return;
        collector = target;
        speed = startSpeed;
    }

    private void PlayCollectSound()
    {
        combo = Time.unscaledTime - lastCollectTime > ComboResetTime ? 0 : Mathf.Min(combo + 1, ComboMaxSteps);
        lastCollectTime = Time.unscaledTime;
        SfxPlayer.Play(collectSound, collectVolume, pitchVariation: 0.02f, minInterval: 0.04f, maxVoices: 5,
            pitch: 1f + combo * ComboPitchStep);
    }

    void Update()
    {
        if (collector == null) return;

        speed += acceleration * Time.deltaTime;
        Vector2 targetPosition = collector.transform.position;
        Vector2 position = Vector2.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
        transform.position = new Vector3(position.x, position.y, transform.position.z);

        if ((targetPosition - position).sqrMagnitude <= collectDistance * collectDistance)
        {
            PlayCollectSound();
            collector.AddExperience(experience);
            Destroy(gameObject);
        }
    }
}
