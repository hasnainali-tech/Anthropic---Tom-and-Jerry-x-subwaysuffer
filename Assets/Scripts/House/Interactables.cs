using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Everything the mouse can use. The nearest usable one in front of the mouse is shown as a
// contextual button by HouseUI ("OPEN", "SEARCH", "HIDE", "TAKE"...).
public abstract class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new List<Interactable>();
    public float range = 2.0f;
    public virtual string Prompt => "USE";
    public virtual Vector3 Point => transform.position;
    public virtual bool CanInteract(MouseController m) => true;
    public abstract void Interact(MouseController m);

    protected virtual void OnEnable() => All.Add(this);
    protected virtual void OnDisable() => All.Remove(this);

    // nearest usable interactable roughly in front of the mouse
    public static Interactable Find(MouseController m)
    {
        Interactable best = null; float bestScore = float.MaxValue;
        Vector3 p = m.transform.position + Vector3.up * 0.4f, fwd = m.transform.forward;
        foreach (var i in All)
        {
            if (!i || !i.isActiveAndEnabled || !i.CanInteract(m)) continue;
            Vector3 d = i.Point - p;
            float flat = new Vector3(d.x, 0f, d.z).magnitude;
            if (flat > i.range || Mathf.Abs(d.y) > i.range + 1f) continue;
            float score = flat - Vector3.Dot(fwd, new Vector3(d.x, 0f, d.z).normalized) * 0.6f;
            if (score < bestScore) { bestScore = score; best = i; }
        }
        return best;
    }
}

// ---------------------------------------------------------------- doors
public class Door : Interactable
{
    public Transform hinge;           // rotates around Y
    public string keyId;              // empty = never locked
    public string keyName;
    public bool locked;
    public bool open;
    public float openAngle = 95f;
    public bool forCatToo = true;     // the cat can push it open
    public UnityEngine.AI.NavMeshObstacle blocker;   // carves the navmesh while locked
    float angle;

    public override string Prompt => locked ? (HouseGame.I && HouseGame.I.Has(keyId) ? "UNLOCK" : "LOCKED") : open ? "CLOSE" : "OPEN";
    public override Vector3 Point => hinge ? hinge.position + hinge.right * 2f + Vector3.up * 0.5f : transform.position;

    public override void Interact(MouseController m)
    {
        if (locked)
        {
            if (HouseGame.I.Has(keyId))
            {
                locked = false;
                if (blocker) blocker.enabled = false;
                HouseGame.I.OnUnlocked(this);
                HouseUI.Message("Unlocked with the " + keyName + "!");
                AudioManager.PlayClick();
                SetOpen(true);
            }
            else
            {
                HouseUI.Message("Locked. You need the " + keyName + ".");
                HouseGame.I.OnLockedDoorTried(this);
                HouseGame.MakeNoise(transform.position, 3f);
            }
            return;
        }
        SetOpen(!open);
    }

    public void SetOpen(bool value, bool quiet = false)
    {
        if (locked && value) return;
        if (open == value) return;
        open = value;
        if (quiet) return;                         // the cat pushing doors doesn't alert itself
        HouseGame.MakeNoise(Point, 7f);            // creaky door - the cat may hear it
        AudioManager.PlayWhoosh();
    }

    void Update()
    {
        if (!hinge) return;
        angle = Mathf.MoveTowards(angle, open ? openAngle : 0f, 160f * Time.deltaTime);
        hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
    }
}

// ---------------------------------------------------------------- searchable furniture
public class Container : Interactable
{
    public string label = "cupboard";
    public string itemId;             // what is inside (may be empty)
    public int amount = 1;
    public bool searched;
    public Transform lid;             // optional part that swings open
    public Vector3 lidOpenEuler = new Vector3(0f, -80f, 0f);

    public override string Prompt => "SEARCH";
    public override bool CanInteract(MouseController m) => !searched;

    public override void Interact(MouseController m) => StartCoroutine(Search(m));

    IEnumerator Search(MouseController m)
    {
        searched = true;
        m.InputEnabled = false;
        HouseUI.Progress("Searching the " + label + "...", 1.1f);
        HouseGame.MakeNoise(transform.position, 5f);   // rummaging is noisy
        float t = 0f;
        Quaternion from = lid ? lid.localRotation : Quaternion.identity, to = Quaternion.Euler(lidOpenEuler);
        while (t < 1.1f)
        {
            t += Time.deltaTime;
            if (lid) lid.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.5f));
            yield return null;
        }
        m.InputEnabled = true;
        if (string.IsNullOrEmpty(itemId)) HouseUI.Message("Nothing useful in the " + label + ".");
        else
        {
            HouseGame.I.Give(itemId, amount);
            GameFX.CheesePop(transform.position + Vector3.up * 1.5f);
        }
    }
}

