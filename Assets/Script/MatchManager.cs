using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Regle unique du jeu : Ring Out. Pas de barre de vie.
// Gere aussi le reset (R / Select) et l'affichage des commandes et du resultat.
public class MatchManager : MonoBehaviour
{
    [Header("References (cablees par le menu)")]
    public RagdollBody player;
    public RagdollBody opponent;
    public RagdollStabilizer opponentStabilizer;
    public GloveGrab gripL, gripR;

    [Header("Ring (octogone, plat vers +-X et +-Z)")]
    public float apothem = 3.0f;       // doit correspondre a SceneBuilder.Apothem
    public float platformTop = 0.5f;
    public float outMargin = 0.10f;    // m au-dela du bord
    public float fallDepth = 0.25f;    // m sous le dessus de la plateforme
    public bool playerCanLose = true;

    public enum State { Playing, Victory, Defeat }
    public State Current { get; private set; }

    GUIStyle small, big, state;

    void FixedUpdate()
    {
        if (Current != State.Playing || player == null || opponent == null) return;
        if (IsOut(opponent)) Current = State.Victory;
        else if (playerCanLose && IsOut(player)) Current = State.Defeat;
    }

    bool IsOut(RagdollBody b)
    {
        Vector3 c = b.CenterOfMass();
        if (c.y < platformTop - fallDepth) return true;
        for (int k = 0; k < 8; k++)
        {
            float a = k * 45f * Mathf.Deg2Rad;
            if (c.x * Mathf.Cos(a) + c.z * Mathf.Sin(a) > apothem + outMargin) return true;
        }
        return false;
    }

    void Update()
    {
        bool key = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        bool pad = Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame;
        if (key || pad) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnGUI()
    {
        if (small == null)
        {
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight };
            small.normal.textColor = Color.white;
            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            state = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter };
            state.normal.textColor = Color.white;
        }
        small.fontSize = Mathf.RoundToInt(Screen.height * 0.024f);
        big.fontSize = Mathf.RoundToInt(Screen.height * 0.10f);
        state.fontSize = small.fontSize;
        float w = Screen.width, h = Screen.height;

        GUI.Label(new Rect(w - 760, 15, 740, h * 0.3f),
            "Sticks : deplacer les gants\n" +
            "LB / RB maintenu : le stick controle la hauteur\n" +
            "LT / RT maintenu : fermer le gant et s'agripper\n" +
            "Objectif : ejecter l'adversaire du ring\n" +
            "R / Select : recommencer", small);

        string g = "Gant G : " + Label(gripL) + "     Gant D : " + Label(gripR);
        if (opponentStabilizer != null && opponentStabilizer.IsKnockedDown) g += "     Adversaire a terre";
        GUI.Label(new Rect(0, h - 60, w, 50), g, state);

        if (Current == State.Playing) return;
        big.normal.textColor = Current == State.Victory ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.3f, 0.3f);
        GUI.Label(new Rect(0, h * 0.25f, w, h * 0.2f), Current == State.Victory ? "RING OUT ! VICTOIRE" : "RING OUT... DEFAITE", big);
    }

    static string Label(GloveGrab g)
    {
        if (g == null) return "-";
        return g.IsGrabbing ? "AGRIPPE" : (g.IsGripping ? "ferme" : "ouvert");
    }
}