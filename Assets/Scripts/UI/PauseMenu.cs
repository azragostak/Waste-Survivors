using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// ESC (veya P) tuşuyla açılıp kapanan durdurma menüsü.
// EndScreen ile aynı yatay şerit (Band) görsel dilini ve LevelUpMenu'nün çerçeveli piksel buton yapısını kullanır.
// İçerik: Devam Et, Yeni Oyun, Çıkış, Arkaplan Müziği Aç/Kapa (Nota), Tüm Sesler Aç/Kapa (Hoparlör).
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }
    public static bool IsPaused => Instance != null && Instance.IsOpen;

    [Header("Opsiyonel Özel İkonlar (Boşsa Kodla Çizilen Piksel İkonlar Kullanılır)")]
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;

    private static Sprite generatedMusicOn;
    private static Sprite generatedMusicOff;
    private static Sprite generatedSoundOn;
    private static Sprite generatedSoundOff;

    private static readonly Color TitleColor = new Color(0.96f, 0.93f, 0.80f);
    private static readonly Color ResumeAccent = new Color(0.45f, 0.88f, 0.52f);
    private static readonly Color RestartAccent = new Color(0.95f, 0.78f, 0.35f);
    private static readonly Color QuitAccent = new Color(1f, 0.45f, 0.40f);
    private static readonly Color ToggleOnBorder = new Color(0.48f, 0.88f, 0.56f, 0.95f);
    private static readonly Color ToggleOffBorder = new Color(0.78f, 0.36f, 0.36f, 0.90f);

    private GameObject rootPanel;
    private TMP_FontAsset font;
    private LevelUpMenu levelUpMenu;
    private PlayerExperience experience;

    private TextMeshProUGUI statsText;
    private UnityEngine.UI.Image musicBorder;
    private UnityEngine.UI.Image musicIcon;
    private TextMeshProUGUI musicLabel;
    private UnityEngine.UI.Image soundBorder;
    private UnityEngine.UI.Image soundIcon;
    private TextMeshProUGUI soundLabel;

    public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    public void Initialize(TMP_FontAsset fontAsset, LevelUpMenu levelUp, PlayerExperience playerExperience)
    {
        Instance = this;
        font = fontAsset;
        levelUpMenu = levelUp;
        experience = playerExperience;

        GameAudio.EnsureInitialized();
        EnsureEventSystem();
        EnsureDefaultSprites();
        BuildUI();
        RefreshAudioVisuals();
        rootPanel.SetActive(false);

        GameAudio.StateChanged += RefreshAudioVisuals;
        if (GameSession.Instance != null)
        {
            GameSession.Instance.Finished += ForceClose;
            GameSession.Instance.GameOver += ForceClose;
        }
    }

    void OnDestroy()
    {
        GameAudio.StateChanged -= RefreshAudioVisuals;
        if (GameSession.Instance != null)
        {
            GameSession.Instance.Finished -= ForceClose;
            GameSession.Instance.GameOver -= ForceClose;
        }
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (GameSession.Instance != null && GameSession.Instance.IsOver)
            return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (IsOpen)
                Resume();
            else
                Pause();
        }
    }

    public void Pause()
    {
        if (IsOpen) return;
        if (GameSession.Instance != null && GameSession.Instance.IsOver) return;

        RefreshStatsLine();
        RefreshAudioVisuals();

        Time.timeScale = 0f;
        rootPanel.SetActive(true);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    public void Resume()
    {
        if (!IsOpen) return;

        rootPanel.SetActive(false);

        // LevelUpMenu arkada açıksa (ör. başlangıç silahı seçimi ya da level-up ekranı) oyun duraklatılmış kalmalı
        bool keepPaused = (levelUpMenu != null && levelUpMenu.IsOpen) ||
                          (GameSession.Instance != null && GameSession.Instance.IsGameOver);
        Time.timeScale = keepPaused ? 0f : 1f;
    }

    private void ForceClose()
    {
        if (!IsOpen) return;
        rootPanel.SetActive(false);
    }

    private void OnNewGameClicked()
    {
        rootPanel.SetActive(false);
        if (GameSession.Instance != null)
            GameSession.Instance.Restart();
        else
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void OnQuitClicked()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnToggleMusicClicked()
    {
        GameAudio.ToggleMusicMute();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void OnToggleSoundClicked()
    {
        GameAudio.ToggleMasterMute();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void RefreshStatsLine()
    {
        if (statsText == null) return;
        float elapsed = GameSession.Instance != null ? GameSession.Instance.Elapsed : 0f;
        int kills = GameSession.Instance != null ? GameSession.Instance.Kills : 0;
        int level = experience != null ? experience.Level : 1;
        statsText.text = $"Süre {GameSession.FormatTime(elapsed)}     Seviye {level}     Temizlenen kirlilik {kills}";
    }

    private void RefreshAudioVisuals()
    {
        bool musicOn = !GameAudio.IsMusicMuted;
        if (musicBorder != null)
            musicBorder.color = musicOn ? ToggleOnBorder : ToggleOffBorder;
        if (musicIcon != null)
            musicIcon.sprite = musicOn
                ? (musicOnSprite != null ? musicOnSprite : generatedMusicOn)
                : (musicOffSprite != null ? musicOffSprite : generatedMusicOff);
        UIBuilder.SetText(musicLabel, musicOn ? "Müzik: Açık" : "Müzik: Kapalı");
        if (musicLabel != null)
            musicLabel.color = musicOn ? new Color(0.92f, 0.95f, 0.92f) : new Color(0.75f, 0.55f, 0.55f);

        bool soundOn = !GameAudio.IsMasterMuted;
        if (soundBorder != null)
            soundBorder.color = soundOn ? ToggleOnBorder : ToggleOffBorder;
        if (soundIcon != null)
            soundIcon.sprite = soundOn
                ? (soundOnSprite != null ? soundOnSprite : generatedSoundOn)
                : (soundOffSprite != null ? soundOffSprite : generatedSoundOff);
        UIBuilder.SetText(soundLabel, soundOn ? "Tüm Sesler: Açık" : "Tüm Sesler: Kapalı");
        if (soundLabel != null)
            soundLabel.color = soundOn ? new Color(0.92f, 0.95f, 0.92f) : new Color(0.75f, 0.55f, 0.55f);
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("PauseCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25; // HUD (10) ve LevelUpMenu üstünde, EndScreen (30) altında

        var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Arkadaki dünyayı kapatmadan hafifçe gölgeleyen tam ekran tıklama tutucu
        var backdrop = UIBuilder.CreateImage(canvasGo.transform, "PauseOverlay", new Color(0f, 0f, 0f, 0.22f), raycastTarget: true);
        UIBuilder.Stretch(backdrop.rectTransform);
        rootPanel = backdrop.gameObject;

        // EndScreen ile aynı dilde ortada yatay koyu şerit
        var band = UIBuilder.CreateImage(backdrop.transform, "Band", new Color(0f, 0f, 0f, 0.68f), raycastTarget: true);
        var bandRect = band.rectTransform;
        bandRect.anchorMin = new Vector2(0f, 0.5f);
        bandRect.anchorMax = new Vector2(1f, 0.5f);
        bandRect.pivot = new Vector2(0.5f, 0.5f);
        bandRect.sizeDelta = new Vector2(0f, 460f);
        bandRect.anchoredPosition = Vector2.zero;

        // Şeridin üst ve alt kenarlarına ince zarif çizgi
        CreateHorizontalDivider(band.transform, "TopLine", 229f);
        CreateHorizontalDivider(band.transform, "BottomLine", -229f);

        // Başlık ve anlık oyun özeti (EndScreen tipografisiyle birebir uyumlu)
        var title = UIBuilder.CreateText(band.transform, "Title", font, 72f, TextAlignmentOptions.Center, bold: true);
        if (title != null)
        {
            title.text = "DURAKLATILDI";
            title.color = TitleColor;
            PlaceCentered(title.rectTransform, new Vector2(0f, 162f), new Vector2(1400f, 88f));
        }

        statsText = UIBuilder.CreateText(band.transform, "Stats", font, 26f, TextAlignmentOptions.Center, bold: true);
        if (statsText != null)
        {
            statsText.color = Color.white;
            PlaceCentered(statsText.rectTransform, new Vector2(0f, 100f), new Vector2(1400f, 38f));
        }

        // Ana aksiyon butonları: Devam Et - Yeni Oyun - Çıkış
        var btnSize = new Vector2(236f, 58f);
        const float btnSpacing = 26f;
        float rowWidth = 3f * btnSize.x + 2f * btnSpacing;
        float startX = -rowWidth * 0.5f + btnSize.x * 0.5f;

        CreateActionButton(band.transform, "ResumeButton", "DEVAM ET", ResumeAccent,
            new Vector2(startX, 24f), btnSize, Resume);
        CreateActionButton(band.transform, "NewGameButton", "YENİ OYUN", RestartAccent,
            new Vector2(startX + (btnSize.x + btnSpacing), 24f), btnSize, OnNewGameClicked);
        CreateActionButton(band.transform, "QuitButton", "ÇIKIŞ", QuitAccent,
            new Vector2(startX + 2f * (btnSize.x + btnSpacing), 24f), btnSize, OnQuitClicked);

        // Ses aç/kapa ikon butonları: Sol = Arkaplan Müziği (Nota), Sağ = Tüm Sesler (Hoparlör)
        CreateAudioToggle(band.transform, "MusicToggle", new Vector2(-115f, -74f),
            out musicBorder, out musicIcon, out musicLabel, OnToggleMusicClicked);
        CreateAudioToggle(band.transform, "SoundToggle", new Vector2(115f, -74f),
            out soundBorder, out soundIcon, out soundLabel, OnToggleSoundClicked);

        // Alt kısayol ipucu (EndScreen'deki "R - Yeniden başla" satırıyla aynı stil)
        var hint = UIBuilder.CreateText(band.transform, "Hint", font, 22f, TextAlignmentOptions.Center, bold: false);
        if (hint != null)
        {
            hint.text = "ESC  -  Devam et";
            hint.color = new Color(0.7f, 0.7f, 0.7f);
            PlaceCentered(hint.rectTransform, new Vector2(0f, -184f), new Vector2(800f, 32f));
        }
    }

    private static void CreateHorizontalDivider(Transform parent, string name, float y)
    {
        var line = UIBuilder.CreateImage(parent, name, new Color(1f, 1f, 1f, 0.16f));
        var rect = line.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(0f, 2f);
        rect.anchoredPosition = new Vector2(0f, y);
    }

    private void CreateActionButton(Transform parent, string name, string label, Color accent,
        Vector2 position, Vector2 size, Action onClick)
    {
        var border = UIBuilder.CreateImage(parent, name, accent, raycastTarget: true);
        PlaceCentered(border.rectTransform, position, size);

        var button = border.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = border;
        var colors = button.colors;
        colors.normalColor = new Color(0.82f, 0.82f, 0.82f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.68f, 0.68f, 0.68f);
        colors.selectedColor = new Color(0.82f, 0.82f, 0.82f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(() => onClick?.Invoke());

        var inner = UIBuilder.CreateImage(border.transform, "Background", new Color(0.09f, 0.09f, 0.12f, 0.94f));
        UIBuilder.Stretch(inner.rectTransform, 3f);

        var text = UIBuilder.CreateText(inner.transform, "Label", font, 26f, TextAlignmentOptions.Center, bold: true);
        if (text != null)
        {
            text.text = label;
            text.color = Color.white;
            UIBuilder.Stretch(text.rectTransform);
        }
    }

    private void CreateAudioToggle(Transform parent, string name, Vector2 position,
        out UnityEngine.UI.Image borderOut, out UnityEngine.UI.Image iconOut, out TextMeshProUGUI labelOut, Action onClick)
    {
        var container = UIBuilder.CreateRect(parent, name);
        PlaceCentered(container, position, new Vector2(200f, 118f));

        var border = UIBuilder.CreateImage(container, "ButtonBorder", ToggleOnBorder, raycastTarget: true);
        PlaceCentered(border.rectTransform, new Vector2(0f, 16f), new Vector2(72f, 72f));

        var button = border.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = border;
        var colors = button.colors;
        colors.normalColor = new Color(0.86f, 0.86f, 0.86f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.70f, 0.70f, 0.70f);
        colors.selectedColor = new Color(0.86f, 0.86f, 0.86f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(() => onClick?.Invoke());

        var inner = UIBuilder.CreateImage(border.transform, "Background", new Color(0.08f, 0.08f, 0.11f, 0.94f));
        UIBuilder.Stretch(inner.rectTransform, 3f);

        var icon = UIBuilder.CreateImage(inner.transform, "Icon", Color.white);
        UIBuilder.Stretch(icon.rectTransform, 10f);
        icon.preserveAspect = true;

        var label = UIBuilder.CreateText(container, "Caption", font, 19f, TextAlignmentOptions.Center, bold: false);
        if (label != null)
            PlaceCentered(label.rectTransform, new Vector2(0f, -40f), new Vector2(200f, 26f));

        borderOut = border;
        iconOut = icon;
        labelOut = label;
    }

    private static void PlaceCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void EnsureDefaultSprites()
    {
        if (generatedMusicOn == null)
        {
            // 20x20 piksel sanatı çift sekizlik nota (♫)
            string[] noteActive =
            {
                "....................",
                "....................",
                ".............WWWW...",
                "..........WWWWWWW...",
                ".......WWWWWWW.WW...",
                ".......WWWW....WW...",
                ".......WW......WW...",
                ".......WW......WW...",
                ".......WW......WW...",
                ".......WW......WW...",
                ".......WW......WW...",
                ".......WW...WWWWW...",
                "....WWWWW..WWWWWW...",
                "...WWWWWW..WWWWWW...",
                "...WWWWWW...WWWW....",
                "....WWWW............",
                "....................",
                "....................",
                "....................",
                "...................."
            };
            generatedMusicOn = BuildPixelIconSprite("PixelMusicOn", noteActive, slashMute: false);
            generatedMusicOff = BuildPixelIconSprite("PixelMusicOff", noteActive, slashMute: true);
        }

        if (generatedSoundOn == null)
        {
            // 20x20 piksel sanatı hoparlör + ses dalgaları
            string[] speakerActive =
            {
                "....................",
                "....................",
                "....................",
                "........WW..........",
                ".......WWW....WW....",
                "......WWWW.....WW...",
                ".....WWWWW..WW..WW..",
                "..WWWWWWWW...WW.WW..",
                "..WWWWWWWW...WW..WW.",
                "..WWWWWWWW...WW..WW.",
                "..WWWWWWWW...WW..WW.",
                "..WWWWWWWW...WW..WW.",
                "..WWWWWWWW...WW.WW..",
                ".....WWWWW..WW..WW..",
                "......WWWW.....WW...",
                ".......WWW....WW....",
                "........WW..........",
                "....................",
                "....................",
                "...................."
            };

            // Sessiz hoparlör: hoparlör gövdesi + sağda kırmızı X işareti
            string[] speakerMuted =
            {
                "....................",
                "....................",
                "....................",
                "........GG..........",
                ".......GGG..........",
                "......GGGG..........",
                ".....GGGGG..........",
                "..GGGGGGGG..RR..RR..",
                "..GGGGGGGG...RRRR...",
                "..GGGGGGGG....RR....",
                "..GGGGGGGG....RR....",
                "..GGGGGGGG...RRRR...",
                "..GGGGGGGG..RR..RR..",
                ".....GGGGG..........",
                "......GGGG..........",
                ".......GGG..........",
                "........GG..........",
                "....................",
                "....................",
                "...................."
            };

            generatedSoundOn = BuildPixelIconSprite("PixelSoundOn", speakerActive, slashMute: false);
            generatedSoundOff = BuildPixelIconSprite("PixelSoundOff", speakerMuted, slashMute: false);
        }
    }

    private static Sprite BuildPixelIconSprite(string texName, string[] rows, bool slashMute)
    {
        const int size = 20;
        var grid = new char[size, size];

        for (int r = 0; r < size; r++)
        {
            string line = r < rows.Length ? rows[r] : "";
            for (int c = 0; c < size; c++)
            {
                char ch = c < line.Length ? line[c] : '.';
                if (slashMute && ch == 'W') ch = 'G';
                grid[r, c] = ch;
            }
        }

        if (slashMute)
        {
            // Nota ikonunun üstüne çapraz kırmızı kapatma çizgisi + etrafına 1px siyah kontur
            for (int i = 3; i <= 16; i++)
            {
                if (i - 1 >= 0 && grid[i, i - 1] != 'R') grid[i, i - 1] = '#';
                if (i + 2 < size && grid[i, i + 2] != 'R') grid[i, i + 2] = '#';
                grid[i, i] = 'R';
                if (i + 1 < size) grid[i, i + 1] = 'R';
            }
        }

        Color white = Color.white;
        Color grey = new Color(0.62f, 0.64f, 0.68f, 1f);
        Color red = new Color(1f, 0.38f, 0.35f, 1f);
        Color outline = new Color(0f, 0f, 0f, 0.92f);

        var pixels = new Color[size * size];
        for (int r = 0; r < size; r++)
        {
            int texY = size - 1 - r;
            for (int c = 0; c < size; c++)
            {
                char ch = grid[r, c];
                if (ch == 'W')
                    pixels[texY * size + c] = white;
                else if (ch == 'G')
                    pixels[texY * size + c] = grey;
                else if (ch == 'R')
                    pixels[texY * size + c] = red;
                else if (ch == '#' || HasFilledNeighbor(grid, r, c, size))
                    pixels[texY * size + c] = outline;
                else
                    pixels[texY * size + c] = Color.clear;
            }
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = texName,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static bool HasFilledNeighbor(char[,] grid, int r, int c, int size)
    {
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                int nr = r + dr;
                int nc = c + dc;
                if (nr >= 0 && nr < size && nc >= 0 && nc < size && grid[nr, nc] != '.')
                    return true;
            }
        }
        return false;
    }
}
