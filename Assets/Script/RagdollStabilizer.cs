using System.Collections.Generic;
using UnityEngine;

// Garde un ragdoll debout (ressorts de posture + redressement du bassin + equilibre).
// Les impacts l'etourdissent (raideur reduite) ; trop de degats ou un desequilibre = KO definitif :
// le corps devient un poids mort. Utilise uniquement sur l'adversaire.
public class RagdollStabilizer : MonoBehaviour
{
    [Header("Etat")]
    [Range(0f, 1f)] public float stability = 1f;
    public float fadeTime = 0.3f;
    public float graceTime = 1.0f;

    [Header("KO (assistance coupee definitivement)")]
    public float knockdownAngle = 40f;      // deg : inclinaison du torse
    public float knockdownHeight = 0.55f;   // m au-dessus du sol d'origine
    public float maxOffBalance = 0.5f;      // m : ecart horizontal bassin / pieds

    [Header("Etourdissement (impacts recus)")]
    public float stunImpulse = 150f;        // N.s qui etourdissent a 100 % (depend du modele)
    public float maxStunPerHit = 0.7f;      // un seul coup n'envoie pas KO
    public float stunRecover = 0.25f;       // par seconde

    [Header("Direction")]
    public Transform facingTarget;

    [Header("Posture : ressort (N.m/rad) et amortisseur (N.m.s/rad)")]
    public float spineSpring = 700f, spineDamper = 60f;
    public float headSpring = 40f, headDamper = 4f;
    public float hipSpring = 900f, hipDamper = 70f;
    public float kneeSpring = 900f, kneeDamper = 70f;
    public float ankleSpring = 350f, ankleDamper = 35f;

    [Header("Redressement du bassin (rad/s2)")]
    public float uprightGain = 60f, uprightDamp = 10f;
    public float yawGain = 30f, yawDamp = 8f;

    [Header("Equilibre horizontal")]
    public float balanceSpring = 20f;
    public float balanceDamper = 3f;

    enum Part { Spine, Head, Hip, Knee, Ankle }
    struct Entry { public ConfigurableJoint joint; public Part part; public float passiveDamper; }

    readonly List<Entry> entries = new List<Entry>();
    Rigidbody pelvis, torso, footL, footR;
    float totalMass, current, applied = -1f, age, floorY, stun;
    bool knockedDown;
    Vector3 initialForward;

    public bool IsKnockedDown { get { return knockedDown; } }
    public bool IsStunned { get { return !knockedDown && stun > 0.4f; } }
    public float Stun { get { return stun; } }
    public float Assist { get { return current; } }   // 0 = mou, 1 = pleine force

    void Awake()
    {
        floorY = transform.position.y;
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
        {
            totalMass += rb.mass;
            switch (rb.name)
            {
                case "Pelvis": pelvis = rb; break;
                case "Torso": torso = rb; break;
                case "Foot_L": footL = rb; break;
                case "Foot_R": footR = rb; break;
            }
        }
        foreach (ConfigurableJoint j in GetComponentsInChildren<ConfigurableJoint>())
        {
            string n = j.name;
            if (n == "Torso") Register(j, Part.Spine);
            else if (n == "Head") Register(j, Part.Head);
            else if (n.StartsWith("Thigh")) Register(j, Part.Hip);
            else if (n.StartsWith("Shin")) Register(j, Part.Knee);
            else if (n.StartsWith("Foot")) Register(j, Part.Ankle);
        }
        if (pelvis == null || torso == null || footL == null || footR == null)
        {
            Debug.LogError("RagdollStabilizer : os introuvables sur " + name);
            enabled = false;
            return;
        }
        initialForward = Vector3.ProjectOnPlane(pelvis.transform.forward, Vector3.up).normalized;
        current = stability;
    }

    void Register(ConfigurableJoint j, Part part)
    {
        entries.Add(new Entry { joint = j, part = part, passiveDamper = j.slerpDrive.positionDamper });
    }

    // Appele par GloveImpact : impulsion recue (N.s)
    public void ReceiveImpact(float impulse)
    {
        if (knockedDown) return;
        stun += Mathf.Min(impulse / Mathf.Max(1f, stunImpulse), maxStunPerHit);
    }

    float Spring(Part p)
    {
        switch (p)
        {
            case Part.Spine: return spineSpring;
            case Part.Head: return headSpring;
            case Part.Hip: return hipSpring;
            case Part.Knee: return kneeSpring;
            default: return ankleSpring;
        }
    }

    float Damper(Part p)
    {
        switch (p)
        {
            case Part.Spine: return spineDamper;
            case Part.Head: return headDamper;
            case Part.Hip: return hipDamper;
            case Part.Knee: return kneeDamper;
            default: return ankleDamper;
        }
    }

    void Apply(float s)
    {
        foreach (Entry e in entries)
        {
            JointDrive d = e.joint.slerpDrive;
            d.positionSpring = Spring(e.part) * s;
            d.positionDamper = Mathf.Lerp(e.passiveDamper, Damper(e.part), s);
            e.joint.slerpDrive = d;
        }
    }

    Vector3 DesiredForward()
    {
        if (facingTarget != null)
        {
            Vector3 d = Vector3.ProjectOnPlane(facingTarget.position - pelvis.position, Vector3.up);
            if (d.sqrMagnitude > 0.01f) return d.normalized;
        }
        return initialForward;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        age += dt;
        stun = Mathf.MoveTowards(stun, 0f, stunRecover * dt);

        if (!knockedDown)
        {
            if (stun >= 1f) knockedDown = true;
            else if (age > graceTime)
            {
                float tilt = Vector3.Angle(torso.transform.up, Vector3.up);
                Vector3 feetC = (footL.position + footR.position) * 0.5f;
                Vector3 off = pelvis.position - feetC; off.y = 0f;
                if (tilt > knockdownAngle || pelvis.position.y < floorY + knockdownHeight || off.magnitude > maxOffBalance)
                    knockedDown = true;
            }
        }

        float target = knockedDown ? 0f : stability * (1f - Mathf.Clamp01(stun));
        current = Mathf.MoveTowards(current, target, dt / Mathf.Max(0.01f, fadeTime));
        if (Mathf.Abs(current - applied) > 0.002f) { Apply(current); applied = current; }
        if (current <= 0.001f) return;

        Vector3 w = pelvis.angularVelocity;
        Vector3 torque = Vector3.Cross(pelvis.transform.up, Vector3.up) * uprightGain
                       - new Vector3(w.x, 0f, w.z) * uprightDamp;
        Vector3 fwd = Vector3.ProjectOnPlane(pelvis.transform.forward, Vector3.up);
        float yawErr = Vector3.SignedAngle(fwd, DesiredForward(), Vector3.up) * Mathf.Deg2Rad;
        torque.y = yawErr * yawGain - w.y * yawDamp;
        pelvis.AddTorque(torque * current, ForceMode.Acceleration);

        Vector3 feet = (footL.position + footR.position) * 0.5f;
        Vector3 err = feet - pelvis.position; err.y = 0f;
        Vector3 vel = pelvis.linearVelocity; vel.y = 0f;
        pelvis.AddForce((err * balanceSpring - vel * balanceDamper) * totalMass * current, ForceMode.Force);
    }
}