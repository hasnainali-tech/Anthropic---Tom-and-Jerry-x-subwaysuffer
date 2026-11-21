using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Tools > Cheese Escape > Create House Scene
// Builds Assets/Scenes/House.unity (free-roam game) and makes it the first scene in the build.
// The old runner scene (SampleScene) is kept in the project but removed from the build.
public static class HouseSceneSetup
{
    const string ScenePath = "Assets/Scenes/House.unity";

    [MenuItem("Tools/Cheese Escape/Create House Scene")]
    public static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // camera
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.62f, 0.78f, 0.95f);
        cam.nearClipPlane = 0.05f; cam.farClipPlane = 160f; cam.fieldOfView = 60f;
        camGo.AddComponent<AudioListener>();
        var cad = camGo.AddComponent<UniversalAdditionalCameraData>();
        cad.renderPostProcessing = true;
        cad.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        camGo.transform.position = new Vector3(15f, 3f, 5f);

        // sun
        var lightGo = new GameObject("Sun");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.93f, 0.82f); light.intensity = 1.45f;
        light.shadows = LightShadows.Soft; light.shadowStrength = 0.7f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        lightGo.AddComponent<UniversalAdditionalLightData>();

        // post processing
        var vol = new GameObject("Global Volume").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");

        // audio (same clips as before)
        var audioGo = new GameObject("AudioManager");
        var am = audioGo.AddComponent<AudioManager>();
        am.backgroundMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sfx/BackgroundMusic.wav");
        am.jump = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sfx/Jump.wav");
        am.slide = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sfx/Slide.wav");
        am.chewCheese = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sfx/ChewCheese.wav");

        // game
        var game = new GameObject("Game");
        var builder = game.AddComponent<HouseBuilder>();
        builder.furniture = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Mnostva_Art/Prefabs" })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).Where(p => p).ToArray();
        game.AddComponent<HouseGame>();
        game.AddComponent<HouseAmbient>();

        // lighting
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.86f, 0.82f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.66f, 0.6f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.4f, 0.34f, 0.3f);
        RenderSettings.skybox = null;
        RenderSettings.fog = false;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Art/Generated/Env_Reflection.cubemap");
        RenderSettings.reflectionIntensity = 0.5f;

        EditorSceneManager.SaveScene(scene, ScenePath);

        // build settings: House first; the runner scene stays in the project but out of the build
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        // free-roam plays best in landscape (joystick left, camera right)
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        AssetDatabase.SaveAssets();
        Debug.Log("[Cheese Escape] House scene created: " + ScenePath);
    }
}
