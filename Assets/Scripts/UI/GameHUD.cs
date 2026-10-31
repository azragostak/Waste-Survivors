using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Oyun içi arayüz: en üstte XP barı ve seviye, sol üstte silah ve pasif item slotları,
// oyuncunun ve düşmanların üstünde can barları, düşmanlarda hasar sayıları ve level-up ekranı.
// Sahnede boş bir objeye eklemek yeterli; her şeyi kendisi oluşturur, Player'ı kendisi bulur.
public class GameHUD : MonoBehaviour
{
    [Tooltip("Level-up'ta çıkabilecek silah ve itemler. Boşsa Resources/UpgradePool kullanılır.")]
    [SerializeField] private UpgradePool upgradePool;

    [Header("XP Barı")]
    [SerializeField] private float xpBarHeight = 24f;
    [SerializeField] private Color xpFillColor = new Color(0.3f, 0.8f, 0.35f);
    [SerializeField] private Color xpBackgroundColor = new Color(0f, 0f, 0f, 0.65f);

    [Header("Silah / Pasif Slotları (sayısı PlayerWeapons ve PlayerPassives'ten gelir)")]
    [SerializeField] private float slotSize = 64f;
    [SerializeField] private float slotSpacing = 6f;
    [SerializeField] private Color weaponSlotBorder = new Color(0.9f, 0.75f, 0.35f, 0.9f);
    [SerializeField] private Color passiveSlotBorder = new Color(0.45f, 0.7f, 0.95f, 0.9f);

    [Header("Can Barları")]
    [SerializeField] private Color playerHealthColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private Color enemyHealthColor = new Color(0.95f, 0.55f, 0.15f);
    [SerializeField] private Vector2 playerBarSize = new Vector2(0.6f, 0.07f);
    [SerializeField] private Vector2 enemyBarSize = new Vector2(0.45f, 0.05f);
    [Tooltip("Düşman can barı sadece hasar alınca görünsün (kalabalıkta ekran karışmasın)")]
    [SerializeField] private bool hideFullEnemyBars;
    [Tooltip("Karakterlerin üstünde, asit yağmurunun altında")]
    [SerializeField] private int healthBarSortingOrder = 50;

    [Header("Hasar Sayıları")]
    [SerializeField] private bool showDamageNumbers = true;

    private class Slot
    {
        public UnityEngine.UI.Image icon;
        public TextMeshProUGUI initials;
        public TextMeshProUGUI level;
    }

    private PlayerExperience experience;
    private PlayerWeapons weapons;
    private PlayerPassives passives;
    private PlayerHealth playerHealth;
    private TMP_FontAsset font;

    private RectTransform xpFill;
    private TextMeshProUGUI levelText;
    private readonly List<Slot> weaponSlots = new();
    private readonly List<Slot> passiveSlots = new();

    void OnEnable() => Enemy.Spawned += AttachEnemyBar;

    void OnDisable() => Enemy.Spawned -= AttachEnemyBar;

    void Start()
    {
        // TMP Settings yoksa (TMP Essentials import edilmemiş) yazısız devam et
        var tmpSettings = Resources.Load<TMP_Settings>("TMP Settings");
        font = tmpSettings != null ? TMP_Settings.defaultFontAsset : null;
        if (font == null)
            Debug.LogWarning("GameHUD: TextMeshPro font'u bulunamadı, yazılar gösterilmeyecek. " +
                             "Window > TextMeshPro > Import TMP Essential Resources ile import et.", this);

        if (upgradePool == null)
            upgradePool = Resources.Load<UpgradePool>(UpgradePool.ResourcePath);
        if (upgradePool == null)
            Debug.LogWarning("GameHUD: UpgradePool bulunamadı, level-up ekranında seçenek çıkmayacak.", this);

        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            experience = player.GetComponent<PlayerExperience>();
            weapons = player.GetComponent<PlayerWeapons>();
            playerHealth = player.GetComponent<PlayerHealth>();

            // Item sistemi her oyunda olmalı; Player'a elle eklenmemişse ekle
            if (!player.TryGetComponent(out passives))
                passives = player.gameObject.AddComponent<PlayerPassives>();
        }

        var canvas = CreateCanvas();
        CreateXpBar(canvas);
        CreateSlots(canvas);

        if (experience != null)
        {
            experience.ExperienceChanged += _ => RefreshExperience();
            RefreshExperience();
        }

        if (weapons != null)
        {
            weapons.WeaponAdded += _ => RefreshWeapons();
            weapons.WeaponLeveledUp += _ => RefreshWeapons();
        }
        RefreshWeapons();

        if (passives != null)
        {
            passives.ItemAdded += _ => RefreshPassives();
            passives.ItemLeveledUp += _ => RefreshPassives();
        }
        RefreshPassives();

        if (playerHealth != null)
            WorldHealthBar.Attach(playerHealth, playerHealth, playerHealthColor,
                playerBarSize.x, playerBarSize.y, false, healthBarSortingOrder + 2);

        // HUD'dan önce doğmuş düşmanlar
        foreach (var enemy in Enemy.Active)
            AttachEnemyBar(enemy);

        if (showDamageNumbers && font != null)
            gameObject.AddComponent<DamageNumbers>().Initialize(font);

