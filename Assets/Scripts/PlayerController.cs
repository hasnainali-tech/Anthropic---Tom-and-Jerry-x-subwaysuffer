using UnityEngine;
using UnityEngine.InputSystem;

// Jerry: auto-runs forward, 3 lanes, jump, slide.
// Root = logic + collider. Child "Visual" = mesh (replace with Blender model later).
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour, IMouseMotion
{
    // IMouseMotion (runner): legs cycle while the run is on; menu = wave; game over = scared
    bool IMouseMotion.IsMoving => IsRunning && GameManager.Instance && GameManager.Instance.IsPlaying;
    bool IMouseMotion.IsCrouching => false;
    bool IMouseMotion.IsScared => GameManager.Instance && GameManager.Instance.IsGameOver;
    bool IMouseMotion.IsWaving => GameManager.Instance && GameManager.Instance.Current == GameManager.State.Menu;
    bool IMouseMotion.IsHidden => false;

    [Header("Run")]
    public float startSpeed = 10f;
    public float maxSpeed = 22f;
    public float speedGainPerSecond = 0.15f;

    [Header("Lanes")]
    public float laneWidth = 2.5f;
    public float laneChangeSpeed = 15f;

    [Header("Jump / Slide")]
    public float jumpVelocity = 9f;
    public float gravity = 28f;
    public float slideDuration = 0.7f;

    [Header("Swipe")]
    public float swipeThresholdPixels = 60f;

    public float Speed { get; private set; }
    public bool IsRunning { get; set; } = true;
    public bool IsSliding => slideTimer > 0f;
    public bool IsGrounded => grounded;           // read by MouseHero for jump/land poses
    public float VerticalVelocity => yVelocity;

    int lane = 0;                 // -1 left, 0 middle, 1 right
    float yVelocity;
    float slideTimer;
    bool grounded = true;

    CapsuleCollider col;
    Transform visual;
    float colHeight, colCenterY;

    Vector2 touchStart;
    bool touchActive, swipeUsed;

    void Awake()
    {
        Application.targetFrameRate = 60;
        col = GetComponent<CapsuleCollider>();
        colHeight = col.height;
        colCenterY = col.center.y;
        visual = transform.Find("Visual");
        Speed = startSpeed;
    }

    void Update()
    {
        if (!IsRunning) return;

        ReadKeyboard();
        ReadTouch();

        float dt = Time.deltaTime;
        Speed = Mathf.Min(maxSpeed, Speed + speedGainPerSecond * dt);

        Vector3 p = transform.position;

        // forward
        p.z += Speed * dt;

        // lanes
        float targetX = lane * laneWidth;
        p.x = Mathf.MoveTowards(p.x, targetX, laneChangeSpeed * dt);

        // vertical
        if (!grounded)
        {
            yVelocity -= gravity * dt;
            p.y += yVelocity * dt;
            if (p.y <= 0f) { p.y = 0f; yVelocity = 0f; grounded = true; }
        }

        transform.position = p;

        // slide timer
        if (slideTimer > 0f)
        {
            slideTimer -= dt;
            if (slideTimer <= 0f) SetSlidePose(false);
        }
    }

    // ---------- actions ----------
    void MoveLane(int dir)
    {
        int next = Mathf.Clamp(lane + dir, -1, 1);
        if (next != lane) AudioManager.PlayWhoosh();
        lane = next;
    }

    void Jump()
    {
        if (!grounded) return;
        if (IsSliding) { slideTimer = 0f; SetSlidePose(false); }
        grounded = false;
        yVelocity = jumpVelocity;
        AudioManager.PlayJump();
    }

    void Slide()
    {
        if (!IsSliding) AudioManager.PlaySlide();   // only when a slide starts
        if (!grounded) { yVelocity = -jumpVelocity * 1.5f; } // fast-fall, then slide
        slideTimer = slideDuration;
        SetSlidePose(true);
    }

    void SetSlidePose(bool sliding)
    {
        float h = sliding ? colHeight * 0.5f : colHeight;
        col.height = h;
        col.center = new Vector3(0f, sliding ? colCenterY * 0.5f : colCenterY, 0f);
        if (visual) visual.localScale = new Vector3(visual.localScale.x,
                                                    sliding ? 0.25f : 0.5f,
                                                    visual.localScale.z);
        if (visual) visual.localPosition = new Vector3(0f, sliding ? 0.25f : 0.5f, 0f);
    }

    // ---------- input ----------
    void ReadKeyboard()
    {
        var k = Keyboard.current;
        if (k == null) return;
        if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) MoveLane(-1);
        if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) MoveLane(1);
        if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame) Jump();
        if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) Slide();
    }

    void ReadTouch()
    {
        var ts = Touchscreen.current;
        if (ts == null) return;
        var t = ts.primaryTouch;

        if (t.press.wasPressedThisFrame)
        {
            touchStart = t.position.ReadValue();
            touchActive = true;
            swipeUsed = false;
        }
        else if (t.press.wasReleasedThisFrame)
        {
            touchActive = false;
        }

        if (!touchActive || swipeUsed) return;

        Vector2 d = t.position.ReadValue() - touchStart;
        if (d.magnitude < swipeThresholdPixels) return;

        swipeUsed = true; // one action per swipe
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) MoveLane(d.x > 0 ? 1 : -1);
        else if (d.y > 0) Jump();
        else Slide();
    }
}
