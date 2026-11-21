using System.Collections.Generic;
using UnityEngine;

// Endless track + room scenery.
// 6 floor segments are recycled ahead of the mouse. Every 2 segments = 1 room,
// so the pool always cycles Kitchen -> Living Room -> Bedroom -> Kitchen ...
// Furniture is decoration only: colliders are stripped, no scripts, no physics.
//
// Visuals: textured floors/walls with world-space UVs (seamless across segments),
// wainscoting, trims, windows with curtains, ceiling + lamp, snap-trap obstacles,
// floating cheese, and a small arched mouse hole in a partition wall between rooms
// (the wall fades out as the camera passes so it never hides the track).
public class TrackManager : MonoBehaviour
{
    public Transform player;
    public int segmentCount = 6;          // must be a multiple of 6 (3 rooms x 2 segments)
    public float segmentLength = 30f;
    public float trackWidth = 8f;         // lane area (lanes at -2.5, 0, 2.5)
    public float roomWidth = 20f;         // floor + wall-to-wall width
    public float wallHeight = 10f;
    public Color floorColor = new Color(0.85f, 0.8f, 0.7f);   // kept for compatibility
    public Color wallColor = new Color(0.95f, 0.95f, 0.85f);  // kept for compatibility

    [Header("Room furniture (prefabs from Mnostva_Art/Prefabs)")]
    public GameObject[] kitchenProps;
    public GameObject[] livingProps;
    public GameObject[] bedroomProps;
    [Tooltip("Small Mnostva items (cups, teapot, jars, plates, bottles, flower, lamp, faucet...) " +
             "placed on top of tables, cabinets, counters and the sink so rooms feel lived-in.")]
    public GameObject[] decorProps;
    [Header("Cheese model (CheeseWedge inside Assets/Model/Obstacle.fbx)")]
    public GameObject cheeseModel;
    public float cheeseSize = 0.8f;
    [Tooltip("Flip if furniture faces the walls instead of the track.")]
    public float propYawOffset = 0f;

    [Header("World scale")]
    [Tooltip("Mnostva models are authored in real metres. One metre = this many world units " +
             "(walls are 10 units = ~2.7 m, so 3.7). Every prop uses the same factor.")]
    public float unitsPerMeter = 3.7f;

    [Header("Materials")]
    [Tooltip("Fallback URP material (used only if Resources/GameArt is missing).")]
    public Material baseMaterial;

    const int SegmentsPerRoom = 2;
    static readonly string[] RoomNames = { "Kitchen", "LivingRoom", "Bedroom" };
    const float WainscotH = 2.5f;

    Transform[] segments;
    float nextZ;
    int nextIndex;
    Material baseMat;
    GameArt art;
    Camera cam;
    readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();
    readonly List<FadeWall> fadeWalls = new List<FadeWall>();

