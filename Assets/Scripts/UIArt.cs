using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Procedurally drawn UI sprites (signed-distance shapes, anti-aliased), generated once
// at startup: rounded panels/buttons (9-sliced), circles, soft vignette, and a small
// icon set (play, pause, gear, speaker, music, home, restart, close, star, cheese).
public static class UIArt
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Rounded => Get("rounded", () => RoundedRect(96, 40), new Vector4(44, 44, 44, 44));
    public static Sprite Pill => Get("pill", () => RoundedRect(128, 63), new Vector4(63, 63, 63, 63));
    public static Sprite Circle => Get("circle", () => Draw(128, p => Circ(p, Vector2.zero, 0.98f)), Vector4.zero);
    public static Sprite Gloss => Get("gloss", () => Gradient(8, 64, 0.0f, 0.55f), Vector4.zero);
    public static Sprite Vignette => Get("vignette", () => VignetteTex(256), Vector4.zero);
    public static Sprite Soft => Get("soft", () => Draw(128, p => Mathf.Clamp01(1f - p.magnitude), true), Vector4.zero);

    public static Sprite Play => Icon("play", p => Poly(p, new Vector2(-0.45f, -0.6f), new Vector2(0.65f, 0f), new Vector2(-0.45f, 0.6f)) - 0.06f);
    public static Sprite Pause => Icon("pause", p => Mathf.Min(Box(p - new Vector2(-0.28f, 0f), new Vector2(0.15f, 0.6f), 0.08f),
                                                               Box(p - new Vector2(0.28f, 0f), new Vector2(0.15f, 0.6f), 0.08f)));
    public static Sprite Gear => Icon("gear", GearSdf);
    public static Sprite SpeakerOn => Icon("spk_on", p => Mathf.Min(Speaker(p), Mathf.Min(Arc(p, 0.42f, 0.07f), Arc(p, 0.7f, 0.07f))));
    public static Sprite SpeakerOff => Icon("spk_off", p => Mathf.Min(Speaker(p), Mathf.Min(Seg(p, new Vector2(0.3f, -0.3f), new Vector2(0.85f, 0.3f), 0.08f),
                                                                                     Seg(p, new Vector2(0.3f, 0.3f), new Vector2(0.85f, -0.3f), 0.08f))));
    public static Sprite Music => Icon("music", p => Mathf.Min(Mathf.Min(Circ(p, new Vector2(-0.35f, -0.5f), 0.22f), Circ(p, new Vector2(0.45f, -0.35f), 0.22f)),
                                                             Mathf.Min(Mathf.Min(Box(p - new Vector2(-0.16f, 0.05f), new Vector2(0.06f, 0.55f), 0f),
                                                                                 Box(p - new Vector2(0.64f, 0.18f), new Vector2(0.06f, 0.55f), 0f)),
                                                                       Seg(p, new Vector2(-0.16f, 0.58f), new Vector2(0.64f, 0.72f), 0.1f))));
    public static Sprite Home => Icon("home", p => Mathf.Max(Mathf.Min(Poly(p, new Vector2(-0.75f, 0.05f), new Vector2(0f, 0.75f), new Vector2(0.75f, 0.05f)),
                                                                     Box(p - new Vector2(0f, -0.3f), new Vector2(0.52f, 0.38f), 0.04f)),
                                                          -Box(p - new Vector2(0f, -0.42f), new Vector2(0.15f, 0.27f), 0.03f)));
    public static Sprite Restart => Icon("restart", p =>
    {
        float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
        float ring = Mathf.Abs(p.magnitude - 0.55f) - 0.11f;
        if (ang > 50f && ang < 110f) ring = Mathf.Max(ring, 0.2f);
        float head = Poly(p, new Vector2(0.02f, 0.92f), new Vector2(0.48f, 0.55f), new Vector2(0.02f, 0.2f));
        return Mathf.Min(ring, head);
    });
    public static Sprite Close => Icon("close", p => Mathf.Min(Seg(p, new Vector2(-0.5f, -0.5f), new Vector2(0.5f, 0.5f), 0.13f),
                                                             Seg(p, new Vector2(-0.5f, 0.5f), new Vector2(0.5f, -0.5f), 0.13f)));
    public static Sprite Star => Icon("star", p => Star5(p, 0.85f, 0.42f) - 0.04f);
    public static Sprite Check => Icon("check", p => Mathf.Min(Seg(p, new Vector2(-0.55f, 0f), new Vector2(-0.15f, -0.4f), 0.13f),
                                                             Seg(p, new Vector2(-0.15f, -0.4f), new Vector2(0.6f, 0.45f), 0.13f)));
    public static Sprite Cheese => Get("cheese_icon", CheeseIcon, Vector4.zero);

    // ---- scrapbook UI pieces ----
    // bottle cap: crimped (scalloped) edge
    public static Sprite BottleCap => Get("bottlecap", () => Draw(160, p =>
    {
        float a = Mathf.Atan2(p.y, p.x);
        float r = 0.9f + 0.045f * Mathf.Sin(a * 21f);
        return p.magnitude - r;
    }), Vector4.zero);
    public static Sprite CapRing => Get("capring", () => Draw(160, p => Mathf.Abs(p.magnitude - 0.68f) - 0.035f), Vector4.zero);
    public static Sprite Ring => Get("ring", () => Draw(160, p => Mathf.Abs(p.magnitude - 0.9f) - 0.06f), Vector4.zero);
    // paper card with slightly uneven, hand-cut edges (9-sliced)
    public static Sprite PaperCard => Get("paper", () => PaperTex(128), new Vector4(40, 40, 40, 40));
    public static Sprite Ruled => Get("ruled", () => RuledTex(), Vector4.zero);
    public static Sprite Tape => Get("tape", () => TapeTex(), Vector4.zero);
    public static Sprite Crate => Icon("crate", p => Mathf.Max(Box(p - new Vector2(0f, -0.1f), new Vector2(0.75f, 0.55f), 0.06f),
                                                             -Mathf.Min(Box(p - new Vector2(0f, 0.12f), new Vector2(0.6f, 0.04f), 0f), Box(p - new Vector2(0f, -0.25f), new Vector2(0.6f, 0.04f), 0f))));
    public static Sprite StarFull => Icon("star_full", p => Star5(p, 0.92f, 0.45f) - 0.02f);
    public static Sprite StarEmpty => Icon("star_empty", p => Mathf.Abs(Star5(p, 0.86f, 0.45f)) - 0.05f);

    public static Sprite Food(string type) => type switch
    {
        "cheese" => Cheese,
        "cookie" => Icon("food_cookie", p => Mathf.Max(Circ(p, Vector2.zero, 0.8f), -Mathf.Min(Mathf.Min(Circ(p, new Vector2(-0.3f, 0.25f), 0.12f), Circ(p, new Vector2(0.3f, 0.1f), 0.11f)), Mathf.Min(Circ(p, new Vector2(0f, -0.35f), 0.12f), Circ(p, new Vector2(-0.35f, -0.2f), 0.09f))))),
        "bread" => Icon("food_bread", p => Mathf.Min(Box(p - new Vector2(0f, -0.2f), new Vector2(0.7f, 0.45f), 0.12f), Circ(p, new Vector2(0f, 0.25f), 0.62f) + 0.0f)),
        "grapes" => Icon("food_grapes", p => Mathf.Min(Mathf.Min(Mathf.Min(Circ(p, new Vector2(-0.3f, 0.3f), 0.26f), Circ(p, new Vector2(0.3f, 0.3f), 0.26f)),
                                                                Mathf.Min(Circ(p, new Vector2(0f, -0.05f), 0.26f), Circ(p, new Vector2(-0.3f, -0.15f), 0.0f))),
                                                     Mathf.Min(Circ(p, new Vector2(0f, -0.48f), 0.24f), Seg(p, new Vector2(0f, 0.55f), new Vector2(0.1f, 0.85f), 0.06f)))),
        _ => Icon("food_peanut", p => Mathf.Min(Circ(p, new Vector2(-0.28f, 0.05f), 0.42f), Circ(p, new Vector2(0.3f, -0.05f), 0.38f))),
    };

    static Texture2D PaperTex(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        float h = s * 0.5f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                var p = new Vector2(x + 0.5f - h, y + 0.5f - h);
                // wobbly edge = hand-cut paper
                float wob = (Mathf.PerlinNoise(x * 0.15f, y * 0.15f) - 0.5f) * 3f;
                float d = Box(p, new Vector2(h - 3f, h - 3f), 10f) + wob;
                float a = Mathf.Clamp01(0.5f - d);
                float fiber = 0.96f + Mathf.PerlinNoise(x * 0.6f, y * 0.6f) * 0.04f;
                tex.SetPixel(x, y, new Color(fiber, fiber, fiber, a));
            }
        tex.Apply(false, false);
        return tex;
    }

    static Texture2D RuledTex()
    {
        var tex = new Texture2D(16, 48, TextureFormat.RGBA32, false);
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 16; x++)
                tex.SetPixel(x, y, new Color(0.45f, 0.62f, 0.85f, y <= 1 ? 0.45f : 0f));
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply(false, false);
        return tex;
    }

    static Texture2D TapeTex()
    {
        int w = 128, hgt = 40;
        var tex = new Texture2D(w, hgt, TextureFormat.RGBA32, false);
        for (int y = 0; y < hgt; y++)
            for (int x = 0; x < w; x++)
            {
                // zig-zag torn ends
                float tooth = Mathf.Abs(Mathf.Repeat(y / 5f, 1f) - 0.5f) * 6f;
                bool inside = x > tooth && x < w - 1 - tooth;
                float n = 0.93f + Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.07f;
                tex.SetPixel(x, y, new Color(0.97f * n, 0.92f * n, 0.78f * n, inside ? 0.78f : 0f));
            }
        tex.Apply(false, false);
        return tex;
    }

    // ---- free-roam HUD icons ----
    public static Sprite Heart => Icon("heart", p =>
    {
        var q = new Vector2(Mathf.Abs(p.x), p.y + 0.55f) * 1.25f;
        float d;
        if (q.y + q.x > 1f) d = (q - new Vector2(0.25f, 0.75f)).magnitude - 0.3536f;
        else
        {
            float a = (q - new Vector2(0f, 1f)).sqrMagnitude;
            float m = 0.5f * Mathf.Max(q.x + q.y, 0f);
            float b = (q - new Vector2(m, m)).sqrMagnitude;
            d = Mathf.Sqrt(Mathf.Min(a, b)) * Mathf.Sign(q.x - q.y);
        }
        return d / 1.25f;
    });
    public static Sprite Eye => Icon("eye", p =>
    {
        float lens = Mathf.Max(Circ(p, new Vector2(0f, -0.62f), 1.05f), Circ(p, new Vector2(0f, 0.62f), 1.05f));
        float ring = Mathf.Max(lens, -Circ(p, Vector2.zero, 0.36f));
        return Mathf.Min(ring, Circ(p, Vector2.zero, 0.2f));
    });
    public static Sprite Key => Icon("key", p => Mathf.Min(Mathf.Max(Circ(p, new Vector2(-0.45f, 0f), 0.32f), -Circ(p, new Vector2(-0.45f, 0f), 0.14f)),
                                                         Mathf.Min(Box(p - new Vector2(0.2f, 0f), new Vector2(0.45f, 0.08f), 0.02f),
                                                                   Mathf.Min(Box(p - new Vector2(0.45f, -0.15f), new Vector2(0.06f, 0.14f), 0f),
                                                                             Box(p - new Vector2(0.62f, -0.12f), new Vector2(0.06f, 0.1f), 0f)))));
    public static Sprite Note => Icon("note", p => Mathf.Max(Box(p, new Vector2(0.55f, 0.7f), 0.08f),
                                                           -Mathf.Min(Box(p - new Vector2(0f, 0.3f), new Vector2(0.35f, 0.05f), 0f),
                                                                      Mathf.Min(Box(p - new Vector2(0f, 0.05f), new Vector2(0.35f, 0.05f), 0f),
                                                                                Box(p - new Vector2(-0.1f, -0.2f), new Vector2(0.25f, 0.05f), 0f)))));
    public static Sprite Bag => Icon("bag", p => Mathf.Min(Box(p - new Vector2(0f, -0.2f), new Vector2(0.62f, 0.5f), 0.15f),
                                                         Mathf.Max(Mathf.Abs((p - new Vector2(0f, 0.3f)).magnitude - 0.3f) - 0.08f, -(p.y - 0.3f))));
    public static Sprite MapPin => Icon("map", p => Mathf.Max(Mathf.Min(Circ(p, new Vector2(0f, 0.25f), 0.45f), Poly(p, new Vector2(-0.39f, 0.03f), new Vector2(0f, -0.75f), new Vector2(0.39f, 0.03f))),
                                                            -Circ(p, new Vector2(0f, 0.25f), 0.18f)));
    public static Sprite Bolt => Icon("bolt", p => Mathf.Min(Poly(p, new Vector2(0.15f, 0.85f), new Vector2(-0.45f, -0.05f), new Vector2(0.05f, -0.05f)),
                                                           Poly(p, new Vector2(-0.05f, 0.05f), new Vector2(0.45f, 0.05f), new Vector2(-0.15f, -0.85f))) - 0.02f);
    public static Sprite ChevronUp => Icon("chev_up", p => Mathf.Min(Seg(p, new Vector2(-0.5f, -0.2f), new Vector2(0f, 0.35f), 0.13f), Seg(p, new Vector2(0f, 0.35f), new Vector2(0.5f, -0.2f), 0.13f)));
    public static Sprite ChevronDown => Icon("chev_down", p => Mathf.Min(Seg(p, new Vector2(-0.5f, 0.2f), new Vector2(0f, -0.35f), 0.13f), Seg(p, new Vector2(0f, -0.35f), new Vector2(0.5f, 0.2f), 0.13f)));
    public static Sprite Hand => Icon("hand", p => Mathf.Min(Box(p - new Vector2(0f, -0.25f), new Vector2(0.42f, 0.38f), 0.18f),
                                                           Mathf.Min(Mathf.Min(Seg(p, new Vector2(-0.3f, 0f), new Vector2(-0.3f, 0.55f), 0.1f), Seg(p, new Vector2(-0.1f, 0f), new Vector2(-0.1f, 0.72f), 0.1f)),
                                                                     Mathf.Min(Seg(p, new Vector2(0.1f, 0f), new Vector2(0.1f, 0.68f), 0.1f), Seg(p, new Vector2(0.3f, 0f), new Vector2(0.3f, 0.5f), 0.1f)))));

    // ------------------------------------------------------------------ infra
    static Sprite Get(string key, Func<Texture2D> make, Vector4 border)
    {
        if (cache.TryGetValue(key, out var s) && s) return s;
        var tex = make();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        s.name = key;
        cache[key] = s;
        return s;
    }

    static Sprite Icon(string key, Func<Vector2, float> sdf) => Get(key, () => Draw(128, sdf), Vector4.zero);

    // sdf in normalized space [-1,1]; alpha from distance with 1.2px AA
    static Texture2D Draw(int size, Func<Vector2, float> sdf, bool rawAlpha = false)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var px = new Color32[size * size];
        float pxSize = 2f / size;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float a = rawAlpha ? sdf(p) : Mathf.Clamp01(0.5f - sdf(p) / (pxSize * 1.2f));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply(true, false);
        return tex;
    }

    static Texture2D RoundedRect(int size, float radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        float h = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - h, y + 0.5f - h);
                float d = Box(p, new Vector2(h - 1f, h - 1f), radius);
                byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }

    static Texture2D Gradient(int w, int h, float aBottom, float aTop)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Lerp(aBottom, aTop, y / (float)(h - 1))));
        tex.Apply(false, false);
        return tex;
    }

    static Texture2D VignetteTex(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 0.8f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((r - 0.55f) / 0.55f)));
            }
        tex.Apply(false, false);
        return tex;
    }

    static Texture2D CheeseIcon()
    {
        int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, true);
        Color top = new Color(1f, 0.86f, 0.3f), side = new Color(0.97f, 0.66f, 0.12f), holeC = new Color(0.86f, 0.55f, 0.08f);
        Color outline = new Color(0.36f, 0.18f, 0.05f);
        float aa = 2f / s * 1.2f;
        Vector2 A = new Vector2(-0.85f, -0.1f), B = new Vector2(0.85f, -0.1f), C = new Vector2(0.35f, 0.72f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                var p = new Vector2((x + 0.5f) / s * 2f - 1f, (y + 0.5f) / s * 2f - 1f);
                float dTop = Poly(p, A, B, C);
                float dSide = Box(p - new Vector2(0f, -0.38f), new Vector2(0.85f, 0.28f), 0.02f);
                float dAll = Mathf.Min(dTop, dSide);
                Color c = dSide < 0f && p.y < -0.1f ? side : top;
                float holes = Mathf.Min(Mathf.Min(Circ(p, new Vector2(-0.35f, -0.4f), 0.13f), Circ(p, new Vector2(0.4f, -0.32f), 0.1f)),
                                        Mathf.Min(Circ(p, new Vector2(0.2f, 0.28f), 0.14f), Circ(p, new Vector2(-0.25f, 0.05f), 0.08f)));
                if (holes < 0f) c = holeC;
                float edge = Mathf.Clamp01(0.5f - (Mathf.Abs(dAll) - 0.035f) / aa);
                c = Color.Lerp(c, outline, edge);
                float a = Mathf.Clamp01(0.5f - (dAll - 0.035f) / aa);
                tex.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
            }
        tex.Apply(true, false);
        return tex;
    }

    // ------------------------------------------------------------------ SDF primitives
    static float Circ(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

    static float Box(Vector2 p, Vector2 b, float r)
    {
        var q = new Vector2(Mathf.Abs(p.x) - b.x + r, Mathf.Abs(p.y) - b.y + r);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    static float Seg(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }

    // convex polygon (counter-clockwise or clockwise), approx outside distance
    static float Poly(Vector2 p, params Vector2[] v)
    {
        float d = float.MinValue;
        float sign = Mathf.Sign(Cross(v[1] - v[0], v[2] - v[0]));
        for (int i = 0; i < v.Length; i++)
        {
            Vector2 a = v[i], b = v[(i + 1) % v.Length];
            Vector2 n = new Vector2(b.y - a.y, a.x - b.x).normalized * sign;
            d = Mathf.Max(d, Vector2.Dot(p - a, n));
        }
        return d;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static float Arc(Vector2 p, float r, float w)
    {
        float ang = Mathf.Abs(Mathf.Atan2(p.y, p.x - 0.05f) * Mathf.Rad2Deg);
        float d = Mathf.Abs((p - new Vector2(0.05f, 0f)).magnitude - r) - w;
        return ang < 50f ? d : Mathf.Max(d, 0.3f);
    }

    static float Speaker(Vector2 p) => Mathf.Min(Box(p - new Vector2(-0.55f, 0f), new Vector2(0.18f, 0.22f), 0.04f),
                                                 Poly(p, new Vector2(-0.5f, -0.22f), new Vector2(0.05f, -0.6f), new Vector2(0.05f, 0.6f), new Vector2(-0.5f, 0.22f)));

    static float GearSdf(Vector2 p)
    {
        float ang = Mathf.Atan2(p.y, p.x);
        float teeth = Mathf.Clamp(Mathf.Cos(ang * 8f) * 3f, -1f, 1f) * 0.5f + 0.5f;
        float outer = p.magnitude - (0.62f + teeth * 0.2f);
        return Mathf.Max(outer, 0.24f - p.magnitude);
    }

    static float Star5(Vector2 p, float r, float rf)
    {
        // polar star: interpolate radius between points and valleys
        float ang = Mathf.Atan2(p.x, p.y);
        float seg = Mathf.PI * 2f / 5f;
        float a = Mathf.Repeat(ang + seg * 0.5f, seg) - seg * 0.5f;
        float t = Mathf.Abs(a) / (seg * 0.5f);
        float rad = Mathf.Lerp(r, r * rf, t);
        return (p.magnitude - rad) * 0.7f;
    }

    // ------------------------------------------------------------------ helpers for widgets
    public static Image Img(Transform parent, string name, Sprite s, Color c, bool sliced = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = s;
        img.color = c;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }
}

// Vertical colour gradient for uGUI Text/Image (top -> bottom).
public class UIGradient : BaseMeshEffect
{
    public Color top = Color.white, bottom = Color.gray;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        var v = new UIVertex();
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < vh.currentVertCount; i++) { vh.PopulateUIVertex(ref v, i); min = Mathf.Min(min, v.position.y); max = Mathf.Max(max, v.position.y); }
        float h = Mathf.Max(0.001f, max - min);
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            Color c = Color.Lerp(bottom, top, (v.position.y - min) / h);
            v.color = c * (Color)v.color;
            vh.SetUIVertex(v, i);
        }
    }
}
