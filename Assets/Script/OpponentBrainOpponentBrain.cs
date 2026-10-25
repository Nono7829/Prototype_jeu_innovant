using UnityEngine;

// IA minimale de l'adversaire : avance vers le joueur, puis frappe avec ses gants
// (garde -> recul -> frappe -> retour), en alternant les mains.
// Elle ne deplace que les cibles ; la physique (GloveDriver) fait le reste.
public class OpponentBrain : MonoBehaviour
{
    [Header("References (cablees par le menu)")]
    public RagdollBody self;
    public RagdollStabilizer stabilizer;
    public RagdollBody player;
    public Rigidbody shoulderL, shoulderR;
    public Transform targetL, targetR;
    public Rigidbody playerGloveL, playerGloveR;

    [Header("Deplacement")]
    public float engageDistance = 1.0f;     // m : distance visee jusqu'au centre de masse du joueur
    public float advanceAccel = 3f;         // m/s2
    public float maxAdvanceSpeed = 0.8f;    // m/s

    [Header("Attaque")]
    public float attackInterval = 1.4f;     // s entre deux attaques
    public float intervalJitter = 0.6f;
    public float windupTime = 0.18f;
    public float windupSpeed = 4f;          // m/s
    public float strikeSpeed = 7f;          // m/s : vitesse de la cible pendant la frappe
    public float strikeMaxTime = 0.45f;
    public float recoverTime = 0.35f;
    public float guardSpeed = 5f;
    public float reach = 0.58f;             // portee du bras
    public float aimRange = 0.95f;          // distance epaule -> gant du joueur pour le viser
    public float floorMargin = 0.12f;

    [Header("Positions (m, repere face au joueur, depuis l'epaule)")]
    public Vector3 guardOffset = new Vector3(0.20f, -0.05f, 0.32f);   // x : miroir par main
    public Vector3 windupOffset = new Vector3(0.38f, 0f, 0f);
    public Vector3 sweepOffset = new Vector3(0f, -0.35f, 0.50f);

    enum Phase { Guard, Windup, Strike, Recover }

    class Hand
    {
        public Transform target;
        public Rigidbody shoulder;
        public float side;
        public Phase phase;
        public float t;
        public Vector3 aim;
    }

    Hand[] hands;
    float nextAttack, floorY;
    int nextHand;

    void Start()
    {
        if (self == null || stabilizer == null || player == null ||
            shoulderL == null || shoulderR == null || targetL == null || targetR == null)
        {
            Debug.LogError("OpponentBrain : references manquantes.");
            enabled = false;
            return;
        }
        hands = new[]
        {
            new Hand { target = targetL, shoulder = shoulderL, side = -1f },
            new Hand { target = targetR, shoulder = shoulderR, side = 1f }
        };
        floorY = self.transform.position.y;
        nextAttack = 2f;   // laisse le temps aux gants de monter en puissance
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (stabilizer.IsKnockedDown) return;   // KO : plus d'ordres, les gants sont mous

        Vector3 toPlayer = player.CenterOfMass() - self.CenterOfMass();
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        Quaternion frame = dist > 0.05f ? Quaternion.LookRotation(toPlayer / dist) : Quaternion.identity;
        bool stunned = stabilizer.IsStunned;

        if (!stunned && dist > engageDistance && dist > 0.05f)
        {
            Vector3 dir = toPlayer / dist;
            if (Vector3.Dot(AverageVelocity(), dir) < maxAdvanceSpeed)
                self.AddDistributedForce(dir * (self.TotalMass * advanceAccel));
        }

        nextAttack -= dt;
        if (!stunned && nextAttack <= 0f && dist < engageDistance + 0.8f)
        {
            Hand h = hands[nextHand];
            if (h.phase == Phase.Guard)
            {
                h.phase = Phase.Windup;
                h.t = 0f;
                nextHand = 1 - nextHand;
                nextAttack = attackInterval + Random.Range(-intervalJitter, intervalJitter);
            }
        }

        foreach (Hand h in hands) UpdateHand(h, frame, dt, stunned);
    }

    void UpdateHand(Hand h, Quaternion frame, float dt, bool stunned)
    {
        Vector3 s = h.shoulder.position;
        h.t += dt;
        if (stunned && h.phase != Phase.Guard) { h.phase = Phase.Recover; h.t = 0f; }

        Vector3 guard = s + frame * new Vector3(h.side * guardOffset.x, guardOffset.y, guardOffset.z);
        Vector3 goal = guard;
        float speed = guardSpeed;

        switch (h.phase)
        {
            case Phase.Windup:
                goal = s + frame * new Vector3(h.side * windupOffset.x, windupOffset.y, windupOffset.z);
                speed = windupSpeed;
                if (h.t >= windupTime) { h.aim = PickAim(h, frame); h.phase = Phase.Strike; h.t = 0f; }
                break;
            case Phase.Strike:
                goal = h.aim;
                speed = strikeSpeed;
                if ((h.target.position - h.aim).sqrMagnitude < 0.0009f || h.t >= strikeMaxTime)
                { h.phase = Phase.Recover; h.t = 0f; }
                break;
            case Phase.Recover:
                if (h.t >= recoverTime) h.phase = Phase.Guard;
                break;
        }
        h.target.position = Vector3.MoveTowards(h.target.position, ClampReach(s, goal), speed * dt);
    }

    Vector3 PickAim(Hand h, Quaternion frame)
    {
        Vector3 s = h.shoulder.position;
        Rigidbody best = null;
        float bestD = aimRange;
        Consider(playerGloveL, s, ref best, ref bestD);
        Consider(playerGloveR, s, ref best, ref bestD);
        if (best != null) return ClampReach(s, best.position + best.linearVelocity * 0.08f);
        return ClampReach(s, s + frame * new Vector3(-h.side * sweepOffset.x, sweepOffset.y, sweepOffset.z));
    }

    static void Consider(Rigidbody g, Vector3 from, ref Rigidbody best, ref float bestD)
    {
        if (g == null) return;
        float d = Vector3.Distance(from, g.position);
        if (d < bestD) { bestD = d; best = g; }
    }

    Vector3 ClampReach(Vector3 shoulder, Vector3 p)
    {
        p = shoulder + Vector3.ClampMagnitude(p - shoulder, reach);
        p.y = Mathf.Max(p.y, floorY + floorMargin);
        return p;
    }

    Vector3 AverageVelocity()
    {
        Vector3 sum = Vector3.zero;
        foreach (Rigidbody rb in self.Bodies) sum += rb.linearVelocity * rb.mass;
        return sum / self.TotalMass;
    }
}