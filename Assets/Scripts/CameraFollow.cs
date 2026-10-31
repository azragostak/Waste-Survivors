using UnityEngine;

// Kamerayı oyuncuya yumuşakça takip ettirir. Main Camera'ya eklemek yeterli;
// target boşsa "Player" tag'li objeyi kendisi bulur.
// AcidRain gibi kameraya göre konumlanan scriptler güncel pozisyonu görsün diye onlardan önce çalışır.
[DefaultExecutionOrder(-50)]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [Tooltip("0 = anında takip, büyüdükçe kamera daha geriden gelir")]
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;
    [SerializeField] private Vector2 offset = Vector2.zero;

    private Vector3 velocity;

    void Start()
    {
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        // İlk karede oyuncunun üstünde başlasın, sahne açılırken kayma olmasın
        if (target != null)
            transform.position = GetTargetPosition();
    }

    // Player Rigidbody2D interpolate kullandığı için LateUpdate'te takip ediyoruz, titreme olmaz
    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = GetTargetPosition();
        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime)
            : desired;
    }

    // Kameranın z'si korunur, yoksa 2D'de sahneyi göremez
    private Vector3 GetTargetPosition()
    {
        Vector3 pos = target.position;
        return new Vector3(pos.x + offset.x, pos.y + offset.y, transform.position.z);
    }
}
