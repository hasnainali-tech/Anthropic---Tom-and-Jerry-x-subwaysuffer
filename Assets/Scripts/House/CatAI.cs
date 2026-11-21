using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// The cat: an independent resident of the house (NavMesh agent + state machine).
// Routine: sleep in its bed, watch TV, eat at its bowl, patrol rooms.
// Senses: vision (cone + line of sight, never through walls; crouching/hiding helps),
// whiskers (very close), hearing (footsteps, doors, rummaging, thrown marbles, TV).
// Suspicion builds before a chase; lost sight -> goes to the last known position and
// searches (sometimes checks hiding spots) -> returns to its routine.
public class CatAI : MonoBehaviour
{
    public enum State { Idle, Sleeping, Patrolling, Activity, HearNoise, Investigating, Searching, Detecting, Chasing, Catching, Returning, Ambush, Stretching, Knocking }
    public State state { get; private set; } = State.Sleeping;

    public float walkSpeed = 3.4f, investigateSpeed = 4.6f, chaseSpeed = 7.0f;
    public float viewDistance = 16f, viewAngle = 115f;

    public float Suspicion01 => suspicion;
    public bool IsChasing => state == State.Chasing || state == State.Catching;

    HouseGame game; MouseController mouse; HouseBuilder house;
    NavMeshAgent agent; CatChaser body;
    Text indicator;

    float suspicion, stateTimer, senseTimer, doorTimer, lostTimer, lastSeenTime = -99f, repathTimer;
    Vector3 lastKnown, investigatePos, activityPos;
    float activityYaw; string activity = "";
    readonly Queue<Vector3> route = new Queue<Vector3>();
    int searchStops;
    HideSpot checkingSpot;
    bool sees;

    // ---------------------------------------------------------------- setup
    public void Init(HouseGame g, MouseController m, HouseBuilder h)
    {
        game = g; mouse = m; house = h;
        body = GetComponent<CatChaser>();
        agent = gameObject.AddComponent<NavMeshAgent>();
        agent.radius = 0.55f; agent.height = 2.1f; agent.speed = walkSpeed;
        agent.angularSpeed = 600f; agent.acceleration = 25f; agent.stoppingDistance = 0.35f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        agent.Warp(transform.position);
        BuildIndicator();
        GoSleep(9999f);   // asleep in its bed during the menu
    }

    void OnEnable() => HouseGame.Noise += OnNoise;
    void OnDisable() => HouseGame.Noise -= OnNoise;

    public void StartRoutine() => GoSleep(Random.Range(14f, 22f));

    // after the intro the cat is awake and hungry: off to its bowl first
    public void StartRoutineAwake()
    {
        graceUntil = Time.time + 4f;
        StartActivity("eat", house.FoodSpot, 200f, 10f);
    }

    // After a catch the cat is satisfied: it goes off to the far side of the house and
    // ignores the mouse for a while, so a respawn is never an instant re-catch.
    public void ResetAfterCatch()
    {
        suspicion = 0f;
        graceUntil = Time.time + 8f;
        Vector3 far = new Vector3(12f, 0f, 37f);   // bedroom, far from the mouse's home hole
        if (NavMesh.SamplePosition(far, out var hit, 3f, NavMesh.AllAreas)) agent.Warp(hit.position);
        StartPatrol();
    }
    float graceUntil;

