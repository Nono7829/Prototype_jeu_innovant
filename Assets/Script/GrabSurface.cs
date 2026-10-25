using UnityEngine;

// Marque un collider comme surface d'accroche speciale.
// Sans ce composant, un collider est agrippe de facon rigide.
public class GrabSurface : MonoBehaviour
{
    public bool elastic = true;
    public float spring = 400f;   // N/m : raideur de la corde (depend du jeu)
    public float damper = 25f;    // N.s/m
    public float slack = 0.05f;   // m : mou avant que le ressort tire
}