// ---------------------------------------------------------------- loose items
public class Pickup : Interactable
{
    public string itemId;             // "cheese", "marble", "storage_key", "padlock_key", "note:<id>", "cheese_wheel"
    public int amount = 1;
    public float bob = 0.08f;
    Vector3 basePos;
    float phase;

    public override string Prompt => itemId.StartsWith("note") ? "READ" : itemId.StartsWith("food") ? "PICK UP" : "TAKE";
    public override bool CanInteract(MouseController m) => !itemId.StartsWith("food") || (HouseGame.I && HouseGame.I.Carried.Count < HouseGame.CarryLimit);
    public override Vector3 Point => basePos;

    void Start() { basePos = transform.position; phase = basePos.x * 1.7f + basePos.z; }

    void Update()
    {
        // gentle float, never spins
        transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2.1f + phase) * bob);
    }

    public override void Interact(MouseController m)
    {
        HouseGame.I.Give(itemId, amount);
        if (itemId.StartsWith("food") || itemId == "cheese_wheel") GameFX.CheesePop(transform.position + Vector3.up * 0.3f);
        gameObject.SetActive(false);
    }
}

// ---------------------------------------------------------------- hiding places
public class HideSpot : Interactable
{
    public string label = "under the bed";
    public Transform HidePoint, ExitPoint, CameraPoint;
    public override string Prompt => "HIDE";
    public override Vector3 Point => ExitPoint ? ExitPoint.position : transform.position;
    public override bool CanInteract(MouseController m) => !m.Hiding;
    public override void Interact(MouseController m)
    {
        m.EnterHide(this);
        HouseUI.Message("Hiding " + label + ". Stay quiet...");
    }
}

// ---------------------------------------------------------------- TV (distraction)
public class TVSet : Interactable
{
    public Renderer screen;
    public bool on;
    float noiseTimer;
    Material mat;

    public override string Prompt => on ? "TV OFF" : "TV ON";
    public override void Interact(MouseController m)
    {
        on = !on;
        HouseUI.Message(on ? "The TV is loud - the cat will come to watch it." : "TV off.");
        AudioManager.PlayClick();
    }

    void Update()
    {
        if (!screen) return;
        if (!mat) mat = screen.material;
        Color c = Color.black;
        if (on)
        {
            float t = Time.time;
            c = new Color(0.35f + 0.35f * Mathf.PerlinNoise(t * 2f, 0f), 0.45f + 0.3f * Mathf.PerlinNoise(0f, t * 1.7f), 0.7f + 0.3f * Mathf.PerlinNoise(t, t)) * 1.4f;
            noiseTimer -= Time.deltaTime;
            if (noiseTimer <= 0f) { noiseTimer = 2.5f; HouseGame.MakeNoise(transform.position, 30f, true); }
        }
        mat.SetColor("_BaseColor", c);
    }
}

// ---------------------------------------------------------------- the den pantry
// STORE what you're carrying; EAT from the stockpile later when hungry.
public class Pantry : Interactable
{
    public readonly List<Vector3> slots = new List<Vector3>();
    readonly List<GameObject> shown = new List<GameObject>();

    public override string Prompt => HouseGame.I && HouseGame.I.Carried.Count > 0 ? "STORE" : "EAT";
    public override bool CanInteract(MouseController m)
    {
        var g = HouseGame.I;
        return g && (g.Carried.Count > 0 || (g.Stored.Count > 0 && g.Hunger < 95f));
    }

    public override void Interact(MouseController m)
    {
        var g = HouseGame.I;
        if (g.Carried.Count > 0) g.StoreCarried();
        else g.EatFromPantry();
    }

    // show the stored food inside the crate
    public void Refresh(List<string> stored)
    {
        foreach (var s in shown) if (s) Destroy(s);
        shown.Clear();
        var builder = HouseGame.I ? HouseGame.I.house : null;
        if (!builder) return;
        for (int i = 0; i < stored.Count && i < slots.Count; i++)
        {
            var holder = new GameObject("Stored");
            holder.transform.SetParent(transform, false);
            holder.transform.position = slots[i];
            var model = builder.FoodModel(stored[i], holder.transform);
            model.transform.localScale = Vector3.one * 0.75f;
            shown.Add(holder);
        }
    }
}

