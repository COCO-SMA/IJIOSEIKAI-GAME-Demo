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
            "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
            "SimHei", "黑体", "SimSun", "宋体", "NSimSun",
            "DengXian", "等线", "KaiTi", "楷体", "FangSong", "仿宋",
            "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC",
            "PingFang SC", "Heiti SC", "Hiragino Sans GB", "Arial Unicode MS"
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

            string[] installed = Font.GetOSInstalledFontNames() ?? new string[0];

            // Exact match first, then a loosened pass. Windows reports family names
            // inconsistently ("Microsoft YaHei" vs "Microsoft YaHei UI Light" vs a
            // localised "微软雅黑"), and an exact-only compare silently fell through
            // to Arial, which has no CJK glyphs at all.
            cached = TryCandidates(installed, exact: true)
                     ?? TryCandidates(installed, exact: false)
                     ?? TryBlind();

            if (cached != null) return cached;

            // Log what the OS actually offered; guessing at this costs a 10-minute
            // editor launch, so the failure has to describe itself.
            Debug.LogWarning("[CJKFont] No CJK font found. Installed fonts (" +
                             installed.Length + "): " +
                             string.Join(", ", installed, 0, Mathf.Min(40, installed.Length)));
            return null;
        }

        private static Font TryCandidates(string[] installed, bool exact)
        {
            foreach (var candidate in Candidates)
            {
                foreach (var name in installed)
                {
                    bool match = exact
                        ? string.Equals(name, candidate, System.StringComparison.OrdinalIgnoreCase)
                        : Normalise(name).Contains(Normalise(candidate));
                    if (!match) continue;

                    // Size here is only the starting atlas size; dynamic fonts
                    // rasterise per-Text fontSize at runtime.
                    var font = Font.CreateDynamicFontFromOSFont(name, 16);
                    if (font != null)
                    {
                        Debug.Log($"[CJKFont] Using OS font: {name}" +
                                  (exact ? "" : $" (loose match on {candidate})"));
                        return font;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Ask for the candidates by name even though the enumeration never listed
        /// them. GetOSInstalledFontNames misses fonts on some Windows setups, but
        /// CreateDynamicFontFromOSFont still resolves them.
        /// </summary>
        private static Font TryBlind()
        {
            foreach (var candidate in Candidates)
            {
                var font = Font.CreateDynamicFontFromOSFont(candidate, 16);
                if (font == null) continue;
                Debug.Log($"[CJKFont] Using unlisted OS font: {candidate}");
                return font;
            }
            return null;
        }

        private static string Normalise(string s) =>
            s == null ? "" : s.Replace(" ", "").Replace("-", "").ToLowerInvariant();

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

    // ApplyCJKFont lives in its own file: Unity only serialises a MonoBehaviour whose
    // class name matches its file name, and as a second class in here it saved into
    // every scene and prefab as a null script reference.
}
