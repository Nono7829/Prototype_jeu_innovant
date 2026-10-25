using UnityEngine;
using UnityEngine.InputSystem;

// Lecture manette. Indice 0 = gauche, 1 = droite.
// Sticks : deplacer le gant. LB/RB (maintenu) : le stick pilote la hauteur.
// LT/RT : fermer le gant (s'agripper).
public class PlayerInputs : MonoBehaviour
{
    readonly InputAction[] stick = new InputAction[2];
    readonly InputAction[] mode = new InputAction[2];
    readonly InputAction[] grip = new InputAction[2];

    void Awake()
    {
        stick[0] = new InputAction("StickL", InputActionType.Value, "<Gamepad>/leftStick");
        stick[1] = new InputAction("StickR", InputActionType.Value, "<Gamepad>/rightStick");
        mode[0] = new InputAction("ModeL", InputActionType.Value, "<Gamepad>/leftShoulder");
        mode[1] = new InputAction("ModeR", InputActionType.Value, "<Gamepad>/rightShoulder");
        grip[0] = new InputAction("GripL", InputActionType.Value, "<Gamepad>/leftTrigger");
        grip[1] = new InputAction("GripR", InputActionType.Value, "<Gamepad>/rightTrigger");
    }

    void OnEnable()
    {
        for (int i = 0; i < 2; i++) { stick[i].Enable(); mode[i].Enable(); grip[i].Enable(); }
    }

    void OnDisable()
    {
        for (int i = 0; i < 2; i++) { stick[i].Disable(); mode[i].Disable(); grip[i].Disable(); }
    }

    void OnDestroy()
    {
        for (int i = 0; i < 2; i++) { stick[i].Dispose(); mode[i].Dispose(); grip[i].Dispose(); }
    }

    public Vector2 Stick(int i) { return stick[i].ReadValue<Vector2>(); }
    public bool VerticalMode(int i) { return mode[i].ReadValue<float>() > 0.5f; }
    public float Grip(int i) { return grip[i].ReadValue<float>(); }
}