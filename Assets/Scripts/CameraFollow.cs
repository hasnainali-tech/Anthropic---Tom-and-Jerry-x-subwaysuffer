using UnityEngine;

// Put on Main Camera. Follows the mouse from behind/above.
// - Portrait phones get a wider vertical FOV and a slightly higher/further camera so all
//   three lanes stay on screen (a fixed 60 deg FOV shows only ~30 deg horizontally there).
// - Smooth blend from the menu shot into the follow shot when a run starts.
// - Subtle FOV kick with speed, and screen shake on hits.
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 4.5f, -7f);           // landscape
    public Vector3 portraitOffset = new Vector3(0f, 5.6f, -8.4f);   // portrait
    public float lookAhead = 6f;
    public float xFollow = 0.5f;   // 0 = camera stays centered, 1 = fully follows lane
    public float smooth = 10f;
    [Tooltip("Horizontal field of view kept on portrait screens.")]
    public float portraitHorizontalFov = 52f;

    static float shake;
    Camera cam;
    PlayerController pc;
    Vector3 blendPos; Quaternion blendRot; float blendT = 1f, blendDur = 1f;

    public static void Shake(float amount) => shake = Mathf.Max(shake, amount);

    void Awake() => cam = GetComponent<Camera>();

    public void BlendFrom(Vector3 pos, Quaternion rot, float duration)
    {
        blendPos = pos; blendRot = rot; blendDur = Mathf.Max(0.01f, duration); blendT = 0f;
    }

    public void SnapNow()
    {
        blendT = 1f;
        if (!target) return;
        Pose(out var p, out var r, true);
        transform.SetPositionAndRotation(p, r);
    }

    float Aspect => cam ? cam.aspect : (float)Screen.width / Mathf.Max(1, Screen.height);
    bool Portrait => Aspect < 1f;

    float BaseFov()
    {
        if (!Portrait) return 60f;
        float h = portraitHorizontalFov * Mathf.Deg2Rad * 0.5f;
        float v = 2f * Mathf.Atan(Mathf.Tan(h) / Aspect) * Mathf.Rad2Deg;
        return Mathf.Clamp(v, 60f, 84f);
    }

    public float MenuFov() => Portrait ? Mathf.Clamp(BaseFov() - 12f, 55f, 72f) : 50f;

    [Header("Mouse-hole pass")]
    [Tooltip("Camera offset while following the mouse through a mouse hole (low, close behind).")]
    public Vector3 holeOffset = new Vector3(0f, 1.6f, -4.4f);
    float dip;   // 0 = normal, 1 = inside the hole

    void Pose(out Vector3 pos, out Quaternion rot, bool snap)
    {
        Vector3 t = target.position;
        dip = TrackManager.CameraDip(t.z);
        Vector3 off = Vector3.Lerp(Portrait ? portraitOffset : offset, holeOffset, dip);
        float x = t.x * xFollow + off.x;
        Vector3 p = transform.position;
        p.x = snap ? x : Mathf.Lerp(p.x, x, smooth * Time.deltaTime);
        p.y = off.y;
        p.z = t.z + off.z;                              // lock z so the camera never lags behind speed
        pos = p;
        // portrait: aim a little lower so the tall screen shows more track and less ceiling
        float lookY = Portrait ? 0.4f : 1f, ahead = Portrait ? lookAhead - 1f : lookAhead;
        lookY = Mathf.Lerp(lookY, 0.9f, dip);                // level gaze while inside the hole
        ahead = Mathf.Lerp(ahead, 7f, dip);
        rot = Quaternion.LookRotation(new Vector3(p.x, lookY, t.z + ahead) - p);
    }

    void LateUpdate()
    {
        if (!target) return;
        if (!pc) pc = target.GetComponent<PlayerController>();
        Pose(out var pos, out var rot, false);

        // speed FOV kick
        float speed01 = pc ? Mathf.InverseLerp(pc.startSpeed, pc.maxSpeed, pc.Speed) : 0f;
        float fov = Mathf.Lerp(BaseFov() + speed01 * 6f, 66f, dip);   // tighter view in the tunnel

        if (blendT < 1f)
        {
            // orbit around the mouse from the menu shot to the chase shot, always looking at it
            blendT += Time.deltaTime / blendDur;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blendT));
            Vector3 c = target.position;
            Vector3 a = blendPos - c, b = pos - c;
            float angA = Mathf.Atan2(a.x, a.z), angB = Mathf.Atan2(b.x, b.z);
            if (angB - angA > Mathf.PI) angB -= Mathf.PI * 2f;
            if (angA - angB > Mathf.PI) angB += Mathf.PI * 2f;
            float ang = Mathf.Lerp(angA, angB, k);
            float ra = new Vector2(a.x, a.z).magnitude, rb = new Vector2(b.x, b.z).magnitude;
            float r = Mathf.Lerp(ra, rb, k) + Mathf.Sin(k * Mathf.PI) * 0.8f;
            float h = Mathf.Lerp(a.y, b.y, k) + Mathf.Sin(k * Mathf.PI) * 0.6f;
            pos = c + new Vector3(Mathf.Sin(ang) * r, h, Mathf.Cos(ang) * r);
            Vector3 lookA = c + new Vector3(0f, 0.7f, 0f);
            Vector3 lookB = pos + rot * Vector3.forward * 8f;
            Vector3 look = Vector3.Lerp(lookA, lookB, k * k);
            rot = Quaternion.LookRotation(look - pos);
            fov = Mathf.Lerp(MenuFov(), fov, k);
        }

        if (shake > 0.001f)
        {
            float t = Time.time * 40f;
            pos += new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * shake * 0.6f;
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 1.6f);
        }

        transform.SetPositionAndRotation(pos, rot);
        if (cam) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-6f * Time.deltaTime));
    }
}
