using UnityEngine;

// "Fermer le gant" : tant que la gachette est tenue, le gant s'agrippe a ce qu'il touche
// (sol, corps adverse, cordes). Sol et adversaire : prise rigide. Cordes : prise elastique.
// Une prise trop forte casse (breakForce) : le gant lache.
public class GloveGrab : MonoBehaviour
{
    [Header("References (cablees par le menu)")]
    public PlayerInputs input;
    public int handIndex;            // 0 = gauche, 1 = droite
    public RagdollBody ownBody;
    public Transform visual;

    [Header("Prise")]
    public float grabRadius = 0.17f;
    public float gripOn = 0.55f;
    public float gripOff = 0.35f;
    public float breakForce = 20000f;      // N (depend du modele)
    public float regrabCooldown = 0.12f;   // s apres une prise cassee ou relachee
    public float closedScale = 0.8f;      // gant "ferme" : plus petit

    public bool IsGripping { get; private set; }  // gachette tenue
    public bool IsGrabbing { get; private set; }  // accroche a quelque chose

    Rigidbody rb;
    ConfigurableJoint joint;
    readonly Collider[] hits = new Collider[16];
    float cooldown;
    Vector3 openScale = Vector3.one;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (visual != null) openScale = visual.localScale;
    }

    void FixedUpdate()
    {
        if (input == null) return;
        cooldown -= Time.fixedDeltaTime;

        float g = input.Grip(handIndex);
        if (!IsGripping && g > gripOn) IsGripping = true;
        else if (IsGripping && g < gripOff) IsGripping = false;

        if (IsGrabbing && joint == null) { IsGrabbing = false; cooldown = regrabCooldown; } // prise cassee

        if (!IsGripping) { if (IsGrabbing) Release(); }
        else if (!IsGrabbing && cooldown <= 0f) TryGrab();

        if (visual != null) visual.localScale = openScale * (IsGripping ? closedScale : 1f);
    }

    void TryGrab()
    {
        Vector3 p = rb.position;
        int n = Physics.OverlapSphereNonAlloc(p, grabRadius, hits, ~0, QueryTriggerInteraction.Collide);
        Collider best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i];
            if (c.attachedRigidbody == rb) continue;
            if (c.attachedRigidbody != null && c.attachedRigidbody.GetComponent<GloveGrab>() != null) continue; // autre gant
            if (ownBody != null && c.transform.IsChildOf(ownBody.transform)) continue;                          // son propre corps
            float d = (c.ClosestPoint(p) - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = c; }
        }
        if (best == null) return;

        Rigidbody other = best.attachedRigidbody;
        GrabSurface surface = best.GetComponent<GrabSurface>();
        bool elastic = surface != null && surface.elastic;

        joint = gameObject.AddComponent<ConfigurableJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector3.zero;
        joint.connectedBody = other;   // null = ancre fixe dans le monde
        joint.connectedAnchor = other != null ? other.transform.InverseTransformPoint(p) : p;
        joint.enableCollision = false;
        joint.breakForce = breakForce;
        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;

        if (elastic)
        {
            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Limited;
            joint.zMotion = ConfigurableJointMotion.Limited;
            joint.linearLimit = new SoftJointLimit { limit = surface.slack };
            joint.linearLimitSpring = new SoftJointLimitSpring { spring = surface.spring, damper = surface.damper };
        }
        else
        {
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
        }
        IsGrabbing = true;
    }

    void Release()
    {
        if (joint != null) Destroy(joint);
        joint = null;
        IsGrabbing = false;
        cooldown = regrabCooldown;
    }
}