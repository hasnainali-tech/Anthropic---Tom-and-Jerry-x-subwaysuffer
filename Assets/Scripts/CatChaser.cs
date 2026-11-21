using System.Collections.Generic;
using UnityEngine;

// The chaser: an original chunky orange tabby cat (cream muzzle and chest, green eyes).
// Built at runtime under the chaser root and animated procedurally:
// menu idle (smug, arms akimbo, tail swish), chase run (cadence from actual speed,
// arms grabbing forward), and a pounce pose when the mouse is caught.
[DefaultExecutionOrder(60)]
public class CatChaser : ToonCharacter
{
    public float modelScale = 1f;   // ~2.25 units tall at 1

    static readonly Color Orange = new Color(0.96f, 0.56f, 0.22f);
    static readonly Color Stripe = new Color(0.78f, 0.36f, 0.12f);
    static readonly Color Cream = new Color(1f, 0.93f, 0.82f);
    static readonly Color Pink = new Color(1f, 0.6f, 0.65f);
    static readonly Color Dark = new Color(0.09f, 0.06f, 0.06f);
    static readonly Color Iris = new Color(0.45f, 0.82f, 0.3f);

    Transform pelvis, head, armL, armR, legL, legR, footL, footR, earL, earR;
    readonly List<Transform> tail = new List<Transform>();
    float phase, speed;
    Vector3 lastPos;

