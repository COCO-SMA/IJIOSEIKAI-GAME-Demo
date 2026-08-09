using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace KunchengRPG.EditorTools
{
    /// <summary>
    /// One entry point for the whole verification pass, because a Unity cold start
    /// costs about ten minutes and running four suites separately costs forty.
    ///
    ///   Unity.exe -batchmode -quit -projectPath unity -logFile run.log \
    ///       -executeMethod KunchengRPG.EditorTools.FullVerify.RunAll
    ///
    /// Rebuilds tileset, prefabs and scenes first — the ChoiceItem prefab carried a
    /// dead script reference, and regenerating it is the fix — then runs every suite
    /// and exits non-zero if any of them failed. Each suite reports its own tagged
    /// RESULT line; grep the log by tag rather than reading it.
    /// </summary>
    public static class FullVerify
    {
        public static void RunAll()
        {
            var problems = new StringBuilder();
            int suites = 0, broken = 0;

            // Scene/prefab rebuild goes first: the suites below run against what it
            // produces, and a build exception here makes their results meaningless.
            Step("scene build + wiring", ref suites, ref broken, problems,
                 () => { BatchVerify.BuildAndVerifyNoExit(); return BatchVerify.FailCount; });

            Step("menu / equip / grant", ref suites, ref broken, problems,
                 () => { MenuTests.RunAll(); return MenuTests.FailCount; });

            Step("grid combat", ref suites, ref broken, problems,
                 () => { GridCombatTests.RunAll(); return GridCombatTests.FailCount; });

            Step("anomaly unfold", ref suites, ref broken, problems,
                 () => { AnomalyTests.RunAll(); return AnomalyTests.FailCount; });

            Step("ending resolution", ref suites, ref broken, problems,
                 () => { BatchEndingTest.RunEndingTestsNoExit(); return BatchEndingTest.FailCount; });

            if (broken == 0)
            {
                Debug.Log($"[FullVerify] RESULT ok — {suites} suites clean");
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError($"[FullVerify] RESULT broken={broken}/{suites}\n{problems}");
            EditorApplication.Exit(1);
        }

        /// <summary>
        /// Run one suite. A throwing suite is a failure, not a crash of the run —
        /// otherwise the first bad suite hides the state of every later one and the
        /// next cold start tells us only slightly more than this one did.
        /// </summary>
        private static void Step(string name, ref int suites, ref int broken,
                                 StringBuilder problems, Func<int> run)
        {
            suites++;
            Debug.Log($"[FullVerify] --- {name} ---");
            try
            {
                int failures = run();
                if (failures > 0)
                {
                    broken++;
                    problems.AppendLine($"  - {name}: {failures} failure(s)");
                }
            }
            catch (Exception e)
            {
                broken++;
                problems.AppendLine($"  - {name}: threw {e.GetType().Name}: {e.Message}");
                Debug.LogError($"[FullVerify] {name} threw: {e}");
            }
        }
    }
}
