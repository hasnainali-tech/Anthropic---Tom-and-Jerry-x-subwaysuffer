using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Whole game UI, built at runtime with uGUI (one Screen Space Overlay canvas).
// Reference resolution 1080x1920 portrait, scales to every Android aspect ratio and
// respects the notch safe area. Screens: splash/loading, main menu, HUD, countdown,
// pause, settings, game over. All animation uses unscaled time (works while paused).
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    static bool splashDone;          // splash only on cold start
    static bool fadeInOnLoad;        // fade from black after a restart / home

    GameManager gm;
    Font titleFont, uiFont;
    Canvas canvas;
    RectTransform safe;
    Rect lastSafe;

    CanvasGroup menuG, hudG, pauseG, settingsG, overG, splashG;
    Image fade, danger, flash;
    Text cheeseText, hudBest, countdownText, toastText, menuBest, overScore, overBest, warnText;
    RectTransform cheesePill, logo, playBtn, warnPill, overPanel, pausePanel, settingsPanel, newBestRibbon, overButtons;
    Image soundIcon;
    Slider musicSlider, sfxSlider;
    readonly List<Text> popups = new List<Text>();
    GameManager.State shown = (GameManager.State)(-1);
    bool pausedShown, settingsOpen;
    float lastBackPress = -10f;
    int shownScore = -1;

    static readonly Color Brown = new Color(0.26f, 0.12f, 0.04f);
    static readonly Color Yellow = new Color(1f, 0.78f, 0.13f);
    static readonly Color Blue = new Color(0.22f, 0.55f, 0.95f);
    static readonly Color Green = new Color(0.3f, 0.78f, 0.35f);
    static readonly Color Red = new Color(0.93f, 0.3f, 0.25f);
    static readonly Color PanelC = new Color(1f, 0.97f, 0.9f);

    public static GameUI Create(GameManager gm)
    {
        if (Instance) return Instance;
        var go = new GameObject("GameUI");
        var ui = go.AddComponent<GameUI>();
        ui.gm = gm;
        ui.Build();
        return ui;
    }

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    // ================================================================ build
    void Build()
    {
        var art = GameArt.Get();
        titleFont = art && art.titleFont ? art.titleFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiFont = art && art.uiFont ? art.uiFont : titleFont;

        EnsureEventSystem();

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = Screen.width < Screen.height ? 0.35f : 1f;
        gameObject.AddComponent<GraphicRaycaster>();

        // full-screen layers (outside the safe area)
        danger = UIArt.Img(transform, "Danger", UIArt.Vignette, new Color(0.9f, 0.05f, 0.02f, 0f));
        Stretch(danger.rectTransform);
        flash = UIArt.Img(transform, "Flash", null, new Color(1f, 0.2f, 0.1f, 0f));
        Stretch(flash.rectTransform);

        safe = Rt(transform, "Safe");
        Stretch(safe);
        ApplySafeArea();

        BuildMenu();
        BuildHud();
        BuildPause();
        BuildGameOver();
        BuildSettings();

        fade = UIArt.Img(transform, "Fade", null, new Color(0.08f, 0.04f, 0.02f, 0f));
        Stretch(fade.rectTransform);

        if (!splashDone) BuildSplash();
        if (fadeInOnLoad) { fadeInOnLoad = false; fade.color = new Color(0.08f, 0.04f, 0.02f, 1f); StartCoroutine(Tween(0.45f, k => SetAlpha(fade, 1f - k))); }

        SetGroup(menuG, false, true); SetGroup(hudG, false, true); SetGroup(pauseG, false, true);
        SetGroup(overG, false, true); SetGroup(settingsG, false, true);
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        var module = es.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    void ApplySafeArea()
    {
        Rect s = Screen.safeArea;
        if (s == lastSafe || Screen.width == 0) return;
        lastSafe = s;
        safe.anchorMin = new Vector2(s.xMin / Screen.width, s.yMin / Screen.height);
        safe.anchorMax = new Vector2(s.xMax / Screen.width, s.yMax / Screen.height);
        safe.offsetMin = safe.offsetMax = Vector2.zero;
    }

    // ---------------------------------------------------------------- splash
    void BuildSplash()
    {
        splashG = Group(transform, "Splash");
        var bg = UIArt.Img(splashG.transform, "Bg", null, new Color(0.98f, 0.56f, 0.16f));
        Stretch(bg.rectTransform);
        var glow = UIArt.Img(splashG.transform, "Glow", UIArt.Soft, new Color(1f, 0.85f, 0.4f, 0.8f));
        Place(glow.rectTransform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(1400f, 1400f));
        var lg = BuildLogo(splashG.transform);
        Place(lg, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(1000f, 600f));

        var barBg = UIArt.Img(splashG.transform, "BarBg", UIArt.Pill, new Color(0.35f, 0.15f, 0.03f, 0.55f), true);
        Place(barBg.rectTransform, new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(640f, 56f));
        var bar = UIArt.Img(barBg.transform, "Bar", UIArt.Pill, Yellow, true);
        bar.rectTransform.anchorMin = new Vector2(0f, 0f); bar.rectTransform.anchorMax = new Vector2(0f, 1f);
        bar.rectTransform.pivot = new Vector2(0f, 0.5f);
        bar.rectTransform.anchoredPosition = Vector2.zero;
        var loading = Txt(splashG.transform, "LOADING...", uiFont, 44, Color.white, TextAnchor.MiddleCenter, 3f);
        Place(loading.rectTransform, new Vector2(0.5f, 0.25f), new Vector2(0f, -80f), new Vector2(800f, 80f));

        splashG.alpha = 1f;
        splashG.blocksRaycasts = true;
        StartCoroutine(SplashRoutine(bar.rectTransform, lg));
    }

    IEnumerator SplashRoutine(RectTransform bar, RectTransform lg)
    {
        float t = 0f, dur = 1.6f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            bar.sizeDelta = new Vector2(Mathf.Lerp(56f, 640f, EaseOutCubic(k)), 0f);
            lg.localScale = Vector3.one * (0.85f + 0.15f * EaseOutBack(Mathf.Clamp01(t / 0.7f)));
            lg.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3f) * 2f);
            yield return null;
        }
        splashDone = true;
        yield return Tween(0.4f, k => splashG.alpha = 1f - k);
        Destroy(splashG.gameObject);
        splashG = null;
    }

    // ---------------------------------------------------------------- logo
    RectTransform BuildLogo(Transform parent)
    {
        var root = Rt(parent, "Logo");
        var cheese = UIArt.Img(root, "CheeseIcon", UIArt.Cheese, Color.white);
        Place(cheese.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(330f, 165f), new Vector2(190f, 190f));
        cheese.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);

        var the = Txt(root, "THE GREAT", uiFont, 74, Color.white, TextAnchor.MiddleCenter, 5f);
        Place(the.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-40f, 175f), new Vector2(900f, 110f));
        var cheeseT = Txt(root, "CHEESE", titleFont, 210, Color.white, TextAnchor.MiddleCenter, 9f, new Color(1f, 0.93f, 0.4f), new Color(1f, 0.62f, 0.08f));
        Place(cheeseT.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1100f, 240f));
        var escape = Txt(root, "ESCAPE!", titleFont, 170, Color.white, TextAnchor.MiddleCenter, 9f, new Color(1f, 0.55f, 0.2f), new Color(0.88f, 0.2f, 0.12f));
        Place(escape.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(20f, -150f), new Vector2(1100f, 210f));
        return root;
    }

    // ---------------------------------------------------------------- menu
    void BuildMenu()
    {
        menuG = Group(safe, "Menu");
        var topShade = UIArt.Img(menuG.transform, "TopShade", UIArt.Gloss, new Color(0.15f, 0.06f, 0f, 0.55f));
        topShade.rectTransform.anchorMin = new Vector2(0f, 0.62f); topShade.rectTransform.anchorMax = Vector2.one;
        topShade.rectTransform.offsetMin = topShade.rectTransform.offsetMax = Vector2.zero;
        var botShade = UIArt.Img(menuG.transform, "BottomShade", UIArt.Gloss, new Color(0.15f, 0.06f, 0f, 0.6f));
        botShade.rectTransform.anchorMin = Vector2.zero; botShade.rectTransform.anchorMax = new Vector2(1f, 0.42f);
        botShade.rectTransform.offsetMin = botShade.rectTransform.offsetMax = Vector2.zero;
        botShade.rectTransform.localScale = new Vector3(1f, -1f, 1f);

        logo = BuildLogo(menuG.transform);
        Place(logo, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(1000f, 560f));

        // best score badge
        var badge = UIArt.Img(menuG.transform, "Best", UIArt.Pill, new Color(0.12f, 0.05f, 0.01f, 0.6f), true);
        Place(badge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 760f), new Vector2(520f, 100f));
        var star = UIArt.Img(badge.transform, "Star", UIArt.Star, Yellow);
        Place(star.rectTransform, new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(70f, 70f));
        menuBest = Txt(badge.transform, "BEST: 0", uiFont, 54, Color.white, TextAnchor.MiddleCenter, 3f);
        Place(menuBest.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(30f, 0f), new Vector2(420f, 90f));

        playBtn = Button3D(menuG.transform, "PLAY", UIArt.Play, Yellow, new Vector2(700f, 210f), 92, () => { if (gm) gm.StartRun(); });
        Place(playBtn, new Vector2(0.5f, 0f), new Vector2(0f, 525f), new Vector2(700f, 210f));

        var settingsBtn = RoundButton(menuG.transform, UIArt.Gear, Blue, 150f, OpenSettings);
        Place(settingsBtn, new Vector2(0.5f, 0f), new Vector2(-110f, 300f), new Vector2(150f, 150f));
        var soundBtn = RoundButton(menuG.transform, UIArt.SpeakerOn, Green, 150f, ToggleSound);
        Place(soundBtn, new Vector2(0.5f, 0f), new Vector2(110f, 300f), new Vector2(150f, 150f));
        soundIcon = soundBtn.Find("Icon").GetComponent<Image>();

        var hint = Txt(menuG.transform, "SWIPE  ← →  TO DODGE   •   ↑ JUMP   •   ↓ SLIDE", uiFont, 34, new Color(1f, 0.95f, 0.85f), TextAnchor.MiddleCenter, 2.5f);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(1060f, 60f));
    }

    // ---------------------------------------------------------------- HUD
    void BuildHud()
    {
        hudG = Group(safe, "HUD");
        var pill = UIArt.Img(hudG.transform, "CheesePill", UIArt.Pill, new Color(0.12f, 0.05f, 0.01f, 0.55f), true);
        cheesePill = pill.rectTransform;
        Place(cheesePill, new Vector2(0f, 1f), new Vector2(210f, -95f), new Vector2(340f, 120f));
        var icon = UIArt.Img(pill.transform, "Icon", UIArt.Cheese, Color.white);
        Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(68f, 4f), new Vector2(120f, 120f));
        cheeseText = Txt(pill.transform, "0", titleFont, 82, Color.white, TextAnchor.MiddleLeft, 4f, new Color(1f, 0.95f, 0.6f), new Color(1f, 0.75f, 0.2f));
        Place(cheeseText.rectTransform, new Vector2(0f, 0.5f), new Vector2(240f, 2f), new Vector2(200f, 110f));
        hudBest = Txt(hudG.transform, "BEST 0", uiFont, 40, Color.white, TextAnchor.MiddleLeft, 3f);
        Place(hudBest.rectTransform, new Vector2(0f, 1f), new Vector2(230f, -190f), new Vector2(360f, 60f));

        var pause = RoundButton(hudG.transform, UIArt.Pause, Yellow, 130f, () => { if (gm) gm.SetPaused(true); });
        Place(pause, new Vector2(1f, 1f), new Vector2(-100f, -95f), new Vector2(130f, 130f));

        var warn = UIArt.Img(hudG.transform, "Warn", UIArt.Pill, Red, true);
        warnPill = warn.rectTransform;
        Place(warnPill, new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(620f, 100f));
        warnText = Txt(warn.transform, "THE CAT IS CLOSE!", uiFont, 54, Color.white, TextAnchor.MiddleCenter, 3f);
        Stretch(warnText.rectTransform);
        warnPill.gameObject.SetActive(false);

        countdownText = Txt(safe, "", titleFont, 260, Color.white, TextAnchor.MiddleCenter, 10f, new Color(1f, 0.95f, 0.5f), new Color(1f, 0.6f, 0.1f));
        Place(countdownText.rectTransform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(1000f, 400f));
        toastText = Txt(safe, "", titleFont, 120, Color.white, TextAnchor.MiddleCenter, 8f, new Color(0.7f, 1f, 0.5f), new Color(0.2f, 0.75f, 0.3f));
        Place(toastText.rectTransform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1000f, 200f));

        // tutorial hint pill (first runs only)
        var hint = UIArt.Img(safe, "Hint", UIArt.Pill, new Color(0.1f, 0.05f, 0.02f, 0.7f), true);
        hintRT = hint.rectTransform;
        Place(hintRT, new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(860f, 130f));
        hintText = Txt(hint.transform, "", uiFont, 58, Color.white, TextAnchor.MiddleCenter, 3f);
        Stretch(hintText.rectTransform);
        hintRT.gameObject.SetActive(false);

        for (int i = 0; i < 6; i++)
        {
            var p = Txt(safe, "+1", titleFont, 72, Color.white, TextAnchor.MiddleCenter, 4f, new Color(1f, 0.95f, 0.5f), new Color(1f, 0.7f, 0.15f));
            p.rectTransform.sizeDelta = new Vector2(200f, 100f);
            p.gameObject.SetActive(false);
            popups.Add(p);
        }
    }

    // ---------------------------------------------------------------- pause
    void BuildPause()
    {
        pauseG = Group(transform, "Pause");
        Dim(pauseG.transform);
        var panel = Panel(pauseG.transform, new Vector2(820f, 1000f));
        pausePanel = panel;
        var title = Txt(panel, "PAUSED", titleFont, 130, Color.white, TextAnchor.MiddleCenter, 8f, new Color(1f, 0.85f, 0.3f), new Color(1f, 0.55f, 0.1f));
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(800f, 180f));
        Place(Button3D(panel, "RESUME", UIArt.Play, Green, new Vector2(620f, 170f), 70, () => gm.SetPaused(false)), new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(620f, 170f));
        Place(Button3D(panel, "RESTART", UIArt.Restart, Yellow, new Vector2(620f, 150f), 62, () => FadeThen(gm.Restart)), new Vector2(0.5f, 1f), new Vector2(0f, -540f), new Vector2(620f, 150f));
        Place(Button3D(panel, "HOME", UIArt.Home, Blue, new Vector2(620f, 150f), 62, () => FadeThen(gm.BackToMenu)), new Vector2(0.5f, 1f), new Vector2(0f, -720f), new Vector2(620f, 150f));
        Place(RoundButton(panel, UIArt.Gear, new Color(0.55f, 0.45f, 0.4f), 120f, OpenSettings), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(120f, 120f));
    }

    // ---------------------------------------------------------------- game over
    void BuildGameOver()
    {
        overG = Group(transform, "GameOver");
        Dim(overG.transform);
        overPanel = Panel(overG.transform, new Vector2(880f, 1180f));
        var title = Txt(overPanel, "CAUGHT!", titleFont, 170, Color.white, TextAnchor.MiddleCenter, 9f, new Color(1f, 0.6f, 0.3f), new Color(0.9f, 0.2f, 0.12f));
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(860f, 220f));
        var sub = Txt(overPanel, "The cat got you this time!", uiFont, 48, new Color(0.45f, 0.28f, 0.15f), TextAnchor.MiddleCenter, 0f);
        Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -275f), new Vector2(860f, 80f));

        var scoreBg = UIArt.Img(overPanel, "ScoreBg", UIArt.Rounded, new Color(1f, 0.88f, 0.6f), true);
        Place(scoreBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(700f, 260f));
        var icon = UIArt.Img(scoreBg.transform, "Icon", UIArt.Cheese, Color.white);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-170f, 10f), new Vector2(190f, 190f));
        overScore = Txt(scoreBg.transform, "0", titleFont, 170, Color.white, TextAnchor.MiddleCenter, 8f, new Color(1f, 0.92f, 0.45f), new Color(1f, 0.6f, 0.1f));
        Place(overScore.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(90f, 5f), new Vector2(380f, 220f));
        overBest = Txt(overPanel, "BEST: 0", uiFont, 58, new Color(0.45f, 0.28f, 0.15f), TextAnchor.MiddleCenter, 0f);
        Place(overBest.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -655f), new Vector2(800f, 80f));

        var ribbon = UIArt.Img(overPanel, "NewBest", UIArt.Pill, Green, true);
        newBestRibbon = ribbon.rectTransform;
        Place(newBestRibbon, new Vector2(0.5f, 1f), new Vector2(215f, -360f), new Vector2(400f, 86f));
        newBestRibbon.localRotation = Quaternion.Euler(0f, 0f, 8f);
        var rt = Txt(ribbon.transform, "NEW BEST!", titleFont, 64, Color.white, TextAnchor.MiddleCenter, 4f);
        Stretch(rt.rectTransform);
        var st1 = UIArt.Img(ribbon.transform, "S1", UIArt.Star, Yellow);
        Place(st1.rectTransform, new Vector2(0f, 0.5f), new Vector2(-10f, 20f), new Vector2(80f, 80f));
        var st2 = UIArt.Img(ribbon.transform, "S2", UIArt.Star, Yellow);
        Place(st2.rectTransform, new Vector2(1f, 0.5f), new Vector2(10f, -18f), new Vector2(64f, 64f));

        overButtons = Rt(overPanel, "Buttons");
        Place(overButtons, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(800f, 360f));
        Place(Button3D(overButtons, "PLAY AGAIN", UIArt.Restart, Yellow, new Vector2(700f, 180f), 70, () => FadeThen(gm.Restart)), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(700f, 180f));
        Place(Button3D(overButtons, "HOME", UIArt.Home, Blue, new Vector2(700f, 140f), 58, () => FadeThen(gm.BackToMenu)), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(700f, 140f));
    }

    // ---------------------------------------------------------------- settings
    void BuildSettings()
    {
        settingsG = Group(transform, "Settings");
        Dim(settingsG.transform);
        settingsPanel = Panel(settingsG.transform, new Vector2(880f, 1180f));
        var title = Txt(settingsPanel, "SETTINGS", titleFont, 120, Color.white, TextAnchor.MiddleCenter, 8f, new Color(0.6f, 0.85f, 1f), new Color(0.2f, 0.5f, 0.95f));
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(860f, 170f));

        musicSlider = SliderRow(settingsPanel, UIArt.Music, "MUSIC", -300f, AudioManager.MusicVolume, AudioManager.SetMusicVolume);
        sfxSlider = SliderRow(settingsPanel, UIArt.SpeakerOn, "SOUND", -470f, AudioManager.SfxVolume, AudioManager.SetSfxVolume);
        SwitchRow(settingsPanel, "VIBRATION", -650f, () => GameSettings.Vibration, v => { GameSettings.Vibration = v; if (v) GameSettings.Vibrate(); });
        SwitchRow(settingsPanel, "HD GRAPHICS", -790f, () => GameSettings.HighGraphics, v => GameSettings.HighGraphics = v);

        Place(Button3D(settingsPanel, "DONE", UIArt.Check, Green, new Vector2(520f, 150f), 64, CloseSettings), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(520f, 150f));
        var close = RoundButton(settingsPanel, UIArt.Close, Red, 100f, CloseSettings);
        Place(close, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(100f, 100f));
    }

    Slider SliderRow(RectTransform panel, Sprite icon, string label, float y, float value, Action<float> onChange)
    {
        var row = Rt(panel, label);
        Place(row, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(760f, 140f));
        var ic = UIArt.Img(row, "Icon", icon, new Color(0.45f, 0.28f, 0.15f));
        Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(45f, 18f), new Vector2(80f, 80f));
        var l = Txt(row, label, uiFont, 50, new Color(0.45f, 0.28f, 0.15f), TextAnchor.MiddleLeft, 0f);
        Place(l.rectTransform, new Vector2(0f, 0.5f), new Vector2(260f, 30f), new Vector2(320f, 70f));

        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        var rt = (RectTransform)go.transform;
        rt.SetParent(row, false);
        Place(rt, new Vector2(0.5f, 0.5f), new Vector2(60f, -35f), new Vector2(620f, 60f));
        var bg = UIArt.Img(rt, "Track", UIArt.Pill, new Color(0.85f, 0.75f, 0.6f), true);
        Stretch(bg.rectTransform);
        var area = Rt(rt, "FillArea"); Stretch(area);
        var fill = UIArt.Img(area, "Fill", UIArt.Pill, Yellow, true);
        Stretch(fill.rectTransform);
        var handleArea = Rt(rt, "HandleArea"); Stretch(handleArea);
        handleArea.offsetMin = new Vector2(30f, 0f); handleArea.offsetMax = new Vector2(-30f, 0f);
        var handle = UIArt.Img(handleArea, "Handle", UIArt.Circle, Color.white);
        handle.rectTransform.sizeDelta = new Vector2(84f, 0f);
        handle.raycastTarget = true;
        var hs = handle.gameObject.AddComponent<Shadow>(); hs.effectColor = new Color(0f, 0f, 0f, 0.35f); hs.effectDistance = new Vector2(0f, -5f);
        var s = go.GetComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.transition = Selectable.Transition.None;
        s.minValue = 0f; s.maxValue = 1f;
        s.SetValueWithoutNotify(value);
        s.onValueChanged.AddListener(v => onChange(v));
        bg.raycastTarget = true;
        return s;
    }

    void SwitchRow(RectTransform panel, string label, float y, Func<bool> get, Action<bool> set)
    {
        var row = Rt(panel, label);
        Place(row, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(760f, 120f));
        var l = Txt(row, label, uiFont, 50, new Color(0.45f, 0.28f, 0.15f), TextAnchor.MiddleLeft, 0f);
        Place(l.rectTransform, new Vector2(0f, 0.5f), new Vector2(230f, 0f), new Vector2(460f, 80f));

        var track = UIArt.Img(row, "Switch", UIArt.Pill, Green, true);
        track.raycastTarget = true;
        Place(track.rectTransform, new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(200f, 100f));
        var knob = UIArt.Img(track.transform, "Knob", UIArt.Circle, Color.white);
        knob.rectTransform.sizeDelta = new Vector2(84f, 84f);
        var ks = knob.gameObject.AddComponent<Shadow>(); ks.effectColor = new Color(0f, 0f, 0f, 0.3f); ks.effectDistance = new Vector2(0f, -4f);
        var btn = track.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        void Refresh()
        {
            bool on = get();
            track.color = on ? Green : new Color(0.7f, 0.65f, 0.6f);
            knob.rectTransform.anchoredPosition = new Vector2(on ? 50f : -50f, 0f);
        }
        btn.onClick.AddListener(() => { set(!get()); AudioManager.PlayClick(); Refresh(); });
        track.gameObject.AddComponent<PressScale>();
        Refresh();
    }

    // ================================================================ runtime
    void Update()
    {
        if (!gm) gm = GameManager.Instance;
        if (!gm) return;
        ApplySafeArea();

        // state changes
        if (gm.Current != shown)
        {
            var prev = shown;
            shown = gm.Current;
            OnState(prev, shown);
        }
        if (gm.IsPaused != pausedShown)
        {
            pausedShown = gm.IsPaused;
            SetGroup(pauseG, pausedShown);
            if (pausedShown) StartCoroutine(PopIn(pausePanel));
        }

        // HUD values
        if (gm.Score != shownScore)
        {
            shownScore = gm.Score;
            cheeseText.text = shownScore.ToString();
            hudBest.text = "BEST " + gm.HighScore;
        }
        menuBest.text = "BEST: " + gm.HighScore;

        float dz = gm.IsPlaying ? gm.Danger01 : 0f;
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 9f);
        SetAlpha(danger, Mathf.Lerp(danger.color.a, Mathf.Clamp01((dz - 0.25f) * 1.1f) * 0.75f * pulse, 0.2f));
        bool warn = gm.IsPlaying && dz > 0.45f;
        if (warnPill.gameObject.activeSelf != warn) warnPill.gameObject.SetActive(warn);
        if (warn) warnPill.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 10f));
        SetAlpha(flash, Mathf.MoveTowards(flash.color.a, 0f, Time.unscaledDeltaTime * 2.5f));

        // idle menu motion
        if (shown == GameManager.State.Menu && logo)
        {
            float t = Time.unscaledTime;
            logo.localRotation = Quaternion.Euler(0f, 0f, -3f + Mathf.Sin(t * 1.3f) * 1.5f);
            logo.anchoredPosition = new Vector2(0f, -330f + Mathf.Sin(t * 1.7f) * 10f);
            if (playBtn && !introPlaying && !playBtn.GetComponent<PressScale>().Pressed) playBtn.localScale = Vector3.one * (1f + Mathf.Sin(t * 4f) * 0.03f);
        }

        HandleBack();
    }

    void OnState(GameManager.State prev, GameManager.State now)
    {
        bool menu = now == GameManager.State.Menu;
        bool hud = now == GameManager.State.Playing || now == GameManager.State.Countdown;
        SetGroup(menuG, menu);
        SetGroup(hudG, hud);
        if (menu) StartCoroutine(MenuIntro());
        if (now == GameManager.State.GameOver) StartCoroutine(ShowGameOver());
        else SetGroup(overG, false);
        RefreshSoundIcon();
    }

    bool introPlaying;

    IEnumerator MenuIntro()
    {
        introPlaying = true;
        logo.localScale = Vector3.one * 0.4f;
        playBtn.localScale = Vector3.zero;
        while (splashG) yield return null;                    // wait for the splash to finish
        yield return Tween(0.6f, k => logo.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, EaseOutBack(k)));
        yield return Tween(0.35f, k => playBtn.localScale = Vector3.one * EaseOutBack(k));
        introPlaying = false;
    }

    IEnumerator ShowGameOver()
    {
        yield return new WaitForSecondsRealtime(0.9f);
        bool newBest = gm.IsNewBest;
        newBestRibbon.gameObject.SetActive(false);
        overBest.text = "BEST: " + gm.HighScore;
        overScore.text = "0";
        SetGroup(overG, true);
        overPanel.anchoredPosition = new Vector2(0f, -1600f);
        yield return Tween(0.5f, k => overPanel.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(-1600f, 0f, EaseOutBack(k))));
        // count up
        int target = gm.Score;
        float dur = Mathf.Clamp(target * 0.05f, 0.3f, 1.2f);
        yield return Tween(dur, k => overScore.text = Mathf.RoundToInt(target * EaseOutCubic(k)).ToString());
        overScore.text = target.ToString();
        StartCoroutine(Tween(0.25f, k => overScore.rectTransform.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(k * Mathf.PI))));
        if (newBest)
        {
            newBestRibbon.gameObject.SetActive(true);
            newBestRibbon.localScale = Vector3.zero;
            AudioManager.PlayFanfare();
            if (gm.player) GameFX.Confetti(gm.player.transform.position + Vector3.up * 1.5f);
            yield return Tween(0.4f, k => newBestRibbon.localScale = Vector3.one * EaseOutBack(k));
        }
    }

    void HandleBack()
    {
        var k = Keyboard.current;
        if (k == null || !k.escapeKey.wasPressedThisFrame) return;   // Android back button maps to Escape
        if (settingsOpen) { CloseSettings(); return; }
        if (gm.IsPaused) { gm.SetPaused(false); return; }
        if (gm.IsPlaying) { gm.SetPaused(true); return; }
        if (gm.IsGameOver) { FadeThen(gm.BackToMenu); return; }
        if (gm.Current == GameManager.State.Menu)
        {
            if (Time.unscaledTime - lastBackPress < 2f) gm.Quit();
            else { lastBackPress = Time.unscaledTime; Toast("PRESS BACK AGAIN TO EXIT", 0.45f); }
        }
    }

    // ================================================================ public hooks
    public static void OnCheeseCollected(Vector3 world)
    {
        var ui = Instance;
        if (!ui || !ui.cheesePill) return;
        ui.StartCoroutine(ui.Tween(0.22f, k => ui.cheesePill.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(k * Mathf.PI))));
        ui.SpawnPopup(world);
    }

    public static void OnHit()
    {
        var ui = Instance;
        if (!ui) return;
        ui.SetAlpha(ui.flash, 0.35f);
    }

    public static void ShowCountdown(string text, bool final)
    {
        var ui = Instance;
        if (!ui) return;
        ui.StartCoroutine(ui.CountdownPop(text, final));
    }

    RectTransform hintRT;
    Text hintText;
    Coroutine hintCo;

    // Short how-to-play prompt in the middle of the screen (tutorial runs).
    public static void Hint(string text)
    {
        var ui = Instance;
        if (!ui || !ui.hintRT) return;
        if (ui.hintCo != null) ui.StopCoroutine(ui.hintCo);
        ui.hintCo = ui.StartCoroutine(ui.HintRoutine(text));
    }

    IEnumerator HintRoutine(string text)
    {
        hintText.text = text;
        hintRT.gameObject.SetActive(true);
        var img = hintRT.GetComponent<Image>();
        yield return Tween(0.3f, k => { hintRT.localScale = Vector3.one * EaseOutBack(k); SetAlpha(img, 0.7f); hintText.color = Color.white; });
        float t = 0f;
        while (t < 1.8f) { t += Time.unscaledDeltaTime; hintRT.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(t * 9f)); yield return null; }
        yield return Tween(0.3f, k => { SetAlpha(img, 0.7f * (1f - k)); hintText.color = new Color(1f, 1f, 1f, 1f - k); });
        hintRT.gameObject.SetActive(false);
        hintCo = null;
    }

    public static void Toast(string text, float scale = 1f)
    {
        var ui = Instance;
        if (!ui) return;
        ui.StartCoroutine(ui.ToastRoutine(text, scale));
    }

    // fade to dark, then run (scene reloads fade back in)
    public static void FadeThen(Action action)
    {
        var ui = Instance;
        if (!ui) { action(); return; }
        ui.StartCoroutine(ui.FadeRoutine(action));
    }

    IEnumerator FadeRoutine(Action action)
    {
        fade.raycastTarget = true;
        yield return Tween(0.3f, k => SetAlpha(fade, k));
        fadeInOnLoad = true;
        action();
    }

    IEnumerator CountdownPop(string text, bool final)
    {
        countdownText.text = text;
        countdownText.gameObject.SetActive(true);
        float dur = final ? 0.6f : 0.42f;
        yield return Tween(dur, k =>
        {
            float s = k < 0.35f ? EaseOutBack(k / 0.35f) : 1f + (k - 0.35f) * 0.3f;
            countdownText.rectTransform.localScale = Vector3.one * s * (final ? 1.2f : 1f);
            countdownText.color = new Color(1f, 1f, 1f, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
        });
        if (countdownText.text == text) countdownText.text = "";
    }

    IEnumerator ToastRoutine(string text, float scale)
    {
        toastText.text = text;
        toastText.rectTransform.localScale = Vector3.zero;
        yield return Tween(0.35f, k => toastText.rectTransform.localScale = Vector3.one * scale * EaseOutBack(k));
        yield return new WaitForSecondsRealtime(1.1f);
        yield return Tween(0.3f, k => toastText.color = new Color(1f, 1f, 1f, 1f - k));
        toastText.text = "";
        toastText.color = Color.white;
    }

    void SpawnPopup(Vector3 world)
    {
        var cam = Camera.main;
        if (!cam) return;
        Text p = popups.Find(x => !x.gameObject.activeSelf);
        if (!p) return;
        Vector3 sp = cam.WorldToScreenPoint(world + Vector3.up * 0.6f);
        if (sp.z < 0f) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, sp, null, out var local);
        p.gameObject.SetActive(true);
        var rt = p.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        StartCoroutine(Tween(0.7f, k =>
        {
            rt.anchoredPosition = local + new Vector2(0f, 160f * EaseOutCubic(k));
            rt.localScale = Vector3.one * (k < 0.2f ? EaseOutBack(k / 0.2f) : 1f);
            p.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((k - 0.5f) / 0.5f));
        }, () => p.gameObject.SetActive(false)));
    }

    void OpenSettings()
    {
        settingsOpen = true;
        musicSlider.SetValueWithoutNotify(AudioManager.MusicVolume);
        sfxSlider.SetValueWithoutNotify(AudioManager.SfxVolume);
        SetGroup(settingsG, true);
        settingsG.transform.SetAsLastSibling();
        fade.transform.SetAsLastSibling();
        StartCoroutine(PopIn(settingsPanel));
    }

    void CloseSettings()
    {
        settingsOpen = false;
        PlayerPrefs.Save();
        SetGroup(settingsG, false);
        RefreshSoundIcon();
    }

    void ToggleSound()
    {
        bool muted = AudioManager.MusicVolume <= 0.001f && AudioManager.SfxVolume <= 0.001f;
        float m = muted ? 0.35f : 0f, s = muted ? 1f : 0f;
        AudioManager.SetMusicVolume(m);
        AudioManager.SetSfxVolume(s);
        PlayerPrefs.Save();
        RefreshSoundIcon();
    }

    void RefreshSoundIcon()
    {
        if (!soundIcon) return;
        bool muted = AudioManager.MusicVolume <= 0.001f && AudioManager.SfxVolume <= 0.001f;
        soundIcon.sprite = muted ? UIArt.SpeakerOff : UIArt.SpeakerOn;
    }

    // ================================================================ widgets
    RectTransform Button3D(Transform parent, string label, Sprite icon, Color color, Vector2 size, int fontSize, Action onClick)
    {
        var root = Rt(parent, "Btn_" + label);
        root.sizeDelta = size;
        Color dark = Color.Lerp(color, Brown, 0.45f);
        var shadow = UIArt.Img(root, "Shadow", UIArt.Rounded, new Color(0f, 0f, 0f, 0.28f), true);
        Stretch(shadow.rectTransform); shadow.rectTransform.offsetMin = new Vector2(4f, -22f); shadow.rectTransform.offsetMax = new Vector2(-4f, -14f);
        var edge = UIArt.Img(root, "Edge", UIArt.Rounded, dark, true);
        Stretch(edge.rectTransform); edge.rectTransform.offsetMin = new Vector2(0f, -14f); edge.rectTransform.offsetMax = new Vector2(0f, -6f);
        var face = UIArt.Img(root, "Face", UIArt.Rounded, color, true);
        Stretch(face.rectTransform);
        face.raycastTarget = true;
        var gloss = UIArt.Img(face.transform, "Gloss", UIArt.Rounded, new Color(1f, 1f, 1f, 0.22f), true);
        gloss.rectTransform.anchorMin = new Vector2(0f, 0.5f); gloss.rectTransform.anchorMax = Vector2.one;
        gloss.rectTransform.offsetMin = new Vector2(14f, 0f); gloss.rectTransform.offsetMax = new Vector2(-14f, -10f);

        Color textC = color == Yellow ? Brown : Color.white;
        float iconSize = size.y * 0.5f;
        var txt = Txt(face.transform, label, uiFont, fontSize, textC, TextAnchor.MiddleCenter, textC == Color.white ? 3f : 0f);
        Stretch(txt.rectTransform);
        if (icon)
        {
            txt.rectTransform.offsetMin = new Vector2(iconSize * 0.9f, 0f);
            var ic = UIArt.Img(face.transform, "Icon", icon, textC);
            float textW = txt.preferredWidth;
            Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-textW * 0.5f - iconSize * 0.15f, 0f), new Vector2(iconSize, iconSize));
        }

        var btn = face.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => { AudioManager.PlayClick(); onClick(); });
        var ps = face.gameObject.AddComponent<PressScale>();
        ps.target = root;
        root.gameObject.AddComponent<PressScale>().enabled = false;   // marker so menu pulse can read Pressed
        root.GetComponent<PressScale>().mirror = ps;
        return root;
    }

    RectTransform RoundButton(Transform parent, Sprite icon, Color color, float size, Action onClick)
    {
        var root = Rt(parent, "Round_" + icon.name);
        root.sizeDelta = new Vector2(size, size);
        Color dark = Color.Lerp(color, Brown, 0.45f);
        var shadow = UIArt.Img(root, "Shadow", UIArt.Circle, new Color(0f, 0f, 0f, 0.28f));
        Stretch(shadow.rectTransform); shadow.rectTransform.offsetMin = new Vector2(2f, -18f); shadow.rectTransform.offsetMax = new Vector2(-2f, -12f);
        var edge = UIArt.Img(root, "Edge", UIArt.Circle, dark);
        Stretch(edge.rectTransform); edge.rectTransform.offsetMin = new Vector2(0f, -10f); edge.rectTransform.offsetMax = new Vector2(0f, -4f);
        var face = UIArt.Img(root, "Face", UIArt.Circle, color);
        Stretch(face.rectTransform);
        face.raycastTarget = true;
        var gloss = UIArt.Img(face.transform, "Gloss", UIArt.Circle, new Color(1f, 1f, 1f, 0.25f));
        Place(gloss.rectTransform, new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(size * 0.62f, size * 0.36f));
        var ic = UIArt.Img(root, "Icon", icon, color == Yellow ? Brown : Color.white);
        Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.55f, size * 0.55f));
        var btn = face.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => { AudioManager.PlayClick(); onClick(); });
        face.gameObject.AddComponent<PressScale>().target = root;
        return root;
    }

    RectTransform Panel(Transform parent, Vector2 size)
    {
        var root = Rt(parent, "Panel");
        Place(root, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        var shadow = UIArt.Img(root, "Shadow", UIArt.Rounded, new Color(0f, 0f, 0f, 0.3f), true);
        Stretch(shadow.rectTransform); shadow.rectTransform.offsetMin = new Vector2(8f, -26f); shadow.rectTransform.offsetMax = new Vector2(-8f, -20f);
        var border = UIArt.Img(root, "Border", UIArt.Rounded, new Color(0.95f, 0.62f, 0.2f), true);
        Stretch(border.rectTransform);
        var bg = UIArt.Img(root, "Bg", UIArt.Rounded, PanelC, true);
        Stretch(bg.rectTransform); bg.rectTransform.offsetMin = new Vector2(14f, 14f); bg.rectTransform.offsetMax = new Vector2(-14f, -14f);
        bg.raycastTarget = true;
        return root;
    }

    void Dim(Transform parent)
    {
        var d = UIArt.Img(parent, "Dim", null, new Color(0.05f, 0.02f, 0f, 0.6f));
        Stretch(d.rectTransform);
        d.raycastTarget = true;   // blocks taps to the game behind
    }

    Text Txt(Transform parent, string s, Font f, int size, Color c, TextAnchor a, float outline, Color? gradTop = null, Color? gradBottom = null)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = f; t.fontSize = size; t.color = c; t.alignment = a; t.text = s;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (gradTop.HasValue)
        {
            var g = go.AddComponent<UIGradient>();
            g.top = gradTop.Value; g.bottom = gradBottom ?? gradTop.Value;
        }
        if (outline > 0f)
        {
            var o = go.AddComponent<Outline>(); o.effectColor = Brown; o.effectDistance = new Vector2(outline, -outline);
            if (outline >= 5f) { var o2 = go.AddComponent<Outline>(); o2.effectColor = Brown; o2.effectDistance = new Vector2(outline * 1.2f, outline * 0.2f); }
            var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.35f); sh.effectDistance = new Vector2(0f, -outline * 1.6f);
        }
        return t;
    }

    static RectTransform Rt(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static CanvasGroup Group(Transform parent, string name)
    {
        var rt = Rt(parent, name);
        Stretch(rt);
        return rt.gameObject.AddComponent<CanvasGroup>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    void SetGroup(CanvasGroup g, bool on, bool instant = false)
    {
        if (!g) return;
        g.interactable = on;
        g.blocksRaycasts = on;
        if (instant) { g.alpha = on ? 1f : 0f; g.gameObject.SetActive(on); return; }
        if (on) g.gameObject.SetActive(true);
        float from = g.alpha;
        StartCoroutine(Tween(0.25f, k => g.alpha = Mathf.Lerp(from, on ? 1f : 0f, k), () => { if (!on) g.gameObject.SetActive(false); }));
    }

    IEnumerator PopIn(RectTransform rt)
    {
        rt.localScale = Vector3.one * 0.7f;
        yield return Tween(0.35f, k => rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, EaseOutBack(k)));
    }

    void SetAlpha(Image img, float a) { if (img) { var c = img.color; c.a = a; img.color = c; } }

    IEnumerator Tween(float dur, Action<float> f, Action done = null)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            f(Mathf.Clamp01(t / dur));
            yield return null;
        }
        f(1f);
        done?.Invoke();
    }

    static float EaseOutBack(float x) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f); }
    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
}

// Squash-on-press feedback for mobile buttons.
public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Transform target;
    public PressScale mirror;           // on a parent: read the face's pressed state
    bool pressed;
    float scale = 1f;
    public bool Pressed => mirror ? mirror.pressed : pressed;

    public void OnPointerDown(PointerEventData e) => pressed = true;
    public void OnPointerUp(PointerEventData e) => pressed = false;
    public void OnPointerExit(PointerEventData e) => pressed = false;

    void Update()
    {
        if (!target) target = transform;
        if (mirror) return;
        float want = pressed ? 0.92f : 1f;
        float prev = scale;
        scale = Mathf.Lerp(scale, want, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
        if (!pressed && Mathf.Abs(scale - want) < 0.001f && Mathf.Abs(prev - want) < 0.001f) return;
        target.localScale = Vector3.one * scale;
    }
}
