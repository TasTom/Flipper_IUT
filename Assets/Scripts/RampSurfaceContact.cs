using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// GDD Rampes: apply the authored ramp restitution to ramp contacts only.
/// The ball uses Maximum restitution, which otherwise overrides even a zero-bounce ramp.
/// This changes contact restitution, never the ball's trajectory or velocity directly.
/// </summary>
public sealed class RampSurfaceContact : MonoBehaviour
{
    [SerializeField] private PhysicsMaterial surface;
    private readonly HashSet<EntityId> colliderIds = new HashSet<EntityId>();
    private Collider[] colliders;
    private bool[] originalFlags;
    private float restitution;

    void Awake()
    {
        colliders = GetComponentsInChildren<Collider>();
        originalFlags = new bool[colliders.Length];
        restitution = surface != null ? surface.bounciness : 0f;
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderIds.Add(colliders[i].GetEntityId());
            originalFlags[i] = colliders[i].hasModifiableContacts;
            colliders[i].hasModifiableContacts = true;
        }
        if (colliders.Length == 0) Debug.LogWarning("[RampSurfaceContact] Aucune surface de rampe.", this);
    }

    void OnEnable()
    {
        Physics.ContactModifyEvent += Modify;
        Physics.ContactModifyEventCCD += Modify;
    }

    void OnDisable()
    {
        Physics.ContactModifyEvent -= Modify;
        Physics.ContactModifyEventCCD -= Modify;
    }

    void OnDestroy()
    {
        if (colliders == null) return;
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].hasModifiableContacts = originalFlags[i];
    }

    // PhysX can invoke this on worker threads. Use cached value data only; no Unity object API.
    void Modify(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
    {
        for (int i = 0; i < pairs.Length; i++)
        {
            var pair = pairs[i];
            if (!colliderIds.Contains(pair.colliderEntityId) && !colliderIds.Contains(pair.otherColliderEntityId)) continue;
            for (int c = 0; c < pair.contactCount; c++) pair.SetBounciness(c, restitution);
        }
    }
}
