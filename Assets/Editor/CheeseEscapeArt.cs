using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Tools > Cheese Escape > Generate Art
// Paints every environment texture procedurally (tileable, with normal maps), builds the
// URP materials, the app icon, and fills Assets/Resources/GameArt.asset.
// Re-runnable: overwrites its own outputs in Assets/Art/Generated only.
public static class CheeseEscapeArt
{
    const string Root = "Assets/Art/Generated";
    const string TexDir = Root + "/Textures";
    const string MatDir = Root + "/Materials";

    [MenuItem("Tools/Cheese Escape/Generate Art")]
    public static void Generate()
    {
        Directory.CreateDirectory(TexDir);
        Directory.CreateDirectory(MatDir);
        Directory.CreateDirectory("Assets/Resources");

        // ---------------- textures
        var kitchenTiles = Save("Floor_KitchenTiles", KitchenTiles(512, out var hKitchen), aniso: 8);
        var kitchenN = SaveNormal("Floor_KitchenTiles_N", hKitchen, 512, 3f);
        var planks = Save("Floor_WoodPlanks", WoodPlanks(512, out var hPlanks), aniso: 8);
        var planksN = SaveNormal("Floor_WoodPlanks_N", hPlanks, 512, 2.5f);
        var carpet = Save("Floor_Carpet", Carpet(256), aniso: 4);
        var wpKitchen = Save("Wall_Kitchen", WallpaperKitchen(256));
        var wpLiving = Save("Wall_Living", WallpaperLiving(256));
        var wpBedroom = Save("Wall_Bedroom", WallpaperBedroom(256));
        var subway = Save("Wainscot_Subway", SubwayTiles(256, out var hSubway));
        var subwayN = SaveNormal("Wainscot_Subway_N", hSubway, 256, 3f);
        var panel = Save("Wainscot_Panel", Panel(256, out var hPanel));
        var panelN = SaveNormal("Wainscot_Panel_N", hPanel, 256, 4f);
        var trapWood = Save("Trap_Wood", TrapWood(256));
        var sky = Save("Window_Sky", Sky(128, 256), clamp: true);
        var soft = Save("FX_SoftCircle", SoftCircle(128), clamp: true, alpha: true);
        var star = Save("FX_Sparkle", Sparkle(128), clamp: true, alpha: true);

        // ---------------- materials
        var art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/Resources/GameArt.asset");
        if (!art) { art = ScriptableObject.CreateInstance<GameArt>(); AssetDatabase.CreateAsset(art, "Assets/Resources/GameArt.asset"); }

        art.litBase = Lit("Mat_LitBase", null, null, 1f, Color.white, 0.3f);
        art.unlitBase = Unlit("Mat_UnlitBase", null, Color.white);

        art.floors = new[]
        {
            Lit("Mat_Floor_Kitchen", kitchenTiles, kitchenN, 5f, Color.white, 0.62f, 0f, 1f),
            Lit("Mat_Floor_Living", planks, planksN, 6f, Color.white, 0.5f, 0f, 1f),
            Lit("Mat_Floor_Bedroom", carpet, null, 3f, Color.white, 0.04f),
        };
        art.walls = new[]
        {
            Lit("Mat_Wall_Kitchen", wpKitchen, null, 2.5f, Color.white, 0.12f),
            Lit("Mat_Wall_Living", wpLiving, null, 2.5f, Color.white, 0.12f),
            Lit("Mat_Wall_Bedroom", wpBedroom, null, 2.5f, Color.white, 0.12f),
        };
        art.wallsFade = new[]
        {
            Lit("Mat_Wall_Kitchen_Fade", wpKitchen, null, 2.5f, Color.white, 0.12f, 0f, 1f, true),
            Lit("Mat_Wall_Living_Fade", wpLiving, null, 2.5f, Color.white, 0.12f, 0f, 1f, true),
            Lit("Mat_Wall_Bedroom_Fade", wpBedroom, null, 2.5f, Color.white, 0.12f, 0f, 1f, true),
        };
        art.wainscots = new[]
        {
            Lit("Mat_Wainscot_Kitchen", subway, subwayN, 2.5f, Color.white, 0.75f, 0f, 0.8f),
            Lit("Mat_Wainscot_Living", panel, panelN, 2.5f, new Color(0.24f, 0.45f, 0.47f), 0.35f, 0f, 1f),
            Lit("Mat_Wainscot_Bedroom", panel, panelN, 2.5f, new Color(0.96f, 0.95f, 0.98f), 0.3f, 0f, 1f),
        };
        art.trim = Lit("Mat_Trim", null, null, 1f, new Color(0.97f, 0.95f, 0.91f), 0.45f);
        art.ceiling = Lit("Mat_Ceiling", null, null, 1f, new Color(0.96f, 0.94f, 0.9f), 0.05f);
        art.windowSky = Unlit("Mat_WindowSky", sky, new Color(1.25f, 1.25f, 1.25f));
        art.holeDark = Lit("Mat_HoleDark", null, null, 1f, new Color(0.1f, 0.065f, 0.045f), 0.0f);
        art.trapWood = Lit("Mat_TrapWood", trapWood, null, 1f, Color.white, 0.25f);
        art.metal = Lit("Mat_Metal", null, null, 1f, new Color(0.78f, 0.79f, 0.82f), 0.75f, 0.9f);
        art.cheese = Lit("Mat_Cheese", null, null, 1f, new Color(1f, 0.77f, 0.18f), 0.45f);
        art.cheese.EnableKeyword("_EMISSION");
        art.cheese.SetColor("_EmissionColor", new Color(0.35f, 0.22f, 0.02f));
        art.cheese.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(art.cheese);

        art.glow = Particles("Mat_FX_Glow", soft, new Color(1f, 0.85f, 0.35f, 0.55f), additive: true);
        art.blobShadow = Particles("Mat_FX_BlobShadow", soft, new Color(0.08f, 0.05f, 0.03f, 0.45f), additive: false);
        art.sparkle = Particles("Mat_FX_Sparkle", star, Color.white, additive: true);
        art.confetti = Particles("Mat_FX_Confetti", null, Color.white, additive: false);

        art.titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/LuckiestGuy-Regular.ttf");
        art.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/LilitaOne-Regular.ttf");
        EditorUtility.SetDirty(art);

        // ---------------- warm indoor reflections (instead of the default blue sky)
        var refl = ReflectionCubemap();
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = refl;
        RenderSettings.skybox = null;   // nothing in the game should ever show or reflect the sky

        // ---------------- app icon
        var icon = Save("App_Icon", AppIcon(512), clamp: true, aniso: 0, isIcon: true);
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

        AssetDatabase.SaveAssets();
        Debug.Log("[Cheese Escape] Art generated.");
    }

