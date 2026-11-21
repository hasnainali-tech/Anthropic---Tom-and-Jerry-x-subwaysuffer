using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Free-roam stealth/survival game state: the family is away, the cat owns the house,
// the mouse must find a way out. Owns hearts, hunger, inventory, notes, objectives,
// the noise system (what the cat can hear), catching/respawning and win/lose.
public class HouseGame : MonoBehaviour
{
    public static HouseGame I { get; private set; }

    public enum Phase { Menu, Intro, Playing, Caught, Won, Lost }
    public Phase phase = Phase.Menu;
    public bool Paused { get; private set; }

    [Header("Survival")]
    public int maxHearts = 3;
    public float hungerSeconds = 420f;      // full -> empty
    public int Hearts { get; private set; }
    public float Hunger { get; private set; } = 100f;
    public int CheeseEaten { get; private set; }
    public int TimesCaught { get; private set; }
    public float PlayTime { get; private set; }

    public MouseController mouse;
    public CatAI cat;
    public ThirdPersonCamera cam;
    public HouseBuilder house;

    // ---- food: carried in the mouse's arms, stockpiled in the den pantry ----
    public const int CarryLimit = 2;
    public const int PantryGoal = 8;
    public readonly List<string> Carried = new List<string>();
    public readonly List<string> Stored = new List<string>();

    // ---- inventory & notes ----
    readonly Dictionary<string, int> items = new Dictionary<string, int>();
    public readonly List<string> notesRead = new List<string>();
    public IReadOnlyDictionary<string, int> Items => items;
    public bool Has(string id) => !string.IsNullOrEmpty(id) && items.TryGetValue(id, out int n) && n > 0;
    public int Count(string id) => items.TryGetValue(id, out int n) ? n : 0;

    // ---- objectives (revealed by exploring; can be done in any order) ----
    public class Objective { public string id, text; public bool revealed, done; }
    public readonly List<Objective> objectives = new List<Objective>
    {
        new Objective { id = "out", text = "Find a way out of the house", revealed = true },
        new Objective { id = "pantry", text = "Stock your pantry with food", revealed = true },
        new Objective { id = "storage", text = "Get into the locked storage room" },
        new Objective { id = "padlock", text = "Find the padlock key" },
        new Objective { id = "backdoor", text = "Unlock the kitchen back door" },
        new Objective { id = "escape", text = "Escape through the garden fence!" },
    };
    // the escape goal currently in focus (the pantry is tracked separately)
    public Objective CurrentObjective
    {
        get { foreach (var o in objectives) if (o.revealed && !o.done && o.id != "out" && o.id != "pantry") return o; return objectives[0]; }
    }

    // ---- noise: what the cat can hear ----
    public static event Action<Vector3, float, bool> Noise;     // position, radius, lure (TV/marble)
    public static void MakeNoise(Vector3 pos, float radius, bool lure = false) => Noise?.Invoke(pos, radius, lure);

    float starveTimer;

    void Awake()
    {
        I = this;
        Time.timeScale = 1f;
        Hearts = maxHearts;
    }

    void OnDestroy() { if (I == this) I = null; }

    void Start()
    {
        house = GetComponent<HouseBuilder>();
        house.Build();
        SpawnCharacters();
        GameSettings.Apply();
        HouseUI.Create(this);
        EnterMenu();
    }

    void SpawnCharacters()
    {
        // mouse: character controller + the animated original mouse model
        var m = new GameObject("Mouse");
        m.AddComponent<CharacterController>();
        mouse = m.AddComponent<MouseController>();
        m.AddComponent<MouseHero>();
        m.AddComponent<BlobShadow>().size = 1.0f;
        mouse.Teleport(house.MenuMouse.position, house.MenuMouse.eulerAngles.y);

        // cat: navmesh agent + AI + the animated original cat model
        var c = new GameObject("Cat");
        c.transform.position = house.CatSpawn.position;
        var body = c.AddComponent<CatChaser>();
        body.BuildModel();
        cat = c.AddComponent<CatAI>();
        cat.Init(this, mouse, house);
        c.AddComponent<BlobShadow>().size = 2.2f;

        var camGo = Camera.main ? Camera.main.gameObject : new GameObject("Main Camera") { tag = "MainCamera" };
        if (!camGo.GetComponent<Camera>()) camGo.AddComponent<Camera>();
        cam = camGo.GetComponent<ThirdPersonCamera>();
        if (!cam) cam = camGo.AddComponent<ThirdPersonCamera>();
        cam.target = mouse;
        cam.enabled = false;
    }

