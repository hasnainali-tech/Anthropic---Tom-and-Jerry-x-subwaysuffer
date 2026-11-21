using System.Collections.Generic;
using UnityEngine;

// Base for the game's two original characters (MouseHero, CatChaser).
// Characters are assembled from smooth primitive parts on a pivot hierarchy and animated
// procedurally (pose targets + critically damped smoothing), so every state blends into
// the next with no popping, and run cycles follow the real movement speed.
// All parts share URP Lit materials cloned from GameArt.litBase (safe for Android builds).
public abstract class ToonCharacter : MonoBehaviour
{
    protected Transform model;                     // scaled root of all parts
    protected readonly List<Renderer> parts = new List<Renderer>();
    static readonly Dictionary<long, Material> mats = new Dictionary<long, Material>();

    protected Transform eyes;                      // scaled on Y to blink
    float nextBlink, blinkT = -1f;
    protected float eyeOpen = 1f;                  // 1 = normal, >1 = wide (scared)

    // ---------------------------------------------------------------- building
    protected Material Mat(Color c, float smooth = 0.32f)
    {
        long key = ((long)(c.r * 255) << 40) | ((long)(c.g * 255) << 32) | ((long)(c.b * 255) << 24) | (long)(smooth * 100);
        if (mats.TryGetValue(key, out var m) && m) return m;
        var art = GameArt.Get();
        var baseM = art && art.litBase ? art.litBase : null;
        m = baseM ? new Material(baseM) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        mats[key] = m;
        return m;
    }

    protected Transform Pivot(Transform parent, string name, Vector3 pos, Vector3 euler = default)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(euler);
        return t;
    }

    protected Transform Part(Transform parent, PrimitiveType type, Color c, Vector3 pos, Vector3 scale,
                             Vector3 euler = default, float smooth = 0.32f, bool shadow = true)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = type.ToString();
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = Mat(c, smooth);
        r.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = true;
        parts.Add(r);
        return go.transform;
    }

    public void SetVisible(bool v)
    {
        foreach (var r in parts) if (r) r.enabled = v;
    }

    // ---------------------------------------------------------------- animation helpers
    protected static float Damp(float sharp, float dt) => 1f - Mathf.Exp(-sharp * dt);

    protected static void Rot(Transform t, Vector3 euler, float k)
    {
        if (t) t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(euler), k);
    }

    protected static void Pos(Transform t, Vector3 p, float k)
    {
        if (t) t.localPosition = Vector3.Lerp(t.localPosition, p, k);
    }

    protected static void Scl(Transform t, Vector3 s, float k)
    {
        if (t) t.localScale = Vector3.Lerp(t.localScale, s, k);
    }

    protected void UpdateBlink(float dt)
    {
        if (!eyes) return;
        if (blinkT < 0f && Time.time > nextBlink) { blinkT = 0f; nextBlink = Time.time + Random.Range(2.2f, 5f); }
        float open = eyeOpen;
        if (blinkT >= 0f)
        {
            blinkT += dt;
            float k = blinkT / 0.14f;
            open = eyeOpen * Mathf.Lerp(1f, 0.1f, 1f - Mathf.Abs(k * 2f - 1f));
            if (k >= 1f) blinkT = -1f;
        }
        eyes.localScale = new Vector3(eyeOpen, open, 1f);
    }

    // tail / scarf: a chain of pivots following a travelling wave
    protected static void Wave(List<Transform> chain, float t, float baseX, float ampY, float ampX, float freq, float lag, float k)
    {
        for (int i = 0; i < chain.Count; i++)
        {
            float w = Mathf.Sin(t * freq - i * lag);
            float fall = 0.5f + 0.5f * i / Mathf.Max(1, chain.Count - 1);
            Rot(chain[i], new Vector3((i == 0 ? baseX : baseX * 0.25f) + w * ampX * fall, w * ampY * fall, 0f), k);
        }
    }
}
