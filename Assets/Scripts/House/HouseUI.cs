using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Free-roam UI in a "mouse-scale scrapbook" style: bottle-cap buttons, paper tags, notebook
// pages held with tape, a sticky-note objective, handwritten messages and captions.
// Landscape 1920x1080 reference, notch safe area. Left: floating joystick. Right: drag to look,
// Jump / Sneak / Run / Throw, and a contextual action cap that only appears when usable.
public class HouseUI : MonoBehaviour
{
    public static HouseUI Inst { get; private set; }
    public static bool JoystickIdle => !Inst || !Inst.stick || !Inst.stick.Active;

    // palette (warm paper + ink)
    static readonly Color Ink = new Color(0.24f, 0.15f, 0.09f);
    static readonly Color Paper = new Color(0.99f, 0.96f, 0.88f);
    static readonly Color Mustard = new Color(0.96f, 0.73f, 0.22f);
    static readonly Color Tomato = new Color(0.86f, 0.34f, 0.25f);
    static readonly Color Teal = new Color(0.22f, 0.5f, 0.55f);
    static readonly Color Sage = new Color(0.47f, 0.63f, 0.36f);
    static readonly Color Sky = new Color(0.36f, 0.56f, 0.8f);
    static readonly Color Sticky = new Color(1f, 0.9f, 0.46f);

    HouseGame game;
    Font titleFont, uiFont, handFont;
    RectTransform safe;
    Rect lastSafe;

    CanvasGroup menuG, hudG, pauseG, invG, mapG, noteG, endG, settingsG, cutG;
    TouchStick stick;
    RectTransform actionBtn, throwBtn, crouchBtn, eyeRT, messageRT, progressRT, invList, mapArea, mapDot, topBar, bottomBar, skipBtn, handsRT;
    Text actionLabel, throwLabel, objectiveText, roomText, messageText, progressText, noteTitle, noteBody, endTitle, endStats, captionText, pantryText;
    Image[] hearts, handIcons;
    Image hungerFill, staminaFill, eyeIcon, danger, flash, progressFill, crouchCap, roomTape;
    Image[] endStars;
    float messageTimer, progressTimer, progressDur;
    string lastRoom = "";
    Interactable current;
    bool alert;
    Action onSkip;

    static readonly Dictionary<string, (string title, string body)> Notes = new Dictionary<string, (string, string)>
    {
        ["vacation"] = ("Note on the fridge", "Dear cat,\n\nWe're off to the seaside for two whole weeks! Your bowl is full. Please do NOT chase anything around the house while we're gone.\n\nLove, Mom"),
        ["spare_key"] = ("A crumpled reminder", "Note to self:\nI hid the spare storage room key somewhere in the house. A cupboard? A drawer? The TV cabinet?\nI'll remember eventually...\n\n- Dad"),
        ["cat_habits"] = ("Pet-sitting tips", "The cat naps in its bed after meals and patrols the rest of the time.\n\nIt can't resist the TV - switch it on and it'll sit and watch for ages.\n\nIt hears EVERYTHING. Walk softly."),
        ["tunnels"] = ("Scratched on a scrap", "Psst - to whoever lives in the walls now:\n\nThe old tunnels link the bedroom, the bathroom and the storage room. The bathroom one hides behind the laundry basket.\n\nThe cat is far too round to follow!\n- a friend"),
        ["padlock"] = ("Dad's to-do list", "1. Fix the dripping tap\n2. Padlock the back door so the cat can't slip out\n3. Padlock key goes in the RED TOOLBOX\n4. Remember where I hid the storage key"),
    };

    // ================================================================ creation
    public static void Create(HouseGame g)
    {
        if (Inst) return;
        var go = new GameObject("HouseUI");
        Inst = go.AddComponent<HouseUI>();
        Inst.game = g;
        Inst.Build();
    }

    void OnDestroy() { if (Inst == this) Inst = null; }