    public void BuildModel()
    {
        if (model) return;
        model = Pivot(transform, "CatModel", Vector3.zero);
        model.localScale = Vector3.one * modelScale;

        pelvis = Pivot(model, "Pelvis", new Vector3(0f, 0.62f, 0f));
        Part(pelvis, PrimitiveType.Sphere, Orange, new Vector3(0f, 0.42f, 0f), new Vector3(0.92f, 1f, 0.78f));
        Part(pelvis, PrimitiveType.Sphere, Cream, new Vector3(0f, 0.38f, 0.22f), new Vector3(0.56f, 0.72f, 0.38f));
        for (int i = 0; i < 3; i++)   // tabby bands hugging the body's curve (no fins sticking out)
        {
            float dy = 0.2f - i * 0.2f;                                  // offset from body centre
            float f = Mathf.Sqrt(Mathf.Max(0f, 1f - (dy / 0.5f) * (dy / 0.5f)));
            Part(pelvis, PrimitiveType.Sphere, Stripe, new Vector3(0f, 0.42f + dy, -0.04f),
                 new Vector3(0.92f * f + 0.012f, 0.055f, 0.7f * f), default, 0.3f, false);
        }

        head = Pivot(pelvis, "Head", new Vector3(0f, 0.86f, 0.02f));
        Part(head, PrimitiveType.Sphere, Orange, new Vector3(0f, 0.32f, 0.02f), new Vector3(0.82f, 0.7f, 0.72f));
        for (int i = -1; i <= 1; i++)  // forehead stripes
            Part(head, PrimitiveType.Sphere, Stripe, new Vector3(i * 0.1f, 0.58f, 0.24f), new Vector3(0.05f, 0.15f, 0.04f), new Vector3(-35f, 0f, i * 12f), 0.3f, false);
        for (int s = -1; s <= 1; s += 2)
            Part(head, PrimitiveType.Sphere, Cream, new Vector3(s * 0.09f, 0.2f, 0.29f), new Vector3(0.23f, 0.17f, 0.18f));
        Part(head, PrimitiveType.Sphere, Cream, new Vector3(0f, 0.12f, 0.26f), new Vector3(0.2f, 0.12f, 0.14f));
        Part(head, PrimitiveType.Sphere, Pink, new Vector3(0f, 0.27f, 0.375f), new Vector3(0.1f, 0.065f, 0.065f), default, 0.7f);
        Part(head, PrimitiveType.Capsule, Dark, new Vector3(0f, 0.1f, 0.33f), new Vector3(0.025f, 0.08f, 0.025f), new Vector3(0f, 0f, 90f), 0.2f, false);

        eyes = Pivot(head, "Eyes", new Vector3(0f, 0.43f, 0.27f));
        for (int s = -1; s <= 1; s += 2)
        {
            Part(eyes, PrimitiveType.Sphere, Color.white, new Vector3(s * 0.15f, 0f, 0f), new Vector3(0.2f, 0.23f, 0.1f), default, 0.6f, false);
            Part(eyes, PrimitiveType.Sphere, Iris, new Vector3(s * 0.14f, -0.01f, 0.04f), new Vector3(0.14f, 0.17f, 0.05f), default, 0.7f, false);
            Part(eyes, PrimitiveType.Sphere, Dark, new Vector3(s * 0.14f, -0.01f, 0.062f), new Vector3(0.035f, 0.13f, 0.03f), default, 0.8f, false);
            Part(eyes, PrimitiveType.Sphere, Color.white, new Vector3(s * 0.115f, 0.045f, 0.075f), new Vector3(0.04f, 0.04f, 0.02f), default, 0.9f, false);
            // mischievous brows
            Part(head, PrimitiveType.Capsule, Stripe, new Vector3(s * 0.15f, 0.57f, 0.29f), new Vector3(0.035f, 0.1f, 0.035f), new Vector3(0f, 0f, 90f + s * 18f), 0.2f, false);
            for (int w = 0; w < 3; w++)
                Part(head, PrimitiveType.Cylinder, Color.white, new Vector3(s * 0.3f, 0.22f - w * 0.04f, 0.3f), new Vector3(0.008f, 0.16f, 0.008f),
                     new Vector3(0f, s * 12f, 90f + s * (w - 1) * 8f), 0.4f, false);
        }
        earL = Pivot(head, "EarL", new Vector3(-0.24f, 0.56f, 0f), new Vector3(0f, 0f, 14f));
        earR = Pivot(head, "EarR", new Vector3(0.24f, 0.56f, 0f), new Vector3(0f, 0f, -14f));
        foreach (var ear in new[] { earL, earR })
        {
            Part(ear, PrimitiveType.Cube, Orange, new Vector3(0f, 0.06f, 0f), new Vector3(0.26f, 0.26f, 0.08f), new Vector3(0f, 0f, 45f));
            Part(ear, PrimitiveType.Cube, Pink, new Vector3(0f, 0.05f, 0.03f), new Vector3(0.16f, 0.16f, 0.04f), new Vector3(0f, 0f, 45f), 0.3f, false);
        }

        armL = Pivot(pelvis, "ArmL", new Vector3(-0.43f, 0.7f, 0.02f));
        armR = Pivot(pelvis, "ArmR", new Vector3(0.43f, 0.7f, 0.02f));
        foreach (var arm in new[] { armL, armR })
        {
            Part(arm, PrimitiveType.Capsule, Orange, new Vector3(0f, -0.26f, 0f), new Vector3(0.17f, 0.28f, 0.17f));
            Part(arm, PrimitiveType.Sphere, Stripe, new Vector3(0f, -0.2f, 0f), new Vector3(0.175f, 0.05f, 0.175f), default, 0.3f, false);
            Part(arm, PrimitiveType.Sphere, Cream, new Vector3(0f, -0.54f, 0f), new Vector3(0.21f, 0.21f, 0.21f));
        }

        legL = Pivot(pelvis, "LegL", new Vector3(-0.22f, 0.04f, 0f));
        legR = Pivot(pelvis, "LegR", new Vector3(0.22f, 0.04f, 0f));
        footL = BuildLeg(legL);
        footR = BuildLeg(legR);

        var parent = Pivot(pelvis, "Tail", new Vector3(0f, 0.15f, -0.34f), new Vector3(-40f, 0f, 0f));
        for (int i = 0; i < 9; i++)
        {
            var seg = Pivot(parent, "T" + i, i == 0 ? Vector3.zero : new Vector3(0f, 0f, -0.13f));
            Color c = i == 8 ? Cream : (i % 2 == 0 ? Orange : Stripe);
            float r = 0.09f - i * 0.003f;
            Part(seg, PrimitiveType.Capsule, c, new Vector3(0f, 0f, -0.07f), new Vector3(r, 0.08f, r), new Vector3(90f, 0f, 0f));
            tail.Add(seg);
            parent = seg;
        }
        lastPos = transform.position;
    }

