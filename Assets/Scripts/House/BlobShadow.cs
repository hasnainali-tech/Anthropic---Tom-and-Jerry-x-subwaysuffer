using UnityEngine;

// Soft contact shadow under a character: stays on whatever surface is below (floor, counter,
// bed), shrinks and fades while jumping. Cheap grounding that works on every Android GPU.
public class BlobShadow : MonoBehaviour
{
    public float size = 1f;
    Transform quad;
    Renderer rend;

    void Start()
    {
        var art = GameArt.Get();
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = "BlobShadow";
        quad = q.transform;
        rend = q.GetComponent<Renderer>();
        rend.sharedMaterial = art ? art.blobShadow : rend.sharedMaterial;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }

    void LateUpdate()
    {
        if (!quad) return;
        Vector3 from = transform.position + Vector3.up * 0.6f;
        bool hidden = GetComponent<MouseController>() is MouseController m && m.IsHidden;
        if (!hidden && Physics.Raycast(from, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            float h = Mathf.Max(0f, transform.position.y - hit.point.y);
            float k = Mathf.Clamp01(1f - h / 3f);
            quad.gameObject.SetActive(k > 0.05f);
            quad.position = hit.point + Vector3.up * 0.02f;
            quad.rotation = Quaternion.LookRotation(Vector3.down, transform.forward);
            quad.localScale = new Vector3(size, size * 1.15f, 1f) * (0.6f + 0.4f * k);
        }
        else quad.gameObject.SetActive(false);
    }

    void OnDestroy() { if (quad) Destroy(quad.gameObject); }
}