    void Build()
    {
        var art = GameArt.Get();
        titleFont = art && art.titleFont ? art.titleFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiFont = art && art.uiFont ? art.uiFont : titleFont;
        handFont = Resources.Load<Font>("Fonts/PatrickHand-Regular");
        if (!handFont) handFont = uiFont;
        if (!EventSystem.current)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.6f;
        gameObject.AddComponent<GraphicRaycaster>();

        danger = Img(transform, "Danger", UIArt.Vignette, new Color(0.75f, 0.08f, 0.04f, 0f)); Stretch(danger.rectTransform);
        flash = Img(transform, "Flash", null, new Color(0.9f, 0.2f, 0.12f, 0f)); Stretch(flash.rectTransform);
        safe = Rt(transform, "Safe"); Stretch(safe); ApplySafeArea();

        BuildHud(); BuildMenu(); BuildCutscene(); BuildPause(); BuildInventory(); BuildMap(); BuildNote(); BuildSettings(); BuildEnd();
        foreach (var g in new[] { hudG, pauseG, invG, mapG, noteG, endG, settingsG, cutG }) Show(g, false, true);
        Show(menuG, true, true);
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

    // ================================================================ HUD
    void BuildHud()
    {
        hudG = Group(safe, "HUD");
        var lookZone = Img(hudG.transform, "LookZone", null, Color.clear); lookZone.raycastTarget = true;
        Anchor(lookZone.rectTransform, new Vector2(0.42f, 0f), Vector2.one);
        lookZone.gameObject.AddComponent<TouchLook>();

        var stickZone = Img(hudG.transform, "StickZone", null, Color.clear); stickZone.raycastTarget = true;
        Anchor(stickZone.rectTransform, Vector2.zero, new Vector2(0.42f, 0.72f));
        stick = stickZone.gameObject.AddComponent<TouchStick>();
        var ring = Img(stickZone.transform, "StickRing", UIArt.Ring, new Color(1f, 0.97f, 0.9f, 0.55f));
        Place(ring.rectTransform, Vector2.zero, new Vector2(250f, 230f), new Vector2(290f, 290f));
        var knob = Img(ring.transform, "Knob", UIArt.BottleCap, new Color(1f, 0.97f, 0.9f, 0.92f));
        Place(knob.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 130f));
        var knobRing = Img(knob.transform, "KnobRing", UIArt.CapRing, new Color(Ink.r, Ink.g, Ink.b, 0.35f)); Stretch(knobRing.rectTransform);
        stick.Setup(ring.rectTransform, knob.rectTransform, 105f);

        // right-hand controls: bottle caps
        var jump = Cap(hudG.transform, UIArt.ChevronUp, Mustard, 180f, "JUMP", () => game.mouse.Jump());
        Place(jump, new Vector2(1f, 0f), new Vector2(-150f, 150f), new Vector2(180f, 180f));
        crouchBtn = Cap(hudG.transform, UIArt.ChevronDown, Teal, 145f, "SNEAK", () => game.mouse.ToggleCrouch());
        Place(crouchBtn, new Vector2(1f, 0f), new Vector2(-345f, 100f), new Vector2(145f, 145f));
        crouchCap = crouchBtn.Find("Cap").GetComponent<Image>();
        var run = Cap(hudG.transform, UIArt.Bolt, Sage, 145f, "RUN", null);
        Place(run, new Vector2(1f, 0f), new Vector2(-125f, 350f), new Vector2(145f, 145f));
        run.Find("Cap").gameObject.AddComponent<HoldButton>().onChange = held => { if (game.mouse) game.mouse.sprintHeld = held; };
        actionBtn = Cap(hudG.transform, UIArt.Hand, Tomato, 165f, "", () => DoAction());
        Place(actionBtn, new Vector2(1f, 0f), new Vector2(-350f, 320f), new Vector2(165f, 165f));
        actionLabel = actionBtn.Find("Label").GetComponent<Text>();
        actionBtn.gameObject.SetActive(false);
        throwBtn = Cap(hudG.transform, UIArt.Circle, Sky, 125f, "THROW", () => game.UseMarble());
        Place(throwBtn, new Vector2(1f, 0f), new Vector2(-545f, 120f), new Vector2(125f, 125f));
        throwLabel = HandText(throwBtn, "x0", 34, Ink);
        Place(throwLabel.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(80f, 50f));
        throwBtn.gameObject.SetActive(false);

        // status card (top left): hearts, hunger, stamina
        var card = PaperCard(hudG.transform, "Status", Paper, new Vector2(400f, 150f), -1.5f);
        Place(card, new Vector2(0f, 1f), new Vector2(225f, -95f), new Vector2(400f, 150f));
        TapeStrip(card, new Vector2(0.5f, 1f), new Vector2(0f, -4f), 140f, 4f);
        hearts = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            hearts[i] = Img(card, "Heart", UIArt.Heart, Tomato);
            Place(hearts[i].rectTransform, new Vector2(0f, 1f), new Vector2(48f + i * 62f, -42f), new Vector2(54f, 54f));
            InkOutline(hearts[i], 2f);
        }
        var cheeseIcon = Img(card, "HungerIcon", UIArt.Cheese, Color.white);
        Place(cheeseIcon.rectTransform, new Vector2(0f, 0f), new Vector2(42f, 58f), new Vector2(54f, 54f));
        hungerFill = Bar(card, new Vector2(240f, 58f), new Vector2(290f, 24f), Mustard);
        var bolt = Img(card, "StaminaIcon", UIArt.Bolt, Sage);
        Place(bolt.rectTransform, new Vector2(0f, 0f), new Vector2(42f, 22f), new Vector2(30f, 30f));
        staminaFill = Bar(card, new Vector2(240f, 22f), new Vector2(290f, 12f), Sage);

        // sticky-note objective
        var note = PaperCard(hudG.transform, "Objective", Sticky, new Vector2(400f, 140f), 1.2f);
        Place(note, new Vector2(0f, 1f), new Vector2(225f, -262f), new Vector2(400f, 140f));
        TapeStrip(note, new Vector2(0.5f, 1f), new Vector2(0f, -2f), 110f, -3f);
        objectiveText = HandText(note, "", 32, Ink, TextAnchor.UpperLeft);
        Place(objectiveText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(360f, 110f));
        objectiveText.horizontalOverflow = HorizontalWrapMode.Wrap;

        // hands (carried food, tap to eat) + pantry count
        handsRT = Rt(hudG.transform, "Hands");
        Place(handsRT, new Vector2(0f, 0f), new Vector2(580f, 78f), new Vector2(330f, 120f));
        handIcons = new Image[HouseGame.CarryLimit];
        for (int i = 0; i < HouseGame.CarryLimit; i++)
        {
            int idx = i;
            var slot = Img(handsRT, "Hand", UIArt.Circle, new Color(1f, 0.97f, 0.9f, 0.85f));
            Place(slot.rectTransform, new Vector2(0f, 0.5f), new Vector2(55f + i * 110f, 0f), new Vector2(96f, 96f));
            var ringS = Img(slot.transform, "Ring", UIArt.Ring, new Color(Ink.r, Ink.g, Ink.b, 0.5f)); Stretch(ringS.rectTransform);
            handIcons[i] = Img(slot.transform, "Food", null, Color.white);
            Place(handIcons[i].rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            slot.raycastTarget = true;
            var b = slot.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { if (game.Carried.Count > idx) { AudioManager.PlayClick(); game.EatCarried(idx); } });
            slot.gameObject.AddComponent<PressScale>();
        }
        var crate = Img(handsRT, "Pantry", UIArt.Crate, Ink);
        Place(crate.rectTransform, new Vector2(0f, 0.5f), new Vector2(265f, 10f), new Vector2(54f, 54f));
        pantryText = HandText(handsRT, "0/8", 36, Paper);
        Place(pantryText.rectTransform, new Vector2(0f, 0.5f), new Vector2(268f, -34f), new Vector2(110f, 44f));
        AddShadow(pantryText);

        // top right caps
        Place(Cap(hudG.transform, UIArt.Pause, Mustard, 105f, null, () => OpenPause()), new Vector2(1f, 1f), new Vector2(-80f, -78f), new Vector2(105f, 105f));
        Place(Cap(hudG.transform, UIArt.MapPin, Teal, 105f, null, () => OpenMap()), new Vector2(1f, 1f), new Vector2(-205f, -78f), new Vector2(105f, 105f));
        Place(Cap(hudG.transform, UIArt.Bag, Tomato, 105f, null, () => OpenInventory()), new Vector2(1f, 1f), new Vector2(-330f, -78f), new Vector2(105f, 105f));