    Transform BuildLeg(Transform leg)
    {
        Part(leg, PrimitiveType.Capsule, Orange, new Vector3(0f, -0.27f, 0f), new Vector3(0.22f, 0.27f, 0.22f));
        var foot = Pivot(leg, "Foot", new Vector3(0f, -0.56f, 0f));
        Part(foot, PrimitiveType.Sphere, Cream, new Vector3(0f, -0.03f, 0.1f), new Vector3(0.28f, 0.15f, 0.42f));
        return foot;
    }

    void LateUpdate()
    {
        if (!model) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        float t = Time.time;
        var gm = GameManager.Instance;
        bool menu = gm ? gm.Current == GameManager.State.Menu : pose == CatPose.Idle;
        bool pounce = gm ? gm.IsGameOver : pose == CatPose.Pounce;
        if (!gm && pose >= CatPose.Sleep) { SpecialPose(dt, t); return; }

        // speed from real movement so the stride matches the ground
        Vector3 p = transform.position;
        float inst = dt > 0f ? (p - lastPos).magnitude / dt : 0f;
        lastPos = p;
        speed = Mathf.Lerp(speed, Mathf.Min(inst, 40f), Damp(6f, dt));
        bool running = !menu && !pounce && speed > 1f;

        float k = Damp(running ? 20f : 8f, dt);
        eyeOpen = Mathf.Lerp(eyeOpen, pounce ? 1.2f : 1f, Damp(6f, dt));
        UpdateBlink(dt);

        Vector3 pelvisPos = new Vector3(0f, 0.62f, 0f), pelvisRot, headRot, aL, aR, lL, lR, fL = Vector3.zero, fR = Vector3.zero;
        Vector3 bodyScale = Vector3.one;
        if (pounce)
        {
            pelvisRot = new Vector3(24f, 0f, 0f);
            headRot = new Vector3(-20f, 0f, 0f);
            aL = new Vector3(-120f, 0f, -18f); aR = new Vector3(-120f, 0f, 18f);
            lL = new Vector3(-30f, 0f, -10f); lR = new Vector3(25f, 0f, 10f);
        }
        else if (!running)
        {
            // smug idle: hands on hips, slow breathing, head tilt
            float br = Mathf.Sin(t * 1.8f);
            bodyScale = new Vector3(1f + br * 0.012f, 1f - br * 0.01f, 1f + br * 0.012f);
            pelvisRot = new Vector3(0f, Mathf.Sin(t * 0.4f) * 5f, 0f);
            headRot = new Vector3(Mathf.Sin(t * 0.8f) * 3f, Mathf.Sin(t * 0.5f) * 14f, 8f + Mathf.Sin(t * 0.6f) * 6f);
            aL = new Vector3(15f, 0f, -38f); aR = new Vector3(15f, 0f, 38f);
            lL = new Vector3(0f, 0f, -4f); lR = new Vector3(0f, 0f, 4f);
        }
        else
        {
            float freq = Mathf.Clamp(speed * 0.22f, 1.8f, 4.4f);
            phase += dt * freq * Mathf.PI * 2f;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            pelvisPos = new Vector3(0f, 0.62f + Mathf.Abs(c) * 0.1f, 0f);
            pelvisRot = new Vector3(18f, s * 8f, s * 4f);
            headRot = new Vector3(-16f + Mathf.Sin(phase * 2f) * 3f, -s * 6f, 0f);
            aL = new Vector3(-s * 55f - 45f, 0f, -12f); aR = new Vector3(s * 55f - 45f, 0f, 12f);   // reaching, grabbing
            lL = new Vector3(s * 50f, 0f, -3f); lR = new Vector3(-s * 50f, 0f, 3f);
            fL = new Vector3(-s * 25f + Mathf.Max(0f, -s) * 18f, 0f, 0f);
            fR = new Vector3(s * 25f + Mathf.Max(0f, s) * 18f, 0f, 0f);
        }

        Pos(pelvis, pelvisPos, k);
        Rot(pelvis, pelvisRot, k);
        Rot(head, headRot, k);
        Rot(armL, aL, k); Rot(armR, aR, k);
        Rot(legL, lL, k); Rot(legR, lR, k);
        Rot(footL, fL, k); Rot(footR, fR, k);
        float earFlick = running ? -18f : (Mathf.Repeat(t, 4.1f) < 0.12f ? -25f : 0f);
        Rot(earL, new Vector3(earFlick, 0f, 14f), Damp(9f, dt));
        Rot(earR, new Vector3(earFlick, 0f, -14f), Damp(9f, dt));
        Scl(model, bodyScale * modelScale, Damp(20f, dt));
        Wave(tail, t, running ? -15f : -45f, running ? 14f : 22f, running ? 8f : 12f, running ? 9f : 2.4f, 0.55f, Damp(8f, dt));
    }