    public static TrackManager Instance { get; private set; }
    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        art = GameArt.Get();
        cam = Camera.main;
        segmentCount = Mathf.Max(6, segmentCount / 6 * 6);
        segments = new Transform[segmentCount];
        nextZ = -segmentLength;   // one segment behind start
        for (int i = 0; i < segmentCount; i++)
        {
            segments[i] = BuildSegment(i);
            PlaceNext(segments[i]);
        }
    }

    void Update()
    {
        if (!player) return;
        if (GameManager.Instance && GameManager.Instance.IsGameOver) return;   // stop spawning
        Transform oldest = segments[nextIndex];
        if (player.position.z - oldest.position.z > segmentLength * 1.5f)
        {
            PlaceNext(oldest);
            nextIndex = (nextIndex + 1) % segmentCount;
        }
    }

    void PlaceNext(Transform seg)
    {
        seg.position = new Vector3(0f, 0f, nextZ);
        nextZ += segmentLength;
        int cheeseLane = RespawnCheese(seg);
        RespawnTraps(seg, cheeseLane);
    }

    // ---------------------------------------------------------------- cheese
    const int CheesePerSegment = 6;
    static readonly float[] LaneX = { -2.5f, 0f, 2.5f };

    void BuildCheese(Transform root)
    {
        var holder = new GameObject("Cheese").transform;
        holder.SetParent(root, false);
        for (int i = 0; i < CheesePerSegment; i++)
        {
            var c = new GameObject("Cheese_" + i);
            c.transform.SetParent(holder, false);
            var col = c.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.8f, 0.8f, 0.8f);
            c.AddComponent<Cheese>();
            MakeCheeseVisual(c.transform);
            AddCheeseExtras(c.transform);
        }
    }

    GameObject cheeseTemplate;

    // Cheese look: the imported CheeseWedge mesh if assigned, otherwise a wedge-ish box.
    public GameObject MakeCheeseVisual(Transform parent)
    {
        if (!cheeseTemplate) cheeseTemplate = BuildCheeseTemplate();
        GameObject v;
        if (cheeseTemplate)
        {
            v = Instantiate(cheeseTemplate, parent);
            v.SetActive(true);
        }
        else
        {
            v = MakeShape(PrimitiveType.Cube, parent, "Visual", Vector3.zero, new Vector3(0.5f, 0.35f, 0.5f), new Color(1f, 0.8f, 0.15f));
            if (art && art.cheese) v.GetComponent<Renderer>().sharedMaterial = art.cheese;
        }
        v.name = "Visual";
        v.transform.localPosition = Vector3.zero;
        v.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);   // fixed 3/4 view - never spins
        return v;
    }

    // soft glow halo + contact shadow so floating cheese reads as a collectible
    void AddCheeseExtras(Transform cheese)
    {
        if (!art) return;
        if (art.glow)
        {
            var glow = MakeQuad(cheese, "Glow", art.glow, new Vector3(0f, 0.05f, 0.15f), 1.6f);
            glow.AddComponent<Billboard>();
        }
        if (art.blobShadow)
        {
            var sh = MakeQuad(cheese, "Shadow", art.blobShadow, new Vector3(0f, -0.58f, 0f), 0.9f);
            sh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    GameObject MakeQuad(Transform parent, string name, Material m, Vector3 pos, float size)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localScale = Vector3.one * size;
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return q;
    }

    GameObject BuildCheeseTemplate()
    {
        var proc = BuildProceduralCheese();
        if (proc) return proc;
        if (!cheeseModel) return null;
        var tmp = Instantiate(cheeseModel);
        Transform wedge = FindDeep(tmp.transform, "CheeseWedge");
        if (!wedge) { Destroy(tmp); return null; }

        var root = new GameObject("CheeseTemplate");
        root.SetActive(false);                                   // template only
        root.transform.SetParent(transform, false);
        wedge.SetParent(root.transform, true);
        Destroy(tmp);                                            // drop the unrelated pieces in the file
        foreach (var c in wedge.GetComponentsInChildren<Collider>()) Destroy(c);

        var mat = art && art.cheese ? art.cheese : ColorMat(new Color(1f, 0.82f, 0.12f));
        foreach (var r in wedge.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // fit: largest side = cheeseSize, centred on the root
        root.transform.position = Vector3.zero;
        Bounds b = GetBounds(wedge.gameObject);
        float maxSide = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (maxSide > 0.0001f) wedge.localScale *= cheeseSize / maxSide;
        b = GetBounds(wedge.gameObject);
        wedge.position -= b.center;
        return root;
    }

    // Classic cartoon cheese wedge: a triangular prism with a few holes, one combined mesh.
    GameObject BuildProceduralCheese()
    {
        if (!art || !art.cheese) return null;
        var root = new GameObject("CheeseTemplate");
        root.SetActive(false);
        root.transform.SetParent(transform, false);
        var body = new GameObject("Wedge");
        body.transform.SetParent(root.transform, false);

        float s = cheeseSize;
        float h = 0.5f * s;
        // footprint: thick rind at the back (z-), point at the front (z+)
        Vector2 a = new Vector2(-0.42f * s, -0.32f * s), b = new Vector2(0.42f * s, -0.32f * s), c = new Vector2(0.05f * s, 0.5f * s);
        var v = new List<Vector3>(); var tri = new List<int>();
        void Face(params Vector3[] p)
        {
            int i0 = v.Count; v.AddRange(p);
            for (int i = 1; i < p.Length - 1; i++) { tri.Add(i0); tri.Add(i0 + i); tri.Add(i0 + i + 1); }
        }
        Vector3 A0 = new Vector3(a.x, -h / 2, a.y), B0 = new Vector3(b.x, -h / 2, b.y), C0 = new Vector3(c.x, -h / 2, c.y);
        Vector3 A1 = A0 + Vector3.up * h, B1 = B0 + Vector3.up * h, C1 = C0 + Vector3.up * h;
        Face(A1, C1, B1);              // top
        Face(A0, B0, C0);              // bottom
        Face(A0, A1, B1, B0);          // back (rind)
        Face(B0, B1, C1, C0);          // right side
        Face(C0, C1, A1, A0);          // left side
        var mesh = new Mesh { name = "CheeseWedge" };
        mesh.SetVertices(v); mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        body.AddComponent<MeshFilter>().sharedMesh = mesh;
        body.AddComponent<MeshRenderer>().sharedMaterial = art.cheese;

        // holes pressed into the visible faces
        var holeM = ColorMat(new Color(0.95f, 0.6f, 0.08f));
        Vector3[] holes =
        {
            new Vector3(-0.1f * s, h / 2, -0.05f * s), new Vector3(0.14f * s, h / 2, 0.12f * s), new Vector3(0.05f * s, h / 2, -0.2f * s),
            new Vector3(0.3f * s, -0.05f * s, 0.0f), new Vector3(0.18f * s, 0.12f * s, 0.25f * s),
            new Vector3(-0.2f * s, 0.0f, -0.32f * s), new Vector3(0.22f * s, -0.08f * s, -0.32f * s),
        };
        float[] sizes = { 0.16f, 0.11f, 0.09f, 0.13f, 0.08f, 0.14f, 0.1f };
        for (int i = 0; i < holes.Length; i++)
        {
            var hole = Part(root.transform, PrimitiveType.Sphere, holeM, holes[i], Vector3.one * sizes[i] * s);
            hole.transform.localScale = new Vector3(sizes[i], sizes[i] * 0.7f, sizes[i]) * s;
        }
        CombineChildren(root, false);
        var r = root.GetComponent<MeshRenderer>();
        if (r) r.receiveShadows = false;
        return root;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r) return r;
        }
        return null;
    }

    int RespawnCheese(Transform seg)
    {
        int laneIndex = Random.Range(0, 3);
        var holder = seg.Find("Cheese");
        if (!holder) return laneIndex;
        if (seg.position.z < 0f) { holder.gameObject.SetActive(false); return laneIndex; }   // nothing behind the start
        holder.gameObject.SetActive(true);
        float lane = LaneX[laneIndex];
        for (int i = 0; i < holder.childCount; i++)
        {
            var c = holder.GetChild(i);
            c.localPosition = new Vector3(lane, 0.6f, 5f + i * 3.5f);
            c.gameObject.SetActive(true);
        }
        return laneIndex;
    }

    // ---------------------------------------------------------------- mouse traps
    // Wooden snap traps (jump over them). Trigger boxes are unchanged from the original design.
    const int TrapsPerSegment = 5;

    void BuildTraps(Transform root)
    {
        var holder = new GameObject("Traps").transform;
        holder.SetParent(root, false);
        for (int i = 0; i < TrapsPerSegment; i++)
        {
            bool square = i % 2 == 0;
            var t = new GameObject(square ? "Trap_Square_" + i : "Trap_Circle_" + i);
            t.transform.SetParent(holder, false);
            var col = t.AddComponent<BoxCollider>();
            col.isTrigger = true;
            float hit = square ? 1.5f : 1.35f;
            col.size = new Vector3(hit, 0.3f, hit);
            col.center = new Vector3(0f, 0.15f, 0f);
            t.AddComponent<Trap>();
            BuildSnapTrap(t.transform, square ? 1f : 0.88f, square ? 0f : 180f);
        }

        // slide-under obstacle: a rolling pin propped across one lane on two stacks of books
        var bars = new GameObject("SlideBars").transform;
        bars.SetParent(root, false);
        var bar = new GameObject("SlideBar");
        bar.transform.SetParent(bars, false);
        var bc = bar.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.center = new Vector3(0f, 0.8f, 0f);          // hits a standing/running mouse (0..1),
        bc.size = new Vector3(1.7f, 0.32f, 0.35f);      // misses a sliding one (0..0.5)
        bar.AddComponent<Trap>().requiresSlide = true;
        BuildRollingPin(bar.transform);
        bar.SetActive(false);
    }

    void BuildRollingPin(Transform parent)
    {
        var vis = new GameObject("Visual").transform;
        vis.SetParent(parent, false);
        Material wood = art && art.trapWood ? art.trapWood : ColorMat(new Color(0.85f, 0.68f, 0.45f));
        Color[] covers = { new Color(0.8f, 0.22f, 0.2f), new Color(0.2f, 0.45f, 0.5f), new Color(0.92f, 0.7f, 0.25f), new Color(0.35f, 0.5f, 0.3f) };
        Material pages = ColorMat(new Color(0.97f, 0.94f, 0.86f));
        // two stacks of books (0.65 high) at the lane edges
        for (int s = -1; s <= 1; s += 2)
        {
            float y = 0f;
            float[] hs = { 0.24f, 0.2f, 0.21f };
            for (int i = 0; i < 3; i++)
            {
                float h = hs[i];
                float twist = (i * 9f - 8f) * s;
                var book = Part(vis, PrimitiveType.Cube, ColorMat(covers[(i + (s > 0 ? 1 : 0)) % covers.Length]),
                                new Vector3(s * 0.98f, y + h * 0.5f, 0f), new Vector3(0.42f, h, 0.62f));
                book.transform.localRotation = Quaternion.Euler(0f, twist, 0f);
                var pg = Part(vis, PrimitiveType.Cube, pages, new Vector3(s * 0.98f + 0.012f, y + h * 0.5f, 0.012f), new Vector3(0.4f, h * 0.78f, 0.62f));
                pg.transform.localRotation = Quaternion.Euler(0f, twist, 0f);
                y += h;
            }
        }
        // the rolling pin (resting on the books, centre at 0.78) with handles
        var pin = Part(vis, PrimitiveType.Cylinder, wood, new Vector3(0f, 0.78f, 0f), new Vector3(0.26f, 0.95f, 0.26f));
        pin.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        for (int s = -1; s <= 1; s += 2)
        {
            var handle = Part(vis, PrimitiveType.Cylinder, wood, new Vector3(s * 1.13f, 0.78f, 0f), new Vector3(0.11f, 0.2f, 0.11f));
            handle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var knob = Part(vis, PrimitiveType.Sphere, wood, new Vector3(s * 1.34f, 0.78f, 0f), Vector3.one * 0.15f);
        }
        CombineChildren(vis.gameObject, true);
    }

    void BuildSnapTrap(Transform trap, float s, float yaw)
    {
        var vis = new GameObject("Visual").transform;
        vis.SetParent(trap, false);
        vis.localRotation = Quaternion.Euler(0f, yaw, 0f);
        vis.localScale = Vector3.one * s;
        Material wood = art && art.trapWood ? art.trapWood : ColorMat(new Color(0.85f, 0.68f, 0.45f));
        Material metal = art && art.metal ? art.metal : ColorMat(new Color(0.75f, 0.75f, 0.78f));
        Material cheeseM = art && art.cheese ? art.cheese : ColorMat(new Color(1f, 0.8f, 0.15f));
        Material red = ColorMat(new Color(0.85f, 0.18f, 0.15f));

        float w = 1.3f, len = 1.8f, h = 0.14f;
        Part(vis, PrimitiveType.Cube, wood, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, len));
        // cocked hammer: U-shaped wire frame lying back over the rear half
        float top = h + 0.025f;
        Part(vis, PrimitiveType.Cube, metal, new Vector3(-w * 0.42f, top, -len * 0.22f), new Vector3(0.05f, 0.05f, len * 0.5f));
        Part(vis, PrimitiveType.Cube, metal, new Vector3(w * 0.42f, top, -len * 0.22f), new Vector3(0.05f, 0.05f, len * 0.5f));
        Part(vis, PrimitiveType.Cube, metal, new Vector3(0f, top, -len * 0.47f), new Vector3(w * 0.89f, 0.05f, 0.05f));
        // springs at the hinge
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            var sp = Part(vis, PrimitiveType.Cylinder, metal, new Vector3(sgn * w * 0.3f, top + 0.04f, 0.02f), new Vector3(0.16f, 0.12f, 0.16f));
            sp.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        Part(vis, PrimitiveType.Cube, metal, new Vector3(0f, top + 0.03f, 0.02f), new Vector3(w * 0.85f, 0.03f, 0.03f));
        // hold-down bar + trigger plate with bait
        Part(vis, PrimitiveType.Cube, metal, new Vector3(0f, top, -len * 0.05f), new Vector3(0.035f, 0.035f, len * 0.5f));
        Part(vis, PrimitiveType.Cube, metal, new Vector3(0f, h + 0.02f, len * 0.27f), new Vector3(0.42f, 0.04f, 0.32f));
        var bait = Part(vis, PrimitiveType.Cube, cheeseM, new Vector3(0f, h + 0.1f, len * 0.27f), new Vector3(0.2f, 0.12f, 0.16f));
        bait.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
        // red warning stripe on the front edge so traps read instantly on a small screen
        Part(vis, PrimitiveType.Cube, red, new Vector3(0f, h * 0.5f, len * 0.5f + 0.005f), new Vector3(w * 1.001f, h * 0.6f, 0.02f));
        CombineChildren(vis.gameObject, true);
    }

    GameObject Part(Transform parent, PrimitiveType type, Material m, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    // Rows never block all 3 lanes; the cheese lane stays open while cheese is on it.
    void RespawnTraps(Transform seg, int cheeseLane)
    {
        var holder = seg.Find("Traps");
        if (!holder) return;
        for (int i = 0; i < holder.childCount; i++) holder.GetChild(i).gameObject.SetActive(false);
        var bars = seg.Find("SlideBars");
        if (bars) for (int i = 0; i < bars.childCount; i++) bars.GetChild(i).gameObject.SetActive(false);
        if (seg.position.z < 30f) return;                           // grace period at the start

        int next = 0;
        int[] free = { (cheeseLane + 1) % 3, (cheeseLane + 2) % 3 }; // the two non-cheese lanes
        foreach (float z in new[] { 10f, 19f })
        {
            int count = Random.Range(1, 3);                          // 1 or 2 obstacles, never 3
            int first = Random.Range(0, 2);
            // from the second trap segment on, the 2nd row sometimes starts with a slide-under bar
            bool slideRow = z > 15f && bars && bars.childCount > 0 && seg.position.z >= 60f && Random.value < 0.45f;
            for (int k = 0; k < count && next < holder.childCount; k++)
            {
                if (slideRow && k == 0) { PlaceTrap(bars.GetChild(0), free[first % 2], z); continue; }
                PlaceTrap(holder.GetChild(next++), free[(first + k) % 2], z);
            }
        }
        if (next < holder.childCount)                                // one single trap after the cheese
            PlaceTrap(holder.GetChild(next), Random.Range(0, 3), 27f);
    }

    void PlaceTrap(Transform trap, int lane, float z)
    {
        trap.localPosition = new Vector3(LaneX[lane], 0f, z);
        trap.gameObject.SetActive(true);
    }

    // ---------------------------------------------------------------- build
    Transform BuildSegment(int i)
    {
        int room = (i / SegmentsPerRoom) % 3;
        int half = i % SegmentsPerRoom;           // 0 = first half of the room
        var root = new GameObject($"Segment_{i}_{RoomNames[room]}").transform;
        root.SetParent(transform, false);

        float halfW = roomWidth * 0.5f;
        float L = segmentLength;

        Material floorM = RoomMat(art ? art.floors : null, room, new Color(0.8f, 0.7f, 0.55f));
        Material wallM = RoomMat(art ? art.walls : null, room, new Color(0.95f, 0.92f, 0.85f));
        Material wainM = RoomMat(art ? art.wainscots : null, room, new Color(0.9f, 0.9f, 0.9f));
        Material trimM = art && art.trim ? art.trim : ColorMat(new Color(0.96f, 0.94f, 0.9f));

        // floor (top at y = 0) - one clean surface, no rugs or lane lines
        var floor = MakeBox(root, "Floor", new Vector3(0f, -0.25f, L * 0.5f), new Vector3(roomWidth, 0.5f, L), floorM);
        floor.GetComponent<Renderer>().receiveShadows = true;

        // side walls: wallpaper above, wainscot panels below, chair rail, skirting, crown moulding
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            MakeBox(root, "Wall_" + side, new Vector3(s * (halfW + 0.25f), wallHeight * 0.5f, L * 0.5f), new Vector3(0.5f, wallHeight, L), wallM);
            MakeBox(root, "Wainscot_" + side, new Vector3(s * (halfW - 0.06f), WainscotH * 0.5f, L * 0.5f), new Vector3(0.12f, WainscotH, L), wainM);
            MakeBox(root, "ChairRail_" + side, new Vector3(s * (halfW - 0.1f), WainscotH + 0.08f, L * 0.5f), new Vector3(0.2f, 0.16f, L), trimM);
            MakeBox(root, "Skirting_" + side, new Vector3(s * (halfW - 0.14f), 0.3f, L * 0.5f), new Vector3(0.28f, 0.6f, L), trimM);
            MakeBox(root, "Crown_" + side, new Vector3(s * (halfW - 0.15f), wallHeight - 0.22f, L * 0.5f), new Vector3(0.3f, 0.44f, L), trimM);
            BuildWindow(root, s, s < 0 ? 10.5f : 13.5f, room);
        }

        // ceiling + lamp (closes the room, so no sky shows at the top of portrait screens)
        MakeBox(root, "Ceiling", new Vector3(0f, wallHeight + 0.15f, L * 0.5f), new Vector3(roomWidth + 1f, 0.3f, L),
                art && art.ceiling ? art.ceiling : ColorMat(new Color(0.95f, 0.93f, 0.9f)));
        BuildCeilingLamp(root, L * 0.5f);

        // little decorative mouse holes in the skirting
        MouseHoleDecal(root, -halfW + 0.01f, L * (half == 0 ? 0.7f : 0.3f));
        MouseHoleDecal(root, halfW - 0.01f, L * (half == 0 ? 0.35f : 0.8f));

        // room entrance: partition wall with a small arched mouse hole
        if (half == 0) BuildDoorway(root, (room + 2) % 3);

        BuildCheese(root);
        BuildTraps(root);

        // furniture
        GameObject[] props = room == 0 ? kitchenProps : room == 1 ? livingProps : bedroomProps;
        PlaceProps(root, props, half, room, i);

        return root;
    }

    Material RoomMat(Material[] list, int room, Color fallback)
    {
        if (list != null && room < list.Length && list[room]) return list[room];
        return ColorMat(fallback);
    }

    static readonly Color[] CurtainColors =
    {
        new Color(0.86f, 0.36f, 0.3f),    // kitchen: tomato red
        new Color(0.2f, 0.36f, 0.4f),     // living: deep teal
        new Color(0.93f, 0.78f, 0.55f),   // bedroom: honey
    };

    void BuildWindow(Transform root, int side, float z, int room)
    {
        float halfW = roomWidth * 0.5f;
        float w = 4f, h = 3.6f, y0 = 4.4f;
        float x = side * (halfW - 0.02f);
        var win = new GameObject("Window").transform;
        win.SetParent(root, false);
        Material trimM = art && art.trim ? art.trim : ColorMat(Color.white);

        // sky "glass"
        var sky = MakeBox(win, "Sky", new Vector3(x, y0 + h * 0.5f, z), new Vector3(0.02f, h, w),
                          art && art.windowSky ? art.windowSky : ColorMat(new Color(0.6f, 0.8f, 1f)));
        sky.GetComponent<Renderer>().receiveShadows = false;
        // frame + mullions + sill
        float fx = side * (halfW - 0.1f);
        MakeBox(win, "Frame_T", new Vector3(fx, y0 + h + 0.1f, z), new Vector3(0.2f, 0.2f, w + 0.4f), trimM);
        MakeBox(win, "Frame_B", new Vector3(side * (halfW - 0.18f), y0 - 0.1f, z), new Vector3(0.36f, 0.2f, w + 0.6f), trimM);
        MakeBox(win, "Frame_L", new Vector3(fx, y0 + h * 0.5f, z - w * 0.5f - 0.1f), new Vector3(0.2f, h, 0.2f), trimM);
        MakeBox(win, "Frame_R", new Vector3(fx, y0 + h * 0.5f, z + w * 0.5f + 0.1f), new Vector3(0.2f, h, 0.2f), trimM);
        MakeBox(win, "Mullion_V", new Vector3(side * (halfW - 0.06f), y0 + h * 0.5f, z), new Vector3(0.1f, h, 0.1f), trimM);
        MakeBox(win, "Mullion_H", new Vector3(side * (halfW - 0.06f), y0 + h * 0.55f, z), new Vector3(0.1f, 0.1f, w), trimM);
        // curtains on a rod
        Material cur = ColorMat(CurtainColors[room]);
        MakeBox(win, "Rod", new Vector3(side * (halfW - 0.45f), y0 + h + 0.55f, z), new Vector3(0.07f, 0.07f, w + 3f),
                art && art.metal ? art.metal : ColorMat(Color.gray));
        for (int k = -1; k <= 1; k += 2)
        {
            var c = MakeBox(win, "Curtain", new Vector3(side * (halfW - 0.42f), y0 + h * 0.5f + 0.15f, z + k * (w * 0.5f + 0.55f)),
                            new Vector3(0.22f, h + 1.0f, 1.1f), cur);
            c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            // gathered folds
            for (int f = -1; f <= 1; f++)
                MakeBox(win, "Fold", new Vector3(side * (halfW - 0.55f), y0 + h * 0.5f + 0.15f, z + k * (w * 0.5f + 0.55f) + f * 0.33f),
                        new Vector3(0.1f, h + 0.95f, 0.14f), cur);
        }
    }

    void BuildCeilingLamp(Transform root, float z)
    {
        var lampMat = art && art.unlitBase ? new Material(art.unlitBase) : ColorMat(Color.white);
        if (art && art.unlitBase) lampMat.SetColor("_BaseColor", new Color(1.6f, 1.45f, 1.2f));
        var shade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shade.name = "CeilingLamp";
        DestroyImmediate(shade.GetComponent<Collider>());
        shade.transform.SetParent(root, false);
        shade.transform.localPosition = new Vector3(0f, wallHeight - 0.05f, z);
        shade.transform.localScale = new Vector3(2.2f, 0.5f, 2.2f);
        var r = shade.GetComponent<Renderer>();
        r.sharedMaterial = lampMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---------------------------------------------------------------- mouse hole wall
    // Partition wall across the room with a small arched mouse hole at its base.
    // Sized for the mouse, not for people: just spans the 3 lanes (mouse reaches x = +-2.8)
    // and is barely taller than a jump - roughly a quarter of the wall height.
    const float HoleHalfW = 3.0f;
    const float HoleStraightH = 1.1f;  // vertical jambs
    const float HoleArchH = 1.4f;      // elliptical cap -> hole top at 2.5
    const float HoleRim = 0.22f;       // worn dark edge around the hole
    const float PartitionT = 0.9f;     // deep wall = reads as a tunnel/hole, not a doorway

    void BuildDoorway(Transform root, int wallRoom)
    {
        float halfW = roomWidth * 0.5f;
        float t = PartitionT, z = t * 0.5f;
        var d = new GameObject("MouseHoleWall").transform;
        d.SetParent(root, false);
        Material wallM = RoomMat(art ? art.walls : null, wallRoom, new Color(0.95f, 0.92f, 0.85f));
        Material wainM = RoomMat(art ? art.wainscots : null, wallRoom, new Color(0.9f, 0.9f, 0.9f));
        Material trimM = art && art.trim ? art.trim : ColorMat(Color.white);
        Material dark = art && art.holeDark ? art.holeDark : ColorMat(new Color(0.1f, 0.07f, 0.05f));

        float sideW = halfW - HoleHalfW;
        var fw = d.gameObject.AddComponent<FadeWall>();
        fw.planeZ = 0f;
        fw.Add(MakeBox(d, "Side_L", new Vector3(-HoleHalfW - sideW * 0.5f, wallHeight * 0.5f, z), new Vector3(sideW, wallHeight, t), wallM));
        fw.Add(MakeBox(d, "Side_R", new Vector3(HoleHalfW + sideW * 0.5f, wallHeight * 0.5f, z), new Vector3(sideW, wallHeight, t), wallM));

        // wainscot, rail and skirting on the approach face, stopping short of the hole
        float edge = HoleHalfW + HoleRim + 0.35f;
        float segW = halfW - edge;
        for (int s = -1; s <= 1; s += 2)
        {
            float cx = s * (edge + segW * 0.5f);
            fw.Add(MakeBox(d, "Wainscot", new Vector3(cx, WainscotH * 0.5f, -0.06f), new Vector3(segW, WainscotH, 0.12f), wainM));
            fw.Add(MakeBox(d, "Rail", new Vector3(cx, WainscotH + 0.08f, -0.1f), new Vector3(segW, 0.16f, 0.2f), trimM));
            fw.Add(MakeBox(d, "Skirting", new Vector3(cx, 0.3f, -0.14f), new Vector3(segW, 0.6f, 0.28f), trimM));
            fw.Add(MakeBox(d, "Crown", new Vector3(s * halfW * 0.5f, wallHeight - 0.22f, -0.15f), new Vector3(halfW, 0.44f, 0.3f), trimM));
        }

        // wall above the arch (front face, back face, top cap) + dark tunnel lining + worn rim
        float zf = 0f, zb = t;
        var wall = new MeshBuilder();
        var lining = new MeshBuilder();
        var rim = new MeshBuilder();
        const int N = 32;
        for (int i = 0; i < N; i++)
        {
            float x0 = Mathf.Lerp(-HoleHalfW, HoleHalfW, i / (float)N);
            float x1 = Mathf.Lerp(-HoleHalfW, HoleHalfW, (i + 1) / (float)N);
            float y0 = ArchY(x0, HoleHalfW, HoleArchH), y1 = ArchY(x1, HoleHalfW, HoleArchH);
            wall.Quad(new Vector3(x0, y0, zf), new Vector3(x0, wallHeight, zf), new Vector3(x1, wallHeight, zf), new Vector3(x1, y1, zf));
            wall.Quad(new Vector3(x0, y0, zb), new Vector3(x0, wallHeight, zb), new Vector3(x1, wallHeight, zb), new Vector3(x1, y1, zb));
            lining.Quad(new Vector3(x0, y0, zf), new Vector3(x0, y0, zb), new Vector3(x1, y1, zb), new Vector3(x1, y1, zf));
            float k = (HoleHalfW + HoleRim) / HoleHalfW;
            float ox0 = x0 * k, ox1 = x1 * k;
            rim.Quad(new Vector3(x0, y0, zf - 0.012f), new Vector3(ox0, ArchY(ox0, HoleHalfW + HoleRim, HoleArchH + HoleRim), zf - 0.012f),
                     new Vector3(ox1, ArchY(ox1, HoleHalfW + HoleRim, HoleArchH + HoleRim), zf - 0.012f), new Vector3(x1, y1, zf - 0.012f));
        }
        for (int s = -1; s <= 1; s += 2)
        {
            float xi = s * (HoleHalfW - 0.01f), xo = s * (HoleHalfW + HoleRim);
            lining.Quad(new Vector3(xi, 0f, zf), new Vector3(xi, HoleStraightH, zf), new Vector3(xi, HoleStraightH, zb), new Vector3(xi, 0f, zb));
            rim.Quad(new Vector3(s * HoleHalfW, 0f, zf - 0.012f), new Vector3(s * HoleHalfW, HoleStraightH, zf - 0.012f),
                     new Vector3(xo, HoleStraightH, zf - 0.012f), new Vector3(xo, 0f, zf - 0.012f));
        }
        fw.Add(MakeMesh(d, "WallAboveHole", wall, wallM));
        fw.Add(MakeMesh(d, "HoleLining", lining, dark));
        fw.Add(MakeMesh(d, "HoleRim", rim, ColorMat(new Color(0.3f, 0.2f, 0.13f))));
        // a darker threshold strip so the hole has depth even when you can see through it
        fw.Add(MakeBox(d, "Threshold", new Vector3(0f, 0.004f, t * 0.5f), new Vector3(HoleHalfW * 2f, 0.008f, t), dark));
        fw.Build(art);
        fadeWalls.Add(fw);
    }

    static float ArchY(float x, float hw, float archH)
    {
        float u = Mathf.Clamp(x / hw, -1f, 1f);
        return HoleStraightH + archH * Mathf.Sqrt(1f - u * u);
    }

    // Room walls stay solid - no fading or transparency. Instead the camera follows the mouse
    // THROUGH the mouse hole: 0 = normal chase camera, 1 = low camera inside the hole.
    // Driven by the mouse's distance to the next wall (dm > 0 = wall still ahead).
    public static float CameraDip(float mouseZ)
    {
        var tm = Instance;
        if (!tm) return 0f;
        float best = 0f;
        foreach (var fw in tm.fadeWalls)
        {
            if (!fw) continue;
            float dm = fw.transform.position.z - mouseZ;
            float d;
            if (dm > 2f) d = Mathf.InverseLerp(11f, 2f, dm);             // lowering on approach
            else if (dm > -5f) d = 1f;                                   // inside / through the hole
            else d = Mathf.InverseLerp(-11f, -5f, dm);                   // rising in the new room
            best = Mathf.Max(best, Mathf.SmoothStep(0f, 1f, d));
        }
        return best;
    }

    // Tiny decorative mouse hole in the skirting, flat against the wall.
    void MouseHoleDecal(Transform root, float x, float z)
    {
        var hole = MakeShape(PrimitiveType.Cylinder, root, "MouseHole", new Vector3(x - Mathf.Sign(x) * 0.28f, 0f, z),
                             new Vector3(1.9f, 0.02f, 1.4f), new Color(0.06f, 0.04f, 0.03f));
        hole.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        if (art && art.holeDark) hole.GetComponent<Renderer>().sharedMaterial = art.holeDark;
    }

    // ---------------------------------------------------------------- furniture
    void PlaceProps(Transform root, GameObject[] props, int half, int room, int segIndex)
    {
        if (props == null || props.Length == 0) return;
        float inner = trackWidth * 0.5f + 0.6f;               // never inside the lanes
        float[] zs = { 6f, 15f, 24f };
        int k = half * 3;
        for (int s = 0; s < zs.Length; s++)
        {
            var l = SpawnProp(root, props[(k + s) % props.Length], -1, inner, zs[s]);
            var r = SpawnProp(root, props[(k + s + 1 + props.Length / 2) % props.Length], 1, inner, zs[s] + 3f);
            Dress(root, l, -1, room, segIndex * 7 + s * 2);
            Dress(root, r, 1, room, segIndex * 7 + s * 2 + 1);
        }
    }

    // ---------------------------------------------------------------- tabletop dressing
    static readonly string[][] KitchenSets =
    {
        new[] { "Teapot", "Cup", "Plate" }, new[] { "Jar_1", "Jar_2", "Jar_3" },
        new[] { "Bottle_1", "Plate", "Cup" }, new[] { "Flower", "Teapot", "Bottle_2" },
    };
    static readonly string[][] LivingSets =
    {
        new[] { "Lamp", "Vinyl_Record", "Cup" }, new[] { "PC_Monitor", "Keyboard", "PC_Mouse" },
        new[] { "Flower", "Teapot", "Cup" },
    };
    static readonly string[][] BedroomSets =
    {
        new[] { "Lamp", "Flower" }, new[] { "Storage_Box_2", "Jar_2" }, new[] { "Iron", "Cup" },
    };

    Dictionary<string, GameObject> decorByName;

    GameObject Decor(string name)
    {
        if (decorByName == null)
        {
            decorByName = new Dictionary<string, GameObject>();
            if (decorProps != null) foreach (var p in decorProps) if (p) decorByName[p.name] = p;
        }
        return decorByName.TryGetValue(name, out var g) ? g : null;
    }

    // Puts small real-size items on top of a furniture piece that has a usable top surface.
    void Dress(Transform root, GameObject surface, int side, int room, int seed)
    {
        if (!surface || decorProps == null || decorProps.Length == 0) return;
        string n = surface.name.ToLowerInvariant();
        bool sink = n.Contains("washstand");
        bool table = n.Contains("table") && !n.Contains("ironing");
        bool counter = n.Contains("cabinet") || n.Contains("washing") || sink;
        if (!table && !counter) return;

        Bounds b = GetBounds(surface);
        var sets = room == 0 ? KitchenSets : room == 1 ? LivingSets : BedroomSets;
        var set = sets[Mathf.Abs(seed) % sets.Length];
        int count = table ? set.Length : Mathf.Min(2, set.Length);
        if (sink)
        {
            // faucet at the back (wall side) of the sink, one item beside it
            PlaceOnTop(root, Decor("Faucet"), b, new Vector2(b.center.x + side * b.size.x * 0.28f, b.center.z), side, seed);
            PlaceOnTop(root, Decor(set[0]), b, new Vector2(b.center.x - side * b.size.x * 0.1f, b.min.z + b.size.z * 0.22f), side, seed + 1);
            return;
        }
        for (int i = 0; i < count; i++)
        {
            float t = (i + 0.5f) / count;
            float z = Mathf.Lerp(b.min.z + 0.5f, b.max.z - 0.5f, t);
            float x = b.center.x + ((i % 2 == 0) ? -1f : 1f) * b.size.x * 0.12f;
            PlaceOnTop(root, Decor(set[i]), b, new Vector2(x, z), side, seed + i);
        }
    }

    // Instantiate a decoration under an inactive holder, strip its colliders, then move it into
    // the scene. Colliders on inactive objects are never cooked, so furniture costs no physics
    // setup time at load (props are decoration only).
    Transform stripHolder;

    GameObject InstantiateClean(GameObject prefab, Transform parent)
    {
        if (!stripHolder)
        {
            var h = new GameObject("PropStaging");
            h.SetActive(false);
            h.transform.SetParent(transform, false);
            stripHolder = h.transform;
        }
        var go = Instantiate(prefab, stripHolder);
        go.name = prefab.name;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        go.transform.SetParent(parent, false);
        return go;
    }

    void PlaceOnTop(Transform root, GameObject prefab, Bounds top, Vector2 xz, int side, int seed)
    {
        if (!prefab) return;
        var go = InstantiateClean(prefab, root);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        float yaw = (side < 0 ? 90f : -90f) + propYawOffset + ((seed * 37) % 50 - 25);   // face the track, slight variety
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * unitsPerMeter;
        go.transform.localPosition = Vector3.zero;
        Bounds b = GetBounds(go);
        // too big for this surface? skip rather than overhang
        if (b.size.x > top.size.x * 0.9f || b.size.z > top.size.z * 0.9f) { Destroy(go); return; }
        float x = Mathf.Clamp(xz.x, top.min.x + b.extents.x, top.max.x - b.extents.x);
        float z = Mathf.Clamp(xz.y, top.min.z + b.extents.z, top.max.z - b.extents.z);
        go.transform.position += new Vector3(x - b.center.x, top.max.y - b.min.y + 0.005f, z - b.center.z);
    }

    // Props keep their REAL authored size (metres) x unitsPerMeter - one factor for everything.
    const float WallGap = 0.32f;       // clears skirting + wainscot
    const float MaxPropLength = 8.5f;  // slots on one side are 9 units apart

    GameObject SpawnProp(Transform root, GameObject prefab, int side, float inner, float z)
    {
        if (!prefab) return null;
        string n = prefab.name.ToLowerInvariant();
        bool onWall = n.Contains("painting") || n.Contains("hanger") || n.Contains("t_shirt");
        float wallY = n.Contains("painting") ? 1.15f : 1.45f;    // bottom edge height in metres

        var go = InstantiateClean(prefab, root);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = onWall ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;

        float yaw = (side < 0 ? 90f : -90f) + propYawOffset;     // front faces the track
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * unitsPerMeter;

        Bounds b = GetBounds(go);
        if (b.size.y < 0.001f) { Destroy(go); return null; }

        float halfW = roomWidth * 0.5f;
        float wallX = halfW - WallGap;
        if (!onWall)
        {
            float avail = wallX - inner;
            if (b.size.x > avail && b.size.z < b.size.x)
            {
                go.transform.localRotation = Quaternion.Euler(0f, yaw + 90f, 0f);
                b = GetBounds(go);
            }
            if (b.size.x > avail) { go.transform.localScale *= avail / b.size.x; b = GetBounds(go); }
        }
        if (b.size.z > MaxPropLength) { go.transform.localScale *= MaxPropLength / b.size.z; b = GetBounds(go); }

        float targetX = side * (onWall ? halfW - 0.02f : wallX);
        Vector3 delta = new Vector3(
            side < 0 ? targetX - b.min.x : targetX - b.max.x,
            (onWall ? wallY * unitsPerMeter : 0f) - b.min.y,
            root.position.z + z - b.center.z);
        go.transform.position += delta;
        return go;
    }

    static Bounds GetBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // ---------------------------------------------------------------- mesh helpers
    // Double-sided quad list -> mesh. UVs = (x, y) so wallpaper lines up with the side walls.
    class MeshBuilder
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> tri = new List<int>();
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Side(a, b, c, d);
            Side(d, c, b, a);
        }
        void Side(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(new Vector2(a.x, a.y)); uv.Add(new Vector2(b.x, b.y)); uv.Add(new Vector2(c.x, c.y)); uv.Add(new Vector2(d.x, d.y));
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }
    }

    GameObject MakeMesh(Transform parent, string name, MeshBuilder mb, Material m)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(mb.v);
        mesh.SetUVs(0, mb.uv);
        mesh.SetTriangles(mb.tri, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    // Box with world-space UVs (1 UV unit = 1 world unit; materials scale by 1/texture size),
    // so textures tile at real size and continue seamlessly from one segment to the next.
    GameObject MakeBox(Transform parent, string name, Vector3 localPos, Vector3 size, Material m)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, localPos);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    public static Mesh BoxMesh(Vector3 size, Vector3 offset)
    {
        var verts = new List<Vector3>(24); var norms = new List<Vector3>(24); var uvs = new List<Vector2>(24); var tris = new List<int>(36);
        Vector3 h = size * 0.5f;
        Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (var n in normals)
        {
            Vector3 f = -n;
            Vector3 up = Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(up, f);
            Vector3 fc = Vector3.Scale(n, h);
            float hr = Mathf.Abs(Vector3.Dot(h, right)), hu = Mathf.Abs(Vector3.Dot(h, up));
            Vector3 c0 = fc - right * hr - up * hu;
            Vector3[] q = { c0, c0 + up * 2f * hu, c0 + right * 2f * hr + up * 2f * hu, c0 + right * 2f * hr };
            int i0 = verts.Count;
            foreach (var p in q)
            {
                verts.Add(p); norms.Add(n);
                Vector3 w = p + offset;
                uvs.Add(Mathf.Abs(n.y) > 0.5f ? new Vector2(w.x, w.z) : Mathf.Abs(n.x) > 0.5f ? new Vector2(w.z, w.y) : new Vector2(w.x, w.y));
            }
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
        }
        var mesh = new Mesh { name = "Box" };
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Merge all child meshes into one renderer (one submesh per material) - fewer draw calls on Android.
    public static void CombineChildren(GameObject root, bool castShadows)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>();
        var byMat = new Dictionary<Material, List<CombineInstance>>();
        Matrix4x4 toLocal = root.transform.worldToLocalMatrix;
        foreach (var f in filters)
        {
            var r = f.GetComponent<MeshRenderer>();
            if (!r || !f.sharedMesh) continue;
            var m = r.sharedMaterial;
            if (!byMat.TryGetValue(m, out var list)) byMat[m] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = f.sharedMesh, transform = toLocal * f.transform.localToWorldMatrix });
        }
        var subs = new List<CombineInstance>(); var mats = new List<Material>();
        foreach (var kv in byMat)
        {
            var part = new Mesh();
            part.CombineMeshes(kv.Value.ToArray(), true, true);
            subs.Add(new CombineInstance { mesh = part, transform = Matrix4x4.identity });
            mats.Add(kv.Key);
        }
        var combined = new Mesh { name = root.name + "_Combined" };
        combined.CombineMeshes(subs.ToArray(), false, true);
        foreach (var f in filters) if (f.gameObject != root) Destroy(f.gameObject);
        var mf = root.GetComponent<MeshFilter>();
        if (!mf) mf = root.AddComponent<MeshFilter>();
        mf.sharedMesh = combined;
        var mr = root.GetComponent<MeshRenderer>();
        if (!mr) mr = root.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mats.ToArray();
        mr.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---------------------------------------------------------------- materials
    Material GetBaseMat()
    {
        if (baseMat) return baseMat;
        if (art && art.litBase) baseMat = art.litBase;
        if (!baseMat) baseMat = baseMaterial;
        if (!baseMat && player)
        {
            var r = player.GetComponentInChildren<Renderer>();
            if (r && r.sharedMaterial && !r.sharedMaterial.shader.name.StartsWith("Standard"))
                baseMat = r.sharedMaterial;
        }
        if (!baseMat)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh) baseMat = new Material(sh);
        }
        if (!baseMat)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMat = tmp.GetComponent<Renderer>().sharedMaterial;
            Destroy(tmp);
        }
        return baseMat;
    }

    public Material ColorMat(Color c)
    {
        if (!matCache.TryGetValue(c, out var m))
        {
            m = new Material(GetBaseMat());
            m.SetColor("_BaseColor", c);
            m.color = c;
            matCache[c] = m;
        }
        return m;
    }

    GameObject MakeShape(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Color c)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = ColorMat(c);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }
}

