using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

// Builds the whole explorable house at runtime: rooms, walls with doors / mouse holes,
// furniture (real scale, with colliders), searchable containers, items, hiding spots,
// windows, lights, the backyard, and the cat's NavMesh. Everything is generated from code
// with materials from GameArt (guaranteed in the Android build).
//
//  z=50 +-----------+---------+-------------+
//       |  BEDROOM  |BATHROOM | STORAGE(lck)|      mouse holes: bed<->bath, bath<->storage (secret),
//  z=30 +--door-----+--door---+--door(lck)--+                   living<->hall, kitchen<->hall
//       |                 HALLWAY           |
//  z=22 +--door---------+-------door--------+
//       |  LIVING ROOM  arch   KITCHEN      |== back door (padlock) ==> BACKYARD -> fence gap = escape
//  z=0  +--front door---+-------------------+
//       x=0            x=26               x=46
public class HouseBuilder : MonoBehaviour
{
    public GameObject[] furniture;            // Mnostva prefabs (assigned in the scene)
    public const float H = 9f;                // wall height (~2.4 m)
    const float T = 0.5f;                     // wall thickness
    const float UPM = 3.7f;                   // units per metre (same as the props)

    public Transform MouseSpawn { get; private set; }
    public Transform CatSpawn { get; private set; }
    public Transform MenuShot { get; private set; }
    public Transform MenuMouse { get; private set; }
    public Vector3 CatBed, TVSpot, FoodSpot;
    public float TVYaw;
    public readonly List<Vector3> PatrolPoints = new List<Vector3>();
    public readonly List<HideSpot> HideSpots = new List<HideSpot>();
    public readonly List<Door> Doors = new List<Door>();
    public readonly List<Transform> Curtains = new List<Transform>();
    public readonly List<Light> RoomLights = new List<Light>();
    public Transform Fan;
    public TVSet TV;
    public Material WindowSky;        // runtime copy (tinted for day/night without touching the asset)

    public class Room { public string name; public Rect rect; }
    public readonly List<Room> Rooms = new List<Room>();

    GameArt art;
    Transform root, geo, colliders, interact;
    readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
    readonly List<(Vector3 pos, float yaw, float w, float h, string key, string keyName, bool alongX, Material mat)> pendingDoors = new List<(Vector3, float, float, float, string, string, bool, Material)>();

    struct Hole { public float c, w, h; public bool mouseHole, door; public Hole(float c, float w, float h, bool mouse = false, bool door = false) { this.c = c; this.w = w; this.h = h; mouseHole = mouse; this.door = door; } }

    public string RoomAt(Vector3 p)
    {
        foreach (var r in Rooms) if (r.rect.Contains(new Vector2(p.x, p.z))) return r.name;
        return "";
    }

    // ================================================================ build
    public void Build()
    {
        art = GameArt.Get();
        WindowSky = new Material(art.windowSky);
        foreach (var f in furniture) if (f) prefabs[f.name] = f;
        root = new GameObject("House").transform;
        geo = new GameObject("Geometry").transform; geo.SetParent(root, false);
        colliders = new GameObject("Colliders").transform; colliders.SetParent(root, false);

        Material planks = art.floors[1], tiles = art.floors[0], carpet = art.floors[2];
        Material wpLiving = art.walls[1], wpKitchen = art.walls[0], wpBed = art.walls[2], bathTile = art.wainscots[0];
        Material storageWall = Col(new Color(0.86f, 0.82f, 0.74f)), concrete = Col(new Color(0.62f, 0.6f, 0.57f));
        Material outside = Col(new Color(0.93f, 0.88f, 0.78f));

        // ---------------- rooms: floor + ceiling
        AddRoom("Living Room", 0, 0, 26, 22, planks);
        AddRoom("Kitchen", 26, 0, 46, 22, tiles);
        AddRoom("Hallway", 0, 22, 46, 30, planks);
        AddRoom("Bedroom", 0, 30, 18, 50, carpet);
        AddRoom("Bathroom", 18, 30, 30, 50, tiles);
        AddRoom("Storage Room", 30, 30, 46, 50, concrete);

        // ---------------- outer walls (inside face = room wallpaper)
        // south wall of the living room: front door + the mouse's home hole into its den
        WallX(0f, 0f, 26f, Col(new Color(0.6f, 0.36f, 0.27f)), wpLiving, new Hole(5f, 4f, 7.5f, door: true), new Hole(20.5f, 1.4f, 1.3f, mouse: true));
        BuildDen(planks);
        WallX(0f, 26f, 46f, outside, wpKitchen);                                                     // south, kitchen
        WallX(50f, 0f, 18f, wpBed, outside);
        WallX(50f, 18f, 30f, bathTile, outside);
        WallX(50f, 30f, 46f, storageWall, outside);
        WallZ(0f, 0f, 22f, outside, wpLiving);
        WallZ(0f, 22f, 30f, outside, wpLiving);
        WallZ(0f, 30f, 50f, outside, wpBed);
        WallZ(46f, 0f, 22f, wpKitchen, outside, new Hole(11f, 4f, 7.5f, door: true));              // back door
        WallZ(46f, 22f, 30f, wpLiving, outside);
        WallZ(46f, 30f, 50f, storageWall, outside);

        // ---------------- inner walls
        WallX(22f, 0f, 26f, wpLiving, wpLiving, new Hole(11f, 4f, 7.5f, door: true), new Hole(22f, 1.4f, 1.3f, mouse: true));
        WallX(22f, 26f, 46f, wpKitchen, wpLiving, new Hole(35f, 4f, 7.5f, door: true), new Hole(43f, 1.4f, 1.3f, mouse: true));
        WallZ(26f, 0f, 22f, wpLiving, wpKitchen, new Hole(11f, 9f, 7.5f));                          // open archway
        WallX(30f, 0f, 18f, wpLiving, wpBed, new Hole(8f, 4f, 7.5f, door: true));
        WallX(30f, 18f, 30f, wpLiving, bathTile, new Hole(24f, 4f, 7.5f, door: true));
        WallX(30f, 30f, 46f, wpLiving, storageWall, new Hole(38f, 4f, 7.5f, door: true));
        WallZ(18f, 30f, 50f, wpBed, bathTile, new Hole(45f, 1.4f, 1.3f, mouse: true));
        WallZ(30f, 30f, 50f, bathTile, storageWall, new Hole(46.6f, 1.4f, 1.3f, mouse: true));   // secret tunnel

        Furnish();
        Backyard();

        // ---------------- navmesh for the cat (doors/items are added AFTER, so they don't block it)
        var surf = root.gameObject.AddComponent<NavMeshSurface>();
        surf.collectObjects = CollectObjects.Children;
        surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surf.BuildNavMesh();

        interact = new GameObject("Interactables").transform; interact.SetParent(root, false);
        BuildDoors();
        Items();
        Hiding();
        Windows();
        Lights();

        var exit = new GameObject("GardenExit");
        exit.transform.SetParent(root, false);
        exit.transform.position = new Vector3(80f, 0.8f, 15.5f);    // just through the fence gap
        var ec = exit.AddComponent<BoxCollider>(); ec.isTrigger = true; ec.size = new Vector3(2f, 2f, 3f);
        exit.AddComponent<ExitZone>();

        MouseSpawn = DenSpawn;                    // start (and respawn) safe inside the den
        CatSpawn = Marker("CatSpawn", CatBed, 0f);
        FrontDoor = Doors.Find(d => d.keyId == "bolted");

        // suitcases by the front door (the family leaves in the opening cutscene)
        var bags = new GameObject("Suitcases").transform; bags.SetParent(root, false);
        Suitcases.Add(Suitcase(bags, new Vector3(4.2f, 0f, 2.6f), new Vector3(2.6f, 3.4f, 1.1f), new Color(0.2f, 0.42f, 0.6f)));
        Suitcases.Add(Suitcase(bags, new Vector3(6.6f, 0f, 2.4f), new Vector3(2f, 2.6f, 1f), new Color(0.78f, 0.3f, 0.22f)));
        FrontDoorShot = Marker("FrontDoorShot", new Vector3(10.5f, 3.6f, 10.5f), 0f);
        FrontDoorShot.LookAt(new Vector3(5f, 2.6f, 1f));
        CatShot = Marker("CatShot", new Vector3(17.2f, 1.8f, 12.6f), 0f);
        CatShot.LookAt(CatBed + new Vector3(0f, 1f, 0f));
        HoleShot = Marker("HoleShot", new Vector3(18.2f, 1.1f, 5.4f), 0f);
        HoleShot.LookAt(new Vector3(20.5f, 0.6f, 0.2f));
        // menu: mouse in the foreground, the cat asleep in its bed behind
        MenuMouse = Marker("MenuMouse", new Vector3(18.6f, 0.05f, 9.6f), 205f);
        MenuShot = Marker("MenuShot", new Vector3(15.6f, 2.1f, 4.4f), 0f);
        MenuShot.LookAt(new Vector3(20f, 1.5f, 11.5f));
    }