    // ---------------------------------------------------------------- free-roam poses (set by CatAI)
    public enum CatPose { Auto, Idle, Pounce, Sleep, Sit, Search, Groom, Stretch, Swipe }
    [HideInInspector] public CatPose pose = CatPose.Auto;

    void SpecialPose(float dt, float t)
    {
        float k = Damp(6f, dt);
        Vector3 pelvisPos, pelvisRot, headRot, aL, aR, lL, lR;
        Vector3 fL = Vector3.zero, fR = Vector3.zero;
        float br = Mathf.Sin(t * (pose == CatPose.Sleep ? 1.1f : 1.8f));
        Vector3 bodyScale = new Vector3(1f + br * 0.02f, 1f - br * 0.015f, 1f + br * 0.02f);
        float tailBase = -45f, tailAmp = 22f, tailRate = 2.4f;
        switch (pose)
        {
            case CatPose.Sleep:
                // curled up on its side, eyes shut, slow breathing
                eyeOpen = Mathf.Lerp(eyeOpen, 0.08f, Damp(4f, dt));
                pelvisPos = new Vector3(0f, 0.42f, 0f);
                pelvisRot = new Vector3(10f, 0f, 78f);
                headRot = new Vector3(20f, 0f, -40f);
                aL = new Vector3(-80f, 0f, -10f); aR = new Vector3(-90f, 0f, 10f);
                lL = new Vector3(-70f, 0f, 0f); lR = new Vector3(-80f, 0f, 0f);
                tailBase = -5f; tailAmp = 4f; tailRate = 0.8f;
                break;
            case CatPose.Sit:
                // sitting back, legs out front, arms resting - watching TV
                eyeOpen = Mathf.Lerp(eyeOpen, 0.85f, Damp(4f, dt));
                pelvisPos = new Vector3(0f, 0.3f, -0.1f);
                pelvisRot = new Vector3(-12f, 0f, 0f);
                headRot = new Vector3(4f, Mathf.Sin(t * 0.3f) * 6f, Mathf.Sin(t * 0.5f) * 5f);
                aL = new Vector3(-20f, 0f, -18f); aR = new Vector3(-20f, 0f, 18f);
                lL = new Vector3(-85f, 0f, -8f); lR = new Vector3(-85f, 0f, 8f);
                fL = fR = new Vector3(60f, 0f, 0f);
                break;
            case CatPose.Groom:
                // sitting, licking a raised paw, eyes half shut
                eyeOpen = Mathf.Lerp(eyeOpen, 0.45f, Damp(4f, dt));
                float lick = Mathf.Sin(t * 7f);
                pelvisPos = new Vector3(0f, 0.3f, -0.1f);
                pelvisRot = new Vector3(-8f, 0f, 0f);
                headRot = new Vector3(22f + lick * 6f, -18f, 12f);
                aL = new Vector3(-15f, 0f, -16f); aR = new Vector3(-150f + lick * 10f, -20f, -35f);
                lL = new Vector3(-85f, 0f, -8f); lR = new Vector3(-85f, 0f, 8f);
                fL = fR = new Vector3(60f, 0f, 0f);
                tailBase = -20f; tailAmp = 10f; tailRate = 1.5f;
                break;
            case CatPose.Stretch:
                // big after-nap stretch: front low, back end up, tail high
                eyeOpen = Mathf.Lerp(eyeOpen, 0.6f, Damp(4f, dt));
                pelvisPos = new Vector3(0f, 0.55f, 0f);
                pelvisRot = new Vector3(48f, 0f, 0f);
                headRot = new Vector3(-55f, 0f, 0f);
                aL = new Vector3(-115f, 0f, -10f); aR = new Vector3(-115f, 0f, 10f);
                lL = new Vector3(-30f, 0f, -6f); lR = new Vector3(-30f, 0f, 6f);
                tailBase = -80f; tailAmp = 6f; tailRate = 1f;
                break;
            case CatPose.Swipe:
                // crouched at a mouse hole, one paw jabbing inside
                eyeOpen = Mathf.Lerp(eyeOpen, 1.2f, Damp(8f, dt));
                float jab = Mathf.Abs(Mathf.Sin(t * 9f));
                pelvisPos = new Vector3(0f, 0.32f, 0f);
                pelvisRot = new Vector3(50f, 0f, 0f);
                headRot = new Vector3(-40f, Mathf.Sin(t * 2f) * 8f, 0f);
                aL = new Vector3(-60f, 0f, -18f); aR = new Vector3(-95f - jab * 60f, 0f, 8f);
                lL = new Vector3(-70f, 0f, -12f); lR = new Vector3(-70f, 0f, 12f);
                tailAmp = 35f; tailRate = 7f;
                break;
            default:   // Search: crouched forward, head sweeping left and right
                eyeOpen = Mathf.Lerp(eyeOpen, 1.15f, Damp(4f, dt));
                pelvisPos = new Vector3(0f, 0.5f, 0f);
                pelvisRot = new Vector3(28f, Mathf.Sin(t * 0.9f) * 12f, 0f);
                headRot = new Vector3(-22f, Mathf.Sin(t * 1.4f) * 45f, 0f);
                aL = new Vector3(-60f, 0f, -20f); aR = new Vector3(-60f, 0f, 20f);
                lL = new Vector3(-25f, 0f, -10f); lR = new Vector3(15f, 0f, 10f);
                tailAmp = 30f; tailRate = 5f;
                break;
        }
        if (pose != CatPose.Sleep) UpdateBlink(dt); else if (eyes) eyes.localScale = new Vector3(1f, eyeOpen, 1f);
        Pos(pelvis, pelvisPos, k);
        Rot(pelvis, pelvisRot, k);
        Rot(head, headRot, k);
        Rot(armL, aL, k); Rot(armR, aR, k);
        Rot(legL, lL, k); Rot(legR, lR, k);
        Rot(footL, fL, k); Rot(footR, fR, k);
        Scl(model, bodyScale * modelScale, Damp(10f, dt));
        Wave(tail, t, tailBase, tailAmp, tailAmp * 0.5f, tailRate, 0.55f, Damp(6f, dt));
        lastPos = transform.position;
    }
}
