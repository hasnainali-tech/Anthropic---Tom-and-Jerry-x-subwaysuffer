using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Score, high score, chase, countdown, pause, Game Over / Restart.
// One per scene; restart reloads the scene (AudioManager survives, so music keeps playing).
// The UI lives in GameUI (uGUI); this class only owns game state.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public PlayerController player;
    public Transform tom;

    [Header("Chaser look")]
    [Tooltip("On: the game's original orange tabby cat (animated). Off: use 'tomModel' below.")]
    public bool useOriginalCat = true;
    [Tooltip("Optional custom chaser model (used only when 'useOriginalCat' is off).")]
    public GameObject tomModel;
    public float tomHeight = 2.2f;
    [Tooltip("Extra Y rotation if the custom model faces the wrong way.")]
    public float tomYaw = 0f;
    [Tooltip("On-screen gap behind the mouse when the cat is about to catch it / when it is safe.")]
    public float tomNearOffset = 1.3f, tomFarOffset = 6.4f;

    [Header("Chase")]
    [Tooltip("Cat's distance behind the mouse when the mouse is safe.")]
    public float safeDistance = 9f;
    [Tooltip("How much closer the cat gets per trap hit.")]
    public float hitPenalty = 3.5f;
    [Tooltip("The cat catches the mouse at or below this distance.")]
    public float catchDistance = 1.5f;
    [Tooltip("Metres per second regained while running cleanly.")]
    public float recoverPerSecond = 0.4f;
    [Tooltip("Seconds of protection after a hit, so one trap can't hit twice.")]
    public float invulnerableTime = 1f;

    public enum State { Menu, Playing, GameOver, Countdown }
    public State Current { get; private set; } = State.Menu;
    public bool IsGameOver => Current == State.GameOver;
    public bool IsPlaying => Current == State.Playing;
    public bool IsPaused { get; private set; }
    public bool IsNewBest => Score > highScoreAtStart && Score > 0;

    // Restart skips the menu; "Home" shows it. Survives the scene reload.
    static bool skipMenuOnLoad;

    public int Score { get; private set; }
    public int HighScore { get; private set; }
    public float ChaseDistance => chaseDistance;
    public float Danger01 => 1f - Mathf.InverseLerp(catchDistance, safeDistance, chaseDistance);

    const string HighScoreKey = "HighScore";
    float chaseDistance;
    float invulnerableUntil;
    float gameOverTime;
    int highScoreAtStart;
    bool restarting, newBestAnnounced;

    Camera cam;
    CameraFollow follow;
    GameObject menuCheese;
    MouseHero hero;
    Transform tomBody;          // only for a custom model (simple bob)

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        highScoreAtStart = HighScore;
        Score = 0;
        chaseDistance = safeDistance;
    }

    void Start()
    {
        if (!player) player = FindFirstObjectByType<PlayerController>();
        if (player) hero = player.GetComponent<MouseHero>();
        if (!tom)
        {
            var t = GameObject.Find("Tom");
            tom = t ? t.transform : CreateTom();
        }
        SetupChaserVisual();
        cam = Camera.main;
        follow = cam ? cam.GetComponent<CameraFollow>() : null;
        PlaceTom(true);
        GameSettings.Apply();
        GameUI.Create(this);

        if (skipMenuOnLoad) { skipMenuOnLoad = false; BeginRun(false); }
        else EnterMenu();
    }

    // ---------------------------------------------------------------- menu
    void EnterMenu()
    {
        Current = State.Menu;
        AudioManager.Duck(false);
        if (player) player.IsRunning = false;
        if (follow) follow.enabled = false;
        var track = FindFirstObjectByType<TrackManager>();
        if (track && player)
        {
            var holder = new GameObject("MenuCheese").transform;
            holder.position = player.transform.position + new Vector3(-0.85f, 1.25f, 0.6f);
            track.MakeCheeseVisual(holder).transform.localScale *= 0.8f;
            menuCheese = holder.gameObject;
        }
        MenuCamera(true);
    }

    public void StartRun()
    {
        if (Current != State.Menu) return;
        BeginRun(true);
    }

    void BeginRun(bool fromMenu)
    {
        Current = State.Countdown;
        if (menuCheese) Destroy(menuCheese);
        if (follow)
        {
            follow.enabled = true;
            if (fromMenu && cam) follow.BlendFrom(cam.transform.position, cam.transform.rotation, 1.1f);
            else follow.SnapNow();
        }
        StartCoroutine(Countdown(fromMenu ? 0.75f : 0.25f));
    }

    IEnumerator Countdown(float delay)
    {
        yield return new WaitForSeconds(delay);
        foreach (var s in new[] { "3", "2", "1" })
        {
            GameUI.ShowCountdown(s, false);
            AudioManager.PlayBeep(false);
            yield return new WaitForSeconds(0.5f);
        }
        GameUI.ShowCountdown("GO!", true);
        AudioManager.PlayBeep(true);
        Current = State.Playing;
        if (player) player.IsRunning = true;
        if (TutorialActive)
        {
            yield return new WaitForSeconds(0.8f);
            if (IsPlaying) GameUI.Hint("SWIPE ← → TO CHANGE LANE");
        }
    }

    // ---------------------------------------------------------------- tutorial (first 2 runs)
    const string TutorialKey = "TutorialRuns";
    bool jumpHintShown, slideHintShown;
    bool TutorialActive => PlayerPrefs.GetInt(TutorialKey, 0) < 2;

    void UpdateTutorial()
    {
        if (!TutorialActive || !player || (jumpHintShown && slideHintShown)) return;
        float pz = player.transform.position.z;
        float look = 10f + player.Speed * 0.45f;                 // ~1 s of warning
        Trap nearest = null; float best = float.MaxValue;
        foreach (var t in Trap.Active)
        {
            if (!t) continue;
            float d = t.transform.position.z - pz;
            if (d > 1f && d < look && d < best) { best = d; nearest = t; }
        }
        if (!nearest) return;
        if (nearest.requiresSlide && !slideHintShown) { slideHintShown = true; GameUI.Hint("SWIPE ↓ TO SLIDE UNDER!"); }
        else if (!nearest.requiresSlide && !jumpHintShown) { jumpHintShown = true; GameUI.Hint("SWIPE ↑ TO JUMP OVER TRAPS!"); }
    }

    public void SetPaused(bool p)
    {
        if (Current != State.Playing && !(p == false && IsPaused)) return;
        if (IsPaused == p) return;
        IsPaused = p;
        Time.timeScale = p ? 0f : 1f;
        AudioManager.Duck(p);
        if (p) PlayerPrefs.Save();
    }

    public void BackToMenu()
    {
        if (restarting) return;
        restarting = true;
        skipMenuOnLoad = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Restart()
    {
        if (restarting) return;
        restarting = true;
        skipMenuOnLoad = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Quit()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Front-on hero shot: mouse in the foreground, the cat looming behind it.
    void MenuCamera(bool snap)
    {
        if (!cam || !player) return;
        Vector3 p = player.transform.position;
        float t = Time.time;
        bool portrait = Screen.height > Screen.width;
        float sway = Mathf.Sin(t * 0.3f) * 0.6f;
        Vector3 pos = p + (portrait ? new Vector3(0.7f + sway, 1.0f, 3.9f) : new Vector3(-1.6f + sway, 1.5f, 3.6f));
        Vector3 look = p + (portrait ? new Vector3(-0.35f, 0.32f, -2.2f) : new Vector3(-1.2f, 1.1f, -2.2f));
        float k = snap ? 1f : 1f - Mathf.Exp(-4f * Time.deltaTime);
        cam.transform.position = Vector3.Lerp(cam.transform.position, pos, k);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look - pos), k);
        if (follow) cam.fieldOfView = follow.MenuFov();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        PlayerPrefs.Save();
        if (IsPlaying) SetPaused(true);       // phone call / home button: pause, don't lose the run
    }

    // ---------------------------------------------------------------- events
    public void AddCheese()
    {
        if (!IsPlaying) return;
        Score++;
        if (Score > HighScore)
        {
            HighScore = Score;
            PlayerPrefs.SetInt(HighScoreKey, HighScore);
        }
        if (!newBestAnnounced && highScoreAtStart > 0 && Score == highScoreAtStart + 1)
        {
            newBestAnnounced = true;
            GameUI.Toast("NEW BEST!");
            AudioManager.PlayFanfare();
            if (player) GameFX.Confetti(player.transform.position + new Vector3(0f, 2.5f, 4f));
        }
    }

    public void TrapHit()
    {
        if (!IsPlaying || Time.time < invulnerableUntil) return;
        invulnerableUntil = Time.time + invulnerableTime;
        chaseDistance -= hitPenalty;
        AudioManager.PlayHit();
        GameSettings.Vibrate();
        GameUI.OnHit();
        CameraFollow.Shake(0.35f);
        if (player) GameFX.HitPuff(player.transform.position);
        if (hero) hero.Hit();
        if (chaseDistance <= catchDistance) GameOver();
        else StartCoroutine(Blink());
    }

    void GameOver()
    {
        if (!IsPlaying) return;
        Current = State.GameOver;
        if (player) player.IsRunning = false;
        PlayerPrefs.SetInt(HighScoreKey, HighScore);
        PlayerPrefs.SetInt(TutorialKey, PlayerPrefs.GetInt(TutorialKey, 0) + 1);
        PlayerPrefs.Save();
        gameOverTime = Time.unscaledTime;
        AudioManager.PlayLose();
        AudioManager.Duck(true);
        CameraFollow.Shake(0.6f);
    }

    // ---------------------------------------------------------------- loop
    void Update()
    {
        if (Current == State.Menu)
        {
            var k = Keyboard.current;
            if (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) StartRun();
            return;
        }
        if (IsGameOver)
        {
            if (Time.unscaledTime - gameOverTime > 1.2f && RestartPressed()) GameUI.FadeThen(Restart);
            return;
        }
        if (IsPlaying)
        {
            chaseDistance = Mathf.Min(safeDistance, chaseDistance + recoverPerSecond * Time.deltaTime);
            UpdateTutorial();
        }
    }

    void LateUpdate()
    {
        PlaceTom(false);
        if (Current == State.Menu) MenuCamera(false);
    }

    void PlaceTom(bool snap)
    {
        if (!tom || !player) return;
        Vector3 p = player.transform.position;
        // Chase logic is unchanged; the visual gap maps chaseDistance (catch..safe)
        // onto a range the camera can actually see.
        float k = Mathf.InverseLerp(catchDistance, safeDistance, chaseDistance);
        float gap = IsGameOver ? 0.6f : Mathf.Lerp(tomNearOffset, tomFarOffset, k);
        // the cat is too big for the mouse hole: it drops back at the wall (out of the low
        // camera's view) and catches up again in the next room
        if (!IsGameOver && Current != State.Menu) gap += TrackManager.CameraDip(p.z) * 8f;
        float targetZ = p.z - gap;
        // when close, run slightly off to the track-centre side so the cat never hides the mouse
        float side = p.x > 0.5f ? -1f : 1f;
        float targetX = p.x + (IsGameOver || Current == State.Menu ? 0f : side * 1.5f * (1f - k));
        if (Current == State.Menu) targetZ = p.z - 4.5f;               // menu shot: cat looming behind
        Vector3 t = tom.position;
        if (snap) t = new Vector3(targetX, 0f, targetZ);
        else
        {
            t.x = Mathf.Lerp(t.x, targetX, 5f * Time.deltaTime);
            t.z = Mathf.MoveTowards(t.z, targetZ, (player.Speed + 10f) * Time.deltaTime);
            t.y = 0f;
        }
        tom.position = t;
        tom.rotation = Quaternion.identity;                       // always faces +Z, toward the mouse

        // simple running bob for a custom (unanimated) chaser model
        if (tomBody)
        {
            bool running = IsPlaying;
            float bob = running ? Mathf.Abs(Mathf.Sin(Time.time * 11f)) * 0.12f : 0f;
            tomBody.localPosition = new Vector3(0f, bob, 0f);
            tomBody.localRotation = Quaternion.Euler(running ? 8f : 0f, tomYaw, running ? Mathf.Sin(Time.time * 11f) * 3f : 0f);
        }
    }

    void SetupChaserVisual()
    {
        var capsule = tom.Find("Visual");
        if (useOriginalCat || !tomModel)
        {
            if (capsule) capsule.gameObject.SetActive(false);
            var cat = tom.GetComponent<CatChaser>();
            if (!cat) cat = tom.gameObject.AddComponent<CatChaser>();
            cat.BuildModel();
            return;
        }
        if (capsule) capsule.gameObject.SetActive(false);

        var body = new GameObject("Body").transform;              // bob/lean pivot at the feet
        body.SetParent(tom, false);
        var model = Instantiate(tomModel, body);
        model.name = "ChaserModel";
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var r in model.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
        Bounds b = WorldBounds(model);
        if (b.size.y > 0.001f) model.transform.localScale = Vector3.one * (tomHeight / b.size.y);
        b = WorldBounds(model);
        model.transform.position += new Vector3(tom.position.x - b.center.x, tom.position.y - b.min.y, tom.position.z - b.center.z);
        tomBody = body;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static bool RestartPressed()
    {
        var k = Keyboard.current;
        return k != null && (k.rKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame);
    }

    IEnumerator Blink()
    {
        float end = invulnerableUntil;
        bool on = true;
        while (Time.time < end && !IsGameOver)
        {
            on = !on;
            if (hero) hero.SetVisible(on);
            yield return new WaitForSeconds(0.08f);
        }
        if (hero) hero.SetVisible(true);
    }

    Transform CreateTom()
    {
        var root = new GameObject("Tom").transform;
        return root;
    }
}
