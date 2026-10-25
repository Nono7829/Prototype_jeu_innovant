using UnityEngine;

// Outils communs d'un ragdoll : liste des os, collisions internes desactivees,
// centre de masse, force repartie sur toute la masse.
public class RagdollBody : MonoBehaviour
{
    public Rigidbody[] Bodies { get { Init(); return bodies; } }
    public Collider[] Colliders { get { Init(); return colliders; } }
    public float TotalMass { get { Init(); return totalMass; } }

    Rigidbody[] bodies;
    Collider[] colliders;
    float totalMass;
    bool ready;

    void Awake() { Init(); }

    void Init()
    {
        if (ready) return;
        ready = true;
        bodies = GetComponentsInChildren<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        foreach (Rigidbody rb in bodies) totalMass += rb.mass;
        for (int i = 0; i < colliders.Length; i++)
            for (int j = i + 1; j < colliders.Length; j++)
                Physics.IgnoreCollision(colliders[i], colliders[j], true);
    }

    public Vector3 CenterOfMass()
    {
        Init();
        Vector3 sum = Vector3.zero;
        foreach (Rigidbody rb in bodies) sum += rb.worldCenterOfMass * rb.mass;
        return totalMass > 0f ? sum / totalMass : transform.position;
    }

    // Meme acceleration pour tous les os : le corps se deplace d'un bloc, sans etirer les joints.
    public void AddDistributedForce(Vector3 force)
    {
        Init();
        float inv = 1f / totalMass;
        foreach (Rigidbody rb in bodies) rb.AddForce(force * (rb.mass * inv), ForceMode.Force);
    }
}