    // ---------------------------------------------------------------- flow
    void EnterMenu()
    {
        phase = Phase.Menu;
        mouse.InputEnabled = false;
        mouse.IsWaving = true;
        AudioManager.Duck(false);
    }

    void Update()
    {
        if (phase == Phase.Menu) MenuCamera();
        if (phase != Phase.Playing || Paused) return;
        float dt = Time.deltaTime;
        PlayTime += dt;

        // hunger: slowly drains; starving costs a heart every 40 s
        Hunger = Mathf.Max(0f, Hunger - 100f / hungerSeconds * dt);
        if (Hunger <= 0f)
        {
            starveTimer += dt;
            if (starveTimer > 40f) { starveTimer = 0f; LoseHeart("You're starving! Eat some cheese."); }
        }
        else starveTimer = 0f;

        // the storage room counts as "got in" however you entered (key or secret tunnel)
        if (!Done("storage") && house.RoomAt(mouse.transform.position) == "Storage Room") Complete("storage", "You're in the storage room!");
    }

    void MenuCamera()
    {
        var c = Camera.main;
        if (!c || !house) return;
        var shot = house.MenuShot;
        float s = Mathf.Sin(Time.time * 0.25f) * 0.6f;
        c.transform.SetPositionAndRotation(shot.position + shot.right * s, shot.rotation);
        c.fieldOfView = Screen.height > Screen.width ? 72f : 55f;
    }

    public void StartGame()
    {
        if (phase != Phase.Menu) return;
        phase = Phase.Intro;
        mouse.IsWaving = false;
        var cs = GetComponent<HouseCutscenes>();
        if (!cs) cs = gameObject.AddComponent<HouseCutscenes>();
        cs.PlayIntro(this, () => { HouseUI.IntroFinished(); BeginPlay(true); });
    }

    // called by HouseUI when the intro story cards are done
    public void BeginPlay(bool afterIntro = false)
    {
        if (phase == Phase.Playing) return;
        phase = Phase.Playing;
        mouse.ScriptedTarget = null;
        if (!afterIntro) mouse.Teleport(house.MouseSpawn.position, house.MouseSpawn.eulerAngles.y);   // in the den, facing the hole
        mouse.InputEnabled = true;
        cam.enabled = true;
        cam.SnapBehind();
        if (afterIntro) cat.StartRoutineAwake(); else cat.StartRoutine();
        HouseUI.Message("Gather food for your pantry - and find the way to the garden.");
    }

    public void SetPaused(bool p)
    {
        if (phase != Phase.Playing && !(Paused && !p)) return;
        Paused = p;
        Time.timeScale = p ? 0f : 1f;
        AudioManager.Duck(p);
        if (p) PlayerPrefs.Save();
    }

    void OnApplicationPause(bool paused) { if (paused && phase == Phase.Playing) SetPaused(true); }

    // ---------------------------------------------------------------- items
    public void Give(string id, int amount = 1)
    {
        if (id.StartsWith("food:"))
        {
            string type = id.Substring(5);
            if (Carried.Count >= CarryLimit) { HouseUI.Message("Your arms are full! Eat something or take it home."); return; }
            Carried.Add(type);
            AudioManager.PlayClick();
            HouseUI.Message("Picked up " + FoodName(type) + ". Take it home to your pantry - or eat it now.");
            HouseUI.CarryChanged();
            return;
        }
        switch (id)
        {
            case "cheese":
                CheeseEaten++;
                Hunger = Mathf.Min(100f, Hunger + 28f);
                AudioManager.PlayChew();
                HouseUI.Message("Yum! Hunger restored.");
                return;
            case "cheese_wheel":
                CheeseEaten++;
                Hunger = 100f;
                Hearts = Mathf.Min(maxHearts, Hearts + 1);
                AudioManager.PlayChew();
                AudioManager.PlayFanfare();
                HouseUI.Message("A whole cheese wheel! Full and healed.");
                return;
        }
        if (id.StartsWith("note:"))
        {
            string note = id.Substring(5);
            if (!notesRead.Contains(note)) notesRead.Add(note);
            HouseUI.ShowNote(note);
            OnNoteRead(note);
            return;
        }
        items[id] = Count(id) + amount;
        AudioManager.PlayFanfare();
        HouseUI.Message("Found: " + ItemName(id) + (amount > 1 ? " x" + amount : "") + "!");
        if (id == "padlock_key") { Complete("storage", null); Reveal("padlock"); Complete("padlock", null); Reveal("backdoor"); }
        if (id == "storage_key") Reveal("storage");
    }

