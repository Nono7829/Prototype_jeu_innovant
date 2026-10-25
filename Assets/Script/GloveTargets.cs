using UnityEngine;

// Calcule la cible de chaque gant : epaule du ragdoll + decalage pilote par le stick,
// exprime dans un repere horizontal aligne sur la camera. Seules les cibles
// (Transform sans physique) sont deplacees ici.
[DefaultExecutionOrder(-50)]
public class GloveTargets : MonoBehaviour
{
    [Header("References (cablees par le menu)")]
    public PlayerInputs input;
    public Transform frameReference;          // Main Camera
    public Rigidbody shoulderL, shoulderR;    // UpperArm_L / UpperArm_R
    public Transform targetL, targetR;

    [Header("Zone (m, repere camera, depuis l'epaule)")]
    public Vector3 restOffset = new Vector3(0.16f, -0.10f, 0.28f); // x : ecart lateral (miroir pour la gauche)
    public Vector3 range = new Vector3(0.40f, 0.40f, 0.40f);      // amplitude des sticks
    public float maxReach = 0.58f;                                 // portee du bras (depend du modele)
    public float floorY = 0.5f;                                    // dessus de la plateforme
    public float floorMargin = 0.12f;                              // rayon du gant + marge

    [Header("Lissage")]
    public float smoothTime = 0.05f;
    public float maxSpeed = 10f;

    [Header("Inversions")]
    public bool invertLateral;
    public bool invertDepth;
    public bool invertHeight;

    Transform[] targets;
    Rigidbody[] shoulders;
    readonly Vector3[] vel = new Vector3[2];
    readonly float[] lateral = new float[2];
    readonly float[] depth = new float[2];
    readonly float[] height = new float[2];

    void Awake()
    {
        if (input == null || targetL == null || targetR == null || shoulderL == null || shoulderR == null)
        {
            Debug.LogError("GloveTargets : references manquantes.");
            enabled = false;
            return;
        }
        targets = new[] { targetL, targetR };
        shoulders = new[] { shoulderL, shoulderR };
    }

    void Start()
    {
        Quaternion frame = Frame();
        for (int i = 0; i < 2; i++) targets[i].position = Desired(i, frame);
    }

    Quaternion Frame()
    {
        float yaw = frameReference != null ? frameReference.eulerAngles.y : 0f;
        return Quaternion.Euler(0f, yaw, 0f);
    }

    void ReadInput(int i)
    {
        Vector2 s = input.Stick(i);
        lateral[i] = invertLateral ? -s.x : s.x;
        // Un stick a 2 axes : l'axe vertical pilote la profondeur, ou la hauteur si LB/RB est maintenu.
        // L'axe non pilote garde sa derniere valeur.
        if (input.VerticalMode(i)) height[i] = invertHeight ? -s.y : s.y;
        else depth[i] = invertDepth ? -s.y : s.y;
    }

    Vector3 Desired(int i, Quaternion frame)
    {
        float side = i == 0 ? -1f : 1f;
        Vector3 local = new Vector3(
            side * restOffset.x + lateral[i] * range.x,
            restOffset.y + height[i] * range.y,
            restOffset.z + depth[i] * range.z);
        Vector3 offset = Vector3.ClampMagnitude(frame * local, maxReach);
        Vector3 p = shoulders[i].position + offset;
        p.y = Mathf.Max(p.y, floorY + floorMargin);
        return p;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Quaternion frame = Frame();
        for (int i = 0; i < 2; i++)
        {
            ReadInput(i);
            targets[i].position = Vector3.SmoothDamp(targets[i].position, Desired(i, frame),
                                                     ref vel[i], smoothTime, maxSpeed, dt);
        }
    }

    void OnDrawGizmos()
    {
        if (targetL != null) { Gizmos.color = new Color(1f, 0.8f, 0.1f); Gizmos.DrawWireSphere(targetL.position, 0.06f); }
        if (targetR != null) { Gizmos.color = new Color(0.2f, 0.9f, 0.3f); Gizmos.DrawWireSphere(targetR.position, 0.06f); }
    }
}