    void BuildIndicator()
    {
        var go = new GameObject("Indicator", typeof(Canvas));
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 2.9f, 0f);
        var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(300f, 200f); rt.localScale = Vector3.one * 0.008f;
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(go.transform, false);
        ((RectTransform)t.transform).sizeDelta = new Vector2(300f, 200f);
        var art = GameArt.Get();
        t.font = art && art.titleFont ? art.titleFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 130; t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.2f, 0.08f, 0.02f); o.effectDistance = new Vector2(6f, -6f);
        go.AddComponent<Billboard>();
        indicator = t;
    }

    // ---------------------------------------------------------------- update
    void Update()
    {
        if (!game || game.phase != HouseGame.Phase.Playing || game.Paused) { UpdateIndicator(); ApplyPose(); return; }
        float dt = Time.deltaTime;
        stateTimer -= dt;

        senseTimer -= dt;
        if (senseTimer <= 0f) { senseTimer = 0.1f; sees = CanSeeMouse(out _); if (sees) { lastKnown = mouse.transform.position; lastSeenTime = Time.time; } }
        doorTimer -= dt;
        if (doorTimer <= 0f) { doorTimer = 0.25f; OpenNearbyDoors(); }

        UpdateSuspicion(dt);

        switch (state)
        {
            case State.Sleeping: if (stateTimer <= 0f) { state = State.Stretching; stateTimer = 1.8f; } break;   // wake up with a big stretch
            case State.Stretching: if (stateTimer <= 0f) NextRoutine(); break;
            case State.Idle: if (stateTimer <= 0f) NextRoutine(); break;
            case State.Patrolling: TickPatrol(); TryKnockSomething(); break;
            case State.Knocking: TickKnock(); break;
            case State.Ambush: TickAmbush(); break;
            case State.Activity: TickActivity(); break;
            case State.Returning: if (Arrived()) NextRoutine(); break;
            case State.HearNoise: FaceTowards(investigatePos); if (stateTimer <= 0f) GoInvestigate(investigatePos); break;
            case State.Investigating: if (Arrived() || stateTimer <= 0f) StartSearch(investigatePos); break;
            case State.Detecting: TickDetecting(); break;
            case State.Searching: TickSearch(); break;
            case State.Chasing: TickChase(dt); break;
            case State.Catching: break;
        }
        UpdateIndicator();
        ApplyPose();
    }

    // ---------------------------------------------------------------- senses
    bool CanSeeMouse(out float dist)
    {
        Vector3 eye = transform.position + Vector3.up * 1.9f;
        Vector3 target = mouse.transform.position + Vector3.up * (mouse.IsCrouching ? 0.25f : 0.5f);
        dist = Vector3.Distance(transform.position, mouse.transform.position);
        if (mouse.IsHidden || state == State.Sleeping || Time.time < graceUntil) return false;
        if (dist < 1.6f && Mathf.Abs(mouse.transform.position.y - transform.position.y) < 1.5f) return true;   // whiskers
        float range = viewDistance * (mouse.IsCrouching ? 0.55f : 1f) * (IsChasing ? 1.4f : 1f);
        if (dist > range) return false;
        Vector3 to = target - eye;
        if (Vector3.Angle(transform.forward, new Vector3(to.x, 0f, to.z)) > viewAngle * 0.5f && !IsChasing) return false;
        // line of sight: walls, doors and furniture block it
        if (Physics.Linecast(eye, target, out var hit, ~0, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(mouse.transform)) return false;
        return true;
    }

    void UpdateSuspicion(float dt)
    {
        if (state == State.Chasing || state == State.Catching) { suspicion = 1f; return; }
        if (sees)
        {
            float d = Vector3.Distance(transform.position, mouse.transform.position);
            float rate = (0.7f + 2.6f * (1f - Mathf.Clamp01(d / viewDistance))) * (mouse.IsCrouching ? 0.45f : 1f) * (mouse.IsMoving ? 1f : 0.6f);
            suspicion = Mathf.Min(1f, suspicion + rate * dt);
            if (suspicion >= 1f) StartChase();
            else if (suspicion > 0.3f && state != State.Detecting) { state = State.Detecting; stateTimer = 2.5f; agent.ResetPath(); }
        }
        else suspicion = Mathf.Max(0f, suspicion - 0.25f * dt);
    }

    void OnNoise(Vector3 pos, float radius, bool lure)
    {
        if (!game || game.phase != HouseGame.Phase.Playing || IsChasing || Time.time < graceUntil) return;
        float d = Vector3.Distance(transform.position, pos);
        float heard = radius * (state == State.Sleeping ? 0.55f : 1f);
        if (d > heard) return;
        // the TV pulls it in to watch
        if (lure && house.TV && house.TV.on && Vector3.Distance(pos, house.TV.transform.position) < 3f)
        {
            if (activity != "tv") StartActivity("tv", house.TVSpot, house.TVYaw, 22f);
            return;
        }
        investigatePos = pos;
        if (state == State.Sleeping || state == State.Activity || state == State.Patrolling || state == State.Idle || state == State.Returning)
        {
            state = State.HearNoise; stateTimer = state == State.Sleeping ? 1.4f : 0.8f;
            agent.ResetPath();
        }
        else if (state == State.Investigating || state == State.Searching) GoInvestigate(pos);
    }

    // ---------------------------------------------------------------- routine
    void NextRoutine()
    {
        if (house.TV && house.TV.on) { StartActivity("tv", house.TVSpot, house.TVYaw, 22f); return; }
        float r = Random.value;
        if (r < 0.22f) StartActivity("sleep", house.CatBed, 0f, Random.Range(15f, 25f));   // nap
        else if (r < 0.4f) StartActivity("eat", house.FoodSpot, 200f, 9f);
        else StartPatrol();
    }

    void GoSleep(float seconds)
    {
        state = State.Sleeping; stateTimer = seconds; activity = "sleep";
        agent.Warp(house.CatBed);
        agent.ResetPath();
    }

    void StartActivity(string what, Vector3 pos, float yaw, float seconds)
    {
        activity = what; activityPos = pos; activityYaw = yaw; stateTimer = seconds + 30f;
        agent.speed = walkSpeed;
        agent.SetDestination(pos);
        state = State.Activity;
        actTimer = seconds;
    }
    float actTimer;

    void TickActivity()
    {
        if (!Arrived()) return;
        FaceYaw(activityYaw);
        actTimer -= Time.deltaTime;
        if (activity == "sleep") { state = State.Sleeping; stateTimer = actTimer; return; }
        if (activity == "tv" && house.TV && !house.TV.on) actTimer = Mathf.Min(actTimer, 1.5f);
        if (actTimer <= 0f) { activity = ""; state = State.Idle; stateTimer = Random.Range(1f, 2.5f); }
    }

    void StartPatrol()
    {
        route.Clear();
        var pts = new List<Vector3>(house.PatrolPoints);
        for (int i = 0; i < 3 && pts.Count > 0; i++) { int k = Random.Range(0, pts.Count); route.Enqueue(pts[k]); pts.RemoveAt(k); }
        state = State.Patrolling; activity = "";
        agent.speed = walkSpeed;
        NextWaypoint();
    }

    void NextWaypoint()
    {
        while (route.Count > 0)
        {
            var p = route.Dequeue();
            if (TrySetDestination(p)) return;
        }
        state = State.Idle; stateTimer = Random.Range(1.5f, 3f);
    }

    void TickPatrol()
    {
        if (Arrived()) { if (route.Count > 0 && Random.value < 0.6f) NextWaypoint(); else { state = State.Idle; stateTimer = Random.Range(1.5f, 3.5f); } }
    }

    // ---------------------------------------------------------------- investigate / search
    void GoInvestigate(Vector3 pos)
    {
        investigatePos = pos;
        state = State.Investigating; stateTimer = 14f; activity = "";
        agent.speed = investigateSpeed;
        if (!TrySetDestination(pos)) StartSearch(transform.position);
    }

    void TickDetecting()
    {
        agent.ResetPath();
        FaceTowards(mouse.transform.position);
        if (!sees && stateTimer <= 0f)
        {
            if (suspicion > 0.15f) GoInvestigate(lastKnown);
            else NextRoutine();
        }
    }

    void StartSearch(Vector3 around)
    {
        state = State.Searching; stateTimer = 10f; searchStops = 3; checkingSpot = null;
        agent.speed = walkSpeed;
        lastKnown = around;
        SearchStep();
    }

    void SearchStep()
    {
        // sometimes check the nearest hiding place
        if (Random.value < 0.4f)
        {
            HideSpot best = null; float bd = 9f;
            foreach (var h in house.HideSpots) { float d = Vector3.Distance(h.ExitPoint.position, lastKnown); if (d < bd) { bd = d; best = h; } }
            if (best && TrySetDestination(best.ExitPoint.position)) { checkingSpot = best; return; }
        }
        checkingSpot = null;
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = lastKnown + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
            if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas) && TrySetDestination(hit.position)) return;
        }
    }

    float lookTimer;
    void TickSearch()
    {
        if (!Arrived()) return;
        if (checkingSpot)
        {
            // checking a hiding place: 40% chance to find a mouse hiding there
            if (mouse.Hiding == checkingSpot && Random.value < 0.4f) { mouse.ExitHide(); Catch(); return; }
            checkingSpot = null;
        }
        lookTimer -= Time.deltaTime;
        if (lookTimer > 0f) return;
        lookTimer = 1.6f;
        if (--searchStops <= 0 || stateTimer <= 0f) { state = State.Returning; activity = ""; NextRoutine(); return; }
        SearchStep();
    }

    // ---------------------------------------------------------------- chase / catch
    void StartChase()
    {
        if (state == State.Chasing) return;
        state = State.Chasing; activity = "";
        agent.speed = chaseSpeed;
        lostTimer = 0f;
        AudioManager.PlayBeep(true);
        HouseUI.Alert(true);
    }

    void TickChase(float dt)
    {
        repathTimer -= dt;
        // the mouse dived into a hiding spot right in front of the cat -> it knows where
        if (mouse.IsHidden)
        {
            if (Time.time - lastSeenTime < 0.6f && Vector3.Distance(transform.position, mouse.transform.position) < 8f)
            {
                checkingSpot = mouse.Hiding;
                if (Vector3.Distance(transform.position, checkingSpot.ExitPoint.position) < 1.4f) { mouse.ExitHide(); Catch(); return; }
                if (repathTimer <= 0f) { repathTimer = 0.2f; TrySetDestination(checkingSpot.ExitPoint.position); }
                return;
            }
            LoseTrack(); return;
        }
        if (sees) { lostTimer = 0f; if (repathTimer <= 0f) { repathTimer = 0.15f; TrySetDestination(mouse.transform.position); } }
        else
        {
            lostTimer += dt;
            if (repathTimer <= 0f) { repathTimer = 0.3f; TrySetDestination(lastKnown); }
            if (lostTimer > 3.5f || (Arrived() && lostTimer > 1f)) { LoseTrack(); return; }
        }
        // the mouse dived into a mouse hole (or its den): wait outside it, paw jabbing in
        if (TryStartAmbush()) return;
        // can't reach it (under the bed, up on a counter)? stare, then give up
        if (agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete && agent.remainingDistance < 1.2f)
        {
            FaceTowards(mouse.transform.position);
            lostTimer += dt * 0.6f;
        }
        Vector3 d = mouse.transform.position - transform.position;
        if (sees && new Vector3(d.x, 0f, d.z).magnitude < 1.3f && Mathf.Abs(d.y) < 1.2f) Catch();
    }

    // ---------------------------------------------------------------- stake-out at a mouse hole
    Vector3 ambushSpot, ambushHole;
    float swipeTimer;
    bool swiping;

    bool TryStartAmbush()
    {
        Vector3 mp = mouse.transform.position;
        bool inDen = house.RoomAt(mp) == "Mouse Den";
        foreach (var h in house.MouseHoles)
        {
            Vector3 mid = (h.inside + h.outside) * 0.5f;
            bool nearHole = Vector3.Distance(new Vector3(mp.x, 0f, mp.z), mid) < 2.2f;
            bool denHole = inDen && Vector3.Distance(mid, new Vector3(20.5f, 0f, 0f)) < 1f;
            if (!nearHole && !denHole) continue;
            // wait on whichever side of the hole the cat can reach
            Vector3 a = h.inside, b = h.outside;
            Vector3 mySide = Vector3.Distance(transform.position, a) < Vector3.Distance(transform.position, b) ? a : b;
            if (!NavMesh.SamplePosition(mySide, out var hit, 2f, NavMesh.AllAreas)) continue;
            ambushSpot = hit.position; ambushHole = mid;
            state = State.Ambush; stateTimer = Random.Range(10f, 15f); swipeTimer = 1.5f;
            agent.speed = investigateSpeed;
            TrySetDestination(ambushSpot);
            HouseUI.Alert(false);
            HouseUI.Message("The cat is waiting outside the hole... be patient.");
            return true;
        }
        return false;
    }

    void TickAmbush()
    {
        if (!Arrived()) return;
        FaceTowards(ambushHole);
        swipeTimer -= Time.deltaTime;
        if (swipeTimer <= 0f)
        {
            swiping = !swiping;
            swipeTimer = swiping ? 0.9f : Random.Range(1.5f, 3f);
            if (swiping) HouseGame.MakeNoise(ambushHole, 2f);
        }
        // the mouse pokes its nose out on the cat's side: pounce
        Vector3 d = mouse.transform.position - transform.position;
        if (!mouse.IsHidden && new Vector3(d.x, 0f, d.z).magnitude < 2.4f && Mathf.Abs(d.y) < 1.2f && sees) { Catch(); return; }
        if (sees && new Vector3(d.x, 0f, d.z).magnitude > 3f) { StartChase(); return; }
        if (stateTimer <= 0f) { swiping = false; NextRoutine(); }
    }

    // ---------------------------------------------------------------- knocking things off counters
    Knockable knockTarget;
    float knockTimer, nextKnockCheck;

    void TryKnockSomething()
    {
        if (Time.time < nextKnockCheck) return;
        nextKnockCheck = Time.time + 1f;
        foreach (var k in house.Knockables)
        {
            if (!k || k.knocked) continue;
            Vector3 d = k.transform.position - transform.position;
            if (new Vector3(d.x, 0f, d.z).magnitude < 3.4f && Random.value < 0.35f)
            {
                knockTarget = k; knockTimer = 1.1f;
                state = State.Knocking;
                agent.ResetPath();
                return;
            }
        }
    }

    void TickKnock()
    {
        if (!knockTarget) { state = State.Patrolling; NextWaypoint(); return; }
        FaceTowards(knockTarget.transform.position);
        knockTimer -= Time.deltaTime;
        if (knockTimer < 0.5f && !knockTarget.knocked)
            knockTarget.Knock(Vector3.ProjectOnPlane(knockTarget.transform.position - transform.position, Vector3.up).normalized, false);
        if (knockTimer <= 0f) { knockTarget = null; state = State.Idle; stateTimer = Random.Range(1.5f, 3f); }
    }

    void LoseTrack()
    {
        HouseUI.Alert(false);
        suspicion = 0.5f;
        StartSearch(lastKnown);
    }

    void Catch()
    {
        state = State.Catching;
        agent.ResetPath();
        FaceTowards(mouse.transform.position);
        HouseUI.Alert(false);
        game.OnCaught();
    }

    // ---------------------------------------------------------------- helpers
    bool TrySetDestination(Vector3 p)
    {
        if (!agent.isOnNavMesh) return false;
        if (!NavMesh.SamplePosition(p, out var hit, 2.5f, NavMesh.AllAreas)) return false;
        return agent.SetDestination(hit.position);
    }

    bool Arrived() => agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f;

    void FaceTowards(Vector3 p)
    {
        Vector3 d = p - transform.position; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * Time.deltaTime);
    }

    void FaceYaw(float yaw) => transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, yaw, 0f), 240f * Time.deltaTime);

    void OpenNearbyDoors()
    {
        foreach (var d in house.Doors)
            if (d && !d.locked && !d.open && Vector3.Distance(d.Point, transform.position + transform.forward * 1.5f) < 3.2f)
                d.SetOpen(true, quiet: true);
    }

    void ApplyPose()
    {
        if (!body) return;
        bool stopped = agent && agent.velocity.sqrMagnitude < 0.2f;
        if (state == State.Idle && stopped && stateTimer > 0f && !groomDecided) { grooming = Random.value < 0.5f; groomDecided = true; }
        if (state != State.Idle) groomDecided = false;
        body.pose = state switch
        {
            State.Sleeping => CatChaser.CatPose.Sleep,
            State.Stretching => CatChaser.CatPose.Stretch,
            State.Knocking => CatChaser.CatPose.Swipe,
            State.Ambush when stopped => swiping ? CatChaser.CatPose.Swipe : CatChaser.CatPose.Sit,
            State.Idle when stopped && grooming => CatChaser.CatPose.Groom,
            State.Catching => CatChaser.CatPose.Pounce,
            State.Activity when stopped && activity == "tv" => CatChaser.CatPose.Sit,
            State.Activity when stopped && activity == "eat" => CatChaser.CatPose.Search,
            State.Searching when stopped => CatChaser.CatPose.Search,
            _ => CatChaser.CatPose.Auto,
        };
    }

    bool grooming, groomDecided;

    void UpdateIndicator()
    {
        if (!indicator) return;
        string s = ""; Color c = Color.white;
        switch (state)
        {
            case State.Sleeping: s = "z z z"; c = new Color(0.7f, 0.85f, 1f); break;
            case State.HearNoise: case State.Investigating: case State.Detecting: case State.Searching: case State.Ambush: s = "?"; c = new Color(1f, 0.85f, 0.2f); break;
            case State.Chasing: case State.Catching: s = "!"; c = new Color(1f, 0.25f, 0.2f); break;
        }
        indicator.text = s;
        indicator.color = c;
        float pulse = 1f + 0.12f * Mathf.Sin(Time.time * (IsChasing ? 14f : 4f));
        indicator.transform.localScale = Vector3.one * pulse;
    }
}
