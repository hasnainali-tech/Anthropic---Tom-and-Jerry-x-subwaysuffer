using System.Collections.Generic;
using UnityEngine;

// The player character: an original cartoon mouse (caramel fur, red scarf, blue sneakers).
// Built at runtime under the player root and animated procedurally from PlayerController:
// idle (menu), run (cycle speed follows run speed), jump (leap + tuck, squash on landing),
// slide (belly slide), hit flinch, and a scared pose when caught.
[DefaultExecutionOrder(50)]
public class MouseHero : ToonCharacter
{
    public float modelScale = 0.92f;

    static readonly Color Fur = new Color(0.78f, 0.5f, 0.3f);
    static readonly Color Cream = new Color(1f, 0.9f, 0.76f);
    static readonly Color Pink = new Color(1f, 0.62f, 0.66f);
    static readonly Color Nose = new Color(0.95f, 0.4f, 0.5f);
    static readonly Color Dark = new Color(0.1f, 0.07f, 0.08f);
    static readonly Color Scarf = new Color(0.9f, 0.16f, 0.18f);
    static readonly Color Shoe = new Color(0.16f, 0.45f, 0.9f);

    IMouseMotion src;
    Transform pelvis, head, earL, earR, armL, armR, legL, legR, footL, footR, scarfTail;
    readonly List<Transform> tail = new List<Transform>();

    float phase, landT = 1f, flinch, waveT;
    Vector3 lastPos;
    bool wasGrounded = true, wasHidden;

    void Start()
    {
        src = GetComponent<IMouseMotion>();
        var capsule = transform.Find("Visual");
        if (capsule) { var r = capsule.GetComponent<Renderer>(); if (r) r.enabled = false; }
        Build();
        lastPos = transform.position;
    }