    public void UseMarble()
    {
        if (!Has("marble") || phase != Phase.Playing) return;
        items["marble"]--;
        var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        b.name = "Marble";
        b.transform.localScale = Vector3.one * 0.28f;
        var art = GameArt.Get();
        var r = b.GetComponent<Renderer>();
        r.sharedMaterial = art && art.metal ? art.metal : r.sharedMaterial;
        Vector3 aim = cam ? cam.transform.forward : mouse.transform.forward;
        aim = Vector3.ProjectOnPlane(aim, Vector3.up).normalized;
        b.transform.position = mouse.transform.position + Vector3.up * 0.9f + aim * 0.5f;
        var rb = b.AddComponent<Rigidbody>();
        rb.mass = 0.1f;
        rb.linearVelocity = aim * 11f + Vector3.up * 5f;
        b.AddComponent<Throwable>();
        AudioManager.PlayWhoosh();
    }

    // ---------------------------------------------------------------- food
    public static string FoodName(string type) => type switch
    {
        "cheese" => "a cheese wedge", "cookie" => "a cookie", "bread" => "a bread crust",
        "grapes" => "some grapes", "peanut" => "a peanut", _ => type,
    };
    static float FoodValue(string type) => type == "cheese" ? 30f : type == "bread" ? 26f : 22f;

    public void EatCarried(int index)
    {
        if (index < 0 || index >= Carried.Count || phase != Phase.Playing) return;
        string type = Carried[index];
        Carried.RemoveAt(index);
        Eat(type);
        HouseUI.CarryChanged();
    }

    public void StoreCarried()
    {
        if (Carried.Count == 0) return;
        int n = Carried.Count;
        Stored.AddRange(Carried);
        Carried.Clear();
        house.PantryBox.Refresh(Stored);
        AudioManager.PlayFanfare();
        HouseUI.Message("Stored " + n + " in the pantry (" + Stored.Count + "/" + PantryGoal + ").");
        if (Stored.Count >= PantryGoal) Complete("pantry", "The pantry is full - you're ready for anything!");
        HouseUI.CarryChanged();
        HouseUI.ObjectiveChanged();
    }

    public void EatFromPantry()
    {
        if (Stored.Count == 0) return;
        string type = Stored[Stored.Count - 1];
        Stored.RemoveAt(Stored.Count - 1);
        house.PantryBox.Refresh(Stored);
        Eat(type);
        HouseUI.CarryChanged();
        HouseUI.ObjectiveChanged();
    }

    void Eat(string type)
    {
        CheeseEaten++;
        Hunger = Mathf.Min(100f, Hunger + FoodValue(type));
        AudioManager.PlayChew();
        HouseUI.Message("Munch munch... " + FoodName(type) + ". Hunger restored.");
    }

    public void OnTrapSnapped(MouseController m)
    {
        Hunger = Mathf.Max(0f, Hunger - 20f);
        HouseUI.Message("SNAP! That hurt - and the cat definitely heard it!");
        HouseUI.CaughtFlash();
        GameSettings.Vibrate();
        StartCoroutine(Stun(m, 1.2f));
    }

    IEnumerator Stun(MouseController m, float t)
    {
        m.InputEnabled = false;
        m.IsScared = true;
        yield return new WaitForSeconds(t);
        if (phase == Phase.Playing) { m.InputEnabled = true; m.IsScared = false; }
    }

