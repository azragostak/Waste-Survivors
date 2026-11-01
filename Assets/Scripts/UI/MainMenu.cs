using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Ana menü ekranı:
// - Tam ekran kapak görseli (anamenü.jpeg)
// - Sağ üst köşede Ayarlar (Settings) butonu ve Dil Seçeneği (varsayılan İngilizce, Türkçe destekli)
// - Ayarların altında Credits butonu: Resources/Credits.txt içeriğini gösteren pencere
// - Alt-ortada "START GAME" / "OYUNA BAŞLA" butonu
// - Tıklanınca açılan Görev Mektubu (Mission Letter) ve "CONTINUE" / "DEVAM ET" ile oyuna geçiş.
public class MainMenu : MonoBehaviour
{
    private const float InputGuardDelay = 0.25f;
    private const float ModalAnimDuration = 0.2f;

    [Header("Sahne ve Görsel")]
    [SerializeField] private string gameSceneName = "SampleScene";
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private UnityEngine.UI.Image backgroundImage;

    [Header("Buton Metinleri (İngilizce Varsayılan)")]
    [SerializeField] private string startButtonLabel = "START GAME";
    [SerializeField] private string continueButtonLabel = "CONTINUE";

    [Header("Görev Mektubu (İngilizce Varsayılan)")]
    [SerializeField] private string letterStamp = "OFFICIAL MISSION DISPATCH";
    [SerializeField] private string letterHeading = "CLEANSE THE FOREST";
    [SerializeField, TextArea(6, 12)] private string letterBody =
        "Dear Forest Guardian,\n\n" +
        "An illegal factory deep inside the woods has flooded our forest with toxic barrels, " +
        "plastic waste, and acid fumes. What was once a thriving woodland is now choking under " +
        "relentless waves of pollution.\n\n" +
        "Your mission is clear: enter the contaminated forest, hold back the endless tide of " +
        "factory waste, and purify the poisoned soil until nature breathes again.";
    [SerializeField] private string letterSignature = "- Forest Restoration Command";

    private const string TurkishLetterBody =
        "Sayın Orman Koruyucusu,\n\n" +
        "Ormanın derinliklerindeki kaçak fabrika; zehirli variller, plastik atıklar ve asit " +
        "dumanlarıyla doğamızı istila etti. Bir zamanlar hayat dolu olan ormanımız şimdi amansız " +
        "kirlilik dalgaları altında boğuluyor.\n\n" +
        "Görevin net: kirletilmiş ormana gir, fabrikanın atık dalgalarını püskürt ve doğa yeniden " +
        "nefes alana dek zehirlenmiş toprağı arındır.";

    private static readonly Color StartButtonAccent = new Color(0.46f, 0.88f, 0.48f, 1f);
    private static readonly Color StartButtonFill = new Color(0.07f, 0.12f, 0.08f, 0.95f);
    private static readonly Color SettingsButtonAccent = new Color(0.76f, 0.86f, 0.80f, 0.96f);
    private static readonly Color SettingsButtonFill = new Color(0.07f, 0.10f, 0.11f, 0.93f);

    private static readonly Color ModalTitleColor = new Color(0.96f, 0.93f, 0.80f, 1f);
    private static readonly Color ActiveLangBorder = new Color(0.46f, 0.90f, 0.50f, 1f);
    private static readonly Color ActiveLangFill = new Color(0.11f, 0.25f, 0.14f, 0.98f);
    private static readonly Color InactiveLangBorder = new Color(0.36f, 0.41f, 0.45f, 0.88f);
    private static readonly Color InactiveLangFill = new Color(0.09f, 0.10f, 0.13f, 0.95f);
    private static readonly Color ToggleOnBorder = new Color(0.48f, 0.88f, 0.56f, 0.95f);
    private static readonly Color ToggleOffBorder = new Color(0.78f, 0.36f, 0.36f, 0.90f);
    private static readonly Color SliderAccent = new Color(0.45f, 0.72f, 1f, 1f);

    private static readonly Color TimberBorder = new Color(0.22f, 0.15f, 0.10f, 1f);
    private static readonly Color BrassTrim = new Color(0.66f, 0.53f, 0.32f, 1f);
    private static readonly Color ParchmentFill = new Color(0.95f, 0.90f, 0.78f, 1f);
    private static readonly Color ParchmentRule = new Color(0.56f, 0.44f, 0.28f, 0.45f);
    private static readonly Color StampColor = new Color(0.66f, 0.19f, 0.14f, 1f);
    private static readonly Color HeadingInk = new Color(0.15f, 0.10f, 0.06f, 1f);
    private static readonly Color BodyInk = new Color(0.20f, 0.14f, 0.09f, 1f);
    private static readonly Color SignatureInk = new Color(0.28f, 0.21f, 0.12f, 1f);
    private static readonly Color ContinueAccent = new Color(0.30f, 0.72f, 0.36f, 1f);

    private static Sprite generatedSealSprite;
    private static Sprite generatedGearSprite;
    private static AudioClip generatedClickClip;
    private static AudioClip generatedPaperClip;

    private TMP_FontAsset font;
    private GameObject startButtonRoot;
    private GameObject settingsButtonRoot;
    private GameObject creditsButtonRoot;

    // Credits penceresi elemanları
    private GameObject creditsOverlay;
    private CanvasGroup creditsCanvasGroup;
    private RectTransform creditsCardRect;
    private Coroutine creditsAnimCoroutine;
    private TextMeshProUGUI creditsButtonText;
    private TextMeshProUGUI creditsTitleText;
    private TextMeshProUGUI creditsBodyText;
    private TextMeshProUGUI closeCreditsButtonText;

    // Ayarlar (Settings) modal elemanları
    private GameObject settingsOverlay;
    private CanvasGroup settingsCanvasGroup;
    private RectTransform settingsCardRect;
    private Coroutine settingsAnimCoroutine;
    private TextMeshProUGUI settingsButtonText;
    private TextMeshProUGUI settingsTitleText;
    private TextMeshProUGUI languageCaptionText;
    private TextMeshProUGUI musicCaptionText;
    private TextMeshProUGUI sfxCaptionText;
    private TextMeshProUGUI musicVolumeValueText;
    private TextMeshProUGUI sfxVolumeValueText;
    private UnityEngine.UI.Slider musicVolumeSlider;
    private UnityEngine.UI.Slider sfxVolumeSlider;
    private UnityEngine.UI.Image musicToggleBorder;
    private UnityEngine.UI.Image musicToggleIcon;
    private TextMeshProUGUI musicToggleLabel;
    private UnityEngine.UI.Image soundToggleBorder;
    private UnityEngine.UI.Image soundToggleIcon;
    private TextMeshProUGUI soundToggleLabel;
    private TextMeshProUGUI closeSettingsButtonText;

