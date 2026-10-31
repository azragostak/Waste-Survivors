using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Level atlayınca oyunu durdurup 3 seçenek sunan ekran: yeni silah, yeni item ya da sahip olunanı geliştirme.
// Kartlara tıklayarak veya 1-2-3 tuşlarıyla seçilir. Arka arkaya birden fazla level atlanırsa sırayla tekrar açılır.
// GameHUD oluşturur.
public class LevelUpMenu : MonoBehaviour
{
    private const int OptionCount = 3;
    // Açıldığı anda yanlışlıkla seçim yapılmasın
    private const float InputDelay = 0.3f;

    private static readonly Color WeaponColor = new Color(0.95f, 0.75f, 0.3f);
    private static readonly Color ItemColor = new Color(0.45f, 0.72f, 1f);
    private static readonly Color HealColor = new Color(0.95f, 0.35f, 0.4f);

    private class Option
    {
        public string tag;
        public string title;
        public string description;
        public Sprite icon;
        public Color color;
        public Action apply;
    }

    private class Card
    {
        public GameObject root;
        public UnityEngine.UI.Image border;
        public UnityEngine.UI.Image icon;
        public TextMeshProUGUI initials;
        public TextMeshProUGUI tag;
        public TextMeshProUGUI title;
        public TextMeshProUGUI description;
    }

    private readonly List<Card> cards = new();
    private readonly List<Option> currentOptions = new();
    private readonly List<Option> candidates = new();

    private GameObject panel;
    private TMP_FontAsset font;
    private UpgradePool pool;
    private PlayerExperience experience;
    private PlayerWeapons weapons;
    private PlayerPassives passives;
    private PlayerHealth health;

    private int pendingLevelUps;
    private float shownAt;
    private float timeScaleBeforePause = 1f;

    public bool IsOpen => panel != null && panel.activeSelf;

    public void Initialize(Transform canvas, TMP_FontAsset fontAsset, UpgradePool upgradePool,
        PlayerExperience playerExperience, PlayerWeapons playerWeapons, PlayerPassives playerPassives, PlayerHealth playerHealth)
    {
        font = fontAsset;
        pool = upgradePool;
        experience = playerExperience;
        weapons = playerWeapons;
        passives = playerPassives;
        health = playerHealth;

        EnsureEventSystem();
        Build(canvas);
        panel.SetActive(false);

        if (experience != null)
            experience.LeveledUp += OnLeveledUp;
    }

    void OnDestroy()
    {
        if (experience != null)
            experience.LeveledUp -= OnLeveledUp;

        // Menü açıkken sahne kapanırsa oyun durmuş kalmasın
        if (IsOpen) Time.timeScale = timeScaleBeforePause;
    }

    private void OnLeveledUp(int level)
    {
        pendingLevelUps++;
        if (!IsOpen) Show();
    }

