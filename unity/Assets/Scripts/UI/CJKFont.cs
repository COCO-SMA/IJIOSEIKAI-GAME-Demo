using UnityEngine;
using UnityEngine.UI;

namespace KunchengRPG.UI
{
    /// <summary>
    /// Supplies a font that actually contains CJK glyphs.
    ///
    /// Unity's built-in Arial has no Chinese coverage, so every Chinese string in a
    /// legacy UI.Text renders as tofu boxes. Rather than committing a font file
    /// (large, and licensing varies), this pulls a dynamic font from the OS. The
    /// candidate list is ordered by preference and covers Windows and macOS.
    /// </summary>
    public static class CJKFont
    {
        private static readonly string[] Candidates =
        {
            "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun",
            "Noto Sans CJK SC", "Source Han Sans SC", "PingFang SC",
            "Heiti SC", "Arial Unicode MS"
        };

        private static Font cached;
        private static bool resolved;

        /// <summary>
        /// The best available CJK font, or null if the OS has none of the candidates
        /// (in which case callers should leave the existing font alone).
        /// </summary>
        public static Font Get()
        {
            if (resolved) return cached;
            resolved = true;

            string[] installed = Font.GetOSInstalledFontNames();
            foreach (var candidate in Candidates)
            {
                foreach (var name in installed)
                {
                    if (name != candidate) continue;

                    // Size here is only the starting atlas size; dynamic fonts rasterise
                    // per-Text fontSize at runtime.
                    cached = Font.CreateDynamicFontFromOSFont(candidate, 16);
                    if (cached != null)
                    {
                        Debug.Log($"[CJKFont] Using OS font: {candidate}");
                        return cached;
                    }
                }
            }

            Debug.LogWarning("[CJKFont] No CJK font found on this system; Chinese text may render as boxes.");
            return null;
        }

        /// <summary>
        /// Apply the CJK font to a Text and every Text beneath it.
        /// </summary>
        public static void ApplyTo(GameObject root)
        {
            if (root == null) return;

            Font font = Get();
            if (font == null) return;

            foreach (var text in root.GetComponentsInChildren<Text>(true))
                text.font = font;
        }
    }

    /// <summary>
    /// Drop this on a Canvas root or on any prefab instantiated at runtime, and every
    /// Text under it gets the CJK font on Awake. Attaching it to prefabs is what keeps
    /// dynamically spawned choice rows from falling back to Arial.
    /// </summary>
    public class ApplyCJKFont : MonoBehaviour
    {
        void Awake()
        {
            CJKFont.ApplyTo(gameObject);
        }
    }
}