    // ================================================================ rooms & walls
    void AddRoom(string name, float x0, float z0, float x1, float z1, Material floor)
    {
        Rooms.Add(new Room { name = name, rect = Rect.MinMaxRect(x0, z0, x1, z1) });
        Box(geo, name + "_Floor", new Vector3((x0 + x1) * 0.5f, -0.25f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.5f, z1 - z0), floor, true, receive: true);
        Box(geo, name + "_Ceiling", new Vector3((x0 + x1) * 0.5f, H + 0.15f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.3f, z1 - z0), art.ceiling, true);
    }

    // wall running along X at z (side A faces -z, side B faces +z)
    void WallX(float z, float x0, float x1, Material a, Material b, params Hole[] holes) => Wall(true, z, x0, x1, a, b, holes);
    // wall running along Z at x (side A faces -x, side B faces +x)
    void WallZ(float x, float z0, float z1, Material a, Material b, params Hole[] holes) => Wall(false, x, z0, z1, a, b, holes);

    void Wall(bool alongX, float at, float s0, float s1, Material a, Material b, Hole[] holes)
    {
        var list = new List<Hole>(holes);
        list.Sort((p, q) => p.c.CompareTo(q.c));
        float cur = s0;
        foreach (var h in list)
        {
            float o0 = h.c - h.w * 0.5f, o1 = h.c + h.w * 0.5f;
            WallPiece(alongX, at, cur, o0, 0f, H, a, b, true);
            // lintel above the opening
            float top = h.mouseHole ? h.h + 0.4f : h.h;     // mouse hole: 1.7 tall, too low for the cat
            WallPiece(alongX, at, o0, o1, top, H, a, b, false);
            if (h.mouseHole)
            {
                MouseHoleArch(alongX, at, h.c, h.w, h.h, top, a, b);
                MouseHoles.Add(alongX ? (new Vector3(h.c, 0f, at - 1.3f), new Vector3(h.c, 0f, at + 1.3f))
                                      : (new Vector3(at - 1.3f, 0f, h.c), new Vector3(at + 1.3f, 0f, h.c)));
            }
            if (h.door) QueueDoor(alongX, at, h, a == b ? b : b);
            cur = o1;
        }
        WallPiece(alongX, at, cur, s1, 0f, H, a, b, true);
    }

