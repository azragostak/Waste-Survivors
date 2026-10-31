using System.Collections.Generic;
using UnityEngine;

// Düşmanların düşürdüğü geri dönüşüm parçası (XP).
// Oyuncunun toplama alanına girince ona doğru çekilir, oyuncuya değince XP verir.
public class RecyclePickup : MonoBehaviour
{
    // Sahnedeki tüm toplanabilir recycle'lar. PlayerExperience alan kontrolü için bunu kullanır (collider/layer gerekmez).
    private static readonly List<RecyclePickup> active = new();
    public static IReadOnlyList<RecyclePickup> Active => active;

    [SerializeField, Min(0)] private int experience = 1;
    [Tooltip("Çekilmeye başladığı andaki hız")]
    [SerializeField] private float startSpeed = 2f;
    [Tooltip("Çekilirken saniyede ne kadar hızlansın")]
    [SerializeField] private float acceleration = 25f;
    [SerializeField] private float collectDistance = 0.2f;

    private PlayerExperience collector;
    private float speed;

    public int Experience
    {
        get => experience;
        set => experience = Mathf.Max(0, value);
    }

    public bool IsAttracted => collector != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();

    void OnEnable() => active.Add(this);

    void OnDisable() => active.Remove(this);

    public void Attract(PlayerExperience target)
    {
        if (collector != null) return;
        collector = target;
        speed = startSpeed;
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
            collector.AddExperience(experience);
            Destroy(gameObject);
        }
    }
}
