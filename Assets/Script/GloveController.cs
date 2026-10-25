using UnityEngine;
using UnityEngine.InputSystem;

// Lit les deux sticks et les boutons de hauteur, et deplace Target_L / Target_R
// dans un repere (ControlSpace) qui suit le torse du joueur.
// Ne touche JAMAIS aux gants ni au ragdoll : seules les cibles (Transform) sont deplacees.
[DefaultExecutionOrder(-50)]
public class GloveController : MonoBehaviour
{
    [Header("References (remplies par Reset)")]
    public Transform controlSpace;
    public Transform targetL;
    public Transform targetR;
    public Rigidbody anchorBody;            // Torso du joueur

    [Header("Repere de controle")]
    public Vector3 anchorOffset = new Vector3(0f, 0.18f, 0f);
    public float followSmoothTime = 0.08f;
    public float yawSmoothTime = 0.15f;

    [Header("Garde et amplitude (m, repere ControlSpace)")]
    public Vector3 guardPosition = new Vector3(0.22f, 0.10f, 0.35f); // x = main droite, miroir pour la gauche
    public float lateralRange = 0.30f;
    public float forwardRange = 0.26f;
    public float backwardRange = 0.20f;

    [Header("Hauteur : LB/RB monter, LT/RT descendre")]
    public float heightMin = -0.40f;
    public float heightMax = 0.30f;
    public float heightSpeed = 0.9f;
    public float heightReturnSpeed = 0f;    // 0 = la hauteur reste

    [Header("Zone autorisee")]
    public Vector3 shoulderOffset = new Vector3(0.24f, 0.22f, 0f); // x = epaule droite, miroir pour la gauche
    public float maxReach = 0.58f;
    public float crossLimit = 0.15f;        // jusqu'ou le gant passe la ligne centrale
    public float outerLimit = 0.60f;        // ecart lateral maximal
    public Vector2 yLimits = new Vector2(-0.30f, 0.40f);
    public Vector2 zLimits = new Vector2(0.12f, 0.62f);

    [Header("Lissage")]
    public float smoothTime = 0.05f;
    public float maxSpeed = 6f;

    [Header("Inversions")]
    public bool invertDepth;
    public bool invertLateral;
    public bool invertHeight;

    readonly InputAction[] move = new InputAction[2];
    readonly InputAction[] up = new InputAction[2];
    readonly InputAction[] down = new InputAction[2];
    readonly Transform[] targets = new Transform[2];
    readonly Vector2[] stick = new Vector2[2];
    readonly float[] upIn = new float[2];
    readonly float[] downIn = new float[2];
    readonly float[] height = new float[2];
    readonly Vector3[] vel = new Vector3[2];

    Vector3 smoothedOrigin, originVel;
    float smoothedYaw, yawVel;
    bool ready;
    GUIStyle style;

    void Reset() { AutoFind(); }

    void AutoFind()
    {
        if (controlSpace == null) controlSpace = transform.Find("ControlSpace");
        if (controlSpace != null)
        {
            if (targetL == null) targetL = controlSpace.Find("Target_L");
            if (targetR == null) targetR = controlSpace.Find("Target_R");
        }
        if (anchorBody == null)
        {
            GameObject p = GameObject.Find("Player_Ragdoll");
            if (p != null)
            {
                Transform t = p.transform.Find("Pelvis/Torso");
                if (t != null) anchorBody = t.GetComponent<Rigidbody>();
            }
        }
    }

    void Awake()
    {
        AutoFind();
        if (controlSpace == null || targetL == null || targetR == null || anchorBody == null)
        {
            Debug.LogError("GloveController : references manquantes (Control Space, Target L, Target R, Anchor Body).");
            enabled = false;
            return;
        }
        targets[0] = targetL;
        targets[1] = targetR;

        move[0] = new InputAction("MoveL", InputActionType.Value, "<Gamepad>/leftStick");
        move[1] = new InputAction("MoveR", InputActionType.Value, "<Gamepad>/rightStick");
        up[0] = new InputAction("UpL", InputActionType.Value, "<Gamepad>/leftShoulder");
        down[0] = new InputAction("DownL", InputActionType.Value, "<Gamepad>/leftTrigger");
        up[1] = new InputAction("UpR", InputActionType.Value, "<Gamepad>/rightShoulder");
        down[1] = new InputAction("DownR", InputActionType.Value, "<Gamepad>/rightTrigger");
        ready = true;
    }

    void OnEnable()
    {
        if (!ready) return;
        for (int i = 0; i < 2; i++) { move[i].Enable(); up[i].Enable(); down[i].Enable(); }
    }

    void OnDisable()
    {
        if (!ready) return;
        for (int i = 0; i < 2; i++) { move[i].Disable(); up[i].Disable(); down[i].Disable(); }
    }

