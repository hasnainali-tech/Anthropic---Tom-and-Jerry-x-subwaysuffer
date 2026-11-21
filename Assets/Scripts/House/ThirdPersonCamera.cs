using UnityEngine;
using UnityEngine.InputSystem;

// Free third-person orbit camera for the mouse.
// - Touch: drag on the right half of the screen (HouseUI feeds LookDelta). Mouse: hold right button.
// - Follows smoothly, never locked to a direction; gently swings behind only while moving
//   with no look input for a while.
// - Sphere-cast collision keeps it out of walls/furniture; it pulls in tight in tunnels and
//   under furniture, and switches to a peek view while hiding.
public class ThirdPersonCamera : MonoBehaviour
{
    public MouseController target;
    public float distance = 5.2f;
    public float minDistance = 0.55f;
    public float yaw, pitch = 21f;
    public float minPitch = -25f, maxPitch = 70f;
    public float lookSensitivity = 0.16f;     // degrees per pixel
    public float collisionRadius = 0.22f;

    public static Vector2 LookDelta;           // accumulated by HouseUI's look area
    float currentDist, lastLookTime, shake;
    Camera cam;
    static ThirdPersonCamera instance;

    public static void Shake(float amount) { if (instance) instance.shake = Mathf.Max(instance.shake, amount); }

    void Awake() { cam = GetComponent<Camera>(); instance = this; currentDist = distance; }

    public void SnapBehind()
    {
        if (!target) return;
        yaw = target.transform.eulerAngles.y;
        pitch = 21f;
        currentDist = distance;
        LateUpdate();
    }

    void LateUpdate()
    {
        if (!target) return;
        float dt = Time.unscaledDeltaTime;
        if (Time.timeScale <= 0f) { LookDelta = Vector2.zero; return; }

        // look input
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed) LookDelta += mouse.delta.ReadValue() * 1.2f;
        if (LookDelta.sqrMagnitude > 0.01f) lastLookTime = Time.unscaledTime;
        yaw += LookDelta.x * lookSensitivity;
        pitch = Mathf.Clamp(pitch - LookDelta.y * lookSensitivity, minPitch, maxPitch);
        LookDelta = Vector2.zero;

        // hiding: peek out from the hiding spot
        if (target.Hiding)
        {
            var view = target.Hiding.CameraPoint;
            transform.position = Vector3.Lerp(transform.position, view.position, 1f - Mathf.Exp(-8f * dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, view.rotation, 1f - Mathf.Exp(-8f * dt));
            if (cam) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 70f, 1f - Mathf.Exp(-6f * dt));
            return;
        }

        // gentle auto-follow while running and not looking around
        if (target.IsMoving && Time.unscaledTime - lastLookTime > 1.6f)
            yaw = Mathf.LerpAngle(yaw, target.transform.eulerAngles.y, 1f - Mathf.Exp(-1.2f * dt));

        Vector3 pivot = target.CameraPivot.position;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 back = rot * Vector3.back;

        // tighter framing when crouched (under furniture / in tunnels)
        float want = target.IsCrouching ? distance * 0.6f : distance;
        float allowed = want;
        if (Physics.SphereCast(pivot, collisionRadius, back, out var hit, want, ~0, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(target.transform))
            allowed = Mathf.Max(minDistance, hit.distance - 0.05f);
        // pull in instantly, ease back out
        currentDist = allowed < currentDist ? allowed : Mathf.Lerp(currentDist, allowed, 1f - Mathf.Exp(-3f * dt));

        Vector3 pos = pivot + back * currentDist;
        if (shake > 0.001f)
        {
            float t = Time.time * 40f;
            pos += new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * shake * 0.4f;
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
        }
        transform.SetPositionAndRotation(pos, rot);
        if (cam)
        {
            float fov = Screen.height > Screen.width ? 70f : 60f;
            if (target.IsSprinting) fov += 6f;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-5f * dt));
        }
    }
}