    // ===================================================================== materials
    static Material GetOrCreate(string name, string shader)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find(shader);
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        else m.shader = sh;
        return m;
    }

    static Material Lit(string name, Texture2D tex, Texture2D normal, float worldSize, Color color,
                        float smooth, float metallic = 0f, float bump = 1f, bool transparent = false)
    {
        var m = GetOrCreate(name, "Universal Render Pipeline/Lit");
        m.SetTexture("_BaseMap", tex);
        m.SetTextureScale("_BaseMap", Vector2.one / worldSize);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metallic);
        m.SetTexture("_BumpMap", normal);
        m.SetFloat("_BumpScale", bump);
        m.SetFloat("_Surface", transparent ? 1f : 0f);
        m.SetFloat("_Blend", 0f);
        if (transparent)
        {
            // plain alpha blending: reflections/highlights fade WITH the surface
            // ("preserve specular" kept a full-strength sky tint on an almost invisible wall)
            m.SetFloat("_BlendModePreserveSpecular", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
        }
        m.SetFloat("_ReceiveShadows", 1f);
        m.SetFloat("_EnvironmentReflections", smooth > 0.3f ? 1f : 0f);
        Validate(m, "LitShader");
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Unlit(string name, Texture2D tex, Color color)
    {
        var m = GetOrCreate(name, "Universal Render Pipeline/Unlit");
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", color);
        Validate(m, "UnlitShader");
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Particles(string name, Texture2D tex, Color color, bool additive)
    {
        var m = GetOrCreate(name, "Universal Render Pipeline/Particles/Unlit");
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);   // 0 alpha, 2 additive
        Validate(m, "ParticlesUnlitShader");
        EditorUtility.SetDirty(m);
        return m;
    }

    // Run URP's own ShaderGUI validation so keywords, blend states and queues are correct
    // (exactly what the Inspector would do) - otherwise transparent/normal-mapped variants
    // can be wrong or stripped in the player.
    static void Validate(Material m, string guiType)
    {
        var t = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEditor.Rendering.Universal.ShaderGUI." + guiType, false))
            .FirstOrDefault(x => x != null);
        if (t == null) { Debug.LogWarning("[Cheese Escape] ShaderGUI not found: " + guiType); return; }
        var gui = (ShaderGUI)Activator.CreateInstance(t, true);
        gui.ValidateMaterial(m);
    }

    // Small cubemap painted with the room's own colours: warm ceiling light above,
    // cream walls at the horizon, wooden/tiled floor tones below.
    static Cubemap ReflectionCubemap()
    {
        const int s = 32;
        string path = Root + "/Env_Reflection.cubemap";
        var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
        if (!cube) { cube = new Cubemap(s, TextureFormat.RGBA32, true); AssetDatabase.CreateAsset(cube, path); }
        Color up = new Color(1f, 0.96f, 0.88f), horizon = new Color(0.86f, 0.8f, 0.7f), down = new Color(0.55f, 0.42f, 0.32f);
        foreach (CubemapFace face in Enum.GetValues(typeof(CubemapFace)))
        {
            if (face == CubemapFace.Unknown) continue;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                    Vector3 d = face switch
                    {
                        CubemapFace.PositiveX => new Vector3(1f, -v, -u),
                        CubemapFace.NegativeX => new Vector3(-1f, -v, u),
                        CubemapFace.PositiveY => new Vector3(u, 1f, v),
                        CubemapFace.NegativeY => new Vector3(u, -1f, -v),
                        CubemapFace.PositiveZ => new Vector3(u, -v, 1f),
                        _ => new Vector3(-u, -v, -1f),
                    };
                    float h = d.normalized.y;
                    px[y * s + x] = h >= 0f ? Color.Lerp(horizon, up, Mathf.Pow(h, 0.7f)) : Color.Lerp(horizon, down, Mathf.Pow(-h, 0.6f));
                }
            cube.SetPixels(px, face);
        }
        cube.Apply(true);
        EditorUtility.SetDirty(cube);
        return cube;
    }

    // ===================================================================== saving
    static Texture2D Save(string name, Color[] px, bool clamp = false, int aniso = 2, bool alpha = false, bool isIcon = false)
    {
        int size = (int)Mathf.Sqrt(px.Length);
        int w = size, h = size;
        if (name == "Window_Sky") { w = 128; h = 256; }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        t.Apply();
        string path = TexDir + "/" + name + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        imp.anisoLevel = aniso;
        imp.mipmapEnabled = !isIcon;
        imp.alphaIsTransparency = alpha;
        imp.sRGBTexture = true;
        imp.maxTextureSize = 512;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D SaveNormal(string name, float[] height, int size, float strength)
    {
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float l = height[y * size + (x - 1 + size) % size], r = height[y * size + (x + 1) % size];
                float d = height[((y - 1 + size) % size) * size + x], u = height[((y + 1) % size) * size + x];
                var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.SetPixels(px); t.Apply();
        string path = TexDir + "/" + name + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.NormalMap;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.anisoLevel = 4;
        imp.maxTextureSize = 512;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ===================================================================== noise
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    // value noise that tiles every `period` lattice cells
    static float PNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        int Wrap(int v) => ((v % period) + period) % period;
        float a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
        float c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    // fractal noise over a [0,1) tile: u,v in 0..1
    static float Fbm(float u, float v, int baseFreq, int octaves, int seed)
    {
        float sum = 0, amp = 0.5f, norm = 0; int f = baseFreq;
        for (int i = 0; i < octaves; i++)
        {
            sum += PNoise(u * f, v * f, f, seed + i * 31) * amp;
            norm += amp; amp *= 0.5f; f *= 2;
        }
        return sum / norm;
    }

    static float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }
    static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

    // ===================================================================== painters
    // 4x4 checker of terracotta / cream tiles with grout and bevels (texture = 5 world units)
    static Color[] KitchenTiles(int s, out float[] height)
    {
        var px = new Color[s * s]; height = new float[s * s];
        int tile = s / 4; float grout = 3.5f;
        Color terra = new Color(0.79f, 0.47f, 0.35f), cream = new Color(0.95f, 0.91f, 0.83f), groutC = new Color(0.66f, 0.62f, 0.57f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int tx = x / tile, ty = y / tile;
                float lx = x % tile + 0.5f, ly = y % tile + 0.5f;
                float edge = Mathf.Min(Mathf.Min(lx, tile - lx), Mathf.Min(ly, tile - ly));
                float u = x / (float)s, v = y / (float)s;
                float n = Fbm(u, v, 8, 4, 7);
                Color baseC = ((tx + ty) % 2 == 0) ? terra : cream;
                baseC = Mul(baseC, 0.96f + Hash(tx, ty, 3) * 0.08f + (n - 0.5f) * 0.08f);
                float bevel = Smooth(grout, grout + 7f, edge);
                Color c = edge < grout ? Mul(groutC, 0.9f + n * 0.2f) : Mul(baseC, 0.9f + 0.1f * bevel);
                px[y * s + x] = c;
                height[y * s + x] = edge < grout ? 0f : 0.35f + 0.65f * bevel + (n - 0.5f) * 0.05f;
            }
        return px;
    }

    // oak planks running along V (the run direction); 8 planks across, staggered joints
    static Color[] WoodPlanks(int s, out float[] height)
    {
        var px = new Color[s * s]; height = new float[s * s];
        int cols = 8, cw = s / cols;
        Color oak = new Color(0.72f, 0.49f, 0.29f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int col = x / cw;
                float lx = x % cw + 0.5f;
                // two planks per column per tile, joint offset per column
                int offset = (int)(Hash(col, 0, 11) * s);
                int yy = (y + offset) % s;
                int plank = yy / (s / 2);
                float ly = yy % (s / 2) + 0.5f;
                float u = x / (float)s, v = y / (float)s;
                float tone = 0.82f + Hash(col, plank, 5) * 0.3f;
                float warp = Fbm(u, v, 4, 3, 13 + col);
                float grain = Mathf.Sin((lx / cw * 9f + warp * 6f + Hash(col, plank, 8) * 10f) * Mathf.PI);
                float fine = Fbm(u * 1f, v, 32, 2, 21);
                Color c = Mul(oak, tone * (0.93f + grain * 0.05f + (fine - 0.5f) * 0.12f));
                c = Color.Lerp(c, new Color(0.83f, 0.62f, 0.4f), 0.15f * Fbm(u, v, 2, 2, 40));
                float gapX = Mathf.Min(lx, cw - lx), gapY = Mathf.Min(ly, s / 2 - ly);
                float gap = Mathf.Min(gapX, gapY);
                if (gap < 1.4f) c = Mul(c, 0.45f);
                else c = Mul(c, 0.93f + 0.07f * Smooth(1.4f, 4f, gap));
                px[y * s + x] = c;
                height[y * s + x] = gap < 1.4f ? 0f : 0.6f + 0.4f * Smooth(1.4f, 4f, gap) + grain * 0.04f;
            }
        return px;
    }

    static Color[] Carpet(int s)
    {
        var px = new Color[s * s];
        Color baseC = new Color(0.74f, 0.65f, 0.66f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)s, v = y / (float)s;
                float n = Fbm(u, v, 64, 2, 3) * 0.6f + Fbm(u, v, 4, 3, 9) * 0.4f;
                px[y * s + x] = Mul(baseC, 0.88f + n * 0.22f);
            }
        return px;
    }

    static float PaperNoise(float u, float v) => (Fbm(u, v, 32, 2, 77) - 0.5f) * 0.05f;

    // sage green with a fine cream diamond lattice
    static Color[] WallpaperKitchen(int s)
    {
        var px = new Color[s * s];
        Color baseC = new Color(0.76f, 0.86f, 0.79f), line = new Color(0.93f, 0.95f, 0.88f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)s, v = y / (float)s;
                float a = Mathf.Abs(Mathf.Repeat((u + v) * 4f, 1f) - 0.5f);
                float b = Mathf.Abs(Mathf.Repeat((u - v) * 4f, 1f) - 0.5f);
                float d = Mathf.Min(a, b) * s / 4f / 1.41f;               // px distance-ish
                float k = 1f - Smooth(0.6f, 1.6f, d);
                Color c = Color.Lerp(baseC, line, k * 0.8f);
                // small dot in each diamond centre
                float cu = Mathf.Repeat(u * 4f, 1f) - 0.5f, cv = Mathf.Repeat(v * 4f, 1f) - 0.5f;
                float dot = 1f - Smooth(0.05f, 0.075f, Mathf.Sqrt(cu * cu + cv * cv));
                c = Color.Lerp(c, line, dot * 0.7f);
                px[y * s + x] = Mul(c, 1f + PaperNoise(u, v));
            }
        return px;
    }

    // warm cream two-tone stripes with a thin golden pinstripe
    static Color[] WallpaperLiving(int s)
    {
        var px = new Color[s * s];
        Color a = new Color(0.98f, 0.9f, 0.74f), b = new Color(0.94f, 0.83f, 0.64f), pin = new Color(0.82f, 0.66f, 0.4f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)s, v = y / (float)s;
                float stripe = Mathf.Repeat(u * 6f, 1f);
                Color c = stripe < 0.5f ? a : b;
                float distEdge = Mathf.Min(Mathf.Abs(stripe - 0.5f), Mathf.Min(stripe, 1f - stripe)) * s / 6f;
                if (stripe >= 0.5f) c = Color.Lerp(c, pin, 1f - Smooth(0.4f, 1.3f, Mathf.Abs(stripe - 0.75f) * s / 6f));
                c = Color.Lerp(c, Mul(c, 0.94f), 1f - Smooth(0f, 1.5f, distEdge));
                px[y * s + x] = Mul(c, 1f + PaperNoise(u, v));
            }
        return px;
    }

    // dusty blue with offset white dots and tiny stars
    static Color[] WallpaperBedroom(int s)
    {
        var px = new Color[s * s];
        Color baseC = new Color(0.64f, 0.71f, 0.86f), dotC = new Color(0.95f, 0.96f, 1f);
        int cells = 8;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)s, v = y / (float)s;
                float gy = v * cells; int row = Mathf.FloorToInt(gy);
                float gx = u * cells + (row % 2) * 0.5f;
                float cu = Mathf.Repeat(gx, 1f) - 0.5f, cv = Mathf.Repeat(gy, 1f) - 0.5f;
                float r = Mathf.Sqrt(cu * cu + cv * cv);
                float dot = 1f - Smooth(0.13f, 0.17f, r);
                Color c = Color.Lerp(baseC, dotC, dot * 0.85f);
                px[y * s + x] = Mul(c, 1f + PaperNoise(u, v));
            }
        return px;
    }

    // white subway tiles, 4 x 8 per tile, brick offset
    static Color[] SubwayTiles(int s, out float[] height)
    {
        var px = new Color[s * s]; height = new float[s * s];
        int tw = s / 4, th = s / 8; float grout = 2f;
        Color white = new Color(0.97f, 0.97f, 0.95f), groutC = new Color(0.78f, 0.78f, 0.76f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int row = y / th;
                int xx = (x + (row % 2) * tw / 2) % s;
                float lx = xx % tw + 0.5f, ly = y % th + 0.5f;
                float edge = Mathf.Min(Mathf.Min(lx, tw - lx), Mathf.Min(ly, th - ly));
                float bevel = Smooth(grout, grout + 5f, edge);
                float var = 0.97f + Hash(xx / tw, row, 2) * 0.05f;
                px[y * s + x] = edge < grout ? groutC : Mul(white, var * (0.88f + 0.12f * bevel));
                height[y * s + x] = edge < grout ? 0f : 0.4f + 0.6f * bevel;
            }
        return px;
    }

    // one raised panel per tile (tinted by material colour)
    static Color[] Panel(int s, out float[] height)
    {
        var px = new Color[s * s]; height = new float[s * s];
        float frame = s * 0.13f, bev = s * 0.035f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float lx = x + 0.5f, ly = y + 0.5f;
                float edge = Mathf.Min(Mathf.Min(lx, s - lx), Mathf.Min(ly, s - ly));
                float h; float shade;
                if (edge < 2f) { h = 0.2f; shade = 0.8f; }                       // seam between panels
                else if (edge < frame) { h = 0.7f; shade = 1f; }                  // flat frame
                else if (edge < frame + bev) { float t = (edge - frame) / bev; h = 0.4f + 0.3f * t; shade = 0.9f + 0.1f * t; }
                else { h = 0.75f; shade = 1f; }                                   // raised field
                float n = Fbm(x / (float)s, y / (float)s, 16, 2, 5);
                px[y * s + x] = Mul(Color.white, shade * (0.96f + n * 0.04f));
                height[y * s + x] = h;
            }
        return px;
    }

    static Color[] TrapWood(int s)
    {
        var px = new Color[s * s];
        Color pine = new Color(0.87f, 0.7f, 0.47f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)s, v = y / (float)s;
                float warp = Fbm(u, v, 3, 3, 4);
                float grain = Mathf.Sin((v * 14f + warp * 4f) * Mathf.PI);
                px[y * s + x] = Mul(pine, 0.92f + grain * 0.06f + (Fbm(u, v, 32, 2, 6) - 0.5f) * 0.1f);
            }
        return px;
    }

    static Color[] Sky(int w, int h)
    {
        var px = new Color[w * h];
        Color top = new Color(0.42f, 0.66f, 0.95f), bottom = new Color(0.86f, 0.94f, 1f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                Color c = Color.Lerp(bottom, top, Mathf.Pow(v, 0.8f));
                float cloud = Smooth(0.5f, 0.75f, Fbm(u, v * 0.5f + 0.3f, 4, 4, 99)) * Smooth(0.15f, 0.5f, v);
                c = Color.Lerp(c, Color.white, cloud * 0.85f);
                px[y * w + x] = c;
            }
        return px;
    }

    static Color[] SoftCircle(int s)
    {
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.8f);
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
        return px;
    }

    static Color[] Sparkle(int s)
    {
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / s * 2f - 1f), dy = Mathf.Abs((y + 0.5f) / s * 2f - 1f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float rays = Mathf.Max(Mathf.Exp(-dx * 14f) * (1f - dy), Mathf.Exp(-dy * 14f) * (1f - dx));
                float core = Mathf.Pow(Mathf.Clamp01(1f - r * 1.6f), 2f);
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(rays * 0.9f + core));
            }
        return px;
    }

    // rounded orange tile with a big cheese wedge
    static Color[] AppIcon(int s)
    {
        var px = new Color[s * s];
        Vector2 A = new Vector2(0.18f, 0.30f), B = new Vector2(0.84f, 0.30f), C = new Vector2(0.62f, 0.78f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                Color bg = Color.Lerp(new Color(0.95f, 0.38f, 0.12f), new Color(1f, 0.7f, 0.2f), v);
                float vig = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.6f));
                bg = Mul(bg, 1.05f - vig * 0.35f);
                Color c = bg;
                // wedge body (triangle top face + front block)
                Vector2 p = new Vector2(u, v);
                bool inTop = InTri(p, A + new Vector2(0, 0.12f), B + new Vector2(0, 0.12f), C + new Vector2(0, 0.0f));
                bool inFront = u > A.x && u < B.x && v > A.y - 0.06f && v < A.y + 0.12f;
                if (inFront) c = new Color(0.96f, 0.68f, 0.12f);
                if (inTop) c = new Color(1f, 0.85f, 0.3f);
                if (inFront || inTop)
                {
                    float h1 = Vector2.Distance(p, new Vector2(0.38f, 0.33f)), h2 = Vector2.Distance(p, new Vector2(0.64f, 0.37f));
                    float h3 = Vector2.Distance(p, new Vector2(0.55f, 0.56f)), h4 = Vector2.Distance(p, new Vector2(0.72f, 0.28f));
                    if (h1 < 0.045f || h2 < 0.035f || h3 < 0.05f || h4 < 0.025f) c = Mul(c, 0.72f);
                }
                // shadow under wedge
                if (!inFront && !inTop && v < A.y - 0.06f && v > A.y - 0.11f && u > A.x + 0.02f && u < B.x + 0.04f)
                    c = Mul(c, 0.82f);
                px[y * s + x] = c;
            }
        return px;
    }

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }
    static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
}