    void WallPiece(bool alongX, float at, float s0, float s1, float y0, float y1, Material a, Material b, bool skirting)
    {
        float len = s1 - s0;
        if (len <= 0.01f || y1 - y0 <= 0.01f) return;
        float mid = (s0 + s1) * 0.5f, ym = (y0 + y1) * 0.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            Material m = side < 0 ? a : b;
            Vector3 c = alongX ? new Vector3(mid, ym, at + side * T * 0.25f) : new Vector3(at + side * T * 0.25f, ym, mid);
            Vector3 size = alongX ? new Vector3(len, y1 - y0, T * 0.5f) : new Vector3(T * 0.5f, y1 - y0, len);
            Box(geo, "Wall", c, size, m, false);
            if (skirting && y0 < 0.01f)
            {
                Vector3 sc = alongX ? new Vector3(mid, 0.28f, at + side * (T * 0.5f + 0.06f)) : new Vector3(at + side * (T * 0.5f + 0.06f), 0.28f, mid);
                Vector3 ss = alongX ? new Vector3(len, 0.56f, 0.12f) : new Vector3(0.12f, 0.56f, len);
                Box(geo, "Skirting", sc, ss, art.trim, false);
            }
        }
        // one collider for the full thickness
        Vector3 cc = alongX ? new Vector3(mid, ym, at) : new Vector3(at, ym, mid);
        Vector3 cs = alongX ? new Vector3(len, y1 - y0, T) : new Vector3(T, y1 - y0, len);
        AddCollider(cc, cs);
    }

    // rounded top for a mouse hole: fills the corners between the arch curve and the lintel,
    // dark lining inside, dark worn rim on both faces
    void MouseHoleArch(bool alongX, float at, float c, float w, float straight, float top, Material a, Material b)
    {
        var wall = new MB(); var lining = new MB(); var rim = new MB();
        const int N = 14;
        float hw = w * 0.5f, archH = top - straight;
        Vector3 P(float s, float y, float d) => alongX ? new Vector3(c + s, y, at + d) : new Vector3(at + d, y, c + s);
        float Arch(float s, float hww, float ah) { float u = Mathf.Clamp(s / hww, -1f, 1f); return straight + ah * Mathf.Sqrt(1f - u * u); }
        for (int i = 0; i < N; i++)
        {
            float s0 = Mathf.Lerp(-hw, hw, i / (float)N), s1 = Mathf.Lerp(-hw, hw, (i + 1) / (float)N);
            float y0 = Arch(s0, hw, archH), y1 = Arch(s1, hw, archH);
            for (int side = -1; side <= 1; side += 2)
            {
                float d = side * T * 0.5f;
                (side < 0 ? wall : wallB).Quad(P(s0, y0, d), P(s0, top, d), P(s1, top, d), P(s1, y1, d));
                float k = (hw + 0.18f) / hw;
                rim.Quad(P(s0, y0, d + side * 0.012f), P(s0 * k, Arch(s0 * k, hw + 0.18f, archH + 0.18f), d + side * 0.012f),
                         P(s1 * k, Arch(s1 * k, hw + 0.18f, archH + 0.18f), d + side * 0.012f), P(s1, y1, d + side * 0.012f));
            }
            lining.Quad(P(s0, y0, -T * 0.5f), P(s0, y0, T * 0.5f), P(s1, y1, T * 0.5f), P(s1, y1, -T * 0.5f));
        }
        for (int e = -1; e <= 1; e += 2)
        {
            lining.Quad(P(e * hw * 0.995f, 0f, -T * 0.5f), P(e * hw * 0.995f, straight, -T * 0.5f), P(e * hw * 0.995f, straight, T * 0.5f), P(e * hw * 0.995f, 0f, T * 0.5f));
            for (int side = -1; side <= 1; side += 2)
            {
                float d = side * (T * 0.5f + 0.012f);
                rim.Quad(P(e * hw, 0f, d), P(e * hw, straight, d), P(e * (hw + 0.18f), straight, d), P(e * (hw + 0.18f), 0f, d));
            }
        }
        Mesh(geo, "HoleArchA", wall, a);
        Mesh(geo, "HoleArchB", wallB, b);
        Mesh(geo, "HoleLining", lining, art.holeDark);
        Mesh(geo, "HoleRim", rim, Col(new Color(0.3f, 0.2f, 0.13f)));
        wallB = new MB();
    }
    MB wallB = new MB();

    // ================================================================ the mouse's den (behind the living room wall)
    // A cosy hideout inside the wall: matchbox bed, spool table, thimble stools, a candle, and
    // the PANTRY crate where carried food is stored. Only reachable through the mouse hole.
    public Transform DenSpawn, DenShot, PantryShot;
    public Pantry PantryBox;
    public readonly List<(Vector3 inside, Vector3 outside)> MouseHoles = new List<(Vector3, Vector3)>();

    void BuildDen(Material floor)
    {
        const float x0 = 16.5f, x1 = 24.5f, z0 = -7f, z1 = 0f, h = 3.6f;
        Rooms.Add(new Room { name = "Mouse Den", rect = Rect.MinMaxRect(x0, z0, x1, z1 - 0.25f) });
        var den = new GameObject("MouseDen").transform; den.SetParent(root, false);
        Material brick = Col(new Color(0.6f, 0.36f, 0.27f)), beam = Col(new Color(0.42f, 0.28f, 0.18f), 0.3f);
        Box(den, "DenFloor", new Vector3((x0 + x1) * 0.5f, -0.25f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.5f, z1 - z0), floor, true, receive: true);
        Box(den, "DenCeiling", new Vector3((x0 + x1) * 0.5f, h + 0.15f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 + 1f, 0.3f, z1 - z0 + 1f), beam, true);
        Box(den, "DenWallW", new Vector3(x0 - 0.25f, h * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.5f, h, z1 - z0), brick, true);
        Box(den, "DenWallE", new Vector3(x1 + 0.25f, h * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.5f, h, z1 - z0), brick, true);
        Box(den, "DenWallS", new Vector3((x0 + x1) * 0.5f, h * 0.5f, z0 - 0.25f), new Vector3(x1 - x0 + 1f, h, 0.5f), brick, true);
        for (float bx = x0 + 1.2f; bx < x1; bx += 2.4f)     // wooden beams across the ceiling
            Box(den, "Beam", new Vector3(bx, h - 0.15f, (z0 + z1) * 0.5f), new Vector3(0.3f, 0.3f, z1 - z0), beam, false, shadow: true);

        // matchbox bed with a cotton pillow and a sock blanket
        Box(den, "Matchbox", new Vector3(x0 + 1.6f, 0.3f, z0 + 1.4f), new Vector3(2.4f, 0.6f, 1.5f), Col(new Color(0.85f, 0.22f, 0.18f)), true, shadow: true);
        Box(den, "MatchboxTray", new Vector3(x0 + 1.6f, 0.62f, z0 + 1.4f), new Vector3(2.2f, 0.06f, 1.35f), Col(new Color(0.92f, 0.85f, 0.7f)), false);
        Box(den, "Blanket", new Vector3(x0 + 1.9f, 0.7f, z0 + 1.4f), new Vector3(1.4f, 0.12f, 1.38f), Col(new Color(0.3f, 0.55f, 0.7f)), false, shadow: true);
        Sphere(den, "Pillow", new Vector3(x0 + 0.85f, 0.78f, z0 + 1.4f), new Vector3(0.7f, 0.3f, 0.9f), Col(new Color(0.98f, 0.97f, 0.94f)));
        // spool table + bottle-cap plate + thimble stools
        Cyl(den, "SpoolBase", new Vector3(x1 - 2.2f, 0.06f, z0 + 2.6f), new Vector3(1.4f, 0.06f, 1.4f), Col(new Color(0.72f, 0.55f, 0.35f)), collider: true);
        Cyl(den, "SpoolCore", new Vector3(x1 - 2.2f, 0.45f, z0 + 2.6f), new Vector3(0.7f, 0.35f, 0.7f), Col(new Color(0.3f, 0.45f, 0.75f)));
        Cyl(den, "SpoolTop", new Vector3(x1 - 2.2f, 0.84f, z0 + 2.6f), new Vector3(1.4f, 0.06f, 1.4f), Col(new Color(0.72f, 0.55f, 0.35f)), collider: true);
        Cyl(den, "CapPlate", new Vector3(x1 - 2.2f, 0.93f, z0 + 2.6f), new Vector3(0.55f, 0.04f, 0.55f), Col(new Color(0.9f, 0.75f, 0.2f), 0.6f));
        foreach (var dx in new[] { -1.1f, 1.1f })
            Cyl(den, "Thimble", new Vector3(x1 - 2.2f + dx, 0.3f, z0 + 2.6f), new Vector3(0.45f, 0.3f, 0.45f), art.metal, collider: true);
        // candle stub with a warm light
        Cyl(den, "Candle", new Vector3(x0 + 0.6f, 0.35f, z1 - 1.2f), new Vector3(0.35f, 0.35f, 0.35f), Col(new Color(0.98f, 0.95f, 0.85f)));
        var flame = Sphere(den, "Flame", new Vector3(x0 + 0.6f, 0.82f, z1 - 1.2f), new Vector3(0.12f, 0.2f, 0.12f), new Material(art.unlitBase));
        flame.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", new Color(2.2f, 1.5f, 0.6f));
        var lgo = new GameObject("CandleLight"); lgo.transform.SetParent(den, false);
        lgo.transform.position = new Vector3(x0 + 0.8f, 1.3f, z1 - 1.5f);
        var cl = lgo.AddComponent<Light>(); cl.type = LightType.Point; cl.range = 9f; cl.intensity = 2.2f; cl.color = new Color(1f, 0.72f, 0.4f); cl.shadows = LightShadows.None;
        DenCandle = cl;
        // postage-stamp pictures on the wall
        Box(den, "Stamp", new Vector3(x1 - 2.2f, 2.1f, z0 + 0.02f), new Vector3(1.1f, 1.3f, 0.04f), Col(new Color(0.98f, 0.96f, 0.9f)), false);
        Box(den, "StampArt", new Vector3(x1 - 2.2f, 2.1f, z0 + 0.05f), new Vector3(0.85f, 1.05f, 0.03f), Col(new Color(0.35f, 0.6f, 0.75f)), false);
        Box(den, "Stamp2", new Vector3(x1 - 4f, 2.3f, z0 + 0.02f), new Vector3(0.9f, 0.9f, 0.04f), Col(new Color(0.98f, 0.96f, 0.9f)), false);
        Box(den, "Stamp2Art", new Vector3(x1 - 4f, 2.3f, z0 + 0.05f), new Vector3(0.7f, 0.7f, 0.03f), Col(new Color(0.85f, 0.4f, 0.3f)), false);

        // the pantry crate (stored food appears inside it)
        var pc = new Vector3(x1 - 1.5f, 0f, z1 - 2.3f);
        Box(den, "PantryBack", pc + new Vector3(0.65f, 0.75f, 0f), new Vector3(0.12f, 1.5f, 2.6f), art.trapWood, false, shadow: true);
        Box(den, "PantryFront", pc + new Vector3(-0.65f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 2.6f), art.trapWood, false, shadow: true);
        Box(den, "PantrySideA", pc + new Vector3(0f, 0.6f, 1.25f), new Vector3(1.4f, 1.2f, 0.12f), art.trapWood, false, shadow: true);
        Box(den, "PantrySideB", pc + new Vector3(0f, 0.6f, -1.25f), new Vector3(1.4f, 1.2f, 0.12f), art.trapWood, false, shadow: true);
        Box(den, "PantryFloor", pc + new Vector3(0f, 0.06f, 0f), new Vector3(1.4f, 0.12f, 2.6f), art.trapWood, false);
        AddCollider(pc + new Vector3(0f, 0.6f, 0f), new Vector3(1.4f, 1.2f, 2.6f));
        var sign = Box(den, "PantrySign", pc + new Vector3(-0.73f, 0.95f, 0f), new Vector3(0.04f, 0.4f, 1.4f), Col(new Color(0.96f, 0.9f, 0.72f)), false);
        PantryBox = MakeInteract<Pantry>("Pantry", pc + new Vector3(-1.2f, 0.4f, 0f));
        PantryBox.range = 2.4f;
        for (int i = 0; i < 12; i++)   // slots for stored food, 3 x 4 grid
            PantryBox.slots.Add(pc + new Vector3(-0.35f + (i % 2) * 0.55f, 0.2f + (i / 8) * 0.45f, -0.95f + ((i / 2) % 4) * 0.62f));

        Blob(new Vector3(x0 + 1.6f, 0f, z0 + 1.4f), 3f, 2f);
        Blob(new Vector3(x1 - 2.2f, 0f, z0 + 2.6f), 3.4f, 2f);
        Blob(pc, 1.9f, 3.1f);

        DenSpawn = Marker("DenSpawn", new Vector3(20.5f, 0.05f, -3.2f), 0f);
        DenShot = Marker("DenShot", new Vector3(17.6f, 2.4f, -1.2f), 0f);
        DenShot.LookAt(new Vector3(21.5f, 0.6f, -4.6f));
        PantryShot = Marker("PantryShot", new Vector3(20.6f, 1.9f, -3.4f), 0f);
        PantryShot.LookAt(pc + Vector3.up * 0.5f);
    }
    public Light DenCandle;
    public Door FrontDoor;
    public Transform FrontDoorShot, CatShot, HoleShot;
    public readonly List<Transform> Suitcases = new List<Transform>();

    Transform Suitcase(Transform parent, Vector3 bottom, Vector3 size, Color c)
    {
        var s = new GameObject("Suitcase").transform;
        s.SetParent(parent, false);
        s.position = bottom;
        var body = Box(s, "Body", bottom + Vector3.up * size.y * 0.5f, size, Col(c, 0.35f), false, shadow: true);
        Box(s, "Band", bottom + Vector3.up * size.y * 0.5f, new Vector3(size.x + 0.04f, 0.18f, size.z + 0.04f), Col(c * 0.7f), false);
        Box(s, "Handle", bottom + Vector3.up * (size.y + 0.2f), new Vector3(0.8f, 0.12f, 0.16f), Col(new Color(0.2f, 0.15f, 0.12f)), false);
        Box(s, "Sticker", bottom + new Vector3(0.4f, size.y * 0.65f, -size.z * 0.5f - 0.02f), new Vector3(0.6f, 0.45f, 0.02f), Col(new Color(0.98f, 0.85f, 0.3f)), false);
        return s;
    }

    // ================================================================ furniture
    void Furnish()
    {
        var f = new GameObject("Furniture").transform; f.SetParent(root, false);

        // ---- living room
        var tvCab = Place(f, "Cabinet_1", 12f, 1.8f, 0f, Solid);
        var couch = Place(f, "Couch", 12f, 19.4f, 180f, Slab(0.95f));
        Place(f, "Table", 12f, 11f, 90f, Slab(2.45f));
        Place(f, "Plant_1", 2.2f, 2.2f, 0f, Solid);
        Place(f, "Lamp", 23.8f, 20.6f, 180f, Solid);
        Place(f, "Chair", 3f, 12f, 90f, Slab(1.6f));
        Place(f, "Painting_1", 0.3f, 15f, 90f, None, 4.3f);
        Place(f, "Painting_2", 25.7f, 19f, -90f, None, 4.3f);
        // TV on the cabinet
        var tv = new GameObject("TV").transform; tv.SetParent(f, false);
        var tvBody = Box(tv, "TVBody", new Vector3(12f, tvCab.max.y + 1.5f, 1.4f), new Vector3(5f, 3f, 0.4f), Col(new Color(0.12f, 0.12f, 0.14f)), false, shadow: true);
        var screen = Box(tv, "Screen", new Vector3(12f, tvCab.max.y + 1.55f, 1.61f), new Vector3(4.5f, 2.6f, 0.02f), new Material(art.unlitBase), false);
        screen.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", Color.black);
        TV = MakeInteract<TVSet>("TVSwitch", new Vector3(12f, 0.6f, tvCab.max.z + 0.6f));
        TV.screen = screen.GetComponent<Renderer>();
        TVSpot = new Vector3(12f, 0f, 7.2f); TVYaw = 180f;
        // cat bed (round cushion)
        CatBed = new Vector3(21.5f, 0f, 16.5f);
        Cyl(f, "CatBed", CatBed + Vector3.up * 0.25f, new Vector3(4f, 0.25f, 4f), Col(new Color(0.55f, 0.25f, 0.35f)));
        Cyl(f, "CatBedPad", CatBed + Vector3.up * 0.45f, new Vector3(3.2f, 0.12f, 3.2f), Col(new Color(0.95f, 0.85f, 0.7f)));
        // ceiling fan
        Fan = new GameObject("CeilingFan").transform; Fan.SetParent(f, false); Fan.position = new Vector3(12f, H - 0.9f, 11f);
        Cyl(Fan, "Hub", Fan.position, new Vector3(0.7f, 0.3f, 0.7f), art.metal, local: false);
        Cyl(Fan, "Rod", Fan.position + Vector3.up * 0.5f, new Vector3(0.15f, 0.5f, 0.15f), art.metal, local: false);
        for (int i = 0; i < 4; i++)
        {
            var blade = Box(Fan, "Blade", Fan.position, new Vector3(3.2f, 0.06f, 0.6f), art.trapWood, false);
            blade.transform.RotateAround(Fan.position, Vector3.up, i * 90f);
            blade.transform.position += blade.transform.right * 1.8f;
        }

        // ---- kitchen
        Place(f, "Fridge", 44f, 3f, -90f, Solid);
        var counter = Place(f, "Cabinet_1", 29f, 20.3f, 180f, Solid);
        var sink = Place(f, "Washstand", 39.5f, 20.2f, 180f, Solid);
        var cab2 = Place(f, "Cabinet_2", 44.1f, 18f, -90f, Solid);
        Place(f, "Table", 36f, 10.5f, 0f, Slab(2.45f));
        Place(f, "Chair", 36f, 6.6f, 0f, Slab(1.6f));
        Place(f, "Chair", 36f, 14.4f, 180f, Slab(1.6f));
        Place(f, "Plant_2", 28.5f, 2.5f, 0f, Solid);
        Decor(f, "Teapot", new Vector3(28.6f, counter.max.y, 20.3f), 160f);
        Decor(f, "Faucet", new Vector3(39.5f, sink.max.y, 21.2f), 180f, false);
        Decor(f, "Cup", new Vector3(30.2f, counter.max.y, 21f), 30f);
        Decor(f, "Bottle_1", new Vector3(42.5f, sink.max.y, 19.4f), 0f);
        Decor(f, "Jar_1", new Vector3(44.2f, cab2.max.y, 16.4f), 0f);
        Decor(f, "Jar_2", new Vector3(44.2f, cab2.max.y, 19.4f), 0f);
        FoodSpot = new Vector3(31f, 0f, 5f);
        Cyl(f, "FoodBowl", new Vector3(30.5f, 0.2f, 2.5f), new Vector3(1.6f, 0.2f, 1.6f), Col(new Color(0.85f, 0.25f, 0.25f)));
        Cyl(f, "Food", new Vector3(30.5f, 0.38f, 2.5f), new Vector3(1.3f, 0.05f, 1.3f), Col(new Color(0.55f, 0.35f, 0.2f)));
        // climbing steps up to the counter: books (1.0) -> cardboard box (1.9) -> counter (2.8)
        Books(f, new Vector3(31.8f, 0f, 17.4f), 3);
        Crate(f, new Vector3(31.9f, 0f, 20.3f), new Vector3(1.9f, 1.9f, 2.4f), new Color(0.72f, 0.55f, 0.36f));

        // ---- hallway
        Place(f, "Plant_2", 2.5f, 26f, 90f, Solid);
        Place(f, "Storage_Box_1", 44.6f, 26.5f, -90f, Solid);
        Place(f, "Painting_1", 18f, 29.7f, 180f, None, 4.3f);
        Place(f, "Mirror", 28f, 29.4f, 180f, Solid);

        // ---- bedroom
        var bed = Place(f, "Bed", 5.6f, 43f, 90f, Slab(0.9f, 2.25f));
        var night = Place(f, "Cabinet_1", 1.9f, 37.4f, 90f, Solid);
        Decor(f, "Lamp", new Vector3(1.9f, night.max.y, 37.4f), 90f);
        Place(f, "Mirror", 16.9f, 35f, -90f, Solid);
        Place(f, "Clothes_Hanger", 9f, 49.7f, 180f, None, 5.4f);
        Place(f, "Plant_2", 15.8f, 48f, 0f, Solid);
        Books(f, new Vector3(12.2f, 0f, 41.2f), 2);
        var toy = Crate(f, new Vector3(14.5f, 0f, 33.2f), new Vector3(3f, 1.6f, 2f), new Color(0.3f, 0.55f, 0.85f));
        // ---- bathroom
        var basin = Place(f, "Washstand", 22.5f, 48.3f, 180f, Solid);
        Decor(f, "Faucet", new Vector3(22.5f, basin.max.y, 49.2f), 180f, false);
        Decor(f, "Bottle_2", new Vector3(23.9f, basin.max.y, 47.8f), 0f);
        Crate(f, new Vector3(27.8f, 0f, 37f), new Vector3(4f, 2.3f, 8.4f), new Color(0.96f, 0.96f, 0.98f));      // bathtub
        var basket = Crate(f, new Vector3(28.4f, 0f, 49.0f), new Vector3(2.4f, 2.2f, 1.8f), new Color(0.78f, 0.62f, 0.38f));  // laundry basket (hides the tunnel)
        // ---- storage room
        var shelf = Place(f, "Cabinet_2", 35.8f, 48.2f, 180f, Solid);
        Place(f, "Cabinet_2", 42f, 48.2f, 180f, Solid);
        Place(f, "Washing_Machine", 44f, 34f, -90f, Solid);
        Place(f, "Ironing_Board", 38f, 40f, 0f, Slab(2.6f));
        Crate(f, new Vector3(32.2f, 0f, 37f), new Vector3(2.4f, 2.4f, 2.4f), new Color(0.7f, 0.52f, 0.33f));
        Crate(f, new Vector3(32.2f, 2.4f, 37f), new Vector3(1.8f, 1.6f, 1.8f), new Color(0.75f, 0.58f, 0.38f));
        var toolbox = Crate(f, new Vector3(34.5f, 0f, 32.4f), new Vector3(3f, 1.4f, 1.6f), new Color(0.82f, 0.16f, 0.14f));

        // ---- searchable containers (storage key is in a random one of three)
        string[] keySpots = { "tv", "sink", "night" };
        string keyAt = keySpots[Random.Range(0, keySpots.Length)];
        Search("TV cabinet", new Vector3(12f, 0.8f, tvCab.max.z + 0.8f), keyAt == "tv" ? "storage_key" : null);
        Search("sink cupboard", new Vector3(39.5f, 0.8f, sink.min.z - 0.8f), keyAt == "sink" ? "storage_key" : null);
        Search("nightstand", new Vector3(night.max.x + 0.8f, 0.8f, 37.4f), keyAt == "night" ? "storage_key" : null);
        Search("fridge", new Vector3(41.6f, 0.8f, 3f), "cheese_wheel");
        Search("kitchen cupboard", new Vector3(cab2.min.x - 0.8f, 0.8f, 18f), "marble", 2);
        Search("toy chest", new Vector3(14.5f, 0.8f, toy.min.z - 0.8f), "marble", 3);
        Search("bathroom cabinet", new Vector3(22.5f, 0.8f, basin.min.z - 0.8f), null);
        Search("red toolbox", new Vector3(34.5f, 0.8f, toolbox.max.z + 0.8f), "padlock_key");
        Search("shelf", new Vector3(35.8f, 0.8f, shelf.min.z - 0.8f), null);

        // ---- cat patrol route (room centres)
        PatrolPoints.AddRange(new[]
        {
            new Vector3(8f, 0f, 8f), new Vector3(20f, 0f, 8f), new Vector3(38f, 0f, 4f), new Vector3(32f, 0f, 16f),
            new Vector3(8f, 0f, 26f), new Vector3(38f, 0f, 26f), new Vector3(12f, 0f, 36f), new Vector3(23f, 0f, 41f),
            new Vector3(38f, 0f, 44f),
        });
    }

    // ================================================================ items, hiding, doors
    void Items()
    {
        // food rests ON surfaces (floor, counters, the bed) - carry it home to the pantry
        (string type, Vector3 p)[] food =
        {
            ("cheese", new Vector3(6f, 0f, 6f)), ("cookie", new Vector3(23.5f, 0f, 9.5f)), ("bread", new Vector3(40f, 0f, 7.5f)),
            ("grapes", new Vector3(24f, 0f, 26.5f)), ("peanut", new Vector3(14f, 0f, 45.5f)), ("cheese", new Vector3(21f, 0f, 34f)),
            ("cookie", new Vector3(42f, 0f, 43f)), ("cheese", new Vector3(28.6f, 2.8f, 19.4f)), ("grapes", new Vector3(5.5f, 2.25f, 43.5f)),
            ("peanut", new Vector3(3.5f, 0f, 24.5f)), ("bread", new Vector3(31.5f, 1.9f, 20.6f)), ("cookie", new Vector3(10f, 0f, 14.6f)),
            ("peanut", new Vector3(44.2f, 4.3f, 18f)), ("cheese", new Vector3(37.5f, 0f, 46f)),
        };
        foreach (var f in food) Item("food:" + f.type, f.p);
        Item("marble", new Vector3(18f, 0f, 13f));
        Item("marble", new Vector3(31f, 0f, 27f));
        Item("marble", new Vector3(29.6f, 2.8f, 21f));
        // story notes lie flat on the floor
        Item("note:vacation", new Vector3(41f, 0f, 7.5f));
        Item("note:spare_key", new Vector3(43.5f, 0f, 24f));
        Item("note:cat_habits", new Vector3(13.5f, 0f, 35.5f));
        Item("note:tunnels", new Vector3(25.8f, 0f, 46.2f));
        Item("note:padlock", new Vector3(36.8f, 0f, 33.6f));
        // baited mousetraps - sneak to steal the bait, or SNAP!
        Trap(new Vector3(30f, 0f, 25.5f));
        Trap(new Vector3(42.5f, 0f, 9f));
        Trap(new Vector3(9f, 0f, 47f));
        Trap(new Vector3(40.5f, 0f, 37.5f));
    }

    // item resting on the surface at y = surface (never floating)
    void Item(string id, Vector3 surface)
    {
        var go = new GameObject("Item_" + id);
        go.transform.SetParent(interact, false);
        go.transform.position = surface;
        var p = go.AddComponent<Pickup>();
        p.itemId = id;
        p.range = 1.7f;
        p.bob = 0f;
        if (id.StartsWith("food:")) FoodModel(id.Substring(5), go.transform);
        else if (id == "marble")
            Sphere(go.transform, "Marble", surface + Vector3.up * 0.16f, Vector3.one * 0.32f, Col(new Color(0.3f, 0.55f, 0.95f), 0.9f));
        else
        {
            var n = Box(go.transform, "Paper", surface + Vector3.up * 0.02f, new Vector3(0.8f, 0.03f, 1f), Col(new Color(1f, 0.96f, 0.82f)), false);
            n.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
            var line = Box(go.transform, "Ink", surface + Vector3.up * 0.04f, new Vector3(0.5f, 0.01f, 0.06f), Col(new Color(0.3f, 0.3f, 0.5f)), false);
            line.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
        }
        // subtle glint so collectibles still read as collectible
        var glint = GameObject.CreatePrimitive(PrimitiveType.Quad);
        DestroyImmediate(glint.GetComponent<Collider>());
        glint.name = "Glint";
        glint.transform.SetParent(go.transform, false);
        glint.transform.position = surface + Vector3.up * 0.7f;
        glint.GetComponent<Renderer>().sharedMaterial = art.sparkle;
        glint.AddComponent<Glint>();
        if (surface.y < 0.05f) Blob(surface, 0.9f, 0.9f);
    }

    // mouse-sized snacks built from simple shapes (all resting on y = 0 of the parent)
    public GameObject FoodModel(string type, Transform parent)
    {
        var f = new GameObject("Food_" + type).transform;
        f.SetParent(parent, false);
        Vector3 o = parent.position;
        switch (type)
        {
            case "cheese":
                var w = Box(f, "Wedge", o + Vector3.up * 0.22f, new Vector3(0.75f, 0.44f, 0.55f), art.cheese, false, shadow: true);
                w.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
                Sphere(f, "Hole", o + new Vector3(0.12f, 0.44f, 0.05f), new Vector3(0.14f, 0.06f, 0.14f), Col(new Color(0.92f, 0.62f, 0.1f)));
                break;
            case "cookie":
                Cyl(f, "Cookie", o + Vector3.up * 0.07f, new Vector3(0.8f, 0.07f, 0.8f), Col(new Color(0.82f, 0.6f, 0.35f)));
                for (int i = 0; i < 5; i++)
                    Sphere(f, "Chip", o + new Vector3(Mathf.Cos(i * 1.3f) * 0.22f, 0.15f, Mathf.Sin(i * 1.3f) * 0.22f), Vector3.one * 0.1f, Col(new Color(0.3f, 0.18f, 0.1f)));
                break;
            case "bread":
                var crust = Box(f, "Crust", o + Vector3.up * 0.22f, new Vector3(0.9f, 0.44f, 0.6f), Col(new Color(0.72f, 0.45f, 0.22f)), false, shadow: true);
                crust.transform.rotation = Quaternion.Euler(0f, -20f, 0f);
                var crumb = Box(f, "Crumb", o + Vector3.up * 0.45f, new Vector3(0.78f, 0.04f, 0.5f), Col(new Color(0.98f, 0.9f, 0.72f)), false);
                crumb.transform.rotation = Quaternion.Euler(0f, -20f, 0f);
                break;
            case "grapes":
                for (int i = 0; i < 6; i++)
                    Sphere(f, "Grape", o + new Vector3((i % 3 - 1) * 0.2f, 0.15f + (i / 3) * 0.2f, (i % 2) * 0.12f - 0.06f), Vector3.one * 0.28f, Col(new Color(0.48f, 0.2f, 0.55f), 0.6f));
                Cyl(f, "Stem", o + new Vector3(0f, 0.55f, 0f), new Vector3(0.05f, 0.1f, 0.05f), Col(new Color(0.35f, 0.5f, 0.2f)));
                break;
            default: // peanut
                Sphere(f, "Nut", o + new Vector3(-0.12f, 0.16f, 0f), new Vector3(0.34f, 0.3f, 0.3f), Col(new Color(0.85f, 0.68f, 0.45f)));
                Sphere(f, "Nut2", o + new Vector3(0.14f, 0.15f, 0f), new Vector3(0.3f, 0.28f, 0.28f), Col(new Color(0.85f, 0.68f, 0.45f)));
                break;
        }
        foreach (var r in f.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return f.gameObject;
    }

    void Trap(Vector3 pos)
    {
        var go = new GameObject("MouseTrap");
        go.transform.SetParent(interact, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        var t = go.AddComponent<MouseTrap>();
        var baseB = Box(go.transform, "Base", pos + Vector3.up * 0.06f, new Vector3(0.75f, 0.12f, 1.25f), art.trapWood, false, shadow: true);
        baseB.transform.localRotation = Quaternion.identity; baseB.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        var bar = new GameObject("BarPivot").transform; bar.SetParent(go.transform, false); bar.localPosition = new Vector3(0f, 0.14f, 0f);
        var b1 = Box(bar, "Bar", Vector3.zero, new Vector3(0.62f, 0.04f, 0.04f), art.metal, false);
        b1.transform.localPosition = new Vector3(0f, 0f, -0.5f); b1.transform.localRotation = Quaternion.identity;
        var b2 = Box(bar, "Side", Vector3.zero, new Vector3(0.04f, 0.04f, 0.5f), art.metal, false);
        b2.transform.localPosition = new Vector3(-0.3f, 0f, -0.25f); b2.transform.localRotation = Quaternion.identity;
        var b3 = Box(bar, "Side", Vector3.zero, new Vector3(0.04f, 0.04f, 0.5f), art.metal, false);
        b3.transform.localPosition = new Vector3(0.3f, 0f, -0.25f); b3.transform.localRotation = Quaternion.identity;
        var bait = Box(go.transform, "Bait", Vector3.zero, new Vector3(0.24f, 0.14f, 0.2f), art.cheese, false);
        bait.transform.localPosition = new Vector3(0f, 0.19f, 0.32f); bait.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
        t.bar = bar; t.bait = bait.transform;
        var col = go.AddComponent<BoxCollider>(); col.isTrigger = true; col.size = new Vector3(0.75f, 0.5f, 1.25f); col.center = new Vector3(0f, 0.25f, 0f);
        Blob(pos, 1f, 1.4f);
    }

    // soft contact shadow on the floor - grounds furniture and characters visually
    public GameObject Blob(Vector3 pos, float sx, float sz)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        DestroyImmediate(q.GetComponent<Collider>());
        q.name = "ContactShadow";
        q.transform.SetParent(geo ? geo : root, false);
        q.transform.position = new Vector3(pos.x, pos.y + 0.015f, pos.z);
        q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        q.transform.localScale = new Vector3(sx, sz, 1f);
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = art.blobShadow;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return q;
    }

    GameObject Sphere(Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    void Hiding()
    {
        Hide("under the bed", new Vector3(6f, 0.05f, 43f), new Vector3(12.6f, 0.05f, 43f), 90f, new Vector3(11.4f, 0.4f, 43f));
        Hide("under the couch", new Vector3(12f, 0.05f, 20f), new Vector3(12f, 0.05f, 16.4f), 180f, new Vector3(12f, 0.4f, 17.4f));
        Hide("in the sink cupboard", new Vector3(37.6f, 0.2f, 20.4f), new Vector3(36.4f, 0.05f, 17.2f), 180f, new Vector3(37.6f, 0.9f, 18.4f));
        Hide("in the laundry basket", new Vector3(28.4f, 0.6f, 49f), new Vector3(26.4f, 0.05f, 47.8f), -90f, new Vector3(27.4f, 1.9f, 48.4f));
    }

    void Hide(string label, Vector3 hidePos, Vector3 exitPos, float exitYaw, Vector3 camPos)
    {
        var go = new GameObject("Hide_" + label);
        go.transform.SetParent(interact, false);
        go.transform.position = exitPos;
        var h = go.AddComponent<HideSpot>();
        h.label = label;
        h.range = 2.2f;
        h.HidePoint = Marker("HidePoint", hidePos, exitYaw, go.transform);
        h.ExitPoint = Marker("ExitPoint", exitPos, exitYaw, go.transform);
        h.CameraPoint = Marker("CamPoint", camPos, exitYaw, go.transform);
        h.CameraPoint.rotation = Quaternion.Euler(6f, exitYaw, 0f);
        HideSpots.Add(h);
    }

    void QueueDoor(bool alongX, float at, Hole h, Material m)
    {
        Vector3 hingePos = alongX ? new Vector3(h.c - h.w * 0.5f, 0f, at) : new Vector3(at, 0f, h.c - h.w * 0.5f);
        string key = null, keyName = null;
        if (!alongX && Mathf.Approximately(at, 46f)) { key = "padlock_key"; keyName = "Padlock Key"; }
        else if (alongX && Mathf.Approximately(at, 30f) && h.c > 30f) { key = "storage_key"; keyName = "Storage Room Key"; }
        else if (alongX && Mathf.Approximately(at, 0f)) { key = "bolted"; keyName = "front door key (it's bolted from outside!)"; }
        pendingDoors.Add((hingePos, alongX ? 0f : -90f, h.w, h.h, key, keyName, alongX, m));
    }

    void BuildDoors()
    {
        Material wood = Col(new Color(0.55f, 0.36f, 0.22f), 0.35f), knob = art.metal;
        foreach (var d in pendingDoors)
        {
            var baseT = new GameObject("Door").transform;
            baseT.SetParent(interact, false);
            baseT.position = d.pos;
            baseT.rotation = Quaternion.Euler(0f, d.yaw, 0f);
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(baseT, false);
            var panel = Box(hinge, "Panel", Vector3.zero, new Vector3(d.w - 0.12f, d.h - 0.08f, 0.24f), wood, false, shadow: true);
            panel.transform.localPosition = new Vector3(d.w * 0.5f, d.h * 0.5f, 0f);
            panel.AddComponent<BoxCollider>();
            for (int s = -1; s <= 1; s += 2)
            {
                var k = Box(hinge, "Knob", Vector3.zero, new Vector3(0.2f, 0.2f, 0.2f), knob, false);
                k.transform.localPosition = new Vector3(d.w - 0.5f, 3.9f, s * 0.18f);
            }
            var door = baseT.gameObject.AddComponent<Door>();
            door.hinge = hinge;
            door.range = 3f;
            if (!string.IsNullOrEmpty(d.key))
            {
                door.locked = true; door.keyId = d.key; door.keyName = d.keyName;
                var ob = panel.AddComponent<NavMeshObstacle>();
                ob.carving = true; ob.shape = NavMeshObstacleShape.Box;
                ob.size = new Vector3(d.w, d.h, 0.8f);
                door.blocker = ob;
                if (d.key == "padlock_key")   // padlock on the back door
                {
                    var pl = Box(hinge, "Padlock", Vector3.zero, new Vector3(0.45f, 0.55f, 0.2f), Col(new Color(0.85f, 0.7f, 0.2f), 0.7f), false);
                    pl.transform.localPosition = new Vector3(d.w - 0.5f, 3.2f, -0.25f);
                }
            }
            Doors.Add(door);
        }
    }

    void Windows()
    {
        Window(new Vector3(0.02f, 4.2f, 11f), 90f);
        Window(new Vector3(36f, 4.2f, 0.02f), 0f);
        Window(new Vector3(0.02f, 4.2f, 38f), 90f);
        Window(new Vector3(24f, 4.2f, 49.98f), 180f);
        Window(new Vector3(40f, 4.6f, 49.98f), 180f, 3f, 2.4f);
    }

    void Window(Vector3 inner, float yaw, float w = 4f, float h = 3.4f)
    {
        var win = new GameObject("Window").transform;
        win.SetParent(geo, false);
        win.position = inner; win.rotation = Quaternion.Euler(0f, yaw, 0f);
        Local(win, "Sky", new Vector3(0f, h * 0.5f, 0.27f), new Vector3(w, h, 0.02f), WindowSky);
        Local(win, "Frame_T", new Vector3(0f, h + 0.1f, 0.3f), new Vector3(w + 0.4f, 0.2f, 0.2f), art.trim);
        Local(win, "Sill", new Vector3(0f, -0.1f, 0.4f), new Vector3(w + 0.6f, 0.2f, 0.4f), art.trim);
        Local(win, "Frame_L", new Vector3(-w * 0.5f - 0.1f, h * 0.5f, 0.3f), new Vector3(0.2f, h, 0.2f), art.trim);
        Local(win, "Frame_R", new Vector3(w * 0.5f + 0.1f, h * 0.5f, 0.3f), new Vector3(0.2f, h, 0.2f), art.trim);
        Local(win, "Mullion", new Vector3(0f, h * 0.5f, 0.3f), new Vector3(0.1f, h, 0.12f), art.trim);
        var cur = Col(new Color(0.86f, 0.36f, 0.3f));
        for (int s = -1; s <= 1; s += 2)
        {
            var pivot = new GameObject("CurtainPivot").transform;
            pivot.SetParent(win, false);
            pivot.localPosition = new Vector3(s * (w * 0.5f + 0.5f), h + 0.5f, 0.55f);
            var c = Local(pivot, "Curtain", new Vector3(0f, -(h + 1f) * 0.5f, 0f), new Vector3(1f, h + 1f, 0.18f), cur);
            c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            Curtains.Add(pivot);
        }
    }

    void Lights()
    {
        foreach (var r in Rooms)
        {
            var go = new GameObject("RoomLight_" + r.name);
            go.transform.SetParent(geo, false);
            go.transform.position = new Vector3(r.rect.center.x, H - 1.2f, r.rect.center.y);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = Mathf.Max(r.rect.width, r.rect.height) * 0.95f;
            l.color = new Color(1f, 0.82f, 0.6f);
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            RoomLights.Add(l);
            var lamp = Cyl(geo, "CeilingLamp", go.transform.position + Vector3.up * 1.05f, new Vector3(1.8f, 0.2f, 1.8f), new Material(art.unlitBase));
            lamp.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", new Color(1f, 0.95f, 0.85f));
        }
    }

    void Backyard()
    {
        var y = new GameObject("Backyard").transform; y.SetParent(root, false);
        Rooms.Add(new Room { name = "Backyard", rect = Rect.MinMaxRect(46.5f, -6f, 82f, 36f) });
        Box(y, "Grass", new Vector3(64f, -0.25f, 15f), new Vector3(36f, 0.5f, 42f), Col(new Color(0.42f, 0.66f, 0.3f), 0.1f), true, receive: true);
        // back steps
        Box(y, "Step", new Vector3(48f, 0.2f, 11f), new Vector3(3f, 0.4f, 6f), Col(new Color(0.7f, 0.68f, 0.64f)), true, receive: true);
        // fence with a gap under one board at the far end = the way out
        Material fence = Col(new Color(0.85f, 0.78f, 0.62f), 0.2f);
        FenceRun(y, new Vector3(46.5f, 0f, -6f), new Vector3(78f, 0f, -6f), fence, -1f);
        FenceRun(y, new Vector3(46.5f, 0f, 36f), new Vector3(78f, 0f, 36f), fence, -1f);
        FenceRun(y, new Vector3(78f, 0f, -6f), new Vector3(78f, 0f, 36f), fence, 15.5f);
        // garden: tree + plants + stepping stones
        Cyl(y, "TreeTrunk", new Vector3(66f, 4f, 26f), new Vector3(1.6f, 4f, 1.6f), Col(new Color(0.45f, 0.3f, 0.18f)), collider: true);
        foreach (var o in new[] { new Vector3(0f, 9f, 0f), new Vector3(1.6f, 8f, 1f), new Vector3(-1.5f, 8.2f, -0.6f), new Vector3(0.3f, 10.5f, -0.8f) })
        {
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(leaf.GetComponent<Collider>());
            leaf.transform.SetParent(y, false);
            leaf.transform.position = new Vector3(66f, 0f, 26f) + o;
            leaf.transform.localScale = Vector3.one * 5f;
            leaf.GetComponent<Renderer>().sharedMaterial = Col(new Color(0.3f, 0.55f, 0.25f), 0.1f);
        }
        for (int i = 0; i < 6; i++)
            Cyl(y, "Stone", new Vector3(51f + i * 4.3f, 0.03f, 13f + Mathf.Sin(i) * 1.6f), new Vector3(2f, 0.05f, 1.6f), Col(new Color(0.72f, 0.7f, 0.66f)));
        Place(y, "Plant_1", 56f, 32f, 180f, Solid);
        Place(y, "Plant_2", 72f, 2f, 0f, Solid);
        Place(y, "Flower", 60f, 33f, 180f, Solid);
        Place(y, "Flower", 74f, 30f, 180f, Solid);
        // the gap: dug-out dirt under the fence
        Cyl(y, "Dirt", new Vector3(78f, 0.02f, 15.5f), new Vector3(3f, 0.04f, 3f), Col(new Color(0.4f, 0.28f, 0.18f)));
        // the meadow beyond the fence (where the escape ends)
        Box(y, "Meadow", new Vector3(91f, -0.25f, 15f), new Vector3(26f, 0.5f, 60f), Col(new Color(0.5f, 0.74f, 0.32f), 0.1f), true, receive: true);
        for (int i = 0; i < 14; i++)
        {
            var pos = new Vector3(84f + (i * 7.3f) % 16f, 0f, -8f + (i * 11.7f) % 46f);
            Place(y, i % 2 == 0 ? "Flower" : "Plant_2", pos.x, pos.z, i * 40f, None);
        }
    }

    void FenceRun(Transform parent, Vector3 a, Vector3 b, Material m, float gapAt)
    {
        Vector3 dir = (b - a).normalized;
        float len = Vector3.Distance(a, b);
        bool alongX = Mathf.Abs(dir.x) > 0.5f;
        for (float s = 0.6f; s < len; s += 1.25f)
        {
            Vector3 p = a + dir * s;
            float along = alongX ? p.x : p.z;
            if (gapAt > 0f && Mathf.Abs(along - gapAt) < 1.5f) continue;     // the escape gap
            var board = Box(parent, "Board", p + Vector3.up * 3.5f, alongX ? new Vector3(1.1f, 7f, 0.2f) : new Vector3(0.2f, 7f, 1.1f), m, true, shadow: true);
        }
        // rails
        Vector3 mid = (a + b) * 0.5f;
        Box(parent, "Rail", mid + Vector3.up * 2f, alongX ? new Vector3(len, 0.3f, 0.3f) : new Vector3(0.3f, 0.3f, len), m, false);
        Box(parent, "Rail", mid + Vector3.up * 5.5f, alongX ? new Vector3(len, 0.3f, 0.3f) : new Vector3(0.3f, 0.3f, len), m, false);
    }

    // ================================================================ helpers
    struct ColMode { public bool none, solid; public float from, to; }
    static readonly ColMode Solid = new ColMode { solid = true };
    static readonly ColMode None = new ColMode { none = true };
    static ColMode Slab(float from, float to = -1f) => new ColMode { from = from, to = to };

    // real-scale prop, bottom on the floor (or at wallY), centred at (x,z), front facing the yaw
    Bounds Place(Transform parent, string name, float x, float z, float yaw, ColMode cm, float wallY = 0f)
    {
        if (!prefabs.TryGetValue(name, out var prefab) || !prefab) return new Bounds(new Vector3(x, 0f, z), Vector3.zero);
        var go = Spawn(prefab, parent);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * UPM;
        Bounds b = WorldBounds(go);
        go.transform.position += new Vector3(x - b.center.x, wallY - b.min.y, z - b.center.z);
        b = WorldBounds(go);
        bool wallItem = cm.none;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = wallItem ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        if (cm.solid) AddCollider(b.center, b.size);
        else if (!cm.none)
        {
            float top = cm.to > 0f ? cm.to : b.max.y;
            AddCollider(new Vector3(b.center.x, (cm.from + top) * 0.5f, b.center.z), new Vector3(b.size.x, top - cm.from, b.size.z));
        }
        if (!cm.none && wallY < 0.01f) Blob(new Vector3(b.center.x, 0f, b.center.z), b.size.x * 1.15f, b.size.z * 1.15f);
        return b;
    }

    // small decoration standing on a surface. Loose items (cups, jars, teapots) can be knocked off:
    // the cat does it for fun, the mouse can PUSH them to make a crash somewhere else.
    public readonly List<Knockable> Knockables = new List<Knockable>();

    void Decor(Transform parent, string name, Vector3 bottomCentre, float yaw, bool knockable = true)
    {
        if (!prefabs.TryGetValue(name, out var prefab) || !prefab) return;
        var go = Spawn(prefab, parent);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * UPM;
        Bounds b = WorldBounds(go);
        go.transform.position += new Vector3(bottomCentre.x - b.center.x, bottomCentre.y - b.min.y + 0.005f, bottomCentre.z - b.center.z);
        if (!knockable) return;
        b = WorldBounds(go);
        var holder = new GameObject("Knock_" + name);
        holder.transform.SetParent(interact ? interact : parent, false);
        holder.transform.position = b.center;
        go.transform.SetParent(holder.transform, true);
        holder.AddComponent<BoxCollider>().size = b.size;
        var k = holder.AddComponent<Knockable>();
        k.range = 2.2f;
        Knockables.Add(k);
    }

    GameObject Spawn(GameObject prefab, Transform parent)
    {
        var staging = new GameObject("staging"); staging.SetActive(false);
        var go = Instantiate(prefab, staging.transform);
        go.name = prefab.name;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);   // never cooked
        go.transform.SetParent(parent, true);
        Destroy(staging);
        return go;
    }

    void Books(Transform parent, Vector3 basePos, int count)
    {
        Color[] cs = { new Color(0.8f, 0.22f, 0.2f), new Color(0.2f, 0.45f, 0.5f), new Color(0.92f, 0.7f, 0.25f), new Color(0.35f, 0.5f, 0.3f) };
        float y = 0f;
        for (int i = 0; i < count; i++)
        {
            float h = 0.33f;
            var b = Box(parent, "Book", basePos + new Vector3(0f, y + h * 0.5f, 0f), new Vector3(1.6f, h, 1.25f), Col(cs[i % cs.Length]), false, shadow: true);
            b.transform.rotation = Quaternion.Euler(0f, (i * 13f) % 24f - 12f, 0f);
            y += h;
        }
        AddCollider(basePos + Vector3.up * y * 0.5f, new Vector3(1.5f, y, 1.2f));
        Blob(basePos, 2f, 1.7f);
    }

    Bounds Crate(Transform parent, Vector3 bottom, Vector3 size, Color c)
    {
        Box(parent, "Crate", bottom + Vector3.up * size.y * 0.5f, size, Col(c, 0.2f), true, shadow: true);
        // a thin rim at the very top (no longer a box hanging around the crate)
        Box(parent, "Lip", bottom + Vector3.up * (size.y + 0.03f), new Vector3(size.x + 0.06f, 0.06f, size.z + 0.06f), Col(c * 0.85f), false);
        if (bottom.y < 0.01f) Blob(bottom, size.x * 1.2f, size.z * 1.2f);
        return new Bounds(bottom + Vector3.up * size.y * 0.5f, size);
    }

    void Search(string label, Vector3 point, string item, int amount = 1)
    {
        var c = MakeInteract<Container>("Search_" + label, point);
        c.label = label; c.itemId = item; c.amount = amount; c.range = 2.2f;
    }

    T MakeInteract<T>(string name, Vector3 pos) where T : Component
    {
        if (!interact) { interact = new GameObject("Interactables").transform; interact.SetParent(root, false); }
        var go = new GameObject(name);
        go.transform.SetParent(interact, false);
        go.transform.position = pos;
        return go.AddComponent<T>();
    }

    Transform Marker(string name, Vector3 pos, float yaw, Transform parent = null)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent ? parent : root, false);
        t.position = pos;
        t.rotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    void AddCollider(Vector3 center, Vector3 size)
    {
        var go = new GameObject("Col");
        go.transform.SetParent(colliders, false);
        go.transform.position = center;
        go.AddComponent<BoxCollider>().size = size;
    }

    GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material m, bool collider, bool receive = true, bool shadow = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.AddComponent<MeshFilter>().sharedMesh = TrackManager.BoxMesh(size, center);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.receiveShadows = receive;
        r.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        if (collider) go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    GameObject Local(Transform parent, string name, Vector3 localPos, Vector3 size, Material m)
    {
        var go = Box(parent, name, Vector3.zero, size, m, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        return go;
    }

    GameObject Cyl(Transform parent, string name, Vector3 pos, Vector3 scale, Material m, bool collider = false, bool local = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        if (!collider) DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    Material Col(Color c, float smooth = 0.25f)
    {
        if (mats.TryGetValue(c, out var m)) return m;
        m = new Material(art.litBase);
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        mats[c] = m;
        return m;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // double-sided quads with (along, y) UVs
    class MB
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Side(a, b, c, d); Side(d, c, b, a); }
        void Side(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            foreach (var p in new[] { a, b, c, d }) uv.Add(new Vector2(p.x + p.z, p.y));
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }
    }

    void Mesh(Transform parent, string name, MB mb, Material m)
    {
        if (mb.v.Count == 0) return;
        var mesh = new UnityEngine.Mesh { name = name };
        mesh.SetVertices(mb.v); mesh.SetUVs(0, mb.uv); mesh.SetTriangles(mb.t, 0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
