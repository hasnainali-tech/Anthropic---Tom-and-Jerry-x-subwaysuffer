using UnityEngine;
using UnityEngine.Rendering.Universal;

// Player-facing settings stored in PlayerPrefs. Graphics "Low" turns off real-time
// shadows and post-processing for older Android phones (no asset is modified).
public static class GameSettings
{
    const string VibKey = "Vibration", GfxKey = "HighGraphics";

    public static bool Vibration
    {
        get => PlayerPrefs.GetInt(VibKey, 1) == 1;
        set { PlayerPrefs.SetInt(VibKey, value ? 1 : 0); }
    }

    public static bool HighGraphics
    {
        get => PlayerPrefs.GetInt(GfxKey, 1) == 1;
        set { PlayerPrefs.SetInt(GfxKey, value ? 1 : 0); Apply(); }
    }

    public static void Apply()
    {
        bool hi = HighGraphics;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.shadows = hi ? LightShadows.Soft : LightShadows.None;
        var cam = Camera.main;
        if (cam)
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data) data.renderPostProcessing = hi;
        }
    }

    public static void Vibrate()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (Vibration) Handheld.Vibrate();
#endif
    }
}