// ---------------------------------------------------------------- baited mousetrap
// Sneak (crouch) to lift the bait safely. Run into it, or grab it standing up: SNAP!
public class MouseTrap : Interactable
{
    public Transform bar, bait;
    bool armed = true;
    float snapAnim = -1f;

    public override string Prompt => "STEAL BAIT";
    public override bool CanInteract(MouseController m) => armed;

    public override void Interact(MouseController m)
    {
        if (!armed) return;
        if (m.IsCrouching)
        {
            armed = false;
            if (bait) bait.gameObject.SetActive(false);
            HouseGame.I.Give("food:cheese");
            HouseUI.Message("Carefully... got the bait without a sound!");
        }
        else Snap(m);
    }

    void OnTriggerEnter(Collider other)
    {
        var m = other.GetComponentInParent<MouseController>();
        if (m && armed && !m.IsCrouching && m.IsMoving) Snap(m);
    }

    void Snap(MouseController m)
    {
        armed = false;
        snapAnim = 0f;
        if (bait) bait.gameObject.SetActive(false);
        HouseGame.MakeNoise(transform.position, 15f, true);
        AudioManager.PlayHit();
        ThirdPersonCamera.Shake(0.4f);
        GameFX.HitPuff(transform.position);
        HouseGame.I.OnTrapSnapped(m);
    }

    void Update()
    {
        if (snapAnim < 0f || !bar) return;
        snapAnim += Time.deltaTime * 6f;
        bar.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -175f, Mathf.Clamp01(snapAnim)), 0f, 0f);
        if (snapAnim >= 1f) snapAnim = -1f;
    }
}

// ---------------------------------------------------------------- knockable counter items
// The mouse can PUSH them off a counter (a crash the cat will go and check); the cat knocks
// them off for fun on its rounds.
public class Knockable : Interactable
{
    public bool knocked;
    public override string Prompt => "PUSH";
    public override bool CanInteract(MouseController m) => !knocked && m.transform.position.y > 1f;   // only from up on the counter

    public override void Interact(MouseController m)
    {
        Vector3 dir = Vector3.ProjectOnPlane(transform.position - m.transform.position, Vector3.up).normalized;
        Knock(dir, true);
    }

    public void Knock(Vector3 dir, bool byMouse)
    {
        if (knocked) return;
        knocked = true;
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.4f;
        rb.linearVelocity = dir * 3.5f + Vector3.up * 2f;
        rb.angularVelocity = Random.insideUnitSphere * 6f;
        var crash = gameObject.AddComponent<CrashOnLand>();
        crash.lure = byMouse;
        if (byMouse) HouseUI.Message("Pushed it off the edge...");
    }
}

public class CrashOnLand : MonoBehaviour
{
    public bool lure;
    bool done;
    void OnCollisionEnter(Collision c)
    {
        if (done || c.relativeVelocity.magnitude < 1.5f) return;
        done = true;
        HouseGame.MakeNoise(transform.position, lure ? 18f : 8f, lure);
        AudioManager.PlayHit();
        GameFX.HitPuff(transform.position);
    }
}

// little twinkle above collectibles (instead of floating them)
public class Glint : MonoBehaviour
{
    float phase;
    Camera cam;
    void Start() { phase = Random.value * 3f; transform.localScale = Vector3.zero; }
    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        if (cam) transform.rotation = cam.transform.rotation;
        float t = Mathf.Repeat(Time.time * 0.45f + phase, 1f);
        float s = t < 0.25f ? Mathf.Sin(t / 0.25f * Mathf.PI) * 0.7f : 0f;
        transform.localScale = Vector3.one * s;
    }
}

// ---------------------------------------------------------------- escape
public class ExitZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<MouseController>() && HouseGame.I) HouseGame.I.Win();
    }
}

// thrown marble: noise where it lands (distraction)
public class Throwable : MonoBehaviour
{
    bool landed;
    void OnCollisionEnter(Collision c)
    {
        if (landed) return;
        landed = true;
        HouseGame.MakeNoise(transform.position, 16f, true);
        GameFX.HitPuff(transform.position);
        Destroy(gameObject, 6f);
    }
}
