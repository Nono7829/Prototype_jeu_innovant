using UnityEngine;

// Camera simple et stable : pivot independant suivant le centre de masse du joueur,
// cap calcule depuis la direction joueur -> adversaire (jamais depuis un os),
// collision avec le decor, planchers de hauteur. Les cordes ne bloquent pas la camera.
[DefaultExecutionOrder(100)]
public class StableCamera : MonoBehaviour
{
    [Header("References (cablees par le menu)")]
    public Transform pivot;
    public RagdollBody player;
    public RagdollBody opponent;

    [Header("Cadrage")]
    public float distance = 3.6f;
    public float height = 1.0f;
    public float lookAhead = 1.2f;
    public float lookDrop = 0.6f;            // la camera vise un peu plus bas que le pivot
    public float minPivotHeight = 1.3f;      // plateforme a y = 0.5
    public float minCameraHeight = 1.0f;

    [Header("Collision")]
    public float collisionRadius = 0.25f;

    [Header("Lissage")]
    public float pivotSmoothTime = 0.25f;
    public float yawSmoothTime = 0.5f;
    public float positionSmoothTime = 0.12f;
    public float rotationSpeed = 6f;
    public float distanceRecoverSpeed = 4f;

    Vector3 pivotVel, camVel;
    float yaw, yawVel, currentDistance;
    static readonly RaycastHit[] hits = new RaycastHit[16];

    void Start()
    {
        if (pivot == null || player == null)
        {
            Debug.LogError("StableCamera : references manquantes (Pivot, Player).");
            enabled = false;
            return;
        }
        if (pivot.IsChildOf(player.transform))
            Debug.LogWarning("StableCamera : le pivot ne doit pas etre enfant du ragdoll.");

        pivot.position = PivotTarget();
        yaw = DesiredYaw(0f);
        currentDistance = distance;
        transform.position = DesiredPosition(pivot.position, yaw, distance);
        transform.rotation = Quaternion.LookRotation(LookPoint(pivot.position, yaw) - transform.position);
    }

    Vector3 PivotTarget()
    {
        Vector3 c = player.CenterOfMass();
        c.y = Mathf.Max(c.y, minPivotHeight);
        return c;
    }

    float DesiredYaw(float fallback)
    {
        if (opponent == null) return fallback;
        Vector3 d = opponent.CenterOfMass() - player.CenterOfMass();
        d.y = 0f;
        if (d.sqrMagnitude < 0.04f) return fallback;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    Vector3 DesiredPosition(Vector3 p, float yawDeg, float dist)
    {
        return p + Quaternion.Euler(0f, yawDeg, 0f) * Vector3.back * dist + Vector3.up * height;
    }

    Vector3 LookPoint(Vector3 p, float yawDeg)
    {
        return p + Quaternion.Euler(0f, yawDeg, 0f) * Vector3.forward * lookAhead + Vector3.down * lookDrop;
    }

    float AllowedDistance(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float len = dir.magnitude;
        if (len < 0.01f) return distance;
        int n = Physics.SphereCastNonAlloc(from, collisionRadius, dir / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
        float best = len;
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (c.attachedRigidbody != null) continue;                                   // ragdolls et gants
            if (c.TryGetComponent(out GrabSurface gs) && gs.elastic) continue;           // cordes
            if (hits[i].distance <= 0f) continue;
            best = Mathf.Min(best, hits[i].distance);
        }
        return best;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        pivot.position = Vector3.SmoothDamp(pivot.position, PivotTarget(), ref pivotVel, pivotSmoothTime);
        yaw = Mathf.SmoothDampAngle(yaw, DesiredYaw(yaw), ref yawVel, yawSmoothTime);

        Vector3 full = DesiredPosition(pivot.position, yaw, distance);
        float ratio = Mathf.Clamp01(AllowedDistance(pivot.position, full) / Mathf.Max(0.01f, (full - pivot.position).magnitude));
        float allowed = distance * ratio;
        currentDistance = allowed < currentDistance ? allowed
                        : Mathf.MoveTowards(currentDistance, allowed, distanceRecoverSpeed * dt);

        Vector3 desired = DesiredPosition(pivot.position, yaw, currentDistance);
        desired.y = Mathf.Max(desired.y, minCameraHeight);

        Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref camVel, positionSmoothTime);
        pos.y = Mathf.Max(pos.y, minCameraHeight);
        transform.position = pos;

        Quaternion look = Quaternion.LookRotation(LookPoint(pivot.position, yaw) - pos);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-rotationSpeed * dt));
    }
}