    private UnityEngine.UI.Image englishBtnBorder;
    private UnityEngine.UI.Image englishBtnFill;
    private TextMeshProUGUI englishBtnText;
    private UnityEngine.UI.Image turkishBtnBorder;
    private UnityEngine.UI.Image turkishBtnFill;
    private TextMeshProUGUI turkishBtnText;

    // Görev mektubu elemanları
    private GameObject letterOverlay;
    private CanvasGroup letterCanvasGroup;
    private RectTransform letterCardRect;
    private Coroutine letterAnimCoroutine;
    private TextMeshProUGUI startBtnText;
    private TextMeshProUGUI stampTextComp;
    private TextMeshProUGUI headingTextComp;
    private TextMeshProUGUI bodyTextComp;
    private TextMeshProUGUI signatureTextComp;
    private TextMeshProUGUI continueBtnText;

    private float stateEnteredAt;
    private bool loadingScene;

    private bool IsLetterOpen => letterOverlay != null && letterOverlay.activeSelf;
    private bool IsSettingsOpen => settingsOverlay != null && settingsOverlay.activeSelf;
    private bool IsCreditsOpen => creditsOverlay != null && creditsOverlay.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        generatedSealSprite = null;
        generatedGearSprite = null;
        generatedClickClip = null;
        generatedPaperClip = null;
    }

    void Awake()
    {
        Time.timeScale = 1f;
        GameAudio.EnsureInitialized();
        GameLanguage.EnsureInitialized();
    }

    void OnEnable()
    {
        GameLanguage.Changed += RefreshLocalizedTexts;
        GameAudio.StateChanged += RefreshAudioSettingsUI;
    }

    void OnDisable()
    {
        GameLanguage.Changed -= RefreshLocalizedTexts;
        GameAudio.StateChanged -= RefreshAudioSettingsUI;
    }

    void Start()
    {
        stateEnteredAt = Time.unscaledTime;

        var tmpSettings = Resources.Load<TMP_Settings>("TMP Settings");
        font = tmpSettings != null ? TMP_Settings.defaultFontAsset : null;

        EnsureEventSystem();
        EnsureGeneratedAssets();

        var canvasTransform = EnsureCanvas();
        EnsureBackground(canvasTransform);
        BuildStartButton(canvasTransform);
        BuildTopRightSettingsButton(canvasTransform);
        BuildCreditsButton(canvasTransform);
        BuildSettingsModal(canvasTransform);
        BuildCreditsModal(canvasTransform);
        BuildMissionLetter(canvasTransform);

        settingsOverlay.SetActive(false);
        creditsOverlay.SetActive(false);
        letterOverlay.SetActive(false);

        RefreshLocalizedTexts();
        RefreshAudioSettingsUI();
    }

    void Update()
    {
        if (loadingScene) return;
        if (Time.unscaledTime - stateEnteredAt < InputGuardDelay) return;

        if (IsSettingsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                CloseSettings();
            return;
        }

        if (IsCreditsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                CloseCredits();
            return;
        }

        if (!IsLetterOpen)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                OpenMissionLetter();
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                StartGame();
            else if (Input.GetKeyDown(KeyCode.Escape))
                CloseMissionLetter();
        }
    }

    private void OpenSettings()
    {
        if (IsSettingsOpen || IsLetterOpen || IsCreditsOpen || loadingScene) return;

        SfxPlayer.Play(generatedClickClip, volume: 0.30f, pitchVariation: 0.04f, minInterval: 0.02f);

        stateEnteredAt = Time.unscaledTime;
        SetMainButtonsVisible(false);
        RefreshAudioSettingsUI();
        RefreshLanguageButtons();

        settingsOverlay.SetActive(true);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (settingsAnimCoroutine != null)
            StopCoroutine(settingsAnimCoroutine);
        settingsAnimCoroutine = StartCoroutine(AnimateModalOpen(settingsCanvasGroup, settingsCardRect, () => settingsAnimCoroutine = null));
    }

    private void CloseSettings()
    {
        if (!IsSettingsOpen || loadingScene) return;

        SfxPlayer.Play(generatedClickClip, volume: 0.25f, pitchVariation: 0.04f, minInterval: 0.02f);

        if (settingsAnimCoroutine != null)
        {
            StopCoroutine(settingsAnimCoroutine);
            settingsAnimCoroutine = null;
        }

        settingsOverlay.SetActive(false);
        SetMainButtonsVisible(true);

        stateEnteredAt = Time.unscaledTime;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void OpenCredits()
    {
        if (IsCreditsOpen || IsSettingsOpen || IsLetterOpen || loadingScene) return;

        SfxPlayer.Play(generatedClickClip, volume: 0.30f, pitchVariation: 0.04f, minInterval: 0.02f);

        stateEnteredAt = Time.unscaledTime;
        SetMainButtonsVisible(false);
        RefreshCreditsText();

        creditsOverlay.SetActive(true);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (creditsAnimCoroutine != null)
            StopCoroutine(creditsAnimCoroutine);
        creditsAnimCoroutine = StartCoroutine(AnimateModalOpen(creditsCanvasGroup, creditsCardRect, () => creditsAnimCoroutine = null));
    }

    private void CloseCredits()
    {
        if (!IsCreditsOpen || loadingScene) return;

        SfxPlayer.Play(generatedClickClip, volume: 0.25f, pitchVariation: 0.04f, minInterval: 0.02f);

        if (creditsAnimCoroutine != null)
        {
            StopCoroutine(creditsAnimCoroutine);
            creditsAnimCoroutine = null;
        }

        creditsOverlay.SetActive(false);
        SetMainButtonsVisible(true);

        stateEnteredAt = Time.unscaledTime;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    // Metin her açılışta Resources/Credits.txt'den okunur; dosya güncellenince pencere de güncellenir
    private void RefreshCreditsText()
    {
        var credits = Resources.Load<TextAsset>("Credits");
        UIBuilder.SetText(creditsBodyText, credits != null
            ? credits.text.Trim()
            : GameLanguage.Tr("Credits file not found.", "Credits dosyası bulunamadı."));
    }

    private void SelectLanguage(Language language)
    {
        SfxPlayer.Play(generatedClickClip, volume: 0.30f, pitchVariation: 0.03f, minInterval: 0.02f);
        GameLanguage.SetLanguage(language);
        RefreshLanguageButtons();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void OpenMissionLetter()
    {
        if (IsLetterOpen || IsSettingsOpen || IsCreditsOpen || loadingScene) return;

        SfxPlayer.Play(generatedPaperClip, volume: 0.32f, pitchVariation: 0.04f, minInterval: 0.02f);

        stateEnteredAt = Time.unscaledTime;
        SetMainButtonsVisible(false);

        letterOverlay.SetActive(true);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (letterAnimCoroutine != null)
            StopCoroutine(letterAnimCoroutine);
        letterAnimCoroutine = StartCoroutine(AnimateModalOpen(letterCanvasGroup, letterCardRect, () => letterAnimCoroutine = null));
    }

    private void CloseMissionLetter()
    {
        if (!IsLetterOpen || loadingScene) return;

        SfxPlayer.Play(generatedClickClip, volume: 0.25f, pitchVariation: 0.04f, minInterval: 0.02f);

        if (letterAnimCoroutine != null)
        {
            StopCoroutine(letterAnimCoroutine);
            letterAnimCoroutine = null;
        }

        letterOverlay.SetActive(false);
        SetMainButtonsVisible(true);

        stateEnteredAt = Time.unscaledTime;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void SetMainButtonsVisible(bool visible)
    {
        if (startButtonRoot != null) startButtonRoot.SetActive(visible);
        if (settingsButtonRoot != null) settingsButtonRoot.SetActive(visible);
        if (creditsButtonRoot != null) creditsButtonRoot.SetActive(visible);
    }

    private void StartGame()
    {
        if (loadingScene) return;
        loadingScene = true;

        SfxPlayer.Play(generatedClickClip, volume: 0.35f, pitchVariation: 0.03f, minInterval: 0.01f);
        Time.timeScale = 1f;

        if (!string.IsNullOrWhiteSpace(gameSceneName) && Application.CanStreamedLevelBeLoaded(gameSceneName))
            SceneManager.LoadScene(gameSceneName);
        else if (SceneManager.sceneCountInBuildSettings > 1)
            SceneManager.LoadScene(1);
        else
            SceneManager.LoadScene("SampleScene");
    }

    private void RefreshLocalizedTexts()
    {
        bool tr = GameLanguage.IsTurkish;

        UIBuilder.SetText(startBtnText, tr ? "OYUNA BAŞLA" : startButtonLabel);
        UIBuilder.SetText(settingsButtonText, tr ? "AYARLAR" : "SETTINGS");

        UIBuilder.SetText(settingsTitleText, tr ? "AYARLAR" : "SETTINGS");
        UIBuilder.SetText(languageCaptionText, tr ? "DİL SEÇENEĞİ" : "LANGUAGE");
        UIBuilder.SetText(musicCaptionText, tr ? "MÜZİK" : "MUSIC");
        UIBuilder.SetText(sfxCaptionText, tr ? "EFEKTLER" : "EFFECTS");
        UIBuilder.SetText(closeSettingsButtonText, tr ? "KAPAT" : "CLOSE");

        UIBuilder.SetText(creditsButtonText, tr ? "EMEĞİ GEÇENLER" : "CREDITS");
        UIBuilder.SetText(creditsTitleText, tr ? "EMEĞİ GEÇENLER" : "CREDITS");
        UIBuilder.SetText(closeCreditsButtonText, tr ? "KAPAT" : "CLOSE");

        UIBuilder.SetText(stampTextComp, tr ? "RESMİ GÖREV MEKTUBU" : letterStamp);
        UIBuilder.SetText(headingTextComp, tr ? "ORMANI TEMİZLE" : letterHeading);
        UIBuilder.SetText(bodyTextComp, tr ? TurkishLetterBody : letterBody);
        UIBuilder.SetText(signatureTextComp, tr ? "- Orman Koruma Komutanlığı" : letterSignature);
        UIBuilder.SetText(continueBtnText, tr ? "DEVAM ET" : continueButtonLabel);

        RefreshLanguageButtons();
        RefreshAudioSettingsUI();
    }

    private void RefreshLanguageButtons()
    {
        bool isEn = GameLanguage.Current == Language.English;
        ApplyLangButtonVisual(englishBtnBorder, englishBtnFill, englishBtnText, isEn, "ENGLISH");
        ApplyLangButtonVisual(turkishBtnBorder, turkishBtnFill, turkishBtnText, !isEn, "TÜRKÇE");
    }

    private static void ApplyLangButtonVisual(UnityEngine.UI.Image border, UnityEngine.UI.Image fill,
        TextMeshProUGUI label, bool selected, string title)
    {
        if (border != null) border.color = selected ? ActiveLangBorder : InactiveLangBorder;
        if (fill != null) fill.color = selected ? ActiveLangFill : InactiveLangFill;
        if (label != null)
        {
            label.text = selected ? $"●  {title}" : title;
            label.color = selected ? new Color(0.92f, 1f, 0.92f, 1f) : new Color(0.75f, 0.78f, 0.82f, 1f);
        }
    }

    private void RefreshAudioSettingsUI()
    {
        PauseMenu.GetAudioToggleSprites(out var musicOnSprite, out var musicOffSprite, out var soundOnSprite, out var soundOffSprite);

        bool musicOn = !GameAudio.IsMusicMuted;
        if (musicToggleBorder != null)
            musicToggleBorder.color = musicOn ? ToggleOnBorder : ToggleOffBorder;
        if (musicToggleIcon != null)
            musicToggleIcon.sprite = musicOn ? musicOnSprite : musicOffSprite;
        UIBuilder.SetText(musicToggleLabel, musicOn
            ? GameLanguage.Tr("Music: On", "Müzik: Açık")
            : GameLanguage.Tr("Music: Off", "Müzik: Kapalı"));
        if (musicToggleLabel != null)
            musicToggleLabel.color = musicOn ? new Color(0.92f, 0.95f, 0.92f, 1f) : new Color(0.75f, 0.55f, 0.55f, 1f);

        bool soundOn = !GameAudio.IsMasterMuted;
        if (soundToggleBorder != null)
            soundToggleBorder.color = soundOn ? ToggleOnBorder : ToggleOffBorder;
        if (soundToggleIcon != null)
            soundToggleIcon.sprite = soundOn ? soundOnSprite : soundOffSprite;
        UIBuilder.SetText(soundToggleLabel, soundOn
            ? GameLanguage.Tr("All Sounds: On", "Tüm Sesler: Açık")
            : GameLanguage.Tr("All Sounds: Off", "Tüm Sesler: Kapalı"));
        if (soundToggleLabel != null)
            soundToggleLabel.color = soundOn ? new Color(0.92f, 0.95f, 0.92f, 1f) : new Color(0.75f, 0.55f, 0.55f, 1f);

        if (musicVolumeSlider != null)
            musicVolumeSlider.SetValueWithoutNotify(GameAudio.MusicVolume);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(GameAudio.SfxVolume);
        UIBuilder.SetText(musicVolumeValueText, $"{GameAudio.MusicVolume * 100f:0}%");
        UIBuilder.SetText(sfxVolumeValueText, $"{GameAudio.SfxVolume * 100f:0}%");
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

    private IEnumerator AnimateModalOpen(CanvasGroup group, RectTransform card, Action onFinished)
    {
        if (group != null) group.alpha = 0f;
        if (card != null) card.localScale = new Vector3(0.95f, 0.95f, 1f);

        for (float t = 0f; t < ModalAnimDuration; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / ModalAnimDuration);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            if (group != null) group.alpha = eased;
            if (card != null)
            {
                float s = Mathf.Lerp(0.95f, 1f, eased);
                card.localScale = new Vector3(s, s, 1f);
            }
            yield return null;
        }

        if (group != null) group.alpha = 1f;
        if (card != null) card.localScale = Vector3.one;
        onFinished?.Invoke();
    }

    private Transform EnsureCanvas()
    {
        if (TryGetComponent<Canvas>(out var existingCanvas))
            return existingCanvas.transform;

        var canvasGo = new GameObject("MainMenuCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return canvasGo.transform;
    }

    private void EnsureBackground(Transform canvasTransform)
    {
        if (backgroundImage == null)
        {
            var frame = UIBuilder.CreateRect(canvasTransform, "BackgroundFrame");
            frame.SetAsFirstSibling();
            UIBuilder.Stretch(frame);
            frame.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();

            backgroundImage = UIBuilder.CreateImage(frame, "BackgroundImage", Color.white, raycastTarget: false);
            UIBuilder.Stretch(backgroundImage.rectTransform);
        }

        if (backgroundSprite != null)
            backgroundImage.sprite = backgroundSprite;

        backgroundImage.raycastTarget = false;
        if (backgroundImage.sprite != null)
        {
            backgroundImage.color = Color.white;
            if (!backgroundImage.TryGetComponent<UnityEngine.UI.AspectRatioFitter>(out var fitter))
                fitter = backgroundImage.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();

            Rect rect = backgroundImage.sprite.rect;
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = rect.height > 0f ? rect.width / rect.height : (16f / 9f);
        }
    }

    private void BuildStartButton(Transform canvasTransform)
    {
        var root = UIBuilder.CreateRect(canvasTransform, "StartButtonGroup");
        PlaceCentered(root, new Vector2(0f, -420f), new Vector2(348f, 82f));
        startButtonRoot = root.gameObject;

        var shadow = UIBuilder.CreateImage(root, "Shadow", new Color(0f, 0f, 0f, 0.62f), raycastTarget: false);
        PlaceCentered(shadow.rectTransform, new Vector2(0f, -6f), new Vector2(340f, 76f));

        startBtnText = CreateFramedButton(root, "StartButton", startButtonLabel, StartButtonAccent, StartButtonFill,
            Vector2.zero, new Vector2(340f, 76f), 32f, OpenMissionLetter);
    }

    private void BuildTopRightSettingsButton(Transform canvasTransform)
    {
        var root = UIBuilder.CreateRect(canvasTransform, "SettingsButtonGroup");
        root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.sizeDelta = new Vector2(214f, 56f);
        root.anchoredPosition = new Vector2(-28f, -26f);
        settingsButtonRoot = root.gameObject;

        var shadow = UIBuilder.CreateImage(root, "Shadow", new Color(0f, 0f, 0f, 0.50f), raycastTarget: false);
        PlaceCentered(shadow.rectTransform, new Vector2(0f, -4f), new Vector2(210f, 54f));

        var border = UIBuilder.CreateImage(root, "SettingsButton", SettingsButtonAccent, raycastTarget: true);
        PlaceCentered(border.rectTransform, Vector2.zero, new Vector2(210f, 54f));

        var button = border.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = border;
        var colors = button.colors;
        colors.normalColor = new Color(0.90f, 0.90f, 0.90f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.74f, 0.74f, 0.74f, 1f);
        colors.selectedColor = new Color(0.90f, 0.90f, 0.90f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(OpenSettings);

        var inner = UIBuilder.CreateImage(border.transform, "Background", SettingsButtonFill, raycastTarget: false);
        UIBuilder.Stretch(inner.rectTransform, 3f);

        var gearIcon = UIBuilder.CreateImage(inner.transform, "GearIcon", Color.white, raycastTarget: false);
        gearIcon.sprite = generatedGearSprite;
        gearIcon.preserveAspect = true;
        gearIcon.rectTransform.anchorMin = gearIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        gearIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        gearIcon.rectTransform.sizeDelta = new Vector2(30f, 30f);
        gearIcon.rectTransform.anchoredPosition = new Vector2(28f, 0f);

        settingsButtonText = UIBuilder.CreateText(inner.transform, "Label", font, 22f, TextAlignmentOptions.Center, bold: true);
        if (settingsButtonText != null)
        {
            settingsButtonText.color = Color.white;
            settingsButtonText.characterSpacing = 2f;
            settingsButtonText.rectTransform.anchorMin = Vector2.zero;
            settingsButtonText.rectTransform.anchorMax = Vector2.one;
            settingsButtonText.rectTransform.offsetMin = new Vector2(48f, 0f);
            settingsButtonText.rectTransform.offsetMax = new Vector2(-10f, 0f);
        }
    }

    // Ayarlar butonunun hemen altında, aynı görünümde
    private void BuildCreditsButton(Transform canvasTransform)
    {
        var root = UIBuilder.CreateRect(canvasTransform, "CreditsButtonGroup");
        root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.sizeDelta = new Vector2(214f, 56f);
        root.anchoredPosition = new Vector2(-28f, -96f);
        creditsButtonRoot = root.gameObject;

        var shadow = UIBuilder.CreateImage(root, "Shadow", new Color(0f, 0f, 0f, 0.50f), raycastTarget: false);
        PlaceCentered(shadow.rectTransform, new Vector2(0f, -4f), new Vector2(210f, 54f));

        creditsButtonText = CreateFramedButton(root, "CreditsButton", "CREDITS", SettingsButtonAccent, SettingsButtonFill,
            Vector2.zero, new Vector2(210f, 54f), 20f, OpenCredits);
        // "EMEĞİ GEÇENLER" butona sığsın
        if (creditsButtonText != null)
        {
            creditsButtonText.enableAutoSizing = true;
            creditsButtonText.fontSizeMin = 14f;
            creditsButtonText.fontSizeMax = 20f;
        }
    }

    // Ayarlar penceresiyle aynı tasarım: başlık, Credits.txt metni ve Kapat butonu
    private void BuildCreditsModal(Transform canvasTransform)
    {
        var backdrop = UIBuilder.CreateImage(canvasTransform, "CreditsOverlay",
            new Color(0.02f, 0.04f, 0.03f, 0.78f), raycastTarget: true);
        UIBuilder.Stretch(backdrop.rectTransform);
        creditsOverlay = backdrop.gameObject;
        creditsCanvasGroup = creditsOverlay.AddComponent<CanvasGroup>();

        var cardSize = new Vector2(1000f, 760f);
        var cardRoot = UIBuilder.CreateRect(backdrop.transform, "CreditsCard");
        PlaceCentered(cardRoot, Vector2.zero, cardSize);
        creditsCardRect = cardRoot;

        var shadow = UIBuilder.CreateImage(cardRoot, "Shadow", new Color(0f, 0f, 0f, 0.62f), raycastTarget: false);
        PlaceCentered(shadow.rectTransform, new Vector2(8f, -10f), cardSize);

        var border = UIBuilder.CreateImage(cardRoot, "OuterBorder", StartButtonAccent, raycastTarget: true);
        UIBuilder.Stretch(border.rectTransform);

        var inner = UIBuilder.CreateImage(border.transform, "PanelFill", new Color(0.08f, 0.10f, 0.12f, 0.97f), raycastTarget: false);
        UIBuilder.Stretch(inner.rectTransform, 4f);
        var content = inner.transform;

        creditsTitleText = UIBuilder.CreateText(content, "Title", font, 44f, TextAlignmentOptions.Center, bold: true);
        if (creditsTitleText != null)
        {
            creditsTitleText.color = ModalTitleColor;
            PlaceCentered(creditsTitleText.rectTransform, new Vector2(0f, 330f), new Vector2(900f, 54f));
        }

        CreateDarkDivider(content, "TopDivider", 290f, 900f);

        // Uzun bağlantılar sığsın diye yazı boyutu kendini küçültebilir
        creditsBodyText = UIBuilder.CreateText(content, "Body", font, 22f, TextAlignmentOptions.Top, bold: false);
        if (creditsBodyText != null)
        {
            creditsBodyText.color = new Color(0.88f, 0.92f, 0.89f, 1f);
            creditsBodyText.outlineWidth = 0f;
            creditsBodyText.enableWordWrapping = true;
            creditsBodyText.enableAutoSizing = true;
            creditsBodyText.fontSizeMin = 12f;
            creditsBodyText.fontSizeMax = 22f;
            creditsBodyText.lineSpacing = 4f;
            PlaceCentered(creditsBodyText.rectTransform, new Vector2(0f, -8f), new Vector2(900f, 560f));
        }

        CreateDarkDivider(content, "BottomDivider", -298f, 900f);

        closeCreditsButtonText = CreateFramedButton(content, "CloseButton", "CLOSE", SettingsButtonAccent,
            new Color(0.11f, 0.13f, 0.16f, 0.96f), new Vector2(0f, -334f), new Vector2(236f, 54f), 25f, CloseCredits);
    }

    private void BuildSettingsModal(Transform canvasTransform)
    {
        var backdrop = UIBuilder.CreateImage(canvasTransform, "SettingsOverlay",
            new Color(0.02f, 0.04f, 0.03f, 0.78f), raycastTarget: true);
        UIBuilder.Stretch(backdrop.rectTransform);
        settingsOverlay = backdrop.gameObject;
        settingsCanvasGroup = settingsOverlay.AddComponent<CanvasGroup>();

        var cardSize = new Vector2(700f, 620f);
        var cardRoot = UIBuilder.CreateRect(backdrop.transform, "SettingsCard");
        PlaceCentered(cardRoot, Vector2.zero, cardSize);
        settingsCardRect = cardRoot;

        var shadow = UIBuilder.CreateImage(cardRoot, "Shadow", new Color(0f, 0f, 0f, 0.62f), raycastTarget: false);
        PlaceCentered(shadow.rectTransform, new Vector2(8f, -10f), cardSize);

        var border = UIBuilder.CreateImage(cardRoot, "OuterBorder", StartButtonAccent, raycastTarget: true);
        UIBuilder.Stretch(border.rectTransform);

        var inner = UIBuilder.CreateImage(border.transform, "PanelFill", new Color(0.08f, 0.10f, 0.12f, 0.97f), raycastTarget: false);
        UIBuilder.Stretch(inner.rectTransform, 4f);

        var content = inner.transform;

        settingsTitleText = UIBuilder.CreateText(content, "Title", font, 44f, TextAlignmentOptions.Center, bold: true);
        if (settingsTitleText != null)
        {
            settingsTitleText.color = ModalTitleColor;
            PlaceCentered(settingsTitleText.rectTransform, new Vector2(0f, 248f), new Vector2(600f, 54f));
        }

        CreateDarkDivider(content, "TopDivider", 206f, 600f);

        languageCaptionText = UIBuilder.CreateText(content, "LanguageCaption", font, 22f, TextAlignmentOptions.Center, bold: true);
        if (languageCaptionText != null)
        {
            languageCaptionText.color = new Color(0.82f, 0.88f, 0.84f, 1f);
            PlaceCentered(languageCaptionText.rectTransform, new Vector2(0f, 174f), new Vector2(600f, 30f));
        }

        CreateLanguageOptionButton(content, "EnglishOption", "ENGLISH", new Vector2(-136f, 120f),
            () => SelectLanguage(Language.English), out englishBtnBorder, out englishBtnFill, out englishBtnText);

        CreateLanguageOptionButton(content, "TurkishOption", "TÜRKÇE", new Vector2(136f, 120f),
            () => SelectLanguage(Language.Turkish), out turkishBtnBorder, out turkishBtnFill, out turkishBtnText);

        CreateDarkDivider(content, "MidDivider", 70f, 600f);

        // Oyun içi PauseMenu ile aynı ses komple kapatma / açma butonları (Sol: Müzik, Sağ: Tüm Sesler)
        CreateSettingsAudioToggle(content, "MusicToggle", new Vector2(-125f, 2f),
            out musicToggleBorder, out musicToggleIcon, out musicToggleLabel, OnToggleMusicClicked);
        CreateSettingsAudioToggle(content, "SoundToggle", new Vector2(125f, 2f),
            out soundToggleBorder, out soundToggleIcon, out soundToggleLabel, OnToggleSoundClicked);

        musicVolumeSlider = CreateSettingsVolumeRow(content, "MusicVolume", -88f, GameAudio.MusicVolume,
            GameAudio.SetMusicVolume, out musicCaptionText, out musicVolumeValueText);
        sfxVolumeSlider = CreateSettingsVolumeRow(content, "SfxVolume", -138f, GameAudio.SfxVolume,
            GameAudio.SetSfxVolume, out sfxCaptionText, out sfxVolumeValueText);

        CreateDarkDivider(content, "BottomDivider", -176f, 600f);

        closeSettingsButtonText = CreateFramedButton(content, "CloseButton", "CLOSE", SettingsButtonAccent,
            new Color(0.11f, 0.13f, 0.16f, 0.96f), new Vector2(0f, -228f), new Vector2(236f, 54f), 25f, CloseSettings);
    }

    private void CreateSettingsAudioToggle(Transform parent, string name, Vector2 position,
        out UnityEngine.UI.Image borderOut, out UnityEngine.UI.Image iconOut, out TextMeshProUGUI labelOut, Action onClick)
    {
        var container = UIBuilder.CreateRect(parent, name);
        PlaceCentered(container, position, new Vector2(210f, 118f));

        var border = UIBuilder.CreateImage(container, "ButtonBorder", ToggleOnBorder, raycastTarget: true);
        PlaceCentered(border.rectTransform, new Vector2(0f, 16f), new Vector2(72f, 72f));

        var button = border.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = border;
        var colors = button.colors;
        colors.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.70f, 0.70f, 0.70f, 1f);
        colors.selectedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(() => onClick?.Invoke());

        var inner = UIBuilder.CreateImage(border.transform, "Background", new Color(0.08f, 0.08f, 0.11f, 0.94f), raycastTarget: false);
        UIBuilder.Stretch(inner.rectTransform, 3f);

        var icon = UIBuilder.CreateImage(inner.transform, "Icon", Color.white, raycastTarget: false);
        UIBuilder.Stretch(icon.rectTransform, 10f);
        icon.preserveAspect = true;

        var label = UIBuilder.CreateText(container, "Caption", font, 19f, TextAlignmentOptions.Center, bold: false);
        if (label != null)
            PlaceCentered(label.rectTransform, new Vector2(0f, -40f), new Vector2(210f, 26f));

        borderOut = border;
        iconOut = icon;
        labelOut = label;
    }

    private void CreateLanguageOptionButton(Transform parent, string name, string initialLabel, Vector2 pos,
        Action onClick, out UnityEngine.UI.Image borderOut, out UnityEngine.UI.Image fillOut, out TextMeshProUGUI textOut)
    {
        var border = UIBuilder.CreateImage(parent, name, InactiveLangBorder, raycastTarget: true);
        PlaceCentered(border.rectTransform, pos, new Vector2(244f, 58f));

        var button = border.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = border;
        var colors = button.colors;
        colors.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
        colors.selectedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(() => onClick?.Invoke());

        var fill = UIBuilder.CreateImage(border.transform, "Background", InactiveLangFill, raycastTarget: false);
        UIBuilder.Stretch(fill.rectTransform, 3f);

        var text = UIBuilder.CreateText(fill.transform, "Label", font, 24f, TextAlignmentOptions.Center, bold: true);
        if (text != null)
        {
            text.text = initialLabel;
            text.characterSpacing = 1.5f;
            UIBuilder.Stretch(text.rectTransform);
        }

        borderOut = border;
        fillOut = fill;
        textOut = text;
    }

    private UnityEngine.UI.Slider CreateSettingsVolumeRow(Transform parent, string name, float y, float initialValue,
        Action<float> onChanged, out TextMeshProUGUI captionOut, out TextMeshProUGUI valueOut)
    {
        captionOut = UIBuilder.CreateText(parent, name + "Caption", font, 20f, TextAlignmentOptions.MidlineRight, bold: true);
        if (captionOut != null)
        {
            captionOut.color = new Color(0.86f, 0.88f, 0.90f, 1f);
            PlaceCentered(captionOut.rectTransform, new Vector2(-195f, y), new Vector2(140f, 30f));
        }

        var slider = UIBuilder.CreateSlider(parent, name, SliderAccent, initialValue, v =>
        {
            onChanged(v);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        });
        PlaceCentered((RectTransform)slider.transform, new Vector2(20f, y), new Vector2(260f, 28f));

        valueOut = UIBuilder.CreateText(parent, name + "Value", font, 20f, TextAlignmentOptions.MidlineLeft, bold: false);
        if (valueOut != null)
            PlaceCentered(valueOut.rectTransform, new Vector2(198f, y), new Vector2(76f, 30f));

        return slider;
    }

    private void BuildMissionLetter(Transform canvasTransform)
    {
        var backdrop = UIBuilder.CreateImage(canvasTransform, "MissionLetterOverlay",
            new Color(0.02f, 0.04f, 0.03f, 0.78f), raycastTarget: true);
        UIBuilder.Stretch(backdrop.rectTransform);
        letterOverlay = backdrop.gameObject;
        letterCanvasGroup = letterOverlay.AddComponent<CanvasGroup>();

        var cardSize = new Vector2(920f, 680f);
        var cardRoot = UIBuilder.CreateRect(backdrop.transform, "LetterCard");
        PlaceCentered(cardRoot, new Vector2(0f, 8f), cardSize);
        letterCardRect = cardRoot;

        var shadow = UIBuilder.CreateImage(cardRoot, "Shadow", new Color(0f, 0f, 0f, 0.60f), raycastTarget: false);
        PlaceCentered(shadow.rectTransform, new Vector2(10f, -12f), cardSize);

        var outerTimber = UIBuilder.CreateImage(cardRoot, "OuterFrame", TimberBorder, raycastTarget: true);
        UIBuilder.Stretch(outerTimber.rectTransform);

        var brassBorder = UIBuilder.CreateImage(outerTimber.transform, "BrassTrim", BrassTrim, raycastTarget: false);
        UIBuilder.Stretch(brassBorder.rectTransform, 5f);

        var paper = UIBuilder.CreateImage(brassBorder.transform, "Parchment", ParchmentFill, raycastTarget: false);
        UIBuilder.Stretch(paper.rectTransform, 4f);

        var innerRuleOuter = UIBuilder.CreateImage(paper.transform, "InnerRule", ParchmentRule, raycastTarget: false);
        UIBuilder.Stretch(innerRuleOuter.rectTransform, 14f);
        var innerPaper = UIBuilder.CreateImage(innerRuleOuter.transform, "InnerPaper", ParchmentFill, raycastTarget: false);
        UIBuilder.Stretch(innerPaper.rectTransform, 2f);

        var content = innerPaper.transform;

        var seal = UIBuilder.CreateImage(content, "EcoSeal", Color.white, raycastTarget: false);
        seal.sprite = generatedSealSprite;
        seal.preserveAspect = true;
        PlaceCentered(seal.rectTransform, new Vector2(0f, 276f), new Vector2(46f, 46f));

        stampTextComp = CreateInkText(content, "Stamp", letterStamp, 19f, StampColor, TextAlignmentOptions.Center, bold: true);
        if (stampTextComp != null)
        {
            stampTextComp.characterSpacing = 6f;
            PlaceCentered(stampTextComp.rectTransform, new Vector2(0f, 234f), new Vector2(760f, 28f));
        }

        headingTextComp = CreateInkText(content, "Heading", letterHeading, 40f, HeadingInk, TextAlignmentOptions.Center, bold: true);
        if (headingTextComp != null)
            PlaceCentered(headingTextComp.rectTransform, new Vector2(0f, 190f), new Vector2(760f, 50f));

        CreatePaperDivider(content, "TopDivider", 154f, 760f);

        bodyTextComp = CreateInkText(content, "Body", letterBody, 24f, BodyInk, TextAlignmentOptions.TopLeft, bold: false);
        if (bodyTextComp != null)
        {
            bodyTextComp.enableWordWrapping = true;
            bodyTextComp.lineSpacing = 8f;
            PlaceCentered(bodyTextComp.rectTransform, new Vector2(0f, -12f), new Vector2(760f, 304f));
        }

        signatureTextComp = CreateInkText(content, "Signature", letterSignature, 22f, SignatureInk,
            TextAlignmentOptions.MidlineRight, bold: true);
        if (signatureTextComp != null)
        {
            signatureTextComp.fontStyle = FontStyles.Bold | FontStyles.Italic;
            PlaceCentered(signatureTextComp.rectTransform, new Vector2(0f, -184f), new Vector2(760f, 34f));
        }

        CreatePaperDivider(content, "BottomDivider", -212f, 760f);

        var continueShadow = UIBuilder.CreateImage(content, "ContinueShadow", new Color(0f, 0f, 0f, 0.28f), raycastTarget: false);
        PlaceCentered(continueShadow.rectTransform, new Vector2(0f, -267f), new Vector2(286f, 62f));

        continueBtnText = CreateFramedButton(content, "ContinueButton", continueButtonLabel, ContinueAccent,
            new Color(0.09f, 0.14f, 0.10f, 0.98f), new Vector2(0f, -262f), new Vector2(286f, 62f), 28f, StartGame);
    }

    private TextMeshProUGUI CreateInkText(Transform parent, string name, string value, float size,
        Color inkColor, TextAlignmentOptions alignment, bool bold)
    {
        var text = UIBuilder.CreateText(parent, name, font, size, alignment, bold);
        if (text == null) return null;

        text.outlineWidth = 0f;
        text.color = inkColor;
        text.text = value;
        return text;
    }

    private static void CreatePaperDivider(Transform parent, string name, float y, float width)
    {
        var line = UIBuilder.CreateImage(parent, name, ParchmentRule, raycastTarget: false);
        PlaceCentered(line.rectTransform, new Vector2(0f, y), new Vector2(width, 2f));
    }

    private static void CreateDarkDivider(Transform parent, string name, float y, float width)
    {
        var line = UIBuilder.CreateImage(parent, name, new Color(1f, 1f, 1f, 0.16f), raycastTarget: false);
        PlaceCentered(line.rectTransform, new Vector2(0f, y), new Vector2(width, 2f));
    }

    private TextMeshProUGUI CreateFramedButton(Transform parent, string name, string label, Color borderColor,
        Color fillColor, Vector2 position, Vector2 size, float fontSize, Action onClick)
    {
        var border = UIBuilder.CreateImage(parent, name, borderColor, raycastTarget: true);
        PlaceCentered(border.rectTransform, position, size);

        var button = border.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = border;
        var colors = button.colors;
        colors.normalColor = new Color(0.90f, 0.90f, 0.90f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.74f, 0.74f, 0.74f, 1f);
        colors.selectedColor = new Color(0.90f, 0.90f, 0.90f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(() => onClick?.Invoke());

        var inner = UIBuilder.CreateImage(border.transform, "Background", fillColor, raycastTarget: false);
        UIBuilder.Stretch(inner.rectTransform, 4f);

        var innerRim = UIBuilder.CreateImage(inner.transform, "InnerRim",
            new Color(borderColor.r, borderColor.g, borderColor.b, 0.18f), raycastTarget: false);
        UIBuilder.Stretch(innerRim.rectTransform, 2f);
        var core = UIBuilder.CreateImage(innerRim.transform, "Core", fillColor, raycastTarget: false);
        UIBuilder.Stretch(core.rectTransform, 1f);

        var text = UIBuilder.CreateText(core.transform, "Label", font, fontSize, TextAlignmentOptions.Center, bold: true);
        if (text != null)
        {
            text.text = label;
            text.color = Color.white;
            text.characterSpacing = 2.5f;
            UIBuilder.Stretch(text.rectTransform);
        }
        return text;
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

    private static void EnsureGeneratedAssets()
    {
        if (generatedSealSprite == null)
            generatedSealSprite = BuildLeafSealSprite();
        if (generatedGearSprite == null)
            generatedGearSprite = BuildGearIconSprite();
        if (generatedClickClip == null)
            generatedClickClip = BuildUiToneClip("MenuClick", 520f, 760f, 0.075f);
        if (generatedPaperClip == null)
            generatedPaperClip = BuildUiToneClip("LetterOpen", 390f, 585f, 0.11f);
    }

    // 20x20 piksel sanatı dişli çark (Ayarlar) ikonu
    private static Sprite BuildGearIconSprite()
    {
        string[] rows =
        {
            "....................",
            "........WWWW........",
            "........WWWW........",
            "...WW..WWWWWW..WW...",
            "..WWWWWWWWWWWWWWWW..",
            "...WWWWWWWWWWWWWW...",
            "....WWWW....WWWW....",
            "....WWW......WWW....",
            ".WWWWWW......WWWWWW.",
            ".WWWWWW......WWWWWW.",
            ".WWWWWW......WWWWWW.",
            ".WWWWWW......WWWWWW.",
            "....WWW......WWW....",
            "....WWWW....WWWW....",
            "...WWWWWWWWWWWWWW...",
            "..WWWWWWWWWWWWWWWW..",
            "...WW..WWWWWW..WW...",
            "........WWWW........",
            "........WWWW........",
            "...................."
        };

        const int size = 20;
        Color fill = new Color(0.92f, 0.96f, 0.93f, 1f);
        Color outline = new Color(0.05f, 0.08f, 0.06f, 0.95f);

        var pixels = new Color[size * size];
        for (int r = 0; r < size; r++)
        {
            int texY = size - 1 - r;
            string line = rows[r];
            for (int c = 0; c < size; c++)
            {
                if (line[c] == 'W')
                {
                    pixels[texY * size + c] = fill;
                }
                else
                {
                    bool neighbor = false;
                    for (int dr = -1; dr <= 1 && !neighbor; dr++)
                    {
                        for (int dc = -1; dc <= 1 && !neighbor; dc++)
                        {
                            int nr = r + dr;
                            int nc = c + dc;
                            if (nr >= 0 && nr < size && nc >= 0 && nc < size && rows[nr][nc] == 'W')
                                neighbor = true;
                        }
                    }
                    pixels[texY * size + c] = neighbor ? outline : Color.clear;
                }
            }
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "PixelGearIcon",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // 20x20 piksel sanatı yeşil yapraklı mühür rozeti
    private static Sprite BuildLeafSealSprite()
    {
        string[] rows =
        {
            "......OOOOOOOO......",
            "....OOSSSSSSSSOO....",
            "...OSSGGGGGGGGSSO...",
            "..OSSGGGGGGGGGGSSO..",
            ".OSSGGGGLLLLGGGGSSO.",
            ".OSGGGGLLLLLLGGGGSO.",
            "OSSGGGLLLLLLLLGGGSSO",
            "OSGGGGLLLWWLLLLGGGSO",
            "OSGGGLLLLWWLLLLGGGSO",
            "OSGGGLLLWWLLLLLGGGSO",
            "OSGGGLLWWLLLLLLGGGSO",
            "OSGGGLLWWLLLLGGGGGSO",
            "OSGGGGWWLLLLGGGGGGSO",
            "OSSGGWWGGGGGGGGGGSSO",
            ".OSGGWGGGGGGGGGGGSO.",
            ".OSSGGGGGGGGGGGGSSO.",
            "..OSSGGGGGGGGGGSSO..",
            "...OSSGGGGGGGGSSO...",
            "....OOSSSSSSSSOO....",
            "......OOOOOOOO......"
        };

        const int size = 20;
        Color outline = new Color(0.16f, 0.24f, 0.14f, 1f);
        Color rim = new Color(0.32f, 0.62f, 0.34f, 1f);
        Color baseGreen = new Color(0.19f, 0.44f, 0.22f, 1f);
        Color leafBright = new Color(0.58f, 0.92f, 0.48f, 1f);
        Color vein = new Color(0.92f, 0.98f, 0.86f, 1f);

        var pixels = new Color[size * size];
        for (int r = 0; r < size; r++)
        {
            int texY = size - 1 - r;
            string line = rows[r];
            for (int c = 0; c < size; c++)
            {
                char ch = line[c];
                pixels[texY * size + c] = ch switch
                {
                    'O' => outline,
                    'S' => rim,
                    'G' => baseGreen,
                    'L' => leafBright,
                    'W' => vein,
                    _ => Color.clear
                };
            }
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "PixelEcoSeal",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static AudioClip BuildUiToneClip(string clipName, float freqA, float freqB, float duration)
    {
        const int sampleRate = 44100;
        int count = Mathf.CeilToInt(sampleRate * duration);
        var samples = new float[count];
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float freq = t < 0.45f ? freqA : freqB;
            phase += 2f * Mathf.PI * freq / sampleRate;
            float env = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.65f)) * (1f - t * 0.55f);
            samples[i] = Mathf.Sin(phase) * env * 0.35f;
        }

        var clip = AudioClip.Create(clipName, count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