// Swaps a partition wall to transparent copies of its materials while the camera passes it.
public class FadeWall : MonoBehaviour
{
    public float planeZ;
    readonly List<Renderer> renderers = new List<Renderer>();
    Material[] opaque, fade;
    float current = 1f;

    public void Add(GameObject go) { var r = go.GetComponent<Renderer>(); if (r) renderers.Add(r); }

    public void Build(GameArt art)
    {
        opaque = new Material[renderers.Count];
        fade = new Material[renderers.Count];
        Material template = art && art.wallsFade != null && art.wallsFade.Length > 0 ? art.wallsFade[0] : null;
        for (int i = 0; i < renderers.Count; i++)
        {
            opaque[i] = renderers[i].sharedMaterial;
            if (!template) continue;
            var f = new Material(template);
            var src = opaque[i];
            if (src.HasProperty("_BaseMap"))
            {
                f.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                f.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap"));
            }
            if (src.HasProperty("_BaseColor")) f.SetColor("_BaseColor", src.GetColor("_BaseColor"));
            fade[i] = f;
        }
    }

    public void SetAlpha(float a)
    {
        a = Mathf.Clamp01(a);
        if (Mathf.Abs(a - current) < 0.004f) return;
        current = a;
        bool solid = a > 0.995f || fade == null;
        bool hidden = a < 0.02f;
        for (int i = 0; i < renderers.Count; i++)
        {
            if (!renderers[i]) continue;
            renderers[i].enabled = !hidden;               // fully gone = no draw, no haze
            if (hidden) continue;
            if (solid || !fade[i]) { renderers[i].sharedMaterial = opaque[i]; continue; }
            renderers[i].sharedMaterial = fade[i];
            Color c = opaque[i].HasProperty("_BaseColor") ? opaque[i].GetColor("_BaseColor") : Color.white;
            c.a = a;
            fade[i].SetColor("_BaseColor", c);
        }
    }
}

// Keeps a quad facing the camera (used for the cheese glow).
public class Billboard : MonoBehaviour
{
    static Camera cam;
    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        if (cam) transform.rotation = cam.transform.rotation;
    }
}
