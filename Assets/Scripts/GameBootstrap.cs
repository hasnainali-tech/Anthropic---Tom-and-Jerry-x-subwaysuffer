using UnityEngine;
using UnityEngine.SceneManagement;

// Safety net: on every scene load, make sure Jerry, the camera follow and the track
// exist and are wired up. Objects already in the scene are reused; anything missing
// is created. No Inspector setup needed.
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;   // avoid double-subscribe (domain reload off)
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Ensure();

    static void Ensure()
    {
        // only for the legacy runner scene - the free-roam House scene sets itself up
        if (SceneManager.GetActiveScene().name != "SampleScene") return;
        // ---- Jerry ----
        var player = Object.FindFirstObjectByType<PlayerController>();
        if (!player)
        {
            var jerry = new GameObject("Jerry");
            var col = jerry.AddComponent<CapsuleCollider>();
            col.radius = 0.3f; col.height = 1f; col.center = new Vector3(0f, 0.5f, 0f);
            col.isTrigger = true;
            var rb = jerry.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;

            var vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vis.name = "Visual";
            Object.Destroy(vis.GetComponent<Collider>());
            vis.transform.SetParent(jerry.transform, false);
            vis.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            vis.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

            player = jerry.AddComponent<PlayerController>(); // after Visual exists (Awake looks it up)
        }
        Tint(player.transform.Find("Visual"), new Color(0.75f, 0.5f, 0.3f));
        if (!player.GetComponent<MouseHero>()) player.gameObject.AddComponent<MouseHero>();   // the animated mouse model

        // ---- Camera ----
        var cam = Camera.main;
        if (!cam)
        {
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
        }
        cam.farClipPlane = 200f;
        var follow = cam.GetComponent<CameraFollow>();
        if (!follow) follow = cam.gameObject.AddComponent<CameraFollow>();
        if (!follow.target) follow.target = player.transform;
        cam.transform.position = player.transform.position + follow.offset;

        // ---- Track ----
        var track = Object.FindFirstObjectByType<TrackManager>();
        if (!track) track = new GameObject("Track").AddComponent<TrackManager>();
        if (!track.player) track.player = player.transform;

        // ---- Game manager (score, chase, game over) ----
        if (!Object.FindFirstObjectByType<GameManager>())
            new GameObject("GameManager").AddComponent<GameManager>();

        // ---- Light ----
        if (!Object.FindFirstObjectByType<Light>())
        {
            var l = new GameObject("Directional Light").AddComponent<Light>();
            l.type = LightType.Directional;
            l.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    // Colour a placeholder; if its material is missing, borrow the pipeline's default.
    static void Tint(Transform t, Color c)
    {
        if (!t) return;
        var r = t.GetComponent<Renderer>();
        if (!r) return;
        if (!r.sharedMaterial)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            r.sharedMaterial = tmp.GetComponent<Renderer>().sharedMaterial;
            Object.Destroy(tmp);
        }
        r.material.color = c;
    }
}
