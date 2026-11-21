using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Tools > Cheese Escape > Setup Scene
// Safe to run again: it only creates what is missing.
public static class CheeseEscapeSetup
{
    [MenuItem("Tools/Cheese Escape/Setup Scene")]
    public static void Setup()
    {
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
            Object.DestroyImmediate(vis.GetComponent<Collider>());
            vis.transform.SetParent(jerry.transform, false);
            vis.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            vis.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
            SetColor(vis, new Color(0.75f, 0.5f, 0.3f), "Mat_Jerry");

            player = jerry.AddComponent<PlayerController>();
            Undo.RegisterCreatedObjectUndo(jerry, "Create Jerry");
        }

        // ---- Camera ----
        var cam = Camera.main;
        if (!cam)
        {
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
        }
        var follow = cam.GetComponent<CameraFollow>();
        if (!follow) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.target = player.transform;
        cam.transform.position = new Vector3(0f, 4.5f, -7f);
        cam.farClipPlane = 150f;

        // ---- Track ----
        var track = Object.FindFirstObjectByType<TrackManager>();
        if (!track) track = new GameObject("Track").AddComponent<TrackManager>();
        track.player = player.transform;

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Cheese Escape] Scene setup done. Press Play.");
    }

    // Saves a URP-compatible material asset so colours survive and work on Android.
    public static void SetColor(GameObject go, Color c, string matName)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        string path = "Assets/Materials/" + matName + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = c;
        EditorUtility.SetDirty(mat);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }
}