    void Build()
    {
        model = Pivot(transform, "MouseModel", Vector3.zero);
        model.localScale = Vector3.one * modelScale;

        pelvis = Pivot(model, "Pelvis", new Vector3(0f, 0.3f, 0f));
        Part(pelvis, PrimitiveType.Sphere, Fur, new Vector3(0f, 0.2f, 0f), new Vector3(0.44f, 0.5f, 0.4f));
        Part(pelvis, PrimitiveType.Sphere, Cream, new Vector3(0f, 0.17f, 0.11f), new Vector3(0.3f, 0.36f, 0.22f));
        // scarf: ring + fluttering tail
        Part(pelvis, PrimitiveType.Cylinder, Scarf, new Vector3(0f, 0.415f, 0.01f), new Vector3(0.36f, 0.04f, 0.34f), default, 0.2f);
        scarfTail = Pivot(pelvis, "ScarfTail", new Vector3(0.07f, 0.41f, -0.13f));
        Part(scarfTail, PrimitiveType.Cube, Scarf, new Vector3(0f, 0f, -0.12f), new Vector3(0.09f, 0.025f, 0.24f), default, 0.2f);

        // head
        head = Pivot(pelvis, "Head", new Vector3(0f, 0.42f, 0.02f));
        Part(head, PrimitiveType.Sphere, Fur, new Vector3(0f, 0.2f, 0.02f), new Vector3(0.46f, 0.42f, 0.42f));
        Part(head, PrimitiveType.Sphere, Cream, new Vector3(0f, 0.13f, 0.18f), new Vector3(0.27f, 0.19f, 0.22f));
        Part(head, PrimitiveType.Sphere, Nose, new Vector3(0f, 0.17f, 0.3f), new Vector3(0.075f, 0.065f, 0.065f), default, 0.7f);
        Part(head, PrimitiveType.Capsule, Dark, new Vector3(0f, 0.085f, 0.275f), new Vector3(0.018f, 0.035f, 0.018f), new Vector3(0f, 0f, 90f), 0.2f, false);
        eyes = Pivot(head, "Eyes", new Vector3(0f, 0.255f, 0.185f));
        for (int s = -1; s <= 1; s += 2)
        {
            Part(eyes, PrimitiveType.Sphere, Color.white, new Vector3(s * 0.088f, 0f, 0f), new Vector3(0.12f, 0.155f, 0.08f), default, 0.6f, false);
            Part(eyes, PrimitiveType.Sphere, Dark, new Vector3(s * 0.082f, -0.008f, 0.034f), new Vector3(0.068f, 0.095f, 0.04f), default, 0.8f, false);
            Part(eyes, PrimitiveType.Sphere, Color.white, new Vector3(s * 0.068f, 0.025f, 0.055f), new Vector3(0.026f, 0.026f, 0.015f), default, 0.9f, false);
            Part(head, PrimitiveType.Capsule, new Color(0.45f, 0.26f, 0.15f), new Vector3(s * 0.092f, 0.4f, 0.165f), new Vector3(0.016f, 0.038f, 0.016f), new Vector3(0f, 0f, 90f - s * 10f), 0.2f, false);
            // whiskers
            for (int w = 0; w < 2; w++)
                Part(head, PrimitiveType.Cylinder, Dark, new Vector3(s * 0.17f, 0.15f - w * 0.035f, 0.26f), new Vector3(0.007f, 0.1f, 0.007f),
                     new Vector3(0f, s * 15f, 90f + s * (w == 0 ? -8f : 6f)), 0.1f, false);
        }
        earL = Pivot(head, "EarL", new Vector3(-0.15f, 0.33f, -0.02f), new Vector3(0f, 0f, 18f));
        earR = Pivot(head, "EarR", new Vector3(0.15f, 0.33f, -0.02f), new Vector3(0f, 0f, -18f));
        foreach (var (ear, s) in new[] { (earL, -1f), (earR, 1f) })
        {
            Part(ear, PrimitiveType.Sphere, Fur, new Vector3(s * 0.06f, 0.1f, 0f), new Vector3(0.3f, 0.3f, 0.05f));
            Part(ear, PrimitiveType.Sphere, Pink, new Vector3(s * 0.06f, 0.1f, 0.018f), new Vector3(0.21f, 0.21f, 0.03f), default, 0.3f, false);
        }

        // arms
        armL = Pivot(pelvis, "ArmL", new Vector3(-0.2f, 0.32f, 0.02f));
        armR = Pivot(pelvis, "ArmR", new Vector3(0.2f, 0.32f, 0.02f));
        foreach (var arm in new[] { armL, armR })
        {
            Part(arm, PrimitiveType.Capsule, Fur, new Vector3(0f, -0.1f, 0f), new Vector3(0.08f, 0.11f, 0.08f));
            Part(arm, PrimitiveType.Sphere, Cream, new Vector3(0f, -0.21f, 0f), new Vector3(0.09f, 0.09f, 0.09f));
        }

        // legs + sneakers
        legL = Pivot(pelvis, "LegL", new Vector3(-0.1f, 0.02f, 0f));
        legR = Pivot(pelvis, "LegR", new Vector3(0.1f, 0.02f, 0f));
        footL = BuildLeg(legL);
        footR = BuildLeg(legR);

        // tail: 8-link chain curling up behind
        var parent = Pivot(pelvis, "Tail", new Vector3(0f, 0.06f, -0.17f), new Vector3(-30f, 0f, 0f));
        for (int i = 0; i < 8; i++)
        {
            var seg = Pivot(parent, "T" + i, i == 0 ? Vector3.zero : new Vector3(0f, 0f, -0.075f));
            Part(seg, PrimitiveType.Capsule, new Color(0.92f, 0.62f, 0.56f), new Vector3(0f, 0f, -0.04f),
                 new Vector3(0.035f - i * 0.002f, 0.045f, 0.035f - i * 0.002f), new Vector3(90f, 0f, 0f));
            tail.Add(seg);
            parent = seg;
        }
    }

    Transform BuildLeg(Transform leg)
    {
        Part(leg, PrimitiveType.Capsule, Fur, new Vector3(0f, -0.1f, 0f), new Vector3(0.09f, 0.12f, 0.09f));
        var foot = Pivot(leg, "Foot", new Vector3(0f, -0.235f, 0f));
        Part(foot, PrimitiveType.Sphere, Shoe, new Vector3(0f, -0.015f, 0.05f), new Vector3(0.13f, 0.1f, 0.22f), default, 0.5f);
        Part(foot, PrimitiveType.Sphere, Color.white, new Vector3(0f, -0.05f, 0.05f), new Vector3(0.14f, 0.04f, 0.23f), default, 0.3f);
        Part(foot, PrimitiveType.Sphere, Color.white, new Vector3(0f, -0.01f, 0.13f), new Vector3(0.1f, 0.07f, 0.07f), default, 0.3f);
        return foot;
    }

    public void Hit() => flinch = 1f;