    public int Stars => phase != Phase.Won ? 0 : Stored.Count >= PantryGoal ? 3 : Stored.Count >= PantryGoal / 2 ? 2 : 1;

    public static string ItemName(string id) => id switch
    {
        "marble" => "Marble",
        "storage_key" => "Storage Room Key",
        "padlock_key" => "Padlock Key",
        _ => id,
    };

    // ---------------------------------------------------------------- objectives
    public bool Done(string id) => objectives.Find(o => o.id == id) is Objective o && o.done;

    public void Reveal(string id)
    {
        var o = objectives.Find(x => x.id == id);
        if (o != null && !o.revealed) { o.revealed = true; HouseUI.ObjectiveChanged(); }
    }

    public void Complete(string id, string msg)
    {
        var o = objectives.Find(x => x.id == id);
        if (o == null || o.done) return;
        o.revealed = true; o.done = true;
        if (!string.IsNullOrEmpty(msg)) HouseUI.Message(msg);
        HouseUI.ObjectiveChanged();
    }

    void OnNoteRead(string note)
    {
        if (note == "spare_key") Reveal("storage");
        if (note == "padlock") { Reveal("padlock"); Reveal("backdoor"); }
    }

    public void OnUnlocked(Door d)
    {
        if (d.keyId == "padlock_key") { Complete("backdoor", "The back door is open - run for the fence!"); Reveal("escape"); }
        if (d.keyId == "storage_key") Reveal("storage");
    }

    public void OnLockedDoorTried(Door d)
    {
        if (d.keyId == "padlock_key") { Reveal("padlock"); Reveal("backdoor"); }
        if (d.keyId == "storage_key") Reveal("storage");
    }

    // ---------------------------------------------------------------- caught / win / lose
    public void OnCaught()
    {
        if (phase != Phase.Playing) return;
        TimesCaught++;
        StartCoroutine(CaughtRoutine());
    }

    IEnumerator CaughtRoutine()
    {
        phase = Phase.Caught;
        mouse.InputEnabled = false;
        mouse.IsScared = true;
        mouse.moveInput = Vector2.zero;
        AudioManager.PlayHit();
        GameSettings.Vibrate();
        ThirdPersonCamera.Shake(0.6f);
        HouseUI.CaughtFlash();
        Hearts--;
        yield return new WaitForSeconds(1.6f);
        if (Hearts <= 0) { Lose(); yield break; }
        HouseUI.Message("Caught! " + Hearts + (Hearts == 1 ? " life" : " lives") + " left. Back to your hole...");
        mouse.IsScared = false;
        if (mouse.Hiding) mouse.ExitHide();
        mouse.CrouchToggled = false;
        mouse.Teleport(house.MouseSpawn.position, house.MouseSpawn.eulerAngles.y);
        cat.ResetAfterCatch();
        cam.SnapBehind();
        yield return new WaitForSeconds(0.4f);
        mouse.InputEnabled = true;
        phase = Phase.Playing;
    }

    void LoseHeart(string why)
    {
        Hearts--;
        HouseUI.Message(why);
        HouseUI.CaughtFlash();
        if (Hearts <= 0) Lose();
    }

    void Lose()
    {
        phase = Phase.Lost;
        mouse.InputEnabled = false;
        AudioManager.PlayLose();
        AudioManager.Duck(true);
        HouseUI.ShowEnd(false);
    }

    public void Win()
    {
        if (phase != Phase.Playing) return;
        phase = Phase.Won;
        Complete("escape", null);
        Complete("out", null);
        mouse.InputEnabled = false;
        AudioManager.PlayFanfare();
        int best = PlayerPrefs.GetInt("BestEscapeTime", 0);
        if (best == 0 || PlayTime < best) PlayerPrefs.SetInt("BestEscapeTime", Mathf.CeilToInt(PlayTime));
        PlayerPrefs.SetInt("BestStars", Mathf.Max(PlayerPrefs.GetInt("BestStars", 0), Stars));
        PlayerPrefs.Save();
        var cs = GetComponent<HouseCutscenes>();
        if (!cs) cs = gameObject.AddComponent<HouseCutscenes>();
        cam.enabled = false;
        cs.PlayEnding(this, () => HouseUI.ShowEnd(true));
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
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
}
