using UnityEngine;
using UnityEngine.InputSystem;

// Free-roam third-person mouse controller (no auto-run, no track).
// Moves relative to the camera with a CharacterController: walk / run (analog), sprint
// (stamina), crouch-sneak (fits under beds and couches, silent), jump (climb onto low
// furniture steps). Footsteps make noise the cat can hear. Touch input comes from HouseUI
// (virtual joystick + buttons); keyboard/mouse also work for testing in the editor.
[RequireComponent(typeof(CharacterController))]
public class MouseController : MonoBehaviour, IMouseMotion
{
    [Header("Movement")]
    public float walkSpeed = 3.2f;
    public float runSpeed = 5.6f;
    public float sprintSpeed = 8.2f;
    public float crouchSpeed = 1.8f;
    public float turnSpeed = 720f;
    public float jumpVelocity = 9f;
    public float gravity = 28f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float sprintDrain = 22f;
    public float staminaRegen = 16f;

    // ---- input written by HouseUI / keyboard ----
    [HideInInspector] public Vector2 moveInput;     // joystick (-1..1)
    [HideInInspector] public bool sprintHeld;
    public bool CrouchToggled { get; set; }
    public bool InputEnabled { get; set; } = true;

    // cutscenes walk the mouse to a point (input ignored while set)
    public Vector3? ScriptedTarget;
    public float scriptedSpeed = 3f;
    public bool AtScriptedTarget => ScriptedTarget.HasValue && Vector3.Distance(Flat(transform.position), Flat(ScriptedTarget.Value)) < 0.25f;
    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    public float Stamina { get; private set; }
    public bool IsSprinting { get; private set; }
    public HideSpot Hiding { get; private set; }
    public Transform CameraPivot { get; private set; }

    CharacterController cc;
    Vector3 velocity;
    float yVel, stepTimer, standHeight = 1f, crouchHeight = 0.5f;
    bool exhausted, grounded = true, wasGrounded = true;
    Transform cam;

