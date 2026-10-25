using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EditorCheck
{
    static EditorCheck() { Debug.Log("Assembly Editor OK : le menu MMA doit exister."); }
}