using UnityEngine;

// Boss'un fırlattığı çöp parçası (Trash Volley). Düz gider, döner, oyuncuya değince hasar verip kaybolur.
// Fizik kullanmaz; oyuncuyla mesafe kontrolü yapar. Final ya da ölümde kendiliğinden yok olur.
public class BossProjectile : MonoBehaviour
{
    private const int SortingOrder = 5; // karakterlerin üstünde, can barlarının altında
    private const float PlayerRadius = 0.3f;

    private static PlayerHealth player;
    private static Collider2D playerCollider;

    private Vector2 velocity;
    private float damage;
    private float hitRadius;
    private float life;
    private float spin;

    public static void Spawn(Sprite sprite, Vector2 position, Vector2 velocity, float damage, float size, float lifetime)
    {
        var go = new GameObject("BossProjectile");
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = SortingOrder;
        if (sprite != null)
        {
            float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            go.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, spriteSize));
        }

        var projectile = go.AddComponent<BossProjectile>();
        projectile.velocity = velocity;
        projectile.damage = damage;
        projectile.hitRadius = size * 0.4f;
        projectile.life = lifetime;
        projectile.spin = Random.Range(-360f, 360f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        var session = GameSession.Instance;
        life -= dt;
        if (life <= 0f || (session != null && session.IsOver))
        {
            Destroy(gameObject);
            return;
        }

        transform.position += (Vector3)(velocity * dt);
        transform.Rotate(0f, 0f, spin * dt);

        if (player == null)
        {
            player = FindObjectOfType<PlayerHealth>();
            playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
        }
        if (player == null || !player.IsAlive) return;

        Vector2 playerCenter = playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.transform.position;
        float reach = hitRadius + PlayerRadius;
        if (((Vector2)transform.position - playerCenter).sqrMagnitude <= reach * reach)
        {
            player.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
