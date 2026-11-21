using UnityEngine;

// Lightweight pooled particle effects. One ParticleSystem per effect, emitted at a
// position on demand (world simulation), so there is no per-pickup allocation.
// All materials come from GameArt (URP Particles/Unlit) so they render on Android.
public class GameFX : MonoBehaviour
{
    static GameFX instance;
    ParticleSystem sparkle, crumbs, dust, confetti, ring;

    static GameFX Get()
    {
        if (instance) return instance;
        var go = new GameObject("GameFX");
        instance = go.AddComponent<GameFX>();
        instance.Build();
        return instance;
    }

    void Build()
    {
        var art = GameArt.Get();
        sparkle = Make("Sparkle", art ? art.sparkle : null, 40, 0.45f, 0.8f, new Vector2(0.25f, 0.5f), new Vector2(2.5f, 5f), 0f,
                       new Color(1f, 0.95f, 0.6f), new Color(1f, 0.75f, 0.2f));
        crumbs = Make("Crumbs", art ? art.confetti : null, 40, 0.5f, 0.7f, new Vector2(0.06f, 0.11f), new Vector2(2f, 4f), 9f,
                      new Color(1f, 0.82f, 0.25f), new Color(0.98f, 0.68f, 0.12f));
        dust = Make("Dust", art ? art.glow : null, 30, 0.5f, 0.8f, new Vector2(0.6f, 1.1f), new Vector2(1f, 2.5f), -0.5f,
                    new Color(0.95f, 0.9f, 0.82f, 0.7f), new Color(0.8f, 0.75f, 0.68f, 0.6f));
        ring = Make("Ring", art ? art.glow : null, 6, 0.3f, 0.3f, new Vector2(1.6f, 1.6f), Vector2.zero, 0f,
                    new Color(1f, 0.9f, 0.4f, 0.9f), new Color(1f, 0.8f, 0.3f, 0.9f));
        var rs = ring.sizeOverLifetime; rs.enabled = true; rs.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.3f, 1, 1.4f));
        confetti = Make("Confetti", art ? art.confetti : null, 120, 2.2f, 2.8f, new Vector2(0.12f, 0.22f), new Vector2(4f, 8f), 4f,
                        Color.white, Color.white);
        var cm = confetti.main;
        cm.startColor = new ParticleSystem.MinMaxGradient(Rainbow()) { mode = ParticleSystemGradientMode.RandomColor };
        cm.startRotation3D = false;
        var rot = confetti.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var sh = confetti.shape; sh.angle = 35f;
    }

    static Gradient Rainbow()
    {
        var g = new Gradient();
        g.SetKeys(new[]
        {
            new GradientColorKey(new Color(1f, 0.3f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0.25f),
            new GradientColorKey(new Color(0.3f, 0.85f, 0.4f), 0.5f), new GradientColorKey(new Color(0.3f, 0.6f, 1f), 0.75f),
            new GradientColorKey(new Color(0.85f, 0.4f, 1f), 1f),
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    ParticleSystem Make(string name, Material mat, int max, float lifeMin, float lifeMax, Vector2 size, Vector2 speed,
                        float gravity, Color a, Color b)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.gravityModifier = gravity / 9.81f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em = ps.emission; em.enabled = false;
        var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.15f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingFudge = -10f;
        ps.Play();
        return ps;
    }

    static void Emit(ParticleSystem ps, Vector3 pos, int count)
    {
        if (!ps) return;
        var p = new ParticleSystem.EmitParams { position = pos, applyShapeToPosition = true };
        ps.Emit(p, count);
    }

    // ---- public API ----
    public static void CheesePop(Vector3 pos)
    {
        var fx = Get();
        Emit(fx.sparkle, pos, 10);
        Emit(fx.crumbs, pos, 8);
        Emit(fx.ring, pos, 1);
        GameUI.OnCheeseCollected(pos);
    }

    public static void HitPuff(Vector3 pos)
    {
        var fx = Get();
        Emit(fx.dust, pos + Vector3.up * 0.3f, 14);
        Emit(fx.sparkle, pos + Vector3.up * 0.6f, 6);
    }

    public static void Confetti(Vector3 pos)
    {
        var fx = Get();
        fx.confetti.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        Emit(fx.confetti, pos, 90);
    }
}
