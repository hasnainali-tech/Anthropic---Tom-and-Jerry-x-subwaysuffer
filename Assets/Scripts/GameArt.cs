using UnityEngine;

// Central art registry. Lives at Assets/Resources/GameArt.asset so every script can
// reach it with GameArt.Get() and - just as important - so every material/shader it
// references is guaranteed to be packed into the Android build.
// Regenerate the contents with: Tools > Cheese Escape > Generate Art
[CreateAssetMenu(menuName = "Cheese Escape/Game Art")]
public class GameArt : ScriptableObject
{
    [Header("Base (URP Lit / Unlit, colour is tinted per use)")]
    public Material litBase;
    public Material unlitBase;

    [Header("Rooms: 0 kitchen, 1 living room, 2 bedroom")]
    public Material[] floors = new Material[3];
    public Material[] walls = new Material[3];
    public Material[] wallsFade = new Material[3];
    public Material[] wainscots = new Material[3];
    public Material trim, ceiling, windowSky, holeDark;

    [Header("Props / gameplay")]
    public Material trapWood, metal, cheese;

    [Header("Effects")]
    public Material glow;        // additive soft circle
    public Material blobShadow;  // soft dark circle under floating things
    public Material sparkle;     // additive star particles
    public Material confetti;    // alpha-blended quads

    [Header("Fonts")]
    public Font titleFont;
    public Font uiFont;

    static GameArt cached;
    public static GameArt Get()
    {
        if (!cached) cached = Resources.Load<GameArt>("GameArt");
        return cached;
    }
}
