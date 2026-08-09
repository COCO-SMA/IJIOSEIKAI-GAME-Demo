using UnityEngine;

namespace KunchengRPG.UI
{
    /// <summary>
    /// Drop this on a Canvas root or on any prefab instantiated at runtime, and every
    /// Text under it gets the CJK font on Awake. Attaching it to prefabs is what keeps
    /// dynamically spawned choice rows from falling back to Arial.
    /// Kept in its own file on purpose: Unity only serialises a MonoBehaviour whose
    /// class name matches the file name, so living inside CJKFont.cs meant every scene
    /// and prefab that used it stored a null script reference instead.
    /// </summary>
    public class ApplyCJKFont : MonoBehaviour
    {
        void Awake()
        {
            CJKFont.ApplyTo(gameObject);
        }
    }
}
