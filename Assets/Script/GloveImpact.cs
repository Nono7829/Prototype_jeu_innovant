using System.Collections.Generic;
using UnityEngine;

// Pas de bouton "frapper" : la collision reste physique (masse, vitesse). Ce script ajoute
// seulement un bonus d'impulsion qui croit avec le CARRE de la vitesse relative a l'impact :
// un arc ample donne un gant rapide et un coup violent. Un contact prolonge ne compte pas.
[RequireComponent(typeof(Rigidbody))]
public class GloveImpact : MonoBehaviour
{
    public float minImpulse = 1.5f;        // N.s : sous ce seuil, ignore
    public float minSpeed = 1.5f;          // m/s : sous ce seuil, pas de bonus
    public float newImpactDelay = 0.15f;   // s sans contact pour un nouvel impact
    public float refSpeed = 8f;            // m/s : vitesse du bonus maximal
    public float maxBonusImpulse = 120f;   // N.s (depend du modele : l'adversaire pese ~72 kg)
    public float flashTime = 0.15f;
    public Color flashColor = Color.white;

    struct Flash { public Renderer r; public float end; }

    readonly Dictionary<Rigidbody, float> lastContact = new Dictionary<Rigidbody, float>();
    readonly List<Flash> flashes = new List<Flash>();
    MaterialPropertyBlock block;
    Renderer ownVisual;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", flashColor);
        block.SetColor("_Color", flashColor);
        Transform v = transform.Find("Visual");
        if (v != null) ownVisual = v.GetComponent<Renderer>();
    }

    void OnCollisionEnter(Collision c) { Handle(c); }
    void OnCollisionStay(Collision c) { Handle(c); }

    void Handle(Collision c)
    {
        Rigidbody other = c.rigidbody;
        if (other == null) return;   // sol et decor : aucun bonus

        float now = Time.time;
        bool isNew = !lastContact.TryGetValue(other, out float last) || now - last > newImpactDelay;
        lastContact[other] = now;    // contact entretenu = jamais "nouveau"
        if (!isNew) return;

        float impulse = c.impulse.magnitude;
        float speed = c.relativeVelocity.magnitude;
        if (impulse < minImpulse || speed < minSpeed) return;

        float k = Mathf.Clamp01(speed / refSpeed);
        float bonus = maxBonusImpulse * k * k;
        ContactPoint cp = c.GetContact(0);
        other.AddForceAtPosition(-cp.normal * bonus, cp.point, ForceMode.Impulse);
        RagdollStabilizer stab = other.GetComponentInParent<RagdollStabilizer>();
        if (stab != null) stab.ReceiveImpact(impulse + bonus);

        Transform v = c.collider.transform.Find("Visual");
        if (v != null) StartFlash(v.GetComponent<Renderer>());
        StartFlash(ownVisual);
    }

    void StartFlash(Renderer r)
    {
        if (r == null) return;
        r.SetPropertyBlock(block);
        flashes.Add(new Flash { r = r, end = Time.time + flashTime });
    }

    void Update()
    {
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            if (Time.time < flashes[i].end) continue;
            if (flashes[i].r != null) flashes[i].r.SetPropertyBlock(null);
            flashes.RemoveAt(i);
        }
    }
}