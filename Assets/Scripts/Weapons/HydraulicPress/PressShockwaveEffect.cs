using System.Collections.Generic;
using UnityEngine;

// Hidrolik pres yere çarptığında dışa doğru genişleyen şok dalgası ve darbe çizgileri.
// Kendi kendini yok eder.
public class PressShockwaveEffect : MonoBehaviour
{
    private const int RingSegments = 36;
    private const int CrackCount = 8;

    private static Material lineMaterial;

    private LineRenderer ringLine;
    private readonly List<LineRenderer> crackLines = new();
    private readonly List<Vector2> crackDirections = new();
    private Vector2 center;
    private float maxRadius;
    private Color color;
    private float lifetime;
    private float age;

    public static void Spawn(Vector2 center, float radius, Color color, float lifetime)
    {
        var effect = new GameObject("PressShockwave").AddComponent<PressShockwaveEffect>();
        effect.transform.position = center;
        effect.center = center;
        effect.maxRadius = Mathf.Max(0.2f, radius);
        effect.color = color;
        effect.lifetime = Mathf.Max(0.08f, lifetime);
        effect.InitializeVisuals();
    }

    private void InitializeVisuals()
    {
        ringLine = CreateLine(0.09f);
        ringLine.loop = true;
        ringLine.positionCount = RingSegments;

        float angleOffset = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < CrackCount; i++)
        {
            float angle = angleOffset + (i / (float)CrackCount) * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
            crackDirections.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));

            var crack = CreateLine(0.06f);
            crack.positionCount = 2;
            crackLines.Add(crack);
        }

        UpdateGeometry(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // Hızlı genişleyip yavaşlayan şok halkası (ease-out)
        float eased = 1f - (1f - t) * (1f - t);
        UpdateGeometry(eased);

        float alpha = 1f - t;
        var faded = color;
        faded.a = alpha;

        ringLine.widthMultiplier = Mathf.Lerp(0.11f, 0.02f, eased);
        ringLine.startColor = ringLine.endColor = faded;

        for (int i = 0; i < crackLines.Count; i++)
        {
            crackLines[i].startColor = crackLines[i].endColor = faded;
        }
    }

    private void UpdateGeometry(float progress)
    {
        float currentRadius = Mathf.Lerp(maxRadius * 0.25f, maxRadius, progress);
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = i / (float)RingSegments * Mathf.PI * 2f;
            ringLine.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * currentRadius);
        }

        float inner = currentRadius * 0.65f;
        float outer = Mathf.Min(maxRadius * 1.05f, currentRadius + maxRadius * 0.2f);
        for (int i = 0; i < crackLines.Count; i++)
        {
            Vector2 dir = crackDirections[i];
            crackLines[i].SetPosition(0, center + dir * inner);
            crackLines[i].SetPosition(1, center + dir * outer);
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
        line.sortingOrder = 4;
        line.startColor = line.endColor = color;
        return line;
    }
}