        // cat awareness eye
        eyeRT = Rt(hudG.transform, "Awareness");
        Place(eyeRT, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(110f, 110f));
        var eyeBg = Img(eyeRT, "Bg", UIArt.BottleCap, Paper); Stretch(eyeBg.rectTransform);
        eyeIcon = Img(eyeRT, "Eye", UIArt.Eye, Ink);
        Place(eyeIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
        eyeRT.localScale = Vector3.zero;

        // room name on a strip of tape
        roomTape = Img(hudG.transform, "RoomTape", UIArt.Tape, Color.white);
        Place(roomTape.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(420f, 70f));
        roomText = HandText(roomTape.transform, "", 44, Ink);
        Stretch(roomText.rectTransform);
        roomTape.gameObject.SetActive(false);

        // handwritten message strip
        messageRT = PaperCard(hudG.transform, "Message", Paper, new Vector2(980f, 84f), 0.6f);
        Place(messageRT, new Vector2(0.5f, 0f), new Vector2(60f, 200f), new Vector2(980f, 84f));
        messageText = HandText(messageRT, "", 36, Ink);
        Stretch(messageText.rectTransform);
        messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
        messageRT.gameObject.SetActive(false);

        progressRT = PaperCard(hudG.transform, "Progress", Paper, new Vector2(560f, 100f), 0f);
        Place(progressRT, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(560f, 100f));
        progressText = HandText(progressRT, "", 34, Ink);
        Place(progressText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(540f, 44f));
        progressFill = Bar(progressRT, new Vector2(280f, 26f), new Vector2(480f, 16f), Mustard, centred: true);
        progressRT.gameObject.SetActive(false);
    }

    Image Bar(Transform parent, Vector2 pos, Vector2 size, Color c, bool centred = false)
    {
        var bg = Img(parent, "BarBg", UIArt.Pill, new Color(Ink.r, Ink.g, Ink.b, 0.18f), true);
        Place(bg.rectTransform, centred ? new Vector2(0.5f, 0f) : Vector2.zero, centred ? new Vector2(0f, pos.y) : pos, size);
        var fill = Img(bg.transform, "Fill", null, c);
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
        Stretch(fill.rectTransform);
        fill.rectTransform.offsetMin = new Vector2(3f, 3f); fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
        var outline = Img(bg.transform, "Edge", UIArt.Pill, Color.clear, true); Stretch(outline.rectTransform);
        InkOutline(bg, 1.5f);
        return fill;
    }

