using System.Collections.Generic;
using UnityEngine;

// Art hazır olana kadar kullanılan, kodla çizilen basit elektrik efekti:
// alan sınırında bir halka + vurulan her düşmana zikzak bir şimşek. Kendi kendini yok eder.
public class ElectricZapEffect : MonoBehaviour
{
    private const int RingSegments = 40;
    private const int BoltSegments = 6;

    private static Material lineMaterial;

    private readonly List<LineRenderer> lines = new();
    private Color color;
    private float lifetime;
    private float age;

    public static void Spawn(Vector2 center, float radius, List<Enemy> targets, Color color, float lifetime)
    {
        var effect = new GameObject("ElectricZap").AddComponent<ElectricZapEffect>();
        effect.transform.position = center;
        effect.color = color;
        effect.lifetime = Mathf.Max(0.05f, lifetime);

        effect.CreateRing(center, radius);
        foreach (var target in targets)
            effect.CreateBolt(center, target.transform.position);
    }

    private void CreateRing(Vector2 center, float radius)
    {
        var line = CreateLine(0.05f);
        line.loop = true;
        line.positionCount = RingSegments;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = i / (float)RingSegments * Mathf.PI * 2f;
            line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    private void CreateBolt(Vector2 from, Vector2 to)
    {
        var line = CreateLine(0.08f);
        line.positionCount = BoltSegments + 1;

        Vector2 perpendicular = Vector2.Perpendicular(to - from).normalized;
        float jitter = Vector2.Distance(from, to) * 0.15f;
        for (int i = 0; i <= BoltSegments; i++)
        {
            Vector2 point = Vector2.Lerp(from, to, i / (float)BoltSegments);
            if (i > 0 && i < BoltSegments)
                point += perpendicular * Random.Range(-jitter, jitter);
            line.SetPosition(i, point);
        }
    }

    private LineRenderer CreateLine(float width)
    {
        if (lineMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader);
        }

        var child = new GameObject("Line");
        child.transform.SetParent(transform, false);

        var line = child.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = lineMaterial;
        line.widthMultiplier = width;
        line.numCapVertices = 2;
        line.sortingOrder = 100;
        line.startColor = line.endColor = color;
        lines.Add(line);
        return line;
    }

    void Update()
    {
        age += Time.deltaTime;
        float alpha = 1f - age / lifetime;
        if (alpha <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        var faded = color;
        faded.a = alpha;
        foreach (var line in lines)
            line.startColor = line.endColor = faded;
    }
}
