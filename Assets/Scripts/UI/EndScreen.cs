using System.Collections;
using TMPro;
using UnityEngine;

// Oyun sonu ekranı (zafer ya da ölüm): başlık, alt başlık, istatistikler ve yeniden başlatma ipucu.
// Kendi Canvas'ını kurar, yavaşça belirir.
public class EndScreen : MonoBehaviour
{
    private const float FadeDuration = 1.2f;

    public static EndScreen Show(string title, string subtitle, Color titleColor, string stats)
    {
        var go = new GameObject("EndScreen");
        var screen = go.AddComponent<EndScreen>();
        screen.Build(title, subtitle, titleColor, stats);
        return screen;
    }

    private CanvasGroup group;

    private void Build(string title, string subtitle, Color titleColor, string stats)
    {
        var tmpSettings = Resources.Load<TMP_Settings>("TMP Settings");
        var font = tmpSettings != null ? TMP_Settings.defaultFontAsset : null;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        // Ortada yatay bir şerit: arkada temizlenen dünya görünmeye devam etsin
        var band = UIBuilder.CreateImage(transform, "Band", new Color(0f, 0f, 0f, 0.6f));
        var rect = band.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(0f, 360f);
        rect.anchoredPosition = Vector2.zero;

        AddLine(band.transform, font, title, 84f, titleColor, 110f, true);
        AddLine(band.transform, font, subtitle, 30f, new Color(0.9f, 0.9f, 0.9f), 35f, false);
        AddLine(band.transform, font, stats, 28f, Color.white, -40f, true);
        AddLine(band.transform, font, GameLanguage.Tr("R  -  Restart", "R  -  Yeniden Başla"), 24f, new Color(0.7f, 0.7f, 0.7f), -125f, false);

        StartCoroutine(FadeIn());
    }

    private static void AddLine(Transform parent, TMP_FontAsset font, string value, float size, Color color, float y, bool bold)
    {
        var text = UIBuilder.CreateText(parent, "Line", font, size, TextAlignmentOptions.Center, bold);
        if (text == null) return;
        text.text = value;
        text.color = color;
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1600f, size * 1.4f);
        rect.anchoredPosition = new Vector2(0f, y);
    }

    private IEnumerator FadeIn()
    {
        for (float t = 0f; t < FadeDuration; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / FadeDuration;
            yield return null;
        }
        group.alpha = 1f;
    }
}