    // ================================================================ menu
    void BuildMenu()
    {
        menuG = Group(safe, "Menu");
        var banner = PaperCard(menuG.transform, "Banner", Paper, new Vector2(900f, 420f), -2.5f);
        Place(banner, new Vector2(0.27f, 0.68f), Vector2.zero, new Vector2(900f, 420f));
        TapeStrip(banner, new Vector2(0f, 1f), new Vector2(70f, -20f), 150f, -30f);
        TapeStrip(banner, new Vector2(1f, 1f), new Vector2(-70f, -20f), 150f, 30f);
        var the = Txt(banner, "THE GREAT", uiFont, 64, Ink);
        Place(the.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-10f, 125f), new Vector2(860f, 90f));
        var t1 = Txt(banner, "CHEESE", titleFont, 176, Mustard);
        Place(t1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(900f, 200f));
        InkOutline(t1, 6f);
        var t2 = Txt(banner, "ESCAPE!", titleFont, 132, Tomato);
        Place(t2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(15f, -120f), new Vector2(900f, 160f));
        InkOutline(t2, 6f);
        var ch = Img(banner, "Cheese", UIArt.Cheese, Color.white);
        Place(ch.rectTransform, new Vector2(1f, 1f), new Vector2(-80f, -50f), new Vector2(150f, 150f));
        ch.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -14f);

        Place(Tag(menuG.transform, "PLAY", Tomato, Paper, new Vector2(470f, 140f), 78, () => StartGame(), -1.5f), new Vector2(0.27f, 0.3f), Vector2.zero, new Vector2(470f, 140f));
        Place(Cap(menuG.transform, UIArt.Gear, Teal, 120f, null, () => OpenSettings()), new Vector2(0.27f, 0.12f), new Vector2(-80f, 0f), new Vector2(120f, 120f));
        Place(Cap(menuG.transform, UIArt.SpeakerOn, Sage, 120f, null, () => ToggleSound()), new Vector2(0.27f, 0.12f), new Vector2(80f, 0f), new Vector2(120f, 120f));
        int best = PlayerPrefs.GetInt("BestEscapeTime", 0), stars = PlayerPrefs.GetInt("BestStars", 0);
        if (best > 0)
        {
            var b = HandText(menuG.transform, "Best escape: " + FormatTime(best) + "   " + new string('*', stars), 38, Paper);
            AddShadow(b);
            Place(b.rectTransform, new Vector2(0.27f, 0.2f), Vector2.zero, new Vector2(800f, 60f));
        }
    }

    void StartGame()
    {
        Show(menuG, false);
        game.StartGame();
    }

    // ================================================================ cutscenes (letterbox + caption)
    void BuildCutscene()
    {
        cutG = Group(transform, "Cutscene");
        topBar = Img(cutG.transform, "Top", null, new Color(0.06f, 0.04f, 0.03f)).rectTransform;
        Anchor(topBar, new Vector2(0f, 0.88f), Vector2.one);
        bottomBar = Img(cutG.transform, "Bottom", null, new Color(0.06f, 0.04f, 0.03f)).rectTransform;
        Anchor(bottomBar, Vector2.zero, new Vector2(1f, 0.14f));
        captionText = HandText(bottomBar, "", 50, new Color(1f, 0.95f, 0.85f));
        Stretch(captionText.rectTransform);
        captionText.rectTransform.offsetMin = new Vector2(160f, 0f); captionText.rectTransform.offsetMax = new Vector2(-160f, 0f);
        captionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        skipBtn = Tag(cutG.transform, "SKIP  >", Paper, Ink, new Vector2(220f, 80f), 38, () => { onSkip?.Invoke(); }, 2f);
        Place(skipBtn, new Vector2(1f, 1f), new Vector2(-150f, -60f), new Vector2(220f, 80f));
    }

    public static void Cutscene(bool on, Action skip)
    {
        if (!Inst) return;
        Inst.onSkip = skip;
        Inst.Show(Inst.cutG, on);
        if (on) { Inst.Show(Inst.hudG, false); Inst.captionText.text = ""; }
    }

    public static void Caption(string text)
    {
        if (!Inst) return;
        Inst.captionText.text = text;
        Inst.StartCoroutine(Inst.Tween(0.5f, k => Inst.captionText.color = new Color(1f, 0.95f, 0.85f, k)));
    }

    public static void IntroFinished()
    {
        if (!Inst) return;
        Inst.Show(Inst.hudG, true);
        ObjectiveChanged();
        CarryChanged();
        Inst.StartCoroutine(Inst.ControlsTips());
    }

    IEnumerator ControlsTips()
    {
        yield return new WaitForSeconds(2f);
        Message("Left thumb moves you. Drag on the right to look around.");
        yield return new WaitForSeconds(4.5f);
        Message("Pick up food and carry it home through your mouse hole.");
        yield return new WaitForSeconds(4.5f);
        Message("Running is loud. Sneaking is silent. The cat hears everything.");
    }

    // ================================================================ runtime
    void Update()
    {
        ApplySafeArea();
        if (!game) return;
        HandleBack();
        if (game.phase != HouseGame.Phase.Playing && game.phase != HouseGame.Phase.Caught) return;
        var m = game.mouse;
        if (stick && m) { if (stick.Active || stick.Value != Vector2.zero) m.moveInput = stick.Value; else if (Keyboard.current == null || !AnyMoveKey()) m.moveInput = Vector2.zero; }

        for (int i = 0; i < hearts.Length; i++) hearts[i].color = i < game.Hearts ? Tomato : new Color(Ink.r, Ink.g, Ink.b, 0.2f);
        hungerFill.fillAmount = game.Hunger / 100f;
        hungerFill.color = game.Hunger < 25f ? Color.Lerp(Tomato, Mustard, Mathf.PingPong(Time.time * 2f, 1f)) : Mustard;
        if (m) staminaFill.fillAmount = m.Stamina / m.maxStamina;
        if (crouchCap && m) crouchCap.color = m.IsCrouching ? Color.Lerp(Teal, Color.white, 0.35f) : Teal;

        current = m && game.phase == HouseGame.Phase.Playing && !m.Hiding ? Interactable.Find(m) : null;
        bool showAction = current || (m && m.Hiding);
        if (actionBtn.gameObject.activeSelf != showAction) actionBtn.gameObject.SetActive(showAction);
        if (showAction) actionLabel.text = m.Hiding ? "LEAVE" : current.Prompt;
        int marbles = game.Count("marble");
        if (throwBtn.gameObject.activeSelf != (marbles > 0)) throwBtn.gameObject.SetActive(marbles > 0);
        throwLabel.text = "x" + marbles;
        var k = Keyboard.current;
        if (k != null && k.eKey.wasPressedThisFrame && showAction) DoAction();
        if (k != null && k.qKey.wasPressedThisFrame) game.UseMarble();
        if (k != null && k.fKey.wasPressedThisFrame && game.Carried.Count > 0) game.EatCarried(0);

        string room = m ? game.house.RoomAt(m.transform.position) : "";
        if (room != lastRoom && !string.IsNullOrEmpty(room)) { lastRoom = room; StartCoroutine(RoomBanner(room)); }

        var cat = game.cat;
        float s = cat ? cat.Suspicion01 : 0f;
        bool chasing = cat && cat.IsChasing;
        float showEye = chasing || s > 0.04f ? 1f : 0f;
        eyeRT.localScale = Vector3.Lerp(eyeRT.localScale, Vector3.one * (showEye * (chasing ? 1.12f + 0.08f * Mathf.Sin(Time.time * 12f) : 1f)), 0.25f);
        eyeIcon.color = Color.Lerp(Ink, Tomato, Mathf.Clamp01(s * 1.4f));
        SetAlpha(danger, Mathf.Lerp(danger.color.a, chasing ? 0.5f + 0.12f * Mathf.Sin(Time.time * 9f) : 0f, 0.15f));
        SetAlpha(flash, Mathf.MoveTowards(flash.color.a, 0f, Time.unscaledDeltaTime * 2f));

        if (messageTimer > 0f) { messageTimer -= Time.unscaledDeltaTime; if (messageTimer <= 0f) messageRT.gameObject.SetActive(false); }
        if (progressTimer > 0f)
        {
            progressTimer -= Time.deltaTime;
            progressFill.fillAmount = 1f - progressTimer / progressDur;
            if (progressTimer <= 0f) progressRT.gameObject.SetActive(false);
        }
        if (mapG.gameObject.activeSelf) UpdateMapDot();
    }

    bool AnyMoveKey()
    {
        var k = Keyboard.current;
        return k.wKey.isPressed || k.aKey.isPressed || k.sKey.isPressed || k.dKey.isPressed || k.upArrowKey.isPressed || k.downArrowKey.isPressed || k.leftArrowKey.isPressed || k.rightArrowKey.isPressed;
    }

    void DoAction()
    {
        var m = game.mouse;
        if (!m || game.phase != HouseGame.Phase.Playing) return;
        AudioManager.PlayClick();
        if (m.Hiding) { m.ExitHide(); return; }
        if (current) current.Interact(m);
    }

    IEnumerator RoomBanner(string room)
    {
        roomText.text = room;
        roomTape.gameObject.SetActive(true);
        roomTape.rectTransform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-3f, 3f));
        yield return Tween(0.25f, k => roomTape.rectTransform.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, k), 1f, 1f));
        yield return new WaitForSeconds(1.8f);
        yield return Tween(0.3f, k => roomTape.color = new Color(1f, 1f, 1f, 1f - k));
        roomTape.gameObject.SetActive(false);
        roomTape.color = Color.white;
    }

    void HandleBack()
    {
        var k = Keyboard.current;
        if (k == null || !k.escapeKey.wasPressedThisFrame) return;   // Android back button
        foreach (var g in new[] { settingsG, noteG, invG, mapG })
            if (g.gameObject.activeSelf && g.interactable) { CloseAllPanels(); if (game.Paused && !pauseG.gameObject.activeSelf) game.SetPaused(false); return; }
        if (cutG.gameObject.activeSelf) { onSkip?.Invoke(); return; }
        if (game.Paused) { Resume(); return; }
        if (game.phase == HouseGame.Phase.Playing) OpenPause();
    }

    // ================================================================ static hooks
    public static void Message(string text, float seconds = 3.2f)
    {
        if (!Inst) return;
        Inst.messageText.text = text;
        Inst.messageRT.gameObject.SetActive(true);
        Inst.messageTimer = seconds;
        Inst.messageRT.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-1f, 1f));
        Inst.StartCoroutine(Inst.Tween(0.18f, k => Inst.messageRT.localScale = Vector3.one * (0.9f + 0.1f * k)));
    }

    public static void Progress(string text, float seconds)
    {
        if (!Inst) return;
        Inst.progressText.text = text;
        Inst.progressRT.gameObject.SetActive(true);
        Inst.progressTimer = Inst.progressDur = seconds;
    }

    public static void ObjectiveChanged()
    {
        if (!Inst || !Inst.game) return;
        var g = Inst.game;
        string pantry = g.Done("pantry") ? "<color=#5a7a3a>- Pantry full! (" + g.Stored.Count + ")</color>" : "- Stock the pantry: " + g.Stored.Count + "/" + HouseGame.PantryGoal;
        Inst.objectiveText.supportRichText = true;
        Inst.objectiveText.text = pantry + "\n- " + g.CurrentObjective.text;
        Inst.StartCoroutine(Inst.Tween(0.35f, k => Inst.objectiveText.transform.parent.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(k * Mathf.PI))));
    }

    public static void CarryChanged()
    {
        if (!Inst || !Inst.game) return;
        var g = Inst.game;
        for (int i = 0; i < Inst.handIcons.Length; i++)
        {
            bool has = i < g.Carried.Count;
            Inst.handIcons[i].enabled = has;
            if (has)
            {
                Inst.handIcons[i].sprite = UIArt.Food(g.Carried[i]);
                Inst.handIcons[i].color = g.Carried[i] switch
                {
                    "cheese" => Color.white, "cookie" => new Color(0.78f, 0.55f, 0.3f), "bread" => new Color(0.82f, 0.55f, 0.3f),
                    "grapes" => new Color(0.5f, 0.25f, 0.58f), _ => new Color(0.82f, 0.65f, 0.42f),
                };
            }
        }
        Inst.pantryText.text = g.Stored.Count + "/" + HouseGame.PantryGoal;
    }

    public static void CaughtFlash() { if (Inst) Inst.SetAlpha(Inst.flash, 0.5f); }

    public static void Alert(bool on)
    {
        if (!Inst) return;
        if (on && !Inst.alert) Message("You've been spotted! Run, hide, or dive into a mouse hole!", 2.5f);
        Inst.alert = on;
    }

    public static void ShowNote(string id)
    {
        if (!Inst || !Notes.TryGetValue(id, out var n)) return;
        Inst.noteTitle.text = n.title;
        Inst.noteBody.text = n.body;
        Inst.OpenPanel(Inst.noteG);
    }

    public static void ShowEnd(bool won)
    {
        if (!Inst) return;
        Inst.StartCoroutine(Inst.EndRoutine(won));
    }

    IEnumerator EndRoutine(bool won)
    {
        yield return new WaitForSecondsRealtime(won ? 0.3f : 1.4f);
        endTitle.text = won ? "YOU ESCAPED!" : "CAUGHT!";
        endTitle.color = won ? new Color(0.36f, 0.55f, 0.24f) : Tomato;
        int stars = game.Stars;
        for (int i = 0; i < endStars.Length; i++) { endStars[i].gameObject.SetActive(won); endStars[i].sprite = UIArt.StarEmpty; }
        endStats.text = (won ? "Out into the meadow, at last!" : "The cat got you one time too many.") +
                        "\n\nTime: " + FormatTime(Mathf.CeilToInt(game.PlayTime)) +
                        "\nFood stored in your pantry: " + game.Stored.Count +
                        "\nTimes caught: " + game.TimesCaught + "     Notes found: " + game.notesRead.Count + "/" + Notes.Count;
        Show(hudG, false);
        OpenPanel(endG);
        if (won)
            for (int i = 0; i < stars; i++)
            {
                yield return new WaitForSecondsRealtime(0.35f);
                endStars[i].sprite = UIArt.StarFull;
                AudioManager.PlayBeep(i == stars - 1);
                var rt = endStars[i].rectTransform;
                StartCoroutine(Tween(0.3f, k => rt.localScale = Vector3.one * (1f + 0.4f * Mathf.Sin(k * Mathf.PI))));
            }
    }

    // ================================================================ panels
    void BuildPause()
    {
        pauseG = Group(transform, "Pause"); Dim(pauseG.transform);
        var p = Notebook(pauseG.transform, new Vector2(760f, 860f), "Paused");
        Place(Tag(p, "RESUME", Sage, Paper, new Vector2(480f, 120f), 56, Resume, -1f), new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(480f, 120f));
        Place(Tag(p, "SETTINGS", Teal, Paper, new Vector2(480f, 110f), 50, OpenSettings, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(480f, 110f));
        Place(Tag(p, "RESTART", Mustard, Ink, new Vector2(480f, 110f), 50, () => game.Restart(), -1f), new Vector2(0.5f, 1f), new Vector2(0f, -560f), new Vector2(480f, 110f));
        Place(Tag(p, "QUIT", Tomato, Paper, new Vector2(480f, 110f), 50, () => game.Quit(), 1.5f), new Vector2(0.5f, 1f), new Vector2(0f, -700f), new Vector2(480f, 110f));
    }

    void OpenPause() { if (game.phase != HouseGame.Phase.Playing) return; game.SetPaused(true); OpenPanel(pauseG); }
    void Resume() { CloseAllPanels(); game.SetPaused(false); }

    void BuildInventory()
    {
        invG = Group(transform, "Inventory"); Dim(invG.transform);
        var p = Notebook(invG.transform, new Vector2(1050f, 820f), "Backpack");
        invList = Rt(p, "List");
        Place(invList, new Vector2(0.5f, 1f), new Vector2(40f, -450f), new Vector2(900f, 560f));
        Place(Cap(p, UIArt.Close, Tomato, 90f, null, CloseInventory), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(90f, 90f));
    }

    void OpenInventory()
    {
        if (game.phase != HouseGame.Phase.Playing) return;
        game.SetPaused(true);
        foreach (Transform c in invList) Destroy(c.gameObject);
        float y = 240f;
        void Row(Sprite icon, Color ic, string text, Action onTap = null)
        {
            var i = Img(invList, "Icon", icon, ic);
            Place(i.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, y), new Vector2(56f, 56f));
            var t = HandText(invList, text, 40, Ink, TextAnchor.MiddleLeft);
            Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(500f, y), new Vector2(880f, 60f));
            if (onTap != null)
            {
                t.raycastTarget = true;
                var b = t.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { AudioManager.PlayClick(); onTap(); });
            }
            y -= 72f;
        }
        Row(UIArt.Crate, Ink, "Pantry: " + game.Stored.Count + " stored   (eat from it in your den)");
        Row(UIArt.Hand, Ink, "Carrying: " + (game.Carried.Count == 0 ? "nothing" : string.Join(", ", game.Carried)) + "   (tap a hand to eat)");
        Row(UIArt.Circle, Sky, "Marbles: " + game.Count("marble") + "   (THROW one to distract the cat)");
        if (game.Has("storage_key")) Row(UIArt.Key, new Color(0.75f, 0.55f, 0.2f), "Storage Room Key");
        if (game.Has("padlock_key")) Row(UIArt.Key, new Color(0.85f, 0.7f, 0.2f), "Padlock Key");
        foreach (var n in game.notesRead) { string id = n; Row(UIArt.Note, Ink, Notes[id].title + "  (read again)", () => ShowNote(id)); }
        OpenPanel(invG);
    }

    void CloseInventory() { CloseAllPanels(); game.SetPaused(false); }

    void BuildMap()
    {
        mapG = Group(transform, "Map"); Dim(mapG.transform);
        var p = Notebook(mapG.transform, new Vector2(1500f, 960f), "House Map");
        mapArea = Rt(p, "Area");
        Place(mapArea, new Vector2(0.5f, 0.5f), new Vector2(20f, -60f), new Vector2(1360f, 720f));
        Place(Cap(p, UIArt.Close, Tomato, 90f, null, () => { CloseAllPanels(); game.SetPaused(false); }), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(90f, 90f));
    }

    Vector2 MapPos(float x, float z) => new Vector2((x - 44f) / 96f * 1340f, (z - 20f) / 60f * 700f);

    void OpenMap()
    {
        if (game.phase != HouseGame.Phase.Playing) return;
        game.SetPaused(true);
        foreach (Transform c in mapArea) Destroy(c.gameObject);
        foreach (var r in game.house.Rooms)
        {
            Color fill = r.name == "Backyard" ? new Color(0.62f, 0.78f, 0.5f) : r.name == "Mouse Den" ? new Color(0.95f, 0.75f, 0.55f) : new Color(0.98f, 0.93f, 0.8f);
            var img = Img(mapArea, r.name, UIArt.PaperCard, fill, true);
            Vector2 a = MapPos(r.rect.xMin, r.rect.yMin), b = MapPos(r.rect.xMax, r.rect.yMax);
            Place(img.rectTransform, new Vector2(0.5f, 0.5f), (a + b) * 0.5f, (b - a) - new Vector2(6f, 6f));
            InkOutline(img, 2f);
            var t = HandText(img.transform, r.name, r.name == "Mouse Den" ? 22 : 30, Ink);
            Stretch(t.rectTransform);
        }
        var dot = Img(mapArea, "You", UIArt.Circle, Tomato);
        mapDot = dot.rectTransform;
        mapDot.sizeDelta = new Vector2(30f, 30f);
        InkOutline(dot, 2f);
        var you = HandText(dot.transform, "you", 28, Tomato);
        Place(you.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 24f), new Vector2(100f, 40f));
        UpdateMapDot();
        OpenPanel(mapG);
    }

    void UpdateMapDot()
    {
        if (!mapDot || !game.mouse) return;
        var p = game.mouse.transform.position;
        mapDot.anchorMin = mapDot.anchorMax = new Vector2(0.5f, 0.5f);
        mapDot.anchoredPosition = MapPos(p.x, p.z);
    }

    void BuildNote()
    {
        noteG = Group(transform, "Note"); Dim(noteG.transform);
        var p = PaperCard(noteG.transform, "Paper", new Color(1f, 0.98f, 0.9f), new Vector2(1000f, 800f), 1.5f);
        Place(p, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 800f));
        var ruled = Img(p, "Ruled", UIArt.Ruled, Color.white); ruled.type = Image.Type.Tiled;
        Anchor(ruled.rectTransform, Vector2.zero, Vector2.one); ruled.rectTransform.offsetMin = new Vector2(30f, 40f); ruled.rectTransform.offsetMax = new Vector2(-30f, -140f);
        TapeStrip(p, new Vector2(0.5f, 1f), new Vector2(0f, -6f), 200f, -2f);
        noteTitle = HandText(p, "", 58, Ink);
        Place(noteTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(900f, 80f));
        noteBody = HandText(p, "", 42, new Color(0.18f, 0.22f, 0.4f), TextAnchor.UpperLeft);
        noteBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        noteBody.lineSpacing = 1.05f;
        Place(noteBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(860f, 520f));
        Place(Tag(p, "GOT IT", Sage, Paper, new Vector2(300f, 100f), 46, () => { CloseAllPanels(); if (game.Paused) game.SetPaused(false); }, -2f),
              new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(300f, 100f));
    }

    void BuildSettings()
    {
        settingsG = Group(transform, "Settings"); Dim(settingsG.transform);
        var p = Notebook(settingsG.transform, new Vector2(960f, 880f), "Settings");
        SliderRow(p, "Music", -230f, AudioManager.MusicVolume, AudioManager.SetMusicVolume);
        SliderRow(p, "Sound", -340f, AudioManager.SfxVolume, AudioManager.SetSfxVolume);
        SliderRow(p, "Look speed", -450f, PlayerPrefs.GetFloat("LookSpeed", 0.5f), v => { PlayerPrefs.SetFloat("LookSpeed", v); ApplyLookSpeed(); });
        SwitchRow(p, "Vibration", -570f, () => GameSettings.Vibration, v => GameSettings.Vibration = v);
        SwitchRow(p, "HD graphics", -680f, () => GameSettings.HighGraphics, v => GameSettings.HighGraphics = v);
        Place(Cap(p, UIArt.Close, Tomato, 90f, null, () => { PlayerPrefs.Save(); Show(settingsG, false); }), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(90f, 90f));
        ApplyLookSpeed();
    }

    void ApplyLookSpeed()
    {
        float v = PlayerPrefs.GetFloat("LookSpeed", 0.5f);
        if (game && game.cam) game.cam.lookSensitivity = Mathf.Lerp(0.07f, 0.3f, v);
    }

    void OpenSettings() { Show(settingsG, true); settingsG.transform.SetAsLastSibling(); }

    void BuildEnd()
    {
        endG = Group(transform, "End"); Dim(endG.transform);
        var p = Notebook(endG.transform, new Vector2(1000f, 880f), "");
        endTitle = Txt(p, "", titleFont, 110, Ink);
        Place(endTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(960f, 140f));
        endStars = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            endStars[i] = Img(p, "Star", UIArt.StarEmpty, Mustard);
            Place(endStars[i].rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 130f, -240f - (i == 1 ? 15f : 0f)), new Vector2(110f, 110f));
            InkOutline(endStars[i], 2f);
        }
        endStats = HandText(p, "", 42, Ink);
        Place(endStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(880f, 320f));
        Place(Tag(p, "PLAY AGAIN", Tomato, Paper, new Vector2(460f, 120f), 56, () => game.Restart(), -1.5f), new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(460f, 120f));
    }

    void OpenPanel(CanvasGroup g)
    {
        Show(g, true);
        g.transform.SetAsLastSibling();
        StartCoroutine(PopIn((RectTransform)g.transform.GetChild(g.transform.childCount > 1 ? 1 : 0)));
    }

    void CloseAllPanels() { foreach (var g in new[] { pauseG, invG, mapG, noteG, settingsG }) Show(g, false); }

    void ToggleSound()
    {
        bool muted = AudioManager.MusicVolume <= 0.001f && AudioManager.SfxVolume <= 0.001f;
        AudioManager.SetMusicVolume(muted ? 0.35f : 0f);
        AudioManager.SetSfxVolume(muted ? 1f : 0f);
        PlayerPrefs.Save();
    }

    // ================================================================ scrapbook widgets
    // bottle-cap button: crimped cap, inner ring, ink icon, small label
    RectTransform Cap(Transform parent, Sprite icon, Color color, float size, string label, Action onClick)
    {
        var root = Rt(parent, "Cap_" + (label ?? icon.name)); root.sizeDelta = new Vector2(size, size);
        var sh = Img(root, "Shadow", UIArt.BottleCap, new Color(0f, 0f, 0f, 0.25f)); Stretch(sh.rectTransform);
        sh.rectTransform.offsetMin = new Vector2(4f, -10f); sh.rectTransform.offsetMax = new Vector2(4f, -10f);
        var cap = Img(root, "Cap", UIArt.BottleCap, color); Stretch(cap.rectTransform); cap.raycastTarget = true;
        var ring = Img(cap.transform, "Ring", UIArt.CapRing, new Color(1f, 1f, 1f, 0.35f)); Stretch(ring.rectTransform);
        Color ink = color == Paper || color == Mustard ? Ink : Paper;
        bool hasLabel = label != null && label != null && (label.Length > 0 || label == "");
        var ic = Img(root, "Icon", icon, ink);
        Place(ic.rectTransform, new Vector2(0.5f, label != null ? 0.6f : 0.5f), Vector2.zero, Vector2.one * size * (label != null ? 0.38f : 0.48f));
        if (label != null)
        {
            var t = Txt(root, label, uiFont, Mathf.RoundToInt(size * 0.17f), ink);
            t.gameObject.name = "Label";
            Place(t.rectTransform, new Vector2(0.5f, 0.27f), Vector2.zero, new Vector2(size * 1.3f, size * 0.25f));
        }
        if (onClick != null)
        {
            var b = cap.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { AudioManager.PlayClick(); onClick(); });
        }
        cap.gameObject.AddComponent<PressScale>().target = root;
        return root;
    }

    // paper tag button: coloured card, ink outline, slight tilt
    RectTransform Tag(Transform parent, string label, Color color, Color textColor, Vector2 size, int fontSize, Action onClick, float tilt)
    {
        var root = Rt(parent, "Tag_" + label); root.sizeDelta = size;
        root.localRotation = Quaternion.Euler(0f, 0f, tilt);
        var sh = Img(root, "Shadow", UIArt.PaperCard, new Color(0f, 0f, 0f, 0.22f), true); Stretch(sh.rectTransform);
        sh.rectTransform.offsetMin = new Vector2(6f, -10f); sh.rectTransform.offsetMax = new Vector2(6f, -10f);
        var face = Img(root, "Face", UIArt.PaperCard, color, true); Stretch(face.rectTransform); face.raycastTarget = true;
        InkOutline(face, 2.5f);
        var t = Txt(face.transform, label, uiFont, fontSize, textColor); Stretch(t.rectTransform);
        var b = face.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { AudioManager.PlayClick(); onClick?.Invoke(); });
        face.gameObject.AddComponent<PressScale>().target = root;
        return root;
    }

    RectTransform PaperCard(Transform parent, string name, Color c, Vector2 size, float tilt)
    {
        var root = Rt(parent, name); root.sizeDelta = size;
        root.localRotation = Quaternion.Euler(0f, 0f, tilt);
        var sh = Img(root, "Shadow", UIArt.PaperCard, new Color(0f, 0f, 0f, 0.2f), true); Stretch(sh.rectTransform);
        sh.rectTransform.offsetMin = new Vector2(5f, -8f); sh.rectTransform.offsetMax = new Vector2(5f, -8f);
        var face = Img(root, "Face", UIArt.PaperCard, c, true); Stretch(face.rectTransform); face.raycastTarget = true;
        return root;
    }

    // notebook page: paper, ruled lines, red margin, tape, handwritten-ish title
    RectTransform Notebook(Transform parent, Vector2 size, string title)
    {
        var p = PaperCard(parent, "Notebook", Paper, size, -0.8f);
        Place(p, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        var ruled = Img(p, "Ruled", UIArt.Ruled, Color.white); ruled.type = Image.Type.Tiled;
        Anchor(ruled.rectTransform, Vector2.zero, Vector2.one); ruled.rectTransform.offsetMin = new Vector2(24f, 30f); ruled.rectTransform.offsetMax = new Vector2(-24f, -150f);
        var margin = Img(p, "Margin", null, new Color(0.85f, 0.35f, 0.3f, 0.45f));
        Anchor(margin.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f));
        margin.rectTransform.offsetMin = new Vector2(96f, 20f); margin.rectTransform.offsetMax = new Vector2(99f, -20f);
        TapeStrip(p, new Vector2(0f, 1f), new Vector2(60f, -14f), 160f, -35f);
        TapeStrip(p, new Vector2(1f, 1f), new Vector2(-60f, -14f), 160f, 35f);
        if (!string.IsNullOrEmpty(title))
        {
            var t = Txt(p, title, titleFont, 86, Ink);
            Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(size.x - 200f, 110f));
        }
        return p;
    }

    void TapeStrip(Transform parent, Vector2 anchor, Vector2 pos, float width, float angle)
    {
        var t = Img(parent, "Tape", UIArt.Tape, Color.white);
        Place(t.rectTransform, anchor, pos, new Vector2(width, 44f));
        t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    void SliderRow(RectTransform panel, string label, float y, float value, Action<float> onChange)
    {
        var l = HandText(panel, label, 48, Ink, TextAnchor.MiddleLeft);
        Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(300f, y), new Vector2(320f, 70f));
        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        var rt = (RectTransform)go.transform; rt.SetParent(panel, false);
        Place(rt, new Vector2(0f, 1f), new Vector2(680f, y), new Vector2(460f, 44f));
        var bg = Img(rt, "Track", UIArt.Pill, new Color(Ink.r, Ink.g, Ink.b, 0.15f), true); Stretch(bg.rectTransform); bg.raycastTarget = true;
        InkOutline(bg, 1.5f);
        var area = Rt(rt, "FillArea"); Stretch(area);
        var fill = Img(area, "Fill", UIArt.Pill, Mustard, true); Stretch(fill.rectTransform);
        var ha = Rt(rt, "HandleArea"); Stretch(ha); ha.offsetMin = new Vector2(26f, 0f); ha.offsetMax = new Vector2(-26f, 0f);
        var handle = Img(ha, "Handle", UIArt.BottleCap, Tomato); handle.rectTransform.sizeDelta = new Vector2(70f, 26f); handle.raycastTarget = true;
        var s = go.GetComponent<Slider>();
        s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
        s.transition = Selectable.Transition.None;
        s.SetValueWithoutNotify(value);
        s.onValueChanged.AddListener(v => onChange(v));
    }

    void SwitchRow(RectTransform panel, string label, float y, Func<bool> get, Action<bool> set)
    {
        var l = HandText(panel, label, 48, Ink, TextAnchor.MiddleLeft);
        Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(300f, y), new Vector2(320f, 70f));
        var box = Img(panel, "Check", UIArt.PaperCard, Paper, true); box.raycastTarget = true;
        Place(box.rectTransform, new Vector2(0f, 1f), new Vector2(520f, y), new Vector2(80f, 80f));
        InkOutline(box, 2.5f);
        var tick = Img(box.transform, "Tick", UIArt.Check, Sage);
        Place(tick.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
        var btn = box.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
        void Refresh() { tick.enabled = get(); }
        btn.onClick.AddListener(() => { set(!get()); AudioManager.PlayClick(); Refresh(); });
        Refresh();
    }

    void Dim(Transform parent)
    {
        var d = Img(parent, "Dim", null, new Color(0.08f, 0.05f, 0.03f, 0.55f)); Stretch(d.rectTransform); d.raycastTarget = true;
    }

    Text Txt(Transform parent, string s, Font f, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = f; t.fontSize = size; t.color = c; t.alignment = a; t.text = s;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    Text HandText(Transform parent, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter) => Txt(parent, s, handFont, size, c, a);

    static void InkOutline(Graphic g, float d)
    {
        var o = g.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.9f);
        o.effectDistance = new Vector2(d, -d);
    }

    static void AddShadow(Graphic g)
    {
        var s = g.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.55f);
        s.effectDistance = new Vector2(2f, -3f);
    }

    static Image Img(Transform parent, string name, Sprite s, Color c, bool sliced = false) => UIArt.Img(parent, name, s, c, sliced);
    static RectTransform Rt(Transform parent, string name) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
    static CanvasGroup Group(Transform parent, string name) { var rt = Rt(parent, name); Stretch(rt); return rt.gameObject.AddComponent<CanvasGroup>(); }
    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    static void Anchor(RectTransform rt, Vector2 min, Vector2 max) { rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size) { rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size; }
    void SetAlpha(Image i, float a) { if (i) { var c = i.color; c.a = a; i.color = c; } }

    void Show(CanvasGroup g, bool on, bool instant = false)
    {
        if (!g) return;
        g.interactable = on; g.blocksRaycasts = on;
        if (instant) { g.alpha = on ? 1f : 0f; g.gameObject.SetActive(on); return; }
        if (on) g.gameObject.SetActive(true);
        float from = g.alpha;
        StartCoroutine(Tween(0.22f, k => g.alpha = Mathf.Lerp(from, on ? 1f : 0f, k), () => { if (!on) g.gameObject.SetActive(false); }));
    }

    IEnumerator PopIn(RectTransform rt)
    {
        Vector3 baseScale = Vector3.one;
        rt.localScale = baseScale * 0.85f;
        yield return Tween(0.28f, k => rt.localScale = baseScale * Mathf.LerpUnclamped(0.85f, 1f, EaseOutBack(k)));
    }

    IEnumerator Tween(float dur, Action<float> f, Action done = null)
    {
        float t = 0f;
        while (t < dur) { t += Time.unscaledDeltaTime; f(Mathf.Clamp01(t / dur)); yield return null; }
        f(1f); done?.Invoke();
    }

    static float EaseOutBack(float x) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f); }
    static string FormatTime(int s) => (s / 60) + ":" + (s % 60).ToString("00");
}

