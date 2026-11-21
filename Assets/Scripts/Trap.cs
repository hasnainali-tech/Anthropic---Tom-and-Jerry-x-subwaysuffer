using System.Collections.Generic;
using UnityEngine;

// Obstacle. Root = trigger BoxCollider (no Rigidbody: the mouse's kinematic Rigidbody drives
// the trigger events). Floor snap traps are jumped over; a "requiresSlide" bar (rolling pin)
// is slid under. Spawned and recycled by TrackManager.
public class Trap : MonoBehaviour
{
    [Tooltip("True for overhead bars you slide under (used for the tutorial hint).")]
    public bool requiresSlide;

    // active obstacles, so the tutorial can find the next one without scene searches
    public static readonly HashSet<Trap> Active = new HashSet<Trap>();

    bool triggered;

    void OnEnable() { triggered = false; Active.Add(this); }   // reset when recycled
    void OnDisable() => Active.Remove(this);

    void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.GetComponentInParent<PlayerController>()) return;
        triggered = true;                    // this obstacle can only hit once
        if (GameManager.Instance) GameManager.Instance.TrapHit();
    }
}
