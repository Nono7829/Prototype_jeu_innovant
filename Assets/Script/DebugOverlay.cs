using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// F1 : affiche l'etat physique. K : tenir/relacher le bassin du joueur.
// E : test des coudes. N : test des genoux (couple en monde, a faire bassin tenu).
public class DebugOverlay : MonoBehaviour
{
    [Header("References (cablees par le menu)")]
    public RagdollBody player;
    public GloveDriver driverL, driverR;
    public GloveGrab grabL, grabR;

    public bool visible;
    public bool holdPelvisOnStart;
    public float elbowTestImpulse = 0.5f;
    public float kneeTestImpulse = 1.5f;

    ConfigurableJoint[] joints;
    Rigidbody pelvis;
    readonly List<Rigidbody> forearms = new List<Rigidbody>();
    readonly List<Rigidbody> shins = new List<Rigidbody>();
    float peakError, peakSpeed, shownError, shownSpeed, timer;
    GUIStyle style;

    void Start()
    {
        if (player == null) { enabled = false; return; }
        joints = player.GetComponentsInChildren<ConfigurableJoint>();
        foreach (Rigidbody rb in player.Bodies)
        {
            if (rb.name == "Pelvis") pelvis = rb;
            else if (rb.name.StartsWith("Forearm")) forearms.Add(rb);
            else if (rb.name.StartsWith("Shin")) shins.Add(rb);
        }
        if (pelvis != null) pelvis.isKinematic = holdPelvisOnStart;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f1Key.wasPressedThisFrame) visible = !visible;
        if (kb.kKey.wasPressedThisFrame && pelvis != null) pelvis.isKinematic = !pelvis.isKinematic;
        if (kb.eKey.wasPressedThisFrame) foreach (Rigidbody rb in forearms) rb.AddTorque(Vector3.left * elbowTestImpulse, ForceMode.Impulse);
        if (kb.nKey.wasPressedThisFrame) foreach (Rigidbody rb in shins) rb.AddTorque(Vector3.right * kneeTestImpulse, ForceMode.Impulse);
    }

    void FixedUpdate()
    {
        float err = 0f;
        foreach (ConfigurableJoint j in joints)
        {
            if (j == null || j.connectedBody == null) continue;
            Vector3 a = j.transform.TransformPoint(j.anchor);
            Vector3 b = j.connectedBody.transform.TransformPoint(j.connectedAnchor);
            err = Mathf.Max(err, Vector3.Distance(a, b));
        }
        float spd = 0f;
        foreach (Rigidbody rb in player.Bodies) spd = Mathf.Max(spd, rb.linearVelocity.magnitude);
        peakError = Mathf.Max(peakError, err);
        peakSpeed = Mathf.Max(peakSpeed, spd);
        timer += Time.fixedDeltaTime;
        if (timer >= 1f) { shownError = peakError; shownSpeed = peakSpeed; peakError = peakSpeed = timer = 0f; }
    }

    void OnGUI()
    {
        if (!visible) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 20 };

        float mm = shownError * 1000f;
        style.normal.textColor = mm > 30f ? Color.red : (mm > 10f ? Color.yellow : Color.green);
        GUI.Label(new Rect(20, 20, 1200, 30), $"Joints joueur : erreur max {mm:F1} mm | vitesse max {shownSpeed:F1} m/s", style);
        Line(driverL, grabL, "G", 55);
        Line(driverR, grabR, "D", 85);
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(20, 115, 1200, 30), "F1 : masquer | K : tenir le bassin | E : test coudes | N : test genoux", style);
    }

    void Line(GloveDriver d, GloveGrab g, string side, float y)
    {
        if (d == null) return;
        style.normal.textColor = d.Gap > 0.08f ? Color.red : (d.Gap > 0.04f ? Color.yellow : Color.green);
        string s = g != null ? (g.IsGrabbing ? "agrippe" : (g.IsGripping ? "ferme" : "ouvert")) : "-";
        GUI.Label(new Rect(20, y, 1200, 30), $"Gant {side} : ecart cible {d.TrackError * 100f:F0} cm | ecart main {d.Gap * 1000f:F0} mm | {s}", style);
    }
}