    void OnDestroy()
    {
        if (!ready) return;
        for (int i = 0; i < 2; i++) { move[i].Dispose(); up[i].Dispose(); down[i].Dispose(); }
    }

    void Start()
    {
        if (!ready) return;
        // Initialisation : on place le repere et les cibles directement en garde (rien de physique n'est touche).
        smoothedOrigin = anchorBody.position + anchorOffset;
        smoothedYaw = DesiredYaw(0f);
        controlSpace.SetPositionAndRotation(smoothedOrigin, Quaternion.Euler(0f, smoothedYaw, 0f));
        for (int i = 0; i < 2; i++)
        {
            float side = Side(i);
            targets[i].localPosition = ClampToZone(new Vector3(side * guardPosition.x, guardPosition.y, guardPosition.z), side);
            vel[i] = Vector3.zero;
            height[i] = 0f;
        }
    }

    static float Side(int i) { return i == 0 ? -1f : 1f; }

    // Update : lecture des entrees uniquement
    void Update()
    {
        for (int i = 0; i < 2; i++)
        {
            stick[i] = move[i].ReadValue<Vector2>();
            upIn[i] = up[i].ReadValue<float>();
            downIn[i] = down[i].ReadValue<float>();
        }
    }

    // FixedUpdate : mise a jour du repere et des cibles, au pas de la physique
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        Vector3 origin = anchorBody.position + anchorOffset;
        smoothedOrigin = Vector3.SmoothDamp(smoothedOrigin, origin, ref originVel, followSmoothTime, Mathf.Infinity, dt);
        smoothedYaw = Mathf.SmoothDampAngle(smoothedYaw, DesiredYaw(smoothedYaw), ref yawVel, yawSmoothTime, Mathf.Infinity, dt);
        controlSpace.SetPositionAndRotation(smoothedOrigin, Quaternion.Euler(0f, smoothedYaw, 0f));

        for (int i = 0; i < 2; i++) UpdateHand(i, dt);
    }

    float DesiredYaw(float fallback)
    {
        Vector3 f = Vector3.ProjectOnPlane(anchorBody.rotation * Vector3.forward, Vector3.up);
        if (f.sqrMagnitude < 0.05f) return fallback;   // torse couche : on garde le dernier cap
        return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }

    void UpdateHand(int i, float dt)
    {
        float side = Side(i);
        Vector2 s = stick[i];
        if (invertLateral) s.x = -s.x;
        if (invertDepth) s.y = -s.y;

        // Hauteur : integree, bornee
        float climb = upIn[i] - downIn[i];
        if (invertHeight) climb = -climb;
        height[i] += climb * heightSpeed * dt;
        if (Mathf.Abs(climb) < 0.01f && heightReturnSpeed > 0f)
            height[i] = Mathf.MoveTowards(height[i], 0f, heightReturnSpeed * dt);
        height[i] = Mathf.Clamp(height[i], heightMin, heightMax);

        Vector3 desired = new Vector3(
            side * guardPosition.x + s.x * lateralRange,
            guardPosition.y + height[i],
            guardPosition.z + (s.y >= 0f ? s.y * forwardRange : s.y * backwardRange));
        desired = ClampToZone(desired, side);

        Vector3 next = Vector3.SmoothDamp(targets[i].localPosition, desired, ref vel[i], smoothTime, maxSpeed, dt);
        targets[i].localPosition = next;
    }

    Vector3 ClampToZone(Vector3 p, float side)
    {
        float outward = Mathf.Clamp(p.x * side, -crossLimit, outerLimit);
        p.x = outward * side;
        p.y = Mathf.Clamp(p.y, yLimits.x, yLimits.y);
        p.z = Mathf.Clamp(p.z, zLimits.x, zLimits.y);

        Vector3 shoulder = new Vector3(side * shoulderOffset.x, shoulderOffset.y, shoulderOffset.z);
        Vector3 d = p - shoulder;
        if (d.magnitude > maxReach) p = shoulder + d.normalized * maxReach;
        return p;
    }

    void OnGUI()
    {
        if (!ready) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(20, 305, 1400, 40),
            $"Hauteur gant G : {height[0]:F2} m | gant D : {height[1]:F2} m   (LB/RB monter, LT/RT descendre)", style);
        float reachL = (targets[0].localPosition - new Vector3(-shoulderOffset.x, shoulderOffset.y, shoulderOffset.z)).magnitude;
        float reachR = (targets[1].localPosition - new Vector3(shoulderOffset.x, shoulderOffset.y, shoulderOffset.z)).magnitude;
        GUI.Label(new Rect(20, 340, 1400, 40), $"Portee G : {reachL:F2} m | D : {reachR:F2} m  (max {maxReach:F2})", style);
    }
}