    // ---- IMouseMotion (drives the MouseHero model) ----
    public float Speed => new Vector3(velocity.x, 0f, velocity.z).magnitude;
    public bool IsMoving => Speed > 0.15f;
    public bool IsGrounded => grounded;
    public float VerticalVelocity => yVel;
    public bool IsSliding => false;
    public bool IsCrouching => CrouchToggled && !Hiding;
    public bool IsScared { get; set; }
    public bool IsWaving { get; set; }
    public bool IsHidden => Hiding != null;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        cc.height = standHeight; cc.radius = 0.28f; cc.center = new Vector3(0f, standHeight * 0.5f, 0f);
        cc.stepOffset = 0.35f; cc.slopeLimit = 50f; cc.skinWidth = 0.03f;
        Stamina = maxStamina;
        CameraPivot = new GameObject("CameraPivot").transform;
        CameraPivot.SetParent(transform, false);
        CameraPivot.localPosition = new Vector3(0f, 0.75f, 0f);
    }

    public void Teleport(Vector3 pos, float yaw)
    {
        cc.enabled = false;
        transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        cc.enabled = true;
        velocity = Vector3.zero; yVel = 0f;
    }

    public void Jump()
    {
        if (!InputEnabled || Hiding || !grounded) return;
        if (IsCrouching) { TryStandUp(); return; }
        yVel = jumpVelocity;
        grounded = false;
        AudioManager.PlayJump();
    }

    public void ToggleCrouch()
    {
        if (Hiding) return;
        if (CrouchToggled) TryStandUp();
        else CrouchToggled = true;
    }

    void TryStandUp()
    {
        // only stand if there is head room (e.g. not still under the bed)
        Vector3 bottom = transform.position + Vector3.up * (cc.radius + 0.05f);
        if (!Physics.SphereCast(bottom, cc.radius * 0.9f, Vector3.up, out _, standHeight - cc.radius, ~0, QueryTriggerInteraction.Ignore))
            CrouchToggled = false;
    }

    public void EnterHide(HideSpot spot)
    {
        Hiding = spot;
        cc.enabled = false;
        transform.position = spot.HidePoint.position;
        velocity = Vector3.zero;
    }

    public void ExitHide()
    {
        if (!Hiding) return;
        var exit = Hiding.ExitPoint;
        Hiding = null;
        transform.SetPositionAndRotation(exit.position, Quaternion.Euler(0f, exit.eulerAngles.y, 0f));
        cc.enabled = true;
    }

    void Update()
    {
        if (!cam && Camera.main) cam = Camera.main.transform;
        ReadKeyboard();
        if (Hiding) { yVel = 0f; return; }

        float dt = Time.deltaTime;
        Vector2 input = InputEnabled ? moveInput : Vector2.zero;
        float mag = Mathf.Clamp01(input.magnitude);

        // camera-relative direction
        Vector3 fwd = cam ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 dir = (fwd * input.y + right * input.x);
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        if (ScriptedTarget.HasValue)
        {
            Vector3 to = Flat(ScriptedTarget.Value) - Flat(transform.position);
            dir = to.magnitude > 0.2f ? to.normalized : Vector3.zero;
            mag = dir == Vector3.zero ? 0f : 1f;
        }

        // carrying food slows the mouse a little (each item ~10%)
        int carried = HouseGame.I ? HouseGame.I.Carried.Count : 0;
        float load = 1f - 0.1f * carried;

        // speed: crouch < walk < run (joystick push) < sprint (button + stamina)
        bool wantSprint = sprintHeld && mag > 0.3f && !IsCrouching && !exhausted && !ScriptedTarget.HasValue;
        IsSprinting = wantSprint && Stamina > 0f;
        float target = ScriptedTarget.HasValue ? scriptedSpeed * mag
                     : (IsCrouching ? crouchSpeed * mag
                     : IsSprinting ? sprintSpeed
                     : Mathf.Lerp(walkSpeed, runSpeed, Mathf.InverseLerp(0.55f, 1f, mag)) * (mag < 0.1f ? 0f : 1f)) * load;
        Vector3 want = dir.normalized * target;
        float accel = grounded ? 22f : 8f;
        velocity = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), want, accel * dt);

        // stamina
        if (IsSprinting) { Stamina -= sprintDrain * dt; if (Stamina <= 0f) { Stamina = 0f; exhausted = true; } }
        else { Stamina = Mathf.Min(maxStamina, Stamina + staminaRegen * dt); if (exhausted && Stamina > maxStamina * 0.3f) exhausted = false; }

        // facing
        if (want.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(want.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * dt);
        }

        // crouch collider (fits under beds/couches)
        float h = IsCrouching ? crouchHeight : standHeight;
        if (!Mathf.Approximately(cc.height, h)) { cc.height = h; cc.center = new Vector3(0f, h * 0.5f, 0f); }
        CameraPivot.localPosition = Vector3.Lerp(CameraPivot.localPosition, new Vector3(0f, IsCrouching ? 0.45f : 0.75f, 0f), 10f * dt);

        // gravity / jump
        if (grounded && yVel < 0f) yVel = -2f;
        yVel -= gravity * dt;
        var flags = cc.Move((velocity + Vector3.up * yVel) * dt);
        wasGrounded = grounded;
        grounded = (flags & CollisionFlags.Below) != 0;
        if ((flags & CollisionFlags.Above) != 0 && yVel > 0f) yVel = 0f;
        if (grounded && !wasGrounded && yVel < -6f) HouseGame.MakeNoise(transform.position, 5f);   // thump

        // footsteps -> noise for the cat (sneaking is silent)
        if (grounded && Speed > 0.5f && !IsCrouching)
        {
            stepTimer -= dt * Speed;
            if (stepTimer <= 0f)
            {
                stepTimer = 1.6f;
                float radius = IsSprinting ? 11f : Speed > walkSpeed + 0.5f ? 6f : 2.5f;
                radius += carried * 1.5f;                          // a full load is clumsier
                HouseGame.MakeNoise(transform.position, radius);
            }
        }
    }

    // food held in the mouse's arms (rebuilt when the carried list changes)
    Transform carryRoot;
    int shownCarry = -1;

    void LateUpdate()
    {
        var g = HouseGame.I;
        if (!g || !g.house) return;
        if (g.Carried.Count != shownCarry)
        {
            shownCarry = g.Carried.Count;
            if (carryRoot) Destroy(carryRoot.gameObject);
            carryRoot = new GameObject("Carry").transform;
            carryRoot.SetParent(transform, false);
            carryRoot.localPosition = new Vector3(0f, 0.42f, 0.32f);
            for (int i = 0; i < g.Carried.Count; i++)
            {
                var holder = new GameObject("Held").transform;
                holder.SetParent(carryRoot, false);
                holder.localPosition = new Vector3(0f, i * 0.28f, 0f);
                var model = g.house.FoodModel(g.Carried[i], holder);
                model.transform.localScale = Vector3.one * 0.55f;
                model.transform.localPosition = Vector3.zero;
            }
        }
        if (carryRoot) carryRoot.gameObject.SetActive(!IsHidden);
    }

    // editor / PC testing: WASD move, Shift sprint, Space jump, C crouch
    void ReadKeyboard()
    {
        var k = Keyboard.current;
        if (k == null || !InputEnabled) return;
        Vector2 kb = Vector2.zero;
        if (k.wKey.isPressed || k.upArrowKey.isPressed) kb.y += 1f;
        if (k.sKey.isPressed || k.downArrowKey.isPressed) kb.y -= 1f;
        if (k.dKey.isPressed || k.rightArrowKey.isPressed) kb.x += 1f;
        if (k.aKey.isPressed || k.leftArrowKey.isPressed) kb.x -= 1f;
        if (kb != Vector2.zero) moveInput = kb.normalized;
        else if (HouseUI.JoystickIdle) moveInput = Vector2.zero;
        if (k.leftShiftKey.isPressed) sprintHeld = true;
        else if (k.leftShiftKey.wasReleasedThisFrame) sprintHeld = false;
        if (k.spaceKey.wasPressedThisFrame) Jump();
        if (k.cKey.wasPressedThisFrame) ToggleCrouch();
    }
}