    void Update()
    {
        if (!IsOpen || Time.unscaledTime - shownAt < InputDelay) return;

        for (int i = 0; i < currentOptions.Count; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                Choose(i);
                return;
            }
        }
    }

    private void Show()
    {
        BuildOptions();

        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (i >= currentOptions.Count)
            {
                card.root.SetActive(false);
                continue;
            }

            var option = currentOptions[i];
            card.root.SetActive(true);
            card.border.color = option.color;
            card.icon.sprite = option.icon;
            card.icon.enabled = option.icon != null;
            UIBuilder.SetText(card.initials, option.icon == null ? UIBuilder.GetInitials(option.title) : "");
            UIBuilder.SetText(card.tag, option.tag);
            if (card.tag != null) card.tag.color = option.color;
            UIBuilder.SetText(card.title, option.title);
            UIBuilder.SetText(card.description, option.description);
        }

        if (!IsOpen)
        {
            timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
        }

        panel.SetActive(true);
        shownAt = Time.unscaledTime;

        // Önceki seçimden kalan buton seçimi yeni kartta vurgulu görünmesin
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Choose(int index)
    {
        if (!IsOpen || index < 0 || index >= currentOptions.Count) return;
        if (Time.unscaledTime - shownAt < InputDelay) return;

        currentOptions[index].apply?.Invoke();
        pendingLevelUps = Mathf.Max(0, pendingLevelUps - 1);

        if (pendingLevelUps > 0)
        {
            Show();
            return;
        }

        panel.SetActive(false);
        Time.timeScale = timeScaleBeforePause;
    }

    private void BuildOptions()
    {
        candidates.Clear();

        if (pool != null && weapons != null)
        {
            foreach (var data in pool.weapons)
            {
                if (data == null) continue;

                var owned = weapons.GetWeapon(data);
                if (owned != null && !owned.IsMaxLevel)
                {
                    candidates.Add(new Option
                    {
                        tag = $"SİLAH  Lv {owned.Level} → {owned.Level + 1}",
                        title = data.displayName,
                        description = data.GetUpgradeDescription(owned.Level + 1),
                        icon = UIBuilder.GetWeaponIcon(data),
                        color = WeaponColor,
                        apply = () => weapons.AddOrUpgrade(data),
                    });
                }
                else if (owned == null && weapons.HasFreeSlot)
                {
                    candidates.Add(new Option
                    {
                        tag = "YENİ SİLAH",
                        title = data.displayName,
                        description = data.description,
                        icon = UIBuilder.GetWeaponIcon(data),
                        color = WeaponColor,
                        apply = () => weapons.AddOrUpgrade(data),
                    });
                }
            }
        }

        if (pool != null && passives != null)
        {
            foreach (var data in pool.passiveItems)
            {
                if (data == null) continue;

                var owned = passives.GetItem(data);
                if (owned != null && !owned.IsMaxLevel)
                {
                    candidates.Add(new Option
                    {
                        tag = $"İTEM  Lv {owned.Level} → {owned.Level + 1}",
                        title = data.displayName,
                        description = $"{data.GetEffectText(owned.Level)}  →  {data.GetEffectText(owned.Level + 1)}",
                        icon = data.icon,
                        color = ItemColor,
                        apply = () => passives.AddOrUpgrade(data),
                    });
                }
                else if (owned == null && passives.HasFreeSlot)
                {
                    candidates.Add(new Option
                    {
                        tag = "YENİ İTEM",
                        title = data.displayName,
                        description = $"{data.description}\n\n<color=#9fd0ff>{data.GetEffectText(1)}</color>",
                        icon = data.icon,
                        color = ItemColor,
                        apply = () => passives.AddOrUpgrade(data),
                    });
                }
            }
        }

        // Rastgele karıştırıp ilk 3'ü al
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        currentOptions.Clear();
        for (int i = 0; i < candidates.Count && currentOptions.Count < OptionCount; i++)
            currentOptions.Add(candidates[i]);

        // Her şey maksimumdaysa boş geçmesin
        if (currentOptions.Count == 0)
        {
            currentOptions.Add(new Option
            {
                tag = "İYİLEŞ",
                title = "Temiz Hava",
                description = "30 can yeniler.",
                color = HealColor,
                apply = () => { if (health != null) health.Heal(30f); },
            });
        }
    }

    private void Build(Transform canvas)
    {
        // Tüm ekranı karartan ve arkadaki tıklamaları engelleyen katman
        var overlay = UIBuilder.CreateImage(canvas, "LevelUpMenu", new Color(0f, 0f, 0f, 0.72f), raycastTarget: true);
        UIBuilder.Stretch(overlay.rectTransform);
        panel = overlay.gameObject;

        var title = UIBuilder.CreateText(overlay.transform, "Title", font, 72f, TextAlignmentOptions.Center);
        if (title != null)
        {
            title.text = "LEVEL UP!";
            title.color = new Color(1f, 0.9f, 0.4f);
            PlaceCentered(title.rectTransform, new Vector2(0f, 340f), new Vector2(900f, 90f));
        }

        var subtitle = UIBuilder.CreateText(overlay.transform, "Subtitle", font, 26f, TextAlignmentOptions.Center, bold: false);
        if (subtitle != null)
        {
            subtitle.text = "Bir geliştirme seç  (1 - 2 - 3)";
            subtitle.color = new Color(0.85f, 0.85f, 0.85f);
            PlaceCentered(subtitle.rectTransform, new Vector2(0f, 280f), new Vector2(900f, 40f));
        }

        var cardSize = new Vector2(360f, 470f);
        const float spacing = 40f;
        float totalWidth = OptionCount * cardSize.x + (OptionCount - 1) * spacing;

        for (int i = 0; i < OptionCount; i++)
        {
            float x = -totalWidth * 0.5f + cardSize.x * 0.5f + i * (cardSize.x + spacing);
            cards.Add(CreateCard(overlay.transform, i, new Vector2(x, -40f), cardSize));
        }
    }

    private Card CreateCard(Transform parent, int index, Vector2 position, Vector2 size)
    {
        // Çerçeve rengi seçenek türünü gösterir; Button üzerine gelince parlatır
        var border = UIBuilder.CreateImage(parent, $"Card{index + 1}", Color.white, raycastTarget: true);
        PlaceCentered(border.rectTransform, position, size);

        var button = border.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = border;
        var colors = button.colors;
        colors.normalColor = new Color(0.75f, 0.75f, 0.75f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
        colors.selectedColor = new Color(0.75f, 0.75f, 0.75f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        int captured = index;
        button.onClick.AddListener(() => Choose(captured));

        var background = UIBuilder.CreateImage(border.transform, "Background", new Color(0.09f, 0.09f, 0.12f, 0.97f));
        UIBuilder.Stretch(background.rectTransform, 5f);
        var content = background.transform;

        var key = UIBuilder.CreateText(content, "Key", font, 22f, TextAlignmentOptions.TopLeft);
        if (key != null)
        {
            key.text = (index + 1).ToString();
            key.color = new Color(0.6f, 0.6f, 0.6f);
            UIBuilder.TopLeft(key.rectTransform, new Vector2(14f, 10f), new Vector2(40f, 30f));
        }

        var tag = UIBuilder.CreateText(content, "Tag", font, 22f, TextAlignmentOptions.Center);
        if (tag != null)
            UIBuilder.TopStrip(tag.rectTransform, 16f, 30f, 40f);

        // İkon için koyu bir çerçeve
        var iconFrame = UIBuilder.CreateImage(content, "IconFrame", new Color(1f, 1f, 1f, 0.06f));
        PlaceTopCenter(iconFrame.rectTransform, 60f, new Vector2(140f, 140f));

        var icon = UIBuilder.CreateImage(iconFrame.transform, "Icon", Color.white);
        UIBuilder.Stretch(icon.rectTransform, 10f);
        icon.preserveAspect = true;

        var initials = UIBuilder.CreateText(iconFrame.transform, "Initials", font, 52f, TextAlignmentOptions.Center);
        if (initials != null)
            UIBuilder.Stretch(initials.rectTransform);

        var title = UIBuilder.CreateText(content, "Title", font, 32f, TextAlignmentOptions.Center);
        if (title != null)
            UIBuilder.TopStrip(title.rectTransform, 215f, 44f, 16f);

        var description = UIBuilder.CreateText(content, "Description", font, 23f, TextAlignmentOptions.Top, bold: false);
        if (description != null)
        {
            description.enableWordWrapping = true;
            description.color = new Color(0.9f, 0.9f, 0.9f);
            description.outlineWidth = 0f;
            var rect = description.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(26f, 22f);
            rect.offsetMax = new Vector2(-26f, -272f);
        }

        return new Card
        {
            root = border.gameObject,
            border = border,
            icon = icon,
            initials = initials,
            tag = tag,
            title = title,
            description = description,
        };
    }

    private static void PlaceCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void PlaceTopCenter(RectTransform rect, float top, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(0f, -top);
    }

    // Tıklamaların çalışması için sahnede bir EventSystem olmalı
    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
