using UnityEngine;

// Collectible cheese. Root = trigger collider, child "Visual" = mesh.
// Spawned and recycled by TrackManager. Each cheese counts once (+1 score).
// Motion: gentle up/down float only - the cheese NEVER rotates.
public class Cheese : MonoBehaviour
{
    public float floatHeight = 0.12f;    // metres above/below rest
    public float floatSpeed = 2.1f;      // radians per second

    bool collected;
    Transform visual, glow, shadow;
    Vector3 visualRest, glowRest, shadowScale;
    float phase;

    void Awake() => Cache();

    void Cache()
    {
        if (visual) return;
        visual = transform.Find("Visual");
        glow = transform.Find("Glow");
        shadow = transform.Find("Shadow");
        if (visual) visualRest = visual.localPosition;
        if (glow) glowRest = glow.localPosition;
        if (shadow) shadowScale = shadow.localScale;
    }

    void OnEnable()
    {
        collected = false;                       // reset when recycled
        Cache();
        phase = transform.position.z * 0.9f;     // neighbours float slightly out of step = a gentle wave
    }

    void Update()
    {
        if (!visual) return;
        float s = Mathf.Sin(Time.time * floatSpeed + phase);
        float y = s * floatHeight;
        visual.localPosition = visualRest + new Vector3(0f, y, 0f);       // position only, rotation untouched
        if (glow)
        {
            glow.localPosition = glowRest + new Vector3(0f, y, 0f);
            glow.localScale = Vector3.one * (1.55f + s * 0.08f);
        }
        if (shadow) shadow.localScale = shadowScale * (1f - s * 0.12f);   // smaller shadow when higher
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected || !other.GetComponentInParent<PlayerController>()) return;
        if (GameManager.Instance && GameManager.Instance.IsGameOver) return;
        collected = true;
        if (GameManager.Instance) GameManager.Instance.AddCheese();
        AudioManager.PlayChew();
        GameFX.CheesePop(visual ? visual.position : transform.position);
        gameObject.SetActive(false);
    }
}
