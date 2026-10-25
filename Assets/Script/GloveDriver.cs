using UnityEngine;

// Pilote un gant physique : force PD vers la cible + ConfigurableJoint gant -> main.
// La force du "muscle" agit sur le gant, et sa reaction agit sur tout le corps du joueur :
// c'est ce qui permet de tracter le corps quand le gant est agrippe.
// Aucun Transform.position n'est ecrit en jeu (hors placement initial unique).
public class GloveDriver : MonoBehaviour
{
    [Header("References (cablees par le menu)")]
    public Rigidbody glove;
    public Transform target;
    public Rigidbody upperArm, forearm, hand;
    public RagdollBody body;
    public GloveGrab grab;
    public Collider otherGlove;
    public RagdollStabilizer powerSource;

    [Header("Suivi PD")]
    public float kp = 400f;           // 1/s2 : raideur
    public float kd = 40f;            // 1/s  : amortissement (~2*sqrt(kp))
    public float maxAccel = 150f;     // m/s2 : plafond de force (depend du modele)
    public float maxError = 0.30f;    // m
    public float maxSpeed = 8f;       // m/s
    public float rampTime = 1.0f;     // s : montee en puissance au demarrage

    [Header("Effort sur le corps")]
    [Range(0f, 1f)] public float freeReaction = 0.15f;  // gant libre : recul leger du corps
    [Range(0f, 1f)] public float gripReaction = 1.0f;   // gant agrippe : le corps est tracte
    public float gripStrength = 2.2f;  // multiplicateur de force quand on est agrippe

    [Header("Bras")]
    [Range(0f, 1f)] public float gravityCompensation = 0.9f;
    public float shoulderDamper = 3f, elbowDamper = 2f, wristDamper = 0.3f;
    public int solverIterations = 20;
    public float gloveAngularDamping = 5f;

    [Header("Joint gant -> main")]
    public float projectionDistance = 0.1f;

    public float Gap { get; private set; }          // m : ecart gant <-> main
    public float TrackError { get; private set; }   // m : ecart gant <-> cible

    ConfigurableJoint joint;
    float effMass, age;
    Vector3 lastTarget;
    bool hasLast, ready;

    void Awake()
    {
        if (glove == null || target == null || upperArm == null || forearm == null || hand == null || body == null)
        {
            Debug.LogError("GloveDriver (" + name + ") : references manquantes.");
            enabled = false;
            return;
        }

        // Le gant ne collisionne ni avec le corps du joueur, ni avec l'autre gant
        Collider gc = glove.GetComponent<Collider>();
        foreach (Collider c in body.Colliders) Physics.IgnoreCollision(gc, c, true);
        if (otherGlove != null) Physics.IgnoreCollision(gc, otherGlove, true);

        // Placement initial unique sur le centre de la main
        SphereCollider hc = hand.GetComponent<SphereCollider>();
        Vector3 center = hc != null ? hand.transform.TransformPoint(hc.center) : hand.position;
        glove.isKinematic = false;
        glove.useGravity = false;
        glove.linearDamping = 0f;
        glove.angularDamping = gloveAngularDamping;
        glove.transform.SetPositionAndRotation(center, hand.transform.rotation);
        Physics.SyncTransforms();

        joint = glove.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = hand;
        joint.anchor = Vector3.zero;
        joint.autoConfigureConnectedAnchor = true;
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;
        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;
        joint.enableCollision = false;
        joint.enablePreprocessing = false;
        joint.projectionMode = JointProjectionMode.PositionAndRotation;
        joint.projectionDistance = projectionDistance;
        joint.projectionAngle = 180f;

        SetDamper(upperArm, shoulderDamper);
        SetDamper(forearm, elbowDamper);
        SetDamper(hand, wristDamper);
        foreach (Rigidbody rb in new[] { glove, upperArm, forearm, hand })
        {
            rb.solverIterations = solverIterations;
            rb.solverVelocityIterations = 8;
        }
        hand.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        effMass = glove.mass + upperArm.mass + forearm.mass + hand.mass;
        ready = true;
    }

    static void SetDamper(Rigidbody rb, float damper)
    {
        ConfigurableJoint j = rb.GetComponent<ConfigurableJoint>();
        if (j == null) return;
        JointDrive d = j.slerpDrive;
        d.positionDamper = damper;
        j.slerpDrive = d;
    }

    void FixedUpdate()
    {
        if (!ready) return;
        float dt = Time.fixedDeltaTime;
        age += dt;
        float power = Mathf.SmoothStep(0f, 1f, age / Mathf.Max(0.01f, rampTime))
            * (powerSource != null ? Mathf.Clamp01(powerSource.Assist) : 1f);

        bool gripping = grab != null && grab.IsGrabbing;
        float mul = gripping ? gripStrength : 1f;

        Vector3 tp = target.position;
        Vector3 tv = Vector3.zero;
        if (!gripping && hasLast) tv = Vector3.ClampMagnitude((tp - lastTarget) / dt, maxSpeed);
        lastTarget = tp;
        hasLast = true;

        Vector3 v = glove.linearVelocity;
        Vector3 err = tp - glove.position;
        TrackError = err.magnitude;
        err = Vector3.ClampMagnitude(err, maxError);

        Vector3 a = (kp * err + kd * (tv - v)) * mul;
        a = Vector3.ClampMagnitude(a, maxAccel * mul);
        Vector3 force = a * effMass * power;

        glove.AddForce(force, ForceMode.Force);
        body.AddDistributedForce(-force * (gripping ? gripReaction : freeReaction));

        // Freinage doux au-dessus de la vitesse maximale (par force, sans ecrire la vitesse)
        float sp = v.magnitude;
        if (sp > maxSpeed && !gripping)
            glove.AddForce(-(v / sp) * ((sp - maxSpeed) / dt) * effMass * 0.5f * power, ForceMode.Force);

        // Le bras ne pese presque rien : seul le corps est un poids mort
        Vector3 g = -Physics.gravity * gravityCompensation * power;
        upperArm.AddForce(g * upperArm.mass, ForceMode.Force);
        forearm.AddForce(g * forearm.mass, ForceMode.Force);
        hand.AddForce(g * hand.mass, ForceMode.Force);

        Vector3 wa = glove.transform.TransformPoint(joint.anchor);
        Vector3 wb = hand.transform.TransformPoint(joint.connectedAnchor);
        Gap = (wa - wb).magnitude;
    }
}