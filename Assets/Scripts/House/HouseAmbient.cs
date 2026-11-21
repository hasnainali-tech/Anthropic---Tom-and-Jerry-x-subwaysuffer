using UnityEngine;

// Makes the house feel alive: ceiling fan, swaying curtains, and a day -> sunset -> night
// -> dawn cycle (sun angle/colour, window sky, ambient light, room lamps switching on).
public class HouseAmbient : MonoBehaviour
{
    public float dayLengthSeconds = 360f;
    [Range(0f, 1f)] public float startTime = 0.3f;    // 0 = dawn, 0.5 = sunset-ish, 0.75 = midnight

    HouseBuilder house;
    Light sun;
    float t;
    Color skyDay = new Color(1.25f, 1.25f, 1.25f), skyDusk = new Color(1.3f, 0.75f, 0.55f), skyNight = new Color(0.12f, 0.16f, 0.35f);

    public float Night01 { get; private set; }

    void Start()
    {
        house = GetComponent<HouseBuilder>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) sun = l;
        t = startTime;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (house && house.Fan) house.Fan.Rotate(0f, 160f * dt, 0f, Space.World);
        if (house)
            for (int i = 0; i < house.Curtains.Count; i++)
            {
                var c = house.Curtains[i];
                float s = Mathf.Sin(Time.time * 0.9f + i * 1.7f) * 2.2f + Mathf.Sin(Time.time * 2.3f + i) * 0.8f;
                c.localRotation = Quaternion.Euler(s, 0f, s * 0.4f);
            }

        // ---- day / night
        if (HouseGame.I && HouseGame.I.phase == HouseGame.Phase.Playing) t = Mathf.Repeat(t + dt / dayLengthSeconds, 1f);
        // night strength: 0 during the day, ramps up through sunset, 1 at midnight
        float night = Mathf.Clamp01(Mathf.InverseLerp(0.52f, 0.66f, t)) * (1f - Mathf.Clamp01(Mathf.InverseLerp(0.9f, 0.99f, t)));
        float dusk = Mathf.Clamp01(1f - Mathf.Abs(t - 0.56f) / 0.08f) + Mathf.Clamp01(1f - Mathf.Abs(t - 0.95f) / 0.05f) * 0.6f;
        Night01 = night;
        if (sun)
        {
            float ang = Mathf.Lerp(15f, 165f, Mathf.Clamp01(t / 0.62f));
            sun.transform.rotation = Quaternion.Euler(Mathf.Sin(ang * Mathf.Deg2Rad) * 55f + 8f, -30f + t * 120f, 0f);
            sun.intensity = Mathf.Lerp(1.45f, 0.12f, night);
            sun.color = Color.Lerp(Color.Lerp(new Color(1f, 0.93f, 0.82f), new Color(1f, 0.62f, 0.38f), dusk), new Color(0.55f, 0.65f, 1f), night);
        }
        float amb = Mathf.Lerp(1f, 0.38f, night);
        RenderSettings.ambientSkyColor = new Color(0.86f, 0.82f, 0.78f) * amb;
        RenderSettings.ambientEquatorColor = new Color(0.66f, 0.6f, 0.55f) * amb;
        RenderSettings.ambientGroundColor = new Color(0.4f, 0.34f, 0.3f) * amb;
        if (house)
        {
            if (house.WindowSky) house.WindowSky.SetColor("_BaseColor", Color.Lerp(Color.Lerp(skyDay, skyDusk, dusk), skyNight, night));
            foreach (var l in house.RoomLights) if (l) l.intensity = Mathf.Lerp(0f, 2.4f, Mathf.InverseLerp(0.3f, 1f, night));
        }
        var cam = Camera.main;
        if (cam) cam.backgroundColor = Color.Lerp(new Color(0.62f, 0.78f, 0.95f), new Color(0.05f, 0.07f, 0.16f), night);
    }
}
