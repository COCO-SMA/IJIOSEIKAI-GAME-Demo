using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KunchengRPG.EditorTools
{
    /// <summary>
    /// Produces a double-clickable Windows player, so "play the game" stops meaning
    /// "open the editor and press Play":
    ///
    ///   Unity.exe -batchmode -quit -projectPath unity -executeMethod \
    ///       KunchengRPG.EditorTools.PlayerBuilder.BuildWindows
    ///
    /// The scene list comes from EditorBuildSettings rather than being hardcoded, so
    /// the build can never disagree with what the editor's Build Settings window says.
    /// The exe filename is ASCII on purpose while productName stays Chinese: the window
    /// title is player-facing, but the path gets handled by bat files and PowerShell 5.1,
    /// which mangles non-ASCII often enough to not be worth the risk.
    /// </summary>
    public static class PlayerBuilder
    {
        private const string Tag = "[Build]";
        public const string ExeName = "KunchengRPG.exe";

        /// <summary>Relative to the repo root, i.e. one level above the Unity project.</summary>
        public const string OutputDir = "Build/Windows";

        [MenuItem("Kuncheng/4. Build Windows Player", false, 13)]
        public static void BuildWindows() => Run(BuildTarget.StandaloneWindows64, false);

        /// <summary>
        /// Development build: keeps the debug symbols and the log window, which is what
        /// you want while the thing is still being assembled.
        /// </summary>
        [MenuItem("Kuncheng/5. Build Windows Player (dev)", false, 14)]
        public static void BuildWindowsDev() => Run(BuildTarget.StandaloneWindows64, true);

        private static void Run(BuildTarget target, bool development)
        {
            var scenes = EnabledScenes();
            if (scenes.Count == 0)
            {
                Fail("No enabled scenes in Build Settings. Nothing to build.");
                return;
            }

            // Scene 0 is the entry point; if it is not the title screen the player boots
            // straight into a half-initialised world, which looks like a crash.
            Debug.Log($"{Tag} entry scene: {scenes[0]}");
            for (int i = 1; i < scenes.Count; i++)
                Debug.Log($"{Tag} scene {i}: {scenes[i]}");

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string repoRoot = Directory.GetParent(projectRoot).FullName;
            string outDir = Path.Combine(repoRoot, OutputDir.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(outDir);
            string exePath = Path.Combine(outDir, ExeName);

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = exePath,
                target = target,
                options = development
                    ? BuildOptions.Development | BuildOptions.AllowDebugging
                    : BuildOptions.None
            };

            Debug.Log($"{Tag} building {(development ? "dev" : "release")} -> {exePath}");
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                    foreach (var msg in step.messages)
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                            Debug.Log($"{Tag} error in {step.name}: {msg.content}");

                Fail($"build {summary.result} with {summary.totalErrors} error(s)");
                return;
            }

            if (!File.Exists(exePath))
            {
                Fail($"build reported success but {exePath} is missing");
                return;
            }

            double mb = System.Math.Round(summary.totalSize / 1024d / 1024d, 1);
            Debug.Log($"{Tag} size {mb} MB, took {summary.totalTime.TotalSeconds:F0}s");
            Debug.Log($"{Tag} PASS {exePath}");
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Only scenes ticked in Build Settings, in their listed order. A scene whose
        /// file has gone missing is dropped with a warning rather than failing the run:
        /// the build is still playable, and a hard failure here would be a confusing
        /// way to learn that someone deleted a scene.
        /// </summary>
        private static List<string> EnabledScenes()
        {
            var result = new List<string>();
            foreach (var s in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                if (File.Exists(s.path)) result.Add(s.path);
                else Debug.LogWarning($"{Tag} skipping missing scene {s.path}");
            }
            return result;
        }

        private static void Fail(string reason)
        {
            Debug.Log($"{Tag} FAIL {reason}");
            EditorApplication.Exit(1);
        }
    }
}
