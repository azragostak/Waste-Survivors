using TMPro;
using UnityEngine;

// Kodla kurulan arayüzler (GameHUD, LevelUpMenu) için ortak yardımcılar
public static class UIBuilder
{
    public static RectTransform CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static UnityEngine.UI.Image CreateImage(Transform parent, string name, Color color, bool raycastTarget = false)
    {
        var image = CreateRect(parent, name).gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    // Font yoksa (TMP Essentials import edilmemiş) null döner; çağıran taraf SetText ile güvenle kullanabilir
    public static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size,
        TextAlignmentOptions alignment, bool bold = true)
    {
        if (font == null) return null;

        var text = CreateRect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        return text;
    }

    public static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null) text.text = value;
    }

    // 0-1 arası yatay kaydırma çubuğu (arka plan, dolgu ve tutamaç)
    public static UnityEngine.UI.Slider CreateSlider(Transform parent, string name, Color fillColor,
        float value, System.Action<float> onChanged)
    {
        var root = CreateRect(parent, name);
        var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();

        var background = CreateImage(root, "Background", new Color(1f, 1f, 1f, 0.15f));
        background.rectTransform.anchorMin = new Vector2(0f, 0.35f);
        background.rectTransform.anchorMax = new Vector2(1f, 0.65f);
        background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;

        var fillArea = CreateRect(root, "FillArea");
        fillArea.anchorMin = new Vector2(0f, 0.35f);
        fillArea.anchorMax = new Vector2(1f, 0.65f);
        fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
        var fill = CreateImage(fillArea, "Fill", fillColor);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

        var handleArea = CreateRect(root, "HandleArea");
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(8f, 0f);
        handleArea.offsetMax = new Vector2(-8f, 0f);
        var handle = CreateImage(handleArea, "Handle", Color.white, raycastTarget: true);
        handle.rectTransform.sizeDelta = new Vector2(16f, 0f);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(value);
        if (onChanged != null)
            slider.onValueChanged.AddListener(v => onChanged(v));

        // Arka plana tıklayınca da değer değişsin
        background.raycastTarget = true;
        return slider;
    }

    public static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    // Ebeveynin üst kenarına yapışık, yatayda ebeveyn genişliğinde (kenarlardan 'sideInset' kadar içeride) şerit
    public static void TopStrip(RectTransform rect, float top, float height, float sideInset = 0f)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(-sideInset * 2f, height);
    }

    // Ebeveynin sol üst köşesine göre sabit boyutlu kutu
    public static void TopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(position.x, -position.y);
    }

    // Data'da ikon yoksa silah prefab'ındaki sprite
    public static Sprite GetWeaponIcon(WeaponData data)
    {
        if (data.icon != null) return data.icon;
        if (data.weaponPrefab == null) return null;
        var sr = data.weaponPrefab.GetComponentInChildren<SpriteRenderer>(true);
        return sr != null ? sr.sprite : null;
    }

    public static string GetInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "?";
        var initials = "";
        foreach (var word in displayName.Split(' '))
        {
            if (word.Length == 0) continue;
            initials += char.ToUpper(word[0]);
            if (initials.Length == 2) break;
        }
        return initials;
    }
}
