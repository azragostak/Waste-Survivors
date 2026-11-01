using System.Collections.Generic;
using UnityEngine;

// Tohum düşmana isabet ettiğinde çıkan küçük filiz/yaprak sıçrama efekti (Object Pool destekli).
public class SeedHitEffect : MonoBehaviour
{
    private const int SparkCount = 5;
    private const float Duration = 0.18f;

    private static readonly Stack<SeedHitEffect> pool = new();
    private static Material lineMaterial;

    private readonly LineRenderer[] sparks = new LineRenderer[SparkCount];
    private readonly Vector2[] directions = new Vector2[SparkCount];
    private Vector2 origin;
    private Color baseColor;
    private float age;
    private bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pool.Clear();
        lineMaterial = null;
    }

    public static void Spawn(Vector2 position, Color color)
    {
        SeedHitEffect effect = null;
        while (pool.Count > 0 && effect == null)
            effect = pool.Pop();

        if (effect == null)
            effect = new GameObject("SeedHitEffect").AddComponent<SeedHitEffect>();

        effect.Play(position, color);
    }

    private void Play(Vector2 position, Color color)
    {
        if (!initialized)
        {
            InitializeLines();
            initialized = true;
        }

        origin = position;
        baseColor = color;
        age = 0f;
        transform.position = position;

        float baseAngle = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < SparkCount; i++)
        {
            float angle = baseAngle + (i / (float)SparkCount) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
            directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        UpdateSparks(0f);
        gameObject.SetActive(true);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = age / Duration;
        if (t >= 1f)
        {
            gameObject.SetActive(false);
            pool.Push(this);
            return;
        }

        UpdateSparks(t);
    }

    private void UpdateSparks(float t)
    {
        float eased = 1f - (1f - t) * (1f - t);
        float inner = eased * 0.14f;
        float outer = 0.06f + eased * 0.24f;

        Color c = baseColor;
        c.a = 1f - t;

        for (int i = 0; i < SparkCount; i++)
        {
            var line = sparks[i];
            Vector2 dir = directions[i];
            line.SetPosition(0, origin + dir * inner);
            line.SetPosition(1, origin + dir * outer);
            line.startColor = line.endColor = c;
        }
    }

    private void InitializeLines()
    {
        if (lineMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader);
        }

        for (int i = 0; i < SparkCount; i++)
        {
            var child = new GameObject("Spark");
            child.transform.SetParent(transform, false);

            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = lineMaterial;
            line.positionCount = 2;
            line.widthMultiplier = 0.045f;
            line.numCapVertices = 2;
            line.sortingOrder = 15;
            sparks[i] = line;
        }
    }
}

