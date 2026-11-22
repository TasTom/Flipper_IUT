using System.Collections.Generic;
using UnityEngine;

/// <summary>Allows balls through a flap from its approach side without opening it for other balls.</summary>
public class OneWayGate : MonoBehaviour
{
    [SerializeField] private Collider clapet;
    [SerializeField] private Collider surveillance;
    [SerializeField] private Transform voletVisuel;
    [SerializeField] private bool utiliserAxeLocal;
    [SerializeField, Min(0f)] private float seuilMontee = 1f;

    private readonly HashSet<Collider> autorisees = new HashSet<Collider>();
    private Quaternion rotationFermee;

    public bool IsOpen => autorisees.Count > 0;

    private void Awake()
    {
        if (surveillance == null) surveillance = GetComponent<Collider>();
        if (clapet == null)
            foreach (var candidate in GetComponentsInChildren<Collider>(true))
                if (!candidate.isTrigger && candidate != surveillance)
                {
                    clapet = candidate;
                    break;
                }

        if (surveillance == null || !surveillance.isTrigger || clapet == null || clapet.isTrigger)
        {
            Debug.LogWarning($"[OneWayGate] « {name} » exige un capteur trigger et un clapet solide.", this);
            enabled = false;
            return;
        }

        if (voletVisuel != null) rotationFermee = voletVisuel.localRotation;
    }

    private void OnTriggerEnter(Collider other) => Evaluer(other);
    private void OnTriggerStay(Collider other) => Evaluer(other);
    private void OnTriggerExit(Collider other) => RetablirCollision(other);

    private void Evaluer(Collider other)
    {
        if (!isActiveAndEnabled || !other.CompareTag("Ball")) return;
        var balle = other.attachedRigidbody;
        if (balle == null || balle.isKinematic) return;

        Vector3 axe = utiliserAxeLocal ? transform.forward : transform.root.forward;
        float position = Vector3.Dot(other.bounds.center - clapet.bounds.center, axe);
        bool approche = position < 0f && Vector3.Dot(balle.linearVelocity, axe) > seuilMontee;
        bool autorisee = autorisees.Contains(other);
        if (autorisee)
        {
            Vector3 projection = new Vector3(Mathf.Abs(axe.x), Mathf.Abs(axe.y), Mathf.Abs(axe.z));
            float degagement = Vector3.Dot(projection, other.bounds.extents + clapet.bounds.extents);
            if (!approche && Mathf.Abs(position) > degagement) RetablirCollision(other);
        }
        else if (approche)
        {
            autorisees.Add(other);
            Physics.IgnoreCollision(other, clapet);
            ActualiserVolet();
        }
    }

    private void FixedUpdate()
    {
        if (autorisees.RemoveWhere(balle => balle == null || !balle.gameObject.activeInHierarchy) > 0)
            ActualiserVolet();
    }

    private void RetablirCollision(Collider balle)
    {
        if (!autorisees.Remove(balle)) return;
        if (balle != null && clapet != null) Physics.IgnoreCollision(balle, clapet, false);
        ActualiserVolet();
    }

    private void ActualiserVolet()
    {
        if (voletVisuel != null)
            voletVisuel.localRotation = IsOpen ? rotationFermee * Quaternion.Euler(-88f, 0f, 0f) : rotationFermee;
    }

    private void OnDisable()
    {
        foreach (var balle in autorisees)
            if (balle != null && clapet != null) Physics.IgnoreCollision(balle, clapet, false);
        autorisees.Clear();
        ActualiserVolet();
    }
}