// ---------------------------------------------------------------- touch widgets
// Floating joystick: appears where the thumb lands inside its zone.
public class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    RectTransform baseRT, knob, zone;
    float radius;
    Vector2 home;
    public Vector2 Value { get; private set; }
    public bool Active { get; private set; }

    public void Setup(RectTransform b, RectTransform k, float r) { baseRT = b; knob = k; radius = r; zone = (RectTransform)transform; home = b.anchoredPosition; }

    public void OnPointerDown(PointerEventData e)
    {
        Active = true;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, e.position, e.pressEventCamera, out var lp);
        baseRT.anchoredPosition = lp - zone.rect.min;
        OnDrag(e);
    }

    public void OnDrag(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRT, e.position, e.pressEventCamera, out var lp);
        Vector2 v = Vector2.ClampMagnitude(lp, radius);
        knob.anchoredPosition = v;
        Value = v / radius;
    }

    public void OnPointerUp(PointerEventData e)
    {
        Active = false; Value = Vector2.zero;
        knob.anchoredPosition = Vector2.zero;
        baseRT.anchoredPosition = home;
    }
}

// Drag anywhere on the right side to orbit the camera.
public class TouchLook : MonoBehaviour, IDragHandler
{
    public void OnDrag(PointerEventData e)
    {
        float scale = 1920f / Mathf.Max(1f, Mathf.Max(Screen.width, Screen.height));
        ThirdPersonCamera.LookDelta += e.delta * scale;
    }
}

// Reports press/release (for the RUN button).
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Action<bool> onChange;
    public void OnPointerDown(PointerEventData e) => onChange?.Invoke(true);
    public void OnPointerUp(PointerEventData e) => onChange?.Invoke(false);
    public void OnPointerExit(PointerEventData e) => onChange?.Invoke(false);
}