        // En son kurulsun ki HUD'un üstünde çizilsin
        gameObject.AddComponent<LevelUpMenu>().Initialize(canvas, font, upgradePool, experience, weapons, passives, playerHealth);
    }

    private void AttachEnemyBar(Enemy enemy)
    {
        if (enemy.GetComponentInChildren<WorldHealthBar>() != null) return;
        WorldHealthBar.Attach(enemy, enemy, enemyHealthColor,
            enemyBarSize.x, enemyBarSize.y, hideFullEnemyBars, healthBarSortingOrder);
    }

    private void RefreshExperience()
    {
        if (xpFill != null)
            xpFill.anchorMax = new Vector2(experience.Progress, 1f);
        UIBuilder.SetText(levelText, $"LV {experience.Level}");
    }

    private void RefreshWeapons()
    {
        for (int i = 0; i < weaponSlots.Count; i++)
        {
            var weapon = weapons != null && i < weapons.Weapons.Count ? weapons.Weapons[i] : null;
            if (weapon == null)
                ShowSlot(weaponSlots[i], null, "", "");
            else
                ShowSlot(weaponSlots[i], UIBuilder.GetWeaponIcon(weapon.Data), weapon.Data.displayName,
                    weapon.IsMaxLevel ? "MAX" : weapon.Level.ToString());
        }
    }

    private void RefreshPassives()
    {
        for (int i = 0; i < passiveSlots.Count; i++)
        {
            var item = passives != null && i < passives.Items.Count ? passives.Items[i] : null;
            if (item == null)
                ShowSlot(passiveSlots[i], null, "", "");
            else
                ShowSlot(passiveSlots[i], item.Data.icon, item.Data.displayName,
                    item.IsMaxLevel ? "MAX" : item.Level.ToString());
        }
    }

    // İkon yoksa isminin baş harfleri gösterilir; name boşsa slot boştur
    private static void ShowSlot(Slot slot, Sprite icon, string name, string level)
    {
        slot.icon.sprite = icon;
        slot.icon.enabled = icon != null;
        UIBuilder.SetText(slot.initials, icon == null && name.Length > 0 ? UIBuilder.GetInitials(name) : "");
        UIBuilder.SetText(slot.level, level);
    }

    private Transform CreateCanvas()
    {
        var go = new GameObject("HUDCanvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return go.transform;
    }

    // En üstte, ekran genişliğinde XP barı; sağında seviye yazısı
    private void CreateXpBar(Transform canvas)
    {
        var background = UIBuilder.CreateImage(canvas, "XpBar", xpBackgroundColor).rectTransform;
        UIBuilder.TopStrip(background, 0f, xpBarHeight);

        // Doluluk anchorMax.x ile verilir (0-1); sprite gerektirmez
        xpFill = UIBuilder.CreateImage(background, "Fill", xpFillColor).rectTransform;
        xpFill.anchorMin = Vector2.zero;
        xpFill.anchorMax = new Vector2(0f, 1f);
        xpFill.offsetMin = new Vector2(0f, 3f);
        xpFill.offsetMax = new Vector2(0f, -3f);

        levelText = UIBuilder.CreateText(background, "Level", font, 20f, TextAlignmentOptions.MidlineRight);
        if (levelText != null)
        {
            UIBuilder.Stretch(levelText.rectTransform);
            levelText.rectTransform.offsetMax = new Vector2(-14f, 0f);
            levelText.text = "LV 1";
        }

        if (experience == null)
            background.gameObject.SetActive(false);
    }

    // Sol üstte, XP barının altında: üst sıra silahlar, alt sıra pasif itemler
    private void CreateSlots(Transform canvas)
    {
        var root = UIBuilder.CreateRect(canvas, "Slots");
        UIBuilder.TopLeft(root, new Vector2(12f, xpBarHeight + 10f), Vector2.zero);

        int weaponCount = weapons != null ? weapons.MaxWeapons : 0;
        for (int i = 0; i < weaponCount; i++)
            weaponSlots.Add(CreateSlot(root, $"Weapon{i + 1}", i, 0, weaponSlotBorder));

        int passiveCount = passives != null ? passives.MaxItems : 0;
        for (int i = 0; i < passiveCount; i++)
            passiveSlots.Add(CreateSlot(root, $"Passive{i + 1}", i, 1, passiveSlotBorder));
    }

    private Slot CreateSlot(Transform parent, string name, int column, int row, Color borderColor)
    {
        var border = UIBuilder.CreateImage(parent, name, borderColor);
        UIBuilder.TopLeft(border.rectTransform,
            new Vector2(column * (slotSize + slotSpacing), row * (slotSize + slotSpacing)),
            new Vector2(slotSize, slotSize));

        var inner = UIBuilder.CreateImage(border.transform, "Background", new Color(0.08f, 0.08f, 0.1f, 0.85f));
        UIBuilder.Stretch(inner.rectTransform, 3f);

        var icon = UIBuilder.CreateImage(inner.transform, "Icon", Color.white);
        UIBuilder.Stretch(icon.rectTransform, 6f);
        icon.preserveAspect = true;
        icon.enabled = false;

        var initials = UIBuilder.CreateText(inner.transform, "Initials", font, 24f, TextAlignmentOptions.Center);
        if (initials != null)
            UIBuilder.Stretch(initials.rectTransform);

        var level = UIBuilder.CreateText(inner.transform, "Level", font, 16f, TextAlignmentOptions.BottomRight);
        if (level != null)
            UIBuilder.Stretch(level.rectTransform, 3f);

        return new Slot { icon = icon, initials = initials, level = level };
    }
}