    void LateUpdate()
    {
        if (!model) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (src == null) src = GetComponent<IMouseMotion>();
        bool hidden = src != null && src.IsHidden;
        if (hidden != wasHidden) { SetVisible(!hidden); wasHidden = hidden; }
        bool menu = src != null && src.IsWaving;
        bool caught = src != null && src.IsScared;
        bool running = src != null && src.IsMoving && !caught;
        bool grounded = src == null || src.IsGrounded;
        bool sliding = src != null && src.IsSliding;
        bool crouching = src != null && src.IsCrouching;
        float speed = src != null ? src.Speed : 0f;
        float vy = src != null ? src.VerticalVelocity : 0f;
        // sideways speed relative to facing -> banking into turns / lane changes
        Vector3 vel = (transform.position - lastPos) / Mathf.Max(dt, 0.0001f);
        lastPos = transform.position;
        float xVel = transform.InverseTransformDirection(vel).x;
        float t = Time.time;

        // landing squash
        if (grounded && !wasGrounded) landT = 0f;
        wasGrounded = grounded;
        landT += dt;
        flinch = Mathf.MoveTowards(flinch, 0f, dt * 2.5f);

        float k = Damp(running ? 22f : 9f, dt);
        eyeOpen = Mathf.Lerp(eyeOpen, caught ? 1.25f : 1f, Damp(8f, dt));
        UpdateBlink(dt);

        Vector3 pelvisPos = new Vector3(0f, 0.3f, 0f);
        Vector3 pelvisRot, headRot, aL, aR, lL, lR, fL, fR, earRotL, earRotR;
        Vector3 bodyScale = Vector3.one;

        if (caught)
        {
            // scared: hands up by the face, knees together, trembling
            float tr = Mathf.Sin(t * 45f) * 2.5f;
            pelvisRot = new Vector3(-8f, 180f * 0f, tr);
            headRot = new Vector3(-12f, 0f, -tr);
            aL = new Vector3(-150f, 0f, -25f + tr); aR = new Vector3(-150f, 0f, 25f - tr);
            lL = new Vector3(0f, 0f, 6f); lR = new Vector3(0f, 0f, -6f);
            fL = fR = Vector3.zero;
            earRotL = new Vector3(-25f, 0f, 35f); earRotR = new Vector3(-25f, 0f, -35f);
        }
        else if (!running && !crouching)
        {
            // idle: breathing, looking around, a friendly wave now and then
            float breathe = Mathf.Sin(t * 2.2f);
            bodyScale = new Vector3(1f + breathe * 0.015f, 1f - breathe * 0.012f, 1f + breathe * 0.015f);
            pelvisRot = new Vector3(0f, Mathf.Sin(t * 0.5f) * 6f, 0f);
            headRot = new Vector3(Mathf.Sin(t * 0.9f) * 4f, Mathf.Sin(t * 0.6f) * 22f, Mathf.Sin(t * 0.45f) * 8f);
            waveT = Mathf.Repeat(t, 5f);
            bool waving = menu && waveT < 1.6f;
            aL = new Vector3(Mathf.Sin(t * 2.2f) * 4f, 0f, -10f);
            aR = waving ? new Vector3(0f, 0f, 150f + Mathf.Sin(t * 14f) * 22f) : new Vector3(-Mathf.Sin(t * 2.2f) * 4f, 0f, 10f);
            lL = new Vector3(0f, 0f, -3f); lR = new Vector3(0f, 0f, 3f);
            fL = fR = Vector3.zero;
            float twitch = Mathf.Repeat(t, 3.3f) < 0.15f ? 18f : 0f;
            earRotL = new Vector3(0f, 0f, 18f + twitch); earRotR = new Vector3(0f, 0f, -18f);
        }
        else if (sliding)
        {
            // belly slide: body flat and forward, arms reaching ahead, legs trailing
            pelvisPos = new Vector3(0f, 0.12f, 0.1f);
            pelvisRot = new Vector3(78f, 0f, Mathf.Sin(t * 20f) * 3f);
            headRot = new Vector3(-62f, 0f, 0f);
            aL = new Vector3(-165f, 0f, -12f); aR = new Vector3(-165f, 0f, 12f);
            lL = new Vector3(20f, 0f, -8f); lR = new Vector3(28f, 0f, 8f);
            fL = fR = new Vector3(40f, 0f, 0f);
            earRotL = new Vector3(-55f, 0f, 30f); earRotR = new Vector3(-55f, 0f, -30f);
        }
        else if (!grounded)
        {
            // leap: split legs, arms up while rising, flail a little while falling
            bool rising = vy > 0f;
            float flail = rising ? 0f : Mathf.Sin(t * 22f) * 18f;
            pelvisRot = new Vector3(rising ? -8f : 6f, 0f, 0f);
            headRot = new Vector3(rising ? -10f : 10f, 0f, 0f);
            aL = new Vector3(-150f + flail, 0f, -30f); aR = new Vector3(-150f - flail, 0f, 30f);
            lL = new Vector3(-55f, 0f, -6f); lR = new Vector3(35f, 0f, 6f);
            fL = new Vector3(30f, 0f, 0f); fR = new Vector3(-10f, 0f, 0f);
            earRotL = new Vector3(rising ? 25f : -30f, 0f, 22f); earRotR = new Vector3(rising ? 25f : -30f, 0f, -22f);
            float stretch = Mathf.Clamp(vy / 9f, -0.5f, 1f);
            bodyScale = new Vector3(1f - stretch * 0.06f, 1f + stretch * 0.1f, 1f - stretch * 0.06f);
        }
        else if (crouching)
        {
            // sneak: low, leaning forward, small careful steps (still when not moving)
            bool step = running && speed > 0.2f;
            if (step) phase += dt * Mathf.Clamp(speed * 0.9f, 1.6f, 3.2f) * Mathf.PI * 2f;
            float s = step ? Mathf.Sin(phase) : 0f;
            pelvisPos = new Vector3(0f, 0.17f, -0.04f);
            pelvisRot = new Vector3(38f, s * 4f, 0f);
            headRot = new Vector3(-34f, Mathf.Sin(t * 1.3f) * 14f, 0f);          // peeking around
            aL = new Vector3(-55f - s * 25f, 0f, -16f); aR = new Vector3(-55f + s * 25f, 0f, 16f);
            lL = new Vector3(-35f + s * 28f, 0f, -6f); lR = new Vector3(-35f - s * 28f, 0f, 6f);
            fL = fR = new Vector3(30f, 0f, 0f);
            earRotL = new Vector3(-40f, 0f, 30f); earRotR = new Vector3(-40f, 0f, -30f);   // ears flat
        }
        else
        {
            // walk -> run cycle: cadence AND stride follow speed
            float run01 = Mathf.Clamp01((speed - 2f) / 5f);
            float freq = Mathf.Clamp(speed * 0.33f, 1.8f, 5.4f);
            phase += dt * freq * Mathf.PI * 2f;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float stride = Mathf.Lerp(30f, 58f, run01), swing = Mathf.Lerp(30f, 62f, run01);
            float lean = Mathf.Clamp(xVel * -1.2f, -14f, 14f);       // bank into turns / lane changes
            pelvisPos = new Vector3(0f, 0.3f + Mathf.Abs(c) * Mathf.Lerp(0.025f, 0.055f, run01), 0f);
            pelvisRot = new Vector3(Mathf.Lerp(5f, 16f, run01), s * 7f, s * 4f + lean);
            headRot = new Vector3(Mathf.Lerp(-4f, -12f, run01) + Mathf.Sin(phase * 2f) * 3f, -s * 6f, -s * 3f);
            aL = new Vector3(-s * swing - 10f, 0f, -14f); aR = new Vector3(s * swing - 10f, 0f, 14f);
            lL = new Vector3(s * stride, 0f, -2f); lR = new Vector3(-s * stride, 0f, 2f);
            fL = new Vector3(-s * stride * 0.5f + Mathf.Max(0f, -s) * 20f, 0f, 0f);
            fR = new Vector3(s * stride * 0.5f + Mathf.Max(0f, s) * 20f, 0f, 0f);
            float flop = Mathf.Abs(c) * 14f * run01;
            earRotL = new Vector3(-22f * run01 - flop, 0f, 22f); earRotR = new Vector3(-22f * run01 - flop, 0f, -22f);
        }

        // landing squash + hit flinch on top of everything
        if (landT < 0.18f)
        {
            float q = Mathf.Sin(landT / 0.18f * Mathf.PI);
            bodyScale = new Vector3(1f + q * 0.14f, 1f - q * 0.18f, 1f + q * 0.14f);
        }
        if (flinch > 0f) { pelvisRot += new Vector3(-30f * flinch, 0f, 0f); headRot += new Vector3(-20f * flinch, 0f, 0f); }

        Pos(pelvis, pelvisPos, k);
        Rot(pelvis, pelvisRot, k);
        Rot(head, headRot, k);
        Rot(armL, aL, k); Rot(armR, aR, k);
        Rot(legL, lL, k); Rot(legR, lR, k);
        Rot(footL, fL, k); Rot(footR, fR, k);
        Rot(earL, earRotL, Damp(7f, dt)); Rot(earR, earRotR, Damp(7f, dt));   // floppy lag
        Scl(model, bodyScale * modelScale, Damp(25f, dt));

        // tail and scarf trail behind, faster when running
        float rate = running ? 10f : 3f;
        Wave(tail, t, running ? -10f : -35f, running ? 18f : 10f, running ? 10f : 6f, rate, 0.7f, Damp(10f, dt));
        if (scarfTail)
            Rot(scarfTail, new Vector3(running || !grounded ? -70f + Mathf.Sin(t * 24f) * 14f : -8f + Mathf.Sin(t * 2f) * 4f,
                                       Mathf.Sin(t * 17f) * (running ? 12f : 3f), 0f), Damp(12f, dt));